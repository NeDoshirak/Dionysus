using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
public class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact] public async Task Health_returns_ok() { var response = await factory.CreateClient().GetAsync("/health"); Assert.True(response.IsSuccessStatusCode); Assert.Contains("ok", await response.Content.ReadAsStringAsync()); }
}
