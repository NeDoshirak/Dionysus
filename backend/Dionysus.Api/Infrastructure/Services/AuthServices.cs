using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;

public sealed class SmtpEmailSender(IConfiguration configuration) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body)
    {
        var host = configuration["SMTP_HOST"] ?? "smtp.gmail.com";
        var username = configuration["SMTP_USERNAME"];
        var password = configuration["SMTP_PASSWORD"];
        if (!int.TryParse(configuration["SMTP_PORT"] ?? "587", out var port) || string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new SmtpConfigurationException();

        using var client = new SmtpClient(host, port)
        { EnableSsl = true, Credentials = new NetworkCredential(username, password) };
        await client.SendMailAsync(new MailMessage(username, to, subject, body));
    }
}

public sealed class SmtpConfigurationException() : Exception("SMTP is not configured");

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
        var attemptsKey = $"auth:attempts:{purpose}:{email}";
        var expected = await cache.GetStringAsync(key);
        if (expected is null) return false;
        if (expected != Hash(code))
        {
            var attempts = int.TryParse(await cache.GetStringAsync(attemptsKey), out var count)
                ? count + 1
                : 1;
            if (attempts >= 5)
            {
                await cache.RemoveAsync(key);
                await cache.RemoveAsync(attemptsKey);
            }
            else
            {
                await cache.SetStringAsync(attemptsKey, attempts.ToString(), new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                });
            }

            return false;
        }

        await cache.RemoveAsync(key);
        await cache.RemoveAsync(attemptsKey);
        return true;
    }
    private string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((configuration["AUTH_CODE_PEPPER"] ?? "development-pepper") + value)));
}

public sealed class TokenService(IConfiguration configuration, IDistributedCache cache, IHostEnvironment environment)
{
    private readonly byte[] key = Encoding.UTF8.GetBytes(configuration["JWT_KEY"] ?? "development-only-key-change-this-to-a-32-byte-secret");

    public async Task<TokenResponse> IssueAsync(AppUser user, HttpResponse response)
    {
        return await IssueAsync(user.Id, user.Email!, response);
    }

    public async Task<TokenResponse?> RefreshAsync(string? refreshToken, HttpResponse response)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return null;

        var principal = Validate(refreshToken);
        if (principal?.FindFirst("typ")?.Value != "refresh") return null;

        var sessionId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(email)) return null;
        if (await cache.GetStringAsync($"auth:session:{sessionId}") != userId) return null;

        await RevokeSessionAsync(userId, sessionId);
        return await IssueAsync(userId, email, response);
    }

    public async Task RevokeAsync(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return;

        try
        {
            var token = new JwtSecurityTokenHandler().ReadJwtToken(refreshToken);
            var sessionId = token.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;
            var userId = token.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrWhiteSpace(sessionId) && !string.IsNullOrWhiteSpace(userId))
                await RevokeSessionAsync(userId, sessionId);
        }
        catch (ArgumentException)
        {
        }
    }

    public async Task RevokeAllAsync(string userId)
    {
        var sessionsKey = $"auth:user-sessions:{userId}";
        var sessions = await ReadSessionsAsync(sessionsKey);
        foreach (var sessionId in sessions)
            await cache.RemoveAsync($"auth:session:{sessionId}");
        await cache.RemoveAsync(sessionsKey);
    }

    public ClaimsPrincipal? Validate(string token)
    {
        try { return new JwtSecurityTokenHandler().ValidateToken(token, Parameters(), out _); }
        catch { return null; }
    }

    public TokenValidationParameters Parameters() => new() { ValidateIssuer = true, ValidIssuer = configuration["JWT_ISSUER"] ?? "Dionysus", ValidateAudience = true, ValidAudience = configuration["JWT_AUDIENCE"] ?? "Dionysus", ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(key), ValidateLifetime = true };

    private async Task<TokenResponse> IssueAsync(string userId, string email, HttpResponse response)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var access = Create(userId, email, 15, "access", Guid.NewGuid().ToString("N"));
        var refresh = Create(userId, email, 20160, "refresh", sessionId);
        var sessionOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) };
        await cache.SetStringAsync($"auth:session:{sessionId}", userId, sessionOptions);
        var sessionsKey = $"auth:user-sessions:{userId}";
        var sessions = await ReadSessionsAsync(sessionsKey);
        sessions.Add(sessionId);
        await cache.SetStringAsync(sessionsKey, JsonSerializer.Serialize(sessions), sessionOptions);
        response.Cookies.Append("refresh_token", refresh, new CookieOptions
        {
            HttpOnly = true,
            Secure = string.Equals(environment.EnvironmentName, Environments.Production, StringComparison.OrdinalIgnoreCase),
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth/refresh",
            MaxAge = TimeSpan.FromDays(14)
        });
        return new TokenResponse(access, DateTimeOffset.UtcNow.AddMinutes(15));
    }

    private async Task RevokeSessionAsync(string userId, string sessionId)
    {
        await cache.RemoveAsync($"auth:session:{sessionId}");
        var sessionsKey = $"auth:user-sessions:{userId}";
        var sessions = await ReadSessionsAsync(sessionsKey);
        sessions.Remove(sessionId);
        if (sessions.Count == 0) await cache.RemoveAsync(sessionsKey);
        else await cache.SetStringAsync(sessionsKey, JsonSerializer.Serialize(sessions), new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(14) });
    }

    private async Task<HashSet<string>> ReadSessionsAsync(string keyName)
    {
        var value = await cache.GetStringAsync(keyName);
        return string.IsNullOrWhiteSpace(value)
            ? []
            : JsonSerializer.Deserialize<HashSet<string>>(value) ?? [];
    }

    private string Create(string userId, string email, int minutes, string type, string sessionId) => new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(configuration["JWT_ISSUER"] ?? "Dionysus", configuration["JWT_AUDIENCE"] ?? "Dionysus", new[] { new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Email, email), new Claim("typ", type), new Claim(JwtRegisteredClaimNames.Jti, sessionId) }, expires: DateTime.UtcNow.AddMinutes(minutes), signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)));
}
