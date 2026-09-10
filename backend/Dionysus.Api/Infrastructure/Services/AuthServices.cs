using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;

public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body)
    {
        using var client = new SmtpClient(configuration["SMTP_HOST"] ?? "smtp.gmail.com", int.Parse(configuration["SMTP_PORT"] ?? "587"))
        { EnableSsl = true, Credentials = new NetworkCredential(configuration["SMTP_USERNAME"], configuration["SMTP_PASSWORD"]) };
        await client.SendMailAsync(new MailMessage(configuration["SMTP_USERNAME"]!, to, subject, body));
    }
}

public sealed class CodeService(IDistributedCache cache, IConfiguration configuration)
{
    public async Task<string?> CreateAsync(string purpose, string email)
    {
        var resendKey = $"auth:resend:{purpose}:{email}";
        if (await cache.GetStringAsync(resendKey) is not null) return null;
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) };
        await cache.SetStringAsync($"auth:code:{purpose}:{email}", Hash(code), options);
        await cache.SetStringAsync($"auth:attempts:{purpose}:{email}", "0", options);
        await cache.SetStringAsync(resendKey, "1", new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1) });
        return code;
    }
    public async Task<bool> VerifyAsync(string purpose, string email, string code)
    {
        var key = $"auth:code:{purpose}:{email}";
        var expected = await cache.GetStringAsync(key);
        if (expected is null || expected != Hash(code)) return false;
        await cache.RemoveAsync(key);
        return true;
    }
    private string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((configuration["AUTH_CODE_PEPPER"] ?? "development-pepper") + value)));
}

public sealed class TokenService(IConfiguration configuration, IDistributedCache cache)
{
    private readonly byte[] key = Encoding.UTF8.GetBytes(configuration["JWT_KEY"] ?? "development-only-key-change-this-to-a-32-byte-secret");
    public async Task<TokenResponse> IssueAsync(AppUser user, HttpResponse response)
    {
        var access = Create(user, 15, "access", null); var sessionId = Guid.NewGuid().ToString("N");
        var refresh = Create(user, 20160, "refresh", sessionId);
        await cache.SetStringAsync($"auth:session:{sessionId}", user.Id, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
        response.Cookies.Append("refresh_token", refresh, new CookieOptions { HttpOnly = true, Secure = true, SameSite = SameSiteMode.Strict, Path = "/api/auth/refresh", MaxAge = TimeSpan.FromDays(14) });
        return new TokenResponse(access, DateTimeOffset.UtcNow.AddMinutes(15));
    }
    public ClaimsPrincipal? Validate(string token) { try { return new JwtSecurityTokenHandler().ValidateToken(token, Parameters(), out _); } catch { return null; } }
    public TokenValidationParameters Parameters() => new() { ValidateIssuer = true, ValidIssuer = configuration["JWT_ISSUER"] ?? "Dionysus", ValidateAudience = true, ValidAudience = configuration["JWT_AUDIENCE"] ?? "Dionysus", ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(key), ValidateLifetime = true };
    private string Create(AppUser user, int minutes, string type, string? sessionId) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(configuration["JWT_ISSUER"] ?? "Dionysus", configuration["JWT_AUDIENCE"] ?? "Dionysus", new[] { new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Email, user.Email!), new Claim("typ", type), new Claim(JwtRegisteredClaimNames.Jti, sessionId ?? Guid.NewGuid().ToString("N")) }, expires: DateTime.UtcNow.AddMinutes(minutes), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)));
}
