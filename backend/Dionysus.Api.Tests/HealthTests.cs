using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
public class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact] public async Task Health_returns_ok() { var response = await factory.CreateClient().GetAsync("/health"); Assert.True(response.IsSuccessStatusCode); Assert.Contains("ok", await response.Content.ReadAsStringAsync()); }

    [Fact]
    public async Task Swagger_contains_specification_routes()
    {
        using var document = JsonDocument.Parse(
            await factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json"));
        var paths = document.RootElement.GetProperty("paths");

        AssertRoute(paths, "/api/projects/{projectId}/specification", "get");
        AssertRoute(paths, "/api/projects/{projectId}/specification/retry", "post");
        AssertRoute(paths, "/api/projects/{projectId}/specification/functions/{functionId}", "get");
        AssertRoute(paths, "/api/projects/{projectId}/specification/functions", "post");
        AssertRoute(paths, "/api/projects/{projectId}/specification/functions/{functionId}", "patch");
        AssertRoute(paths, "/api/projects/{projectId}/specification/functions/{functionId}", "delete");
        AssertRoute(paths, "/api/projects/{projectId}/specification/functions/{functionId}/items", "post");
        AssertRoute(paths, "/api/projects/{projectId}/specification/functions/{functionId}/items/{itemId}", "patch");
        AssertRoute(paths, "/api/projects/{projectId}/specification/functions/{functionId}/items/{itemId}", "delete");

        Assert.True(paths.GetProperty("/api/projects/{projectId}/specification").GetProperty("get").GetProperty("security").GetArrayLength() > 0);
    }

    private static void AssertRoute(JsonElement paths, string path, string method)
    {
        Assert.True(paths.TryGetProperty(path, out var pathItem), $"Missing Swagger path {path}");
        Assert.True(pathItem.TryGetProperty(method, out _), $"Missing Swagger operation {method.ToUpperInvariant()} {path}");
    }
}
