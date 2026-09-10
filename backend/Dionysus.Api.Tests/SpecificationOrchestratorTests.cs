using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

public sealed class SpecificationOrchestratorTests
{
    [Fact]
    public async Task Persists_cleaned_segments_context_topics_statements_relations_and_stage3_status()
    {
        await using var db = CreateContext();
        var (analysis, segment) = await SeedAsync(db);
        var ai = new ScriptedStructuredAiService(ValidResponses(segment.Id));
        var orchestrator = new SpecificationOrchestrator(db, ai, new SpecificationContractValidator());

        await orchestrator.RunAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), CancellationToken.None);

        db.ChangeTracker.Clear();
        var saved = await db.SpecificationAnalyses
            .Include(x => x.Topics).ThenInclude(x => x.Statements).ThenInclude(x => x.SegmentLinks)
            .Include(x => x.Statements).ThenInclude(x => x.SegmentLinks)
            .Include(x => x.Relations).ThenInclude(x => x.SourceStatementLinks)
            .Include(x => x.Relations).ThenInclude(x => x.TargetStatementLinks)
            .SingleAsync();
        var savedSegment = await db.TranscriptSegments.SingleAsync();

        Assert.True(saved.Status == SpecificationAnalysisStatus.RunningStage3, saved.Error);
        Assert.Equal("cleaned transcript", savedSegment.CleanedText);
        Assert.Equal("cleaned transcript", ai.Stage1Request!.Segments[0].Text);
        Assert.NotNull(saved.Stage0RawResponse);
        Assert.NotNull(saved.Stage1RawResponse);
        Assert.NotNull(saved.Stage2RawResponse);
        Assert.Equal(3, saved.Statements.Count);
        Assert.Contains(saved.Statements, x => x.IsBusinessContext && x.ExternalId == "ctx-1");
        var statements = saved.Statements.Where(x => !x.IsBusinessContext).ToList();
        Assert.Equal(2, statements.Count);
        Assert.Equal(2, saved.Topics.Single(x => x.ExternalId == "topic-1").Statements.Count);
        Assert.Empty(saved.Topics.Single(x => x.ExternalId == "topic-2").Statements);
        Assert.All(statements, statement => Assert.Equal(AnalysisStatementStatus.Active, statement.Status));
        var statement = statements[0];
        Assert.Equal(segment.Id, Assert.Single(statement.SegmentLinks).TranscriptSegmentId);
        var relation = Assert.Single(saved.Relations);
        Assert.Equal("st-1", saved.Statements.Single(x => x.Id == Assert.Single(relation.SourceStatementLinks).AnalysisStatementId).ExternalId);
        Assert.Equal("st-2", saved.Statements.Single(x => x.Id == Assert.Single(relation.TargetStatementLinks).AnalysisStatementId).ExternalId);
    }

    [Fact]
    public async Task Invalid_stage2_marks_failed_without_publishing_final_functions()
    {
        await using var db = CreateContext();
        var (analysis, segment) = await SeedAsync(db);
        var responses = ValidResponses(segment.Id) with
        {
            Stage2 = new Stage2ReviewResponse("1.0", [], [], [], [])
        };
        var orchestrator = new SpecificationOrchestrator(db, new ScriptedStructuredAiService(responses), new SpecificationContractValidator());

        await orchestrator.RunAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), CancellationToken.None);

        db.ChangeTracker.Clear();
        var saved = await db.SpecificationAnalyses.Include(x => x.Functions).Include(x => x.Items).SingleAsync();
        Assert.Equal(SpecificationAnalysisStatus.Failed, saved.Status);
        Assert.Equal("stage2 failed: SpecificationContractException", saved.Error);
        Assert.Empty(saved.Functions);
        Assert.Empty(saved.Items);
        Assert.NotNull(saved.Stage0RawResponse);
        Assert.NotNull(saved.Stage1RawResponse);
        Assert.Null(saved.Stage2RawResponse);
    }

    [Fact]
    public async Task Stale_run_id_leaves_analysis_and_transcript_unchanged()
    {
        await using var db = CreateContext();
        var (analysis, segment) = await SeedAsync(db);
        var staleJob = new SpecificationAnalysisJob(analysis.Id, Guid.NewGuid());
        var orchestrator = new SpecificationOrchestrator(db, new ScriptedStructuredAiService(ValidResponses(segment.Id)), new SpecificationContractValidator());

        await orchestrator.RunAsync(staleJob, CancellationToken.None);

        db.ChangeTracker.Clear();
        var saved = await db.SpecificationAnalyses.SingleAsync();
        var savedSegment = await db.TranscriptSegments.SingleAsync();
        Assert.Equal(SpecificationAnalysisStatus.Queued, saved.Status);
        Assert.Null(saved.Stage0RawResponse);
        Assert.Null(savedSegment.CleanedText);
    }

    [Fact]
    public async Task Provider_cancellation_is_propagated_without_marking_failed()
    {
        await using var db = CreateContext();
        var (analysis, segment) = await SeedAsync(db);
        using var cancellation = new CancellationTokenSource();
        var ai = new ScriptedStructuredAiService(ValidResponses(segment.Id), cancelStage0: true);
        var orchestrator = new SpecificationOrchestrator(db, ai, new SpecificationContractValidator());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            orchestrator.RunAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), cancellation.Token));

        db.ChangeTracker.Clear();
        var saved = await db.SpecificationAnalyses.SingleAsync();
        Assert.NotEqual(SpecificationAnalysisStatus.Failed, saved.Status);
        Assert.Equal(SpecificationAnalysisStatus.RunningStage0, saved.Status);
    }

    [Fact]
    public async Task Run_id_change_during_save_rejects_the_stale_mutation()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var seedDb = CreateContext(databaseName);
        var (analysis, segment) = await SeedAsync(seedDb);
        var interceptor = new RunIdChangeInterceptor(databaseName);
        await using var db = CreateContext(databaseName, interceptor);
        var orchestrator = new SpecificationOrchestrator(db, new ScriptedStructuredAiService(ValidResponses(segment.Id)), new SpecificationContractValidator());

        await orchestrator.RunAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), CancellationToken.None);

        db.ChangeTracker.Clear();
        var saved = await db.SpecificationAnalyses.SingleAsync();
        var savedSegment = await db.TranscriptSegments.SingleAsync();
        Assert.Equal(SpecificationAnalysisStatus.Queued, saved.Status);
        Assert.NotEqual(analysis.RunId, saved.RunId);
        Assert.Null(saved.Stage0RawResponse);
        Assert.Null(savedSegment.CleanedText);
    }

    [Fact]
    public async Task Stage2_clears_memberships_for_topics_omitted_from_response()
    {
        await using var db = CreateContext();
        var (analysis, segment) = await SeedAsync(db);
        var orchestrator = new SpecificationOrchestrator(db, new ScriptedStructuredAiService(ValidResponses(segment.Id)), new SpecificationContractValidator());

        await orchestrator.RunAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), CancellationToken.None);

        db.ChangeTracker.Clear();
        var topics = await db.AnalysisTopics.Include(x => x.Statements).OrderBy(x => x.ExternalId).ToListAsync();
        Assert.Equal(2, topics.Count);
        Assert.Equal(2, topics[0].Statements.Count);
        Assert.Empty(topics[1].Statements);
    }

    private static AppDbContext CreateContext(string? databaseName = null, params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString());
        builder.AddInterceptors(interceptors);
        return new AppDbContext(builder.Options);
    }

    private static async Task<(SpecificationAnalysis Analysis, TranscriptSegment Segment)> SeedAsync(AppDbContext db)
    {
        var project = new ProjectEntity { OwnerId = "owner", Name = "Interview" };
        var recording = new VoiceRecording { ProjectEntityId = project.Id, FileName = "recording.wav", ContentType = "audio/wav", SourceType = "audio", Transcript = "original transcript" };
        var segment = new TranscriptSegment { VoiceRecordingId = recording.Id, StartSeconds = 1, EndSeconds = 2, Text = "original transcript" };
        var analysis = new SpecificationAnalysis { ProjectEntityId = project.Id };
        project.Recordings.Add(recording);
        recording.Segments.Add(segment);
        project.SpecificationAnalysis = analysis;
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (analysis, segment);
    }

    private static ScriptedResponses ValidResponses(Guid segmentId)
    {
        var context = new StageBusinessContextDto("ctx-1", "The business needs sign-in.", [segmentId]);
        return new(
        new Stage0CleanupResponse("1.0", [new Stage0CleanedSegmentDto(segmentId, "cleaned transcript")]),
        new Stage1ExtractionResponse("1.0", [context], [
            new Stage1TopicDto("topic-1", "Authentication", [
                new Stage1StatementDto("st-1", "Users sign in.", [segmentId])]),
            new Stage1TopicDto("topic-2", "Administration", [
                new Stage1StatementDto("st-2", "Admins manage users.", [segmentId])])]),
        new Stage2ReviewResponse("1.0", [context], [
            new Stage2TopicDto("topic-1", "Authentication", ["st-1", "st-2"])], [
            new Stage2StatementDto("st-1", "Users sign in.", AnalysisStatementStatus.Active, [segmentId]),
            new Stage2StatementDto("st-2", "Admins manage users.", AnalysisStatementStatus.Active, [segmentId])], [
            new StageRelationDto("rel-1", AnalysisRelationType.Related, ["st-1"], ["st-2"], "Related statements")]),
        new Stage2ReviewResponse("1.0", [], [], [], []));
    }

    private sealed record ScriptedResponses(Stage0CleanupResponse Stage0, Stage1ExtractionResponse Stage1, Stage2ReviewResponse Stage2, Stage2ReviewResponse Unused);

    private sealed class ScriptedStructuredAiService(ScriptedResponses responses, bool cancelStage0 = false) : IStructuredSpecificationAiService
    {
        public Stage1ExtractionRequest? Stage1Request { get; private set; }
        public Task<Stage0CleanupResponse> RunStage0Async(Stage0CleanupRequest request, CancellationToken ct) => CancelOr(() => responses.Stage0);
        public Task<Stage1ExtractionResponse> RunStage1Async(Stage1ExtractionRequest request, CancellationToken ct)
        {
            Stage1Request = request;
            return Task.FromResult(responses.Stage1);
        }
        public Task<Stage2ReviewResponse> RunStage2Async(Stage1ExtractionResponse request, CancellationToken ct) => Task.FromResult(responses.Stage2);
        public Task<Stage3FunctionResponse> RunStage3Async(Stage3FunctionRequest request, CancellationToken ct) => throw new InvalidOperationException("Stage 3 must not run");

        private Task<T> CancelOr<T>(Func<T> result)
        {
            if (cancelStage0) throw new OperationCanceledException();
            return Task.FromResult(result());
        }
    }

    private sealed class RunIdChangeInterceptor(string databaseName) : SaveChangesInterceptor
    {
        private bool _armed = true;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (_armed && eventData.Context is AppDbContext)
            {
                _armed = false;
                await using var other = CreateContext(databaseName);
                var analysis = await other.SpecificationAnalyses.SingleAsync(cancellationToken);
                analysis.RunId = Guid.NewGuid();
                await other.SaveChangesAsync(cancellationToken);
            }

            return await base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
