using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(UserManager<AppUser> users, CodeService codes, TokenService tokens, IEmailSender email, IHostEnvironment environment) : ControllerBase
{
    [HttpPost("register")]
    public async Task<IActionResult> Register(Credentials request)
    {
        var address = request.Email.Trim().ToLowerInvariant();
        if (await users.FindByEmailAsync(address) is not null) return Conflict();
        var user = new AppUser { UserName = address, Email = address };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return BadRequest(result.Errors);
        var code = await codes.CreateAsync("verify", address);
        if (code is null) return StatusCode(429);
        try { await email.SendAsync(address, "Dionysus: подтверждение почты", $"Ваш код: {code}"); }
        catch (SmtpConfigurationException) { return Problem("Email delivery is not configured", statusCode: 503); }
        return Accepted();
    }
    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(CodeRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null || !await codes.VerifyAsync("verify", user.Email!, request.Code)) return BadRequest();
        user.EmailConfirmed = true; await users.UpdateAsync(user); return Ok(await tokens.IssueAsync(user, Response));
    }
    [HttpPost("login")]
    public async Task<IActionResult> Login(Credentials request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim().ToLowerInvariant());
        if (user is null || !await users.CheckPasswordAsync(user, request.Password)) return Unauthorized();
        if (!user.EmailConfirmed) return Forbid(); return Ok(await tokens.IssueAsync(user, Response));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var result = await tokens.RefreshAsync(Request.Cookies["refresh_token"], Response);
        return result is null ? Unauthorized() : Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await tokens.RevokeAsync(Request.Cookies["refresh_token"]);
        Response.Cookies.Delete("refresh_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = environment.IsProduction(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth/refresh"
        });
        return NoContent();
    }

    [HttpPost("password-reset/request")]
    public async Task<IActionResult> RequestPasswordReset(ResetRequest request)
    {
        var address = request.Email.Trim().ToLowerInvariant();
        var user = await users.FindByEmailAsync(address);
        if (user is not null && user.EmailConfirmed)
        {
            var code = await codes.CreateAsync("reset-password", address);
            if (code is not null)
            {
                try { await email.SendAsync(address, "Dionysus: сброс пароля", $"Ваш код: {code}"); }
                catch (SmtpConfigurationException) { return Problem("Email delivery is not configured", statusCode: 503); }
            }
        }

        return Accepted();
    }

    [HttpPost("password-reset/confirm")]
    public async Task<IActionResult> ConfirmPasswordReset(ResetConfirm request)
    {
        var address = request.Email.Trim().ToLowerInvariant();
        var user = await users.FindByEmailAsync(address);
        if (user is null || !await codes.VerifyAsync("reset-password", address, request.Code)) return BadRequest();

        var resetToken = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded) return BadRequest(result.Errors);
        await tokens.RevokeAllAsync(user.Id);
        return NoContent();
    }

    [HttpGet("me")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public IActionResult Me() => Ok(new
    {
        Id = User.FindFirstValue(ClaimTypes.NameIdentifier),
        Email = User.FindFirstValue(ClaimTypes.Email),
        EmailConfirmed = true
    });
}
