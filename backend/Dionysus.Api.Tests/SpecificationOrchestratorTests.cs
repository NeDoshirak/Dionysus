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
            .Include(x => x.Items).ThenInclude(x => x.StatementLinks)
            .SingleAsync();
        var savedSegment = await db.TranscriptSegments.SingleAsync();

        Assert.True(saved.Status == SpecificationAnalysisStatus.Completed, saved.Error);
        Assert.Equal("cleaned transcript", savedSegment.CleanedText);
        Assert.Equal("cleaned transcript", ai.Stage1Request!.Segments[0].Text);
        Assert.NotNull(saved.Stage0RawResponse);
        Assert.NotNull(saved.Stage1RawResponse);
        Assert.NotNull(saved.Stage2RawResponse);
        Assert.Equal(3, saved.Statements.Count);
        Assert.Contains(saved.Statements, x => x.IsBusinessContext && x.ExternalId == "ctx-1");
        var businessContext = Assert.Single(saved.Items, x => x.Kind == SpecificationItemKind.BusinessContext);
        Assert.Null(businessContext.SpecificationFunctionId);
        Assert.Equal("The business needs sign-in.", businessContext.Description);
        Assert.Equal(0, businessContext.SortOrder);
        var contextStatement = Assert.Single(saved.Statements, x => x.IsBusinessContext && x.ExternalId == "ctx-1");
        Assert.Equal(contextStatement.Id, Assert.Single(businessContext.StatementLinks).AnalysisStatementId);
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
        Assert.Equal("stage2 failed: stage2/context-identity", saved.Error);
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

    [Fact]
    public async Task Stage2_reconciles_renamed_merged_topics_by_statement_membership()
    {
        await using var db = CreateContext();
        var (analysis, segment) = await SeedAsync(db);
        var responses = ValidResponses(segment.Id) with
        {
            Stage2 = ValidResponses(segment.Id).Stage2 with
            {
                Topics = [new Stage2TopicDto("topic-9", "Identity and access", ["st-1", "st-2"])]
            }
        };
        var orchestrator = new SpecificationOrchestrator(db, new ScriptedStructuredAiService(responses), new SpecificationContractValidator());

        await orchestrator.RunAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), CancellationToken.None);

        var saved = await db.AnalysisTopics.Include(x => x.Statements).SingleAsync(x => x.ExternalId == "topic-9");
        Assert.Equal("topic-9", saved.ExternalId);
        Assert.Equal("Identity and access", saved.Name);
        Assert.Equal(2, saved.Statements.Count);
    }

    [Fact]
    public async Task Stage3_runs_no_more_than_four_function_requests_at_once()
    {
        await using var db = CreateContext();
        var (analysis, segment) = await SeedAsync(db);
        var responses = ManyTopicResponses(segment.Id, 7);
        var ai = new BlockingStage3AiService(responses);
        var orchestrator = new SpecificationOrchestrator(db, ai, new SpecificationContractValidator());

        await orchestrator.RunAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), CancellationToken.None);

        Assert.InRange(ai.MaximumConcurrentStage3Calls, 1, 4);
        Assert.Equal(7, await db.SpecificationFunctions.CountAsync());
    }

    [Fact]
    public async Task Stage3_publishes_items_sources_and_project_contradictions_atomically()
    {
        await using var db = CreateContext();
        var (analysis, segment) = await SeedAsync(db);
        var responses = ValidResponses(segment.Id);
        responses = responses with
        {
            Stage2 = responses.Stage2 with
            {
                Relations = [new StageRelationDto("rel-1", AnalysisRelationType.Contradicts, ["st-1"], ["st-2"], "Conflicting behavior")]
            }
        };
        var orchestrator = new SpecificationOrchestrator(db, new ScriptedStructuredAiService(responses), new SpecificationContractValidator());

        await orchestrator.RunAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), CancellationToken.None);

        var saved = await db.SpecificationAnalyses
            .Include(x => x.Functions).ThenInclude(x => x.StatementLinks)
            .Include(x => x.Items).ThenInclude(x => x.StatementLinks)
            .SingleAsync();
        Assert.True(saved.Status == SpecificationAnalysisStatus.Completed, saved.Error ?? "no error");
        Assert.NotNull(saved.CompletedAt);
        Assert.Single(saved.Functions);
        Assert.Equal(1, saved.Items.Count(x => x.Kind == SpecificationItemKind.ProjectContradiction));
        Assert.Contains(saved.Items, x => x.Kind == SpecificationItemKind.FunctionalRequirement && !x.IsManual);
        Assert.All(saved.Functions.Single().StatementLinks, link => Assert.Contains(link.AnalysisStatementId, saved.Statements.Select(statement => statement.Id)));
        Assert.Equal(2, saved.Items.Single(x => x.Kind == SpecificationItemKind.ProjectContradiction).StatementLinks.Count);
    }

    [Fact]
    public async Task Failed_one_stage3_function_publishes_no_final_rows()
    {
        await using var db = CreateContext();
        var (analysis, segment) = await SeedAsync(db);
        var responses = ManyTopicResponses(segment.Id, 2);
        var orchestrator = new SpecificationOrchestrator(db, new FailingStage3AiService(responses, "topic-2"), new SpecificationContractValidator());

        await orchestrator.RunAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), CancellationToken.None);

        var saved = await db.SpecificationAnalyses.Include(x => x.Functions).Include(x => x.Items).SingleAsync();
        Assert.Equal(SpecificationAnalysisStatus.Failed, saved.Status);
        Assert.Equal("stage3 failed: topic-2/InvalidOperationException", saved.Error);
        Assert.Empty(saved.Functions);
        Assert.Empty(saved.Items);
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
        new Stage3FunctionResponse("1.0",
            new Stage3FunctionDto("Authentication", "Authentication function", ["st-1", "st-2"]),
            [new Stage3RoleDto("role-1", "User", "A person who signs in.", ["st-1"])],
            [new Stage3FunctionalRequirementDto("req-1", "Sign in", "Users can sign in.", SpecificationPriority.Required, ["st-1"])],
            [], [], [], [], []));
    }

    private static ScriptedResponses ManyTopicResponses(Guid segmentId, int count)
    {
        var context = new StageBusinessContextDto("ctx-1", "The business needs sign-in.", [segmentId]);
        var topics = Enumerable.Range(1, count)
            .Select(i => new Stage1TopicDto($"topic-{i}", $"Topic {i}", [new Stage1StatementDto($"st-{i}", $"Statement {i}.", [segmentId])]))
            .ToList();
        var statements = topics.SelectMany(x => x.Statements)
            .Select(x => new Stage2StatementDto(x.Id, x.Text, AnalysisStatementStatus.Active, x.SourceSegmentIds)).ToList();
        var stage2Topics = topics.Select(x => new Stage2TopicDto(x.Id, x.Name, x.Statements.Select(statement => statement.Id).ToList())).ToList();
        return new(
            new Stage0CleanupResponse("1.0", [new Stage0CleanedSegmentDto(segmentId, "cleaned transcript")]),
            new Stage1ExtractionResponse("1.0", [context], topics),
            new Stage2ReviewResponse("1.0", [context], stage2Topics, statements, []),
            new Stage3FunctionResponse("1.0", new Stage3FunctionDto("Function", "Description", ["st-1"]), [], [], [], [], [], [], []));
    }

    private sealed record ScriptedResponses(Stage0CleanupResponse Stage0, Stage1ExtractionResponse Stage1, Stage2ReviewResponse Stage2, Stage3FunctionResponse Stage3);

    private class ScriptedStructuredAiService : IStructuredSpecificationAiService
    {
        protected ScriptedResponses Responses { get; }
        private readonly bool _cancelStage0;

        public ScriptedStructuredAiService(ScriptedResponses responses, bool cancelStage0 = false)
        {
            Responses = responses;
            _cancelStage0 = cancelStage0;
        }

        public Stage1ExtractionRequest? Stage1Request { get; private set; }
        public Task<Stage0CleanupResponse> RunStage0Async(Stage0CleanupRequest request, CancellationToken ct) => CancelOr(() => Responses.Stage0);
        public Task<Stage1ExtractionResponse> RunStage1Async(Stage1ExtractionRequest request, CancellationToken ct)
        {
            Stage1Request = request;
            return Task.FromResult(Responses.Stage1);
        }
        public Task<Stage2ReviewResponse> RunStage2Async(Stage1ExtractionResponse request, CancellationToken ct) => Task.FromResult(Responses.Stage2);
        public virtual Task<Stage3FunctionResponse> RunStage3Async(Stage3FunctionRequest request, CancellationToken ct) => Task.FromResult(Responses.Stage3);

        private Task<T> CancelOr<T>(Func<T> result)
        {
            if (_cancelStage0) throw new OperationCanceledException();
            return Task.FromResult(result());
        }
    }

    private sealed class BlockingStage3AiService : ScriptedStructuredAiService
    {
        private int _active;
        public int MaximumConcurrentStage3Calls { get; private set; }

        public BlockingStage3AiService(ScriptedResponses responses) : base(responses) { }

        public override async Task<Stage3FunctionResponse> RunStage3Async(Stage3FunctionRequest request, CancellationToken ct)
        {
            var active = Interlocked.Increment(ref _active);
            MaximumConcurrentStage3Calls = Math.Max(MaximumConcurrentStage3Calls, active);
            await Task.Delay(50, ct);
            Interlocked.Decrement(ref _active);
            var number = request.Function.TopicId["topic-".Length..];
            return Responses.Stage3 with
            {
                Function = Responses.Stage3.Function with { Title = $"Function {number}", SourceStatementIds = request.Function.Statements.Select(x => x.Id).ToList() }
            };
        }
    }

    private sealed class FailingStage3AiService : ScriptedStructuredAiService
    {
        private readonly string _failingTopic;

        public FailingStage3AiService(ScriptedResponses responses, string failingTopic) : base(responses)
        {
            _failingTopic = failingTopic;
        }

        public override Task<Stage3FunctionResponse> RunStage3Async(Stage3FunctionRequest request, CancellationToken ct)
        {
            if (request.Function.TopicId == _failingTopic) throw new InvalidOperationException("stage3 failure");
            return Task.FromResult(Responses.Stage3 with
            {
                Function = Responses.Stage3.Function with { SourceStatementIds = request.Function.Statements.Select(x => x.Id).ToList() }
            });
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
