using System.Net;
using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using Xunit;

public class AuthApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Register_accepts_email_and_password()
    {
        var app = factory.WithWebHostBuilder(b => b.ConfigureServices(s => { s.AddScoped<IEmailSender, TestEmailSender>(); }));
        var response = await app.CreateClient().PostAsJsonAsync("/api/auth/register", new { email = "new@example.com", password = "ValidPass123!" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Reset_code_is_rejected_after_five_invalid_attempts()
    {
        var codes = CreateCodeService();
        var code = await codes.CreateAsync("reset-password", "user@example.com");

        for (var i = 0; i < 5; i++)
        {
            Assert.False(await codes.VerifyAsync("reset-password", "user@example.com", "000000"));
        }

        Assert.False(await codes.VerifyAsync("reset-password", "user@example.com", code!));
    }

    [Fact]
    public async Task Refresh_without_cookie_returns_unauthorized()
    {
        var response = await factory.CreateClient().PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_without_cookie_returns_no_content()
    {
        var response = await factory.CreateClient().PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_cookie_and_rejects_the_old_token()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_KEY"] = "test-key-that-is-long-enough-for-hmac-sha256",
                ["JWT_ISSUER"] = "Dionysus",
                ["JWT_AUDIENCE"] = "Dionysus"
            })
            .Build();
        var tokens = new TokenService(configuration, cache, new TestHostEnvironment());
        var issueContext = new DefaultHttpContext();

        await tokens.IssueAsync(new AppUser { Id = "user-1", Email = "user@example.com" }, issueContext.Response);
        var oldToken = issueContext.Response.Headers.SetCookie.ToString().Split(';')[0].Split('=', 2)[1];
        var refreshContext = new DefaultHttpContext();
        refreshContext.Request.Headers.Cookie = $"refresh_token={oldToken}";

        Assert.NotNull(await tokens.RefreshAsync(oldToken, refreshContext.Response));
        Assert.Null(await tokens.RefreshAsync(oldToken, new DefaultHttpContext().Response));
        Assert.DoesNotContain("Secure", refreshContext.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public async Task Production_refresh_cookie_is_secure()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JWT_KEY"] = "test-key-that-is-long-enough-for-hmac-sha256"
            })
            .Build();
        var tokens = new TokenService(configuration, cache, new TestHostEnvironment { EnvironmentName = Environments.Production });
        var context = new DefaultHttpContext();

        await tokens.IssueAsync(new AppUser { Id = "user-1", Email = "user@example.com" }, context.Response);

        Assert.Contains("secure", context.Response.Headers.SetCookie.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Password_reset_request_does_not_disclose_unknown_email()
    {
        var response = await factory.CreateClient().PostAsJsonAsync(
            "/api/auth/password-reset/request", new { email = "missing@example.com" });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Me_requires_access_token()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_contains_session_and_recovery_routes()
    {
        var document = await factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json");

        Assert.Contains("/api/auth/refresh", document);
        Assert.Contains("/api/auth/password-reset/confirm", document);
        Assert.Contains("securitySchemes", document);
        Assert.Contains("Bearer", document);
        Assert.Contains("/api/auth/change-password", document);
    }

    [Fact]
    public void Me_response_contract_contains_email()
    {
        var controller = new AuthController(null!, null!, null!, null!, new TestHostEnvironment())
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                    [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, "user@example.com")],
                    "test"))
                }
            }
        };

        var result = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(controller.Me());
        var email = result.Value!.GetType().GetProperty("Email")!.GetValue(result.Value);

        Assert.Equal("user@example.com", email);
    }

    [Fact]
    public async Task Swagger_marks_only_protected_operations_with_security()
    {
        var document = JsonDocument.Parse(
            await factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json"));
        var paths = document.RootElement.GetProperty("paths");

        Assert.False(paths.GetProperty("/api/auth/login").GetProperty("post").TryGetProperty("security", out _));
        Assert.True(paths.GetProperty("/api/auth/me").GetProperty("get").GetProperty("security").GetArrayLength() > 0);
        Assert.True(paths.GetProperty("/api/projects").GetProperty("get").GetProperty("security").GetArrayLength() > 0);
        Assert.True(paths.GetProperty("/api/projects/search").GetProperty("get").GetProperty("security").GetArrayLength() > 0);
        Assert.True(paths.GetProperty("/api/projects/{id}/transcription-search").GetProperty("get").GetProperty("security").GetArrayLength() > 0);
        Assert.True(paths.GetProperty("/api/projects/{projectId}/recordings/{recordingId}/stream").GetProperty("get").GetProperty("security").GetArrayLength() > 0);
    }

    private static CodeService CreateCodeService()
    {
        IDistributedCache cache = new MemoryDistributedCache(
            Options.Create(new MemoryDistributedCacheOptions()));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AUTH_CODE_PEPPER"] = "test-pepper"
            })
            .Build();
        return new CodeService(cache, configuration);
    }
}

internal sealed class TestHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "Dionysus.Api.Tests";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
public sealed class TestEmailSender : IEmailSender { public Task SendAsync(string to, string subject, string body) => Task.CompletedTask; }
