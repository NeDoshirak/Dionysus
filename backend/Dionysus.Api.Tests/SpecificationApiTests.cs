using System.Net;
using System.Security.Claims;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class SpecificationApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Unauthenticated_request_returns_unauthorized()
    {
        var response = await factory.CreateClient().GetAsync($"/api/projects/{Guid.NewGuid()}/specification");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Other_user_gets_not_found_for_specification()
    {
        await using var db = CreateDb();
        var project = SeedCompleted(db, "user-1");
        var controller = CreateController(db, "user-2", new RecordingQueue());

        var result = await controller.Get(project.Id, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Retry_accepts_only_failed_analysis_and_enqueues_new_run()
    {
        await using var db = CreateDb();
        var project = SeedCompleted(db, "user-1");
        project.SpecificationAnalysis!.Status = SpecificationAnalysisStatus.Failed;
        var oldRun = project.SpecificationAnalysis.RunId;
        var queue = new RecordingQueue();
        var controller = CreateController(db, "user-1", queue);

        var result = await controller.Retry(project.Id, CancellationToken.None);

        Assert.IsType<AcceptedResult>(result);
        var analysis = await db.SpecificationAnalyses.SingleAsync();
        Assert.Equal(SpecificationAnalysisStatus.Queued, analysis.Status);
        Assert.NotEqual(oldRun, analysis.RunId);
        Assert.Equal(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), Assert.Single(queue.Jobs));
    }

    [Fact]
    public async Task Manual_item_is_marked_manual_and_rejects_foreign_source()
    {
        await using var db = CreateDb();
        var project = SeedCompleted(db, "user-1");
        var function = project.SpecificationAnalysis!.Functions.Single();
        var controller = CreateController(db, "user-1", new RecordingQueue());

        var created = await controller.CreateItem(project.Id, function.Id,
            new CreateSpecificationItemRequest(SpecificationItemKind.Constraint, null, "Manual restriction", null, []), CancellationToken.None);

        var item = Assert.IsType<CreatedAtActionResult>(created).Value as SpecificationItemDto;
        Assert.NotNull(item);
        Assert.True(item!.IsManual);

        var invalid = await controller.CreateItem(project.Id, function.Id,
            new CreateSpecificationItemRequest(SpecificationItemKind.Constraint, null, "bad", null, [Guid.NewGuid()]), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(invalid);
    }

    [Fact]
    public async Task Get_reconstructs_source_segment_times()
    {
        await using var db = CreateDb();
        var project = SeedCompleted(db, "user-1");
        var controller = CreateController(db, "user-1", new RecordingQueue());

        var result = Assert.IsType<OkObjectResult>(await controller.Get(project.Id, CancellationToken.None));
        var details = Assert.IsType<SpecificationDetailsDto>(result.Value);
        var segment = Assert.Single(Assert.Single(details.Functions).Items).SourceSegments.Single();
        Assert.Equal(12.5, segment.StartSeconds);
        Assert.Equal(18.75, segment.EndSeconds);
    }

    [Fact]
    public async Task Function_create_update_delete_requires_completed_analysis()
    {
        await using var db = CreateDb();
        var project = SeedCompleted(db, "user-1");
        var analysis = project.SpecificationAnalysis!;
        var sourceStatementId = analysis.Statements.Single().Id;
        analysis.Status = SpecificationAnalysisStatus.RunningStage0;
        var controller = CreateController(db, "user-1", new RecordingQueue());

        Assert.IsType<ConflictResult>(await controller.CreateFunction(project.Id,
            new CreateSpecificationFunctionRequest("New function", "New description", 1, [sourceStatementId]), CancellationToken.None));
        Assert.IsType<ConflictResult>(await controller.UpdateFunction(project.Id, analysis.Functions.Single().Id,
            new UpdateSpecificationFunctionRequest("Updated", "Updated description", 2, null), CancellationToken.None));
        Assert.IsType<ConflictResult>(await controller.DeleteFunction(project.Id, analysis.Functions.Single().Id, CancellationToken.None));

        analysis.Status = SpecificationAnalysisStatus.Completed;
        var created = Assert.IsType<CreatedAtActionResult>(await controller.CreateFunction(project.Id,
            new CreateSpecificationFunctionRequest("New function", "New description", 1, [sourceStatementId]), CancellationToken.None));
        var createdFunction = Assert.IsType<SpecificationFunctionDto>(created.Value);

        var updated = Assert.IsType<OkObjectResult>(await controller.UpdateFunction(project.Id, createdFunction.Id,
            new UpdateSpecificationFunctionRequest("Updated", "Updated description", 2, null), CancellationToken.None));
        Assert.Equal("Updated", Assert.IsType<SpecificationFunctionDto>(updated.Value).Title);
        Assert.IsType<NoContentResult>(await controller.DeleteFunction(project.Id, createdFunction.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Item_create_update_delete_requires_completed_analysis()
    {
        await using var db = CreateDb();
        var project = SeedCompleted(db, "user-1");
        var analysis = project.SpecificationAnalysis!;
        var function = analysis.Functions.Single();
        var item = function.Items.Single();
        var sourceStatementId = analysis.Statements.Single().Id;
        analysis.Status = SpecificationAnalysisStatus.RunningStage0;
        var controller = CreateController(db, "user-1", new RecordingQueue());

        Assert.IsType<ConflictResult>(await controller.CreateItem(project.Id, function.Id,
            new CreateSpecificationItemRequest(SpecificationItemKind.Constraint, "New item", "New description", "high", [sourceStatementId]), CancellationToken.None));
        Assert.IsType<ConflictResult>(await controller.UpdateItem(project.Id, function.Id, item.Id,
            new UpdateSpecificationItemRequest("Updated", "Updated description", "low", null), CancellationToken.None));
        Assert.IsType<ConflictResult>(await controller.DeleteItem(project.Id, function.Id, item.Id, CancellationToken.None));

        analysis.Status = SpecificationAnalysisStatus.Completed;
        var created = Assert.IsType<CreatedAtActionResult>(await controller.CreateItem(project.Id, function.Id,
            new CreateSpecificationItemRequest(SpecificationItemKind.Constraint, "New item", "New description", "high", [sourceStatementId]), CancellationToken.None));
        var createdItem = Assert.IsType<SpecificationItemDto>(created.Value);

        var updated = Assert.IsType<OkObjectResult>(await controller.UpdateItem(project.Id, function.Id, createdItem.Id,
            new UpdateSpecificationItemRequest("Updated", "Updated description", "low", null), CancellationToken.None));
        Assert.Equal("Updated", Assert.IsType<SpecificationItemDto>(updated.Value).Title);
        Assert.IsType<NoContentResult>(await controller.DeleteItem(project.Id, function.Id, createdItem.Id, CancellationToken.None));
    }

    private static AppDbContext CreateDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static ProjectEntity SeedCompleted(AppDbContext db, string owner)
    {
        var project = new ProjectEntity { OwnerId = owner, Name = "Project" };
        var recording = new VoiceRecording { ProjectEntityId = project.Id, FileName = "voice.wav", ContentType = "audio/wav", SourceType = "audio" };
        var segment = new TranscriptSegment { VoiceRecordingId = recording.Id, StartSeconds = 12.5, EndSeconds = 18.75, Text = "source" };
        recording.Segments.Add(segment);
        var analysis = new SpecificationAnalysis { ProjectEntityId = project.Id, Status = SpecificationAnalysisStatus.Completed, Project = project };
        var statement = new AnalysisStatement { SpecificationAnalysisId = analysis.Id, ExternalId = "st-1", Text = "statement", SegmentLinks = [new AnalysisStatementSegment { TranscriptSegment = segment }] };
        var function = new SpecificationFunction { SpecificationAnalysisId = analysis.Id, Title = "Function", Description = "Description", SortOrder = 0, Topic = new AnalysisTopic { SpecificationAnalysisId = analysis.Id, ExternalId = "topic-1", Name = "Topic" }, StatementLinks = [new SpecificationFunctionStatement { AnalysisStatement = statement }] };
        function.Items.Add(new SpecificationItem { SpecificationAnalysisId = analysis.Id, SpecificationFunction = function, Kind = SpecificationItemKind.Constraint, Description = "Constraint", StatementLinks = [new SpecificationItemStatement { AnalysisStatement = statement }] });
        analysis.Statements.Add(statement); analysis.Functions.Add(function); project.Recordings.Add(recording); project.SpecificationAnalysis = analysis;
        db.Projects.Add(project); db.SaveChanges();
        return project;
    }

    private static SpecificationsController CreateController(AppDbContext db, string user, ISpecificationAnalysisQueue queue) => new(db, queue)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user)], "test")) } }
    };

    private sealed class RecordingQueue : ISpecificationAnalysisQueue
    {
        public List<SpecificationAnalysisJob> Jobs { get; } = [];
        public ValueTask EnqueueAsync(SpecificationAnalysisJob job, CancellationToken ct) { Jobs.Add(job); return ValueTask.CompletedTask; }
        public ValueTask<SpecificationAnalysisJob> DequeueAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
