using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
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
}
public sealed class TestEmailSender : IEmailSender { public Task SendAsync(string to, string subject, string body) => Task.CompletedTask; }
