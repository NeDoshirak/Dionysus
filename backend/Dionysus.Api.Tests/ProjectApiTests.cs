using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task Transcript_segments_are_inserted_after_recording_is_saved()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);
        var recording = new VoiceRecording
        {
            ProjectEntityId = Guid.NewGuid(),
            FileName = "voice.wav",
            ContentType = "audio/wav",
            SourceType = "audio"
        };

        db.VoiceRecordings.Add(recording);
        await db.SaveChangesAsync();

        var segment = new TranscriptSegment
        {
            VoiceRecordingId = recording.Id,
            StartSeconds = 1.2,
            EndSeconds = 3.4,
            Text = "segment"
        };
        recording.Segments.Add(segment);
        db.TranscriptSegments.AddRange(recording.Segments);

        await db.SaveChangesAsync();

        Assert.Equal(1, await db.TranscriptSegments.CountAsync());
    }
}
