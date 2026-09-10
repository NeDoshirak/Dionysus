using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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

    [Fact]
    public async Task Stream_returns_audio_with_range_processing_for_owner()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options);
        var project = new ProjectEntity { OwnerId = "user-1", Name = "Project" };
        var recording = new VoiceRecording
        {
            ProjectEntityId = project.Id,
            FileName = "voice.mp3",
            ContentType = "audio/mpeg",
            SourceType = "audio",
            AudioData = [1, 2, 3]
        };
        project.Recordings.Add(recording);
        db.Projects.Add(project);
        await db.SaveChangesAsync();

        var controller = new ProjectsController(db, null!, null!)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "user-1")], "test"))
                }
            }
        };
        var result = await controller.Stream(project.Id, recording.Id);
        var file = Assert.IsType<FileContentResult>(result);

        Assert.Equal("audio/mpeg", file.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, file.FileContents);
        Assert.True(file.EnableRangeProcessing);
    }
}
