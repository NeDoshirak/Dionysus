using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(UserManager<AppUser> users, CodeService codes, TokenService tokens, IEmailSender email) : ControllerBase
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
        await email.SendAsync(address, "Dionysus: подтверждение почты", $"Ваш код: {code}");
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
}
