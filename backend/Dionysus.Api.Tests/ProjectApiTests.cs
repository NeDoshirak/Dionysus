using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class ProjectApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Create_project_requires_authentication()
    {
        using var form = new MultipartFormDataContent { { new StringContent("Meeting"), "title" }, { new ByteArrayContent([1, 2]), "file", "voice.wav" } };
        var response = await factory.CreateClient().PostAsync("/api/projects", form);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_document_is_available_for_multipart_project_upload()
    {
        var response = await factory.CreateClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
