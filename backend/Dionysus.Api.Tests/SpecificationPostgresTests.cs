using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;

[CollectionDefinition(nameof(SpecificationPostgresCollection))]
public sealed class SpecificationPostgresCollection : ICollectionFixture<SpecificationApiFactory> { }

[Collection(nameof(SpecificationPostgresCollection))]
public sealed class SpecificationPostgresTests(SpecificationApiFactory fixture) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetDatabaseAsync();
    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Successful_transcription_creates_queued_analysis_without_external_calls()
    {
        await using var db = fixture.CreateDbContext();
        var client = fixture.ClientForOwner();
        using var media = new ByteArrayContent([1, 2, 3]);
        media.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        using var form = new MultipartFormDataContent { { new StringContent("Interview"), "name" }, { media, "media", "voice.wav" } };

        var response = await client.PostAsync("/api/projects", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var analysis = await db.SpecificationAnalyses.SingleAsync();
        Assert.Equal(SpecificationAnalysisStatus.Queued, analysis.Status);
        Assert.Contains(fixture.Queue.Jobs, job => job.AnalysisId == analysis.Id && job.RunId == analysis.RunId);
    }

    [Fact]
    public async Task Completed_analysis_is_singleton_and_exposes_timestamped_provenance()
    {
        var project = await SeedCompletedProjectAsync("user-1");
        var client = fixture.ClientForOwner();

        var first = await client.GetFromJsonAsync<SpecificationDetailsDto>($"/api/projects/{project.Id}/specification");
        var second = await client.PostAsync($"/api/projects/{project.Id}/specification/retry", null);

        Assert.NotNull(first);
        var item = first!.Functions.Single().Items.Single();
        Assert.NotEmpty(item.SourceSegments);
        Assert.Equal(12.5, item.SourceSegments.Single().EndSeconds);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Other_owner_cannot_read_or_retry_analysis()
    {
        var project = await SeedCompletedProjectAsync("user-1");
        var client = fixture.ClientForOwner("user-2");

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/projects/{project.Id}/specification")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/projects/{project.Id}/specification/retry", null)).StatusCode);
    }

    [Fact]
    public async Task Failed_analysis_can_be_retried_once_and_is_requeued()
    {
        var project = await SeedCompletedProjectAsync("user-1");
        await using (var db = fixture.CreateDbContext())
        {
            var analysis = await db.SpecificationAnalyses.SingleAsync(x => x.ProjectEntityId == project.Id);
            analysis.Status = SpecificationAnalysisStatus.Failed;
            await db.SaveChangesAsync();
        }

        var response = await fixture.ClientForOwner().PostAsync($"/api/projects/{project.Id}/specification/retry", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        await using var verify = fixture.CreateDbContext();
        var retried = await verify.SpecificationAnalyses.SingleAsync(x => x.ProjectEntityId == project.Id);
        Assert.Equal(SpecificationAnalysisStatus.Queued, retried.Status);
        Assert.Equal(1, retried.RetryCount);
    }

    private async Task<ProjectEntity> SeedCompletedProjectAsync(string ownerId)
    {
        await using var db = fixture.CreateDbContext();
        var project = new ProjectEntity { OwnerId = ownerId, Name = "Interview" };
        var recording = new VoiceRecording { ProjectEntityId = project.Id, FileName = "voice.wav", ContentType = "audio/wav", SourceType = "audio" };
        var segment = new TranscriptSegment { VoiceRecordingId = recording.Id, StartSeconds = 0, EndSeconds = 12.5, Text = "Need sign-in." };
        var analysis = new SpecificationAnalysis { ProjectEntityId = project.Id, Status = SpecificationAnalysisStatus.Completed, CompletedAt = DateTimeOffset.UtcNow };
        var topic = new AnalysisTopic { SpecificationAnalysisId = analysis.Id, ExternalId = "topic-1", Name = "Authentication" };
        var statement = new AnalysisStatement { SpecificationAnalysisId = analysis.Id, AnalysisTopicId = topic.Id, ExternalId = "st-1", Text = "Users sign in." };
        var function = new SpecificationFunction { SpecificationAnalysisId = analysis.Id, AnalysisTopicId = topic.Id, Title = "Authentication", Description = "Corporate access.", SortOrder = 0 };
        var item = new SpecificationItem { SpecificationAnalysisId = analysis.Id, SpecificationFunctionId = function.Id, Kind = SpecificationItemKind.FunctionalRequirement, Description = "Support sign-in." };
        db.AddRange(project, recording, segment, analysis, topic, statement, function, item,
            new AnalysisStatementSegment { AnalysisStatementId = statement.Id, TranscriptSegmentId = segment.Id },
            new SpecificationFunctionStatement { SpecificationFunctionId = function.Id, AnalysisStatementId = statement.Id },
            new SpecificationItemStatement { SpecificationItemId = item.Id, AnalysisStatementId = statement.Id });
        await db.SaveChangesAsync();
        return project;
    }
}
