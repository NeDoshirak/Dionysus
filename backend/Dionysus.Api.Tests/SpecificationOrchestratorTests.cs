using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;

public sealed class SpecificationOrchestratorTests
{
    [Fact]
    public async Task Runs_stage0_then_one_final_request_and_publishes_one_function_with_segment_sources()
    {
        await using var db = CreateContext();
        var (analysis, segment) = await SeedAsync(db);
        var ai = new ScriptedStructuredAiService(ValidFinal(segment.Id));
        var orchestrator = new SpecificationOrchestrator(db, ai, new SpecificationContractValidator());

        await orchestrator.RunAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), CancellationToken.None);

        db.ChangeTracker.Clear();
        var saved = await db.SpecificationAnalyses
            .Include(x => x.Functions).ThenInclude(x => x.StatementLinks).ThenInclude(x => x.AnalysisStatement).ThenInclude(x => x.SegmentLinks).ThenInclude(x => x.TranscriptSegment)
            .Include(x => x.Functions).ThenInclude(x => x.Items).ThenInclude(x => x.StatementLinks).ThenInclude(x => x.AnalysisStatement).ThenInclude(x => x.SegmentLinks).ThenInclude(x => x.TranscriptSegment)
            .SingleAsync();

        Assert.Equal(SpecificationAnalysisStatus.Completed, saved.Status);
        Assert.Equal("Authentication", saved.Functions.Single().Title);
        Assert.Empty(saved.Topics);
        Assert.Single(saved.Functions);
        Assert.NotNull(saved.Stage0RawResponse);
        Assert.Null(saved.Stage1RawResponse);
        Assert.Null(saved.Stage2RawResponse);
        Assert.Equal(1, ai.FinalCalls);
        Assert.Equal(0, ai.Stage1Calls + ai.Stage2Calls + ai.Stage3Calls);
        Assert.Equal(segment.Id, ai.FinalRequest!.Segments.Single().Id);
        Assert.Equal(segment.StartSeconds, ai.FinalRequest.Segments.Single().StartSeconds);
        Assert.Equal("cleaned transcript", ai.FinalRequest.Segments.Single().Text);

        var source = Assert.Single(saved.Functions.Single().StatementLinks).AnalysisStatement.SegmentLinks.Single().TranscriptSegment;
        Assert.Equal(segment.StartSeconds, source.StartSeconds);
        Assert.Equal(segment.EndSeconds, source.EndSeconds);
        Assert.Equal("original transcript", source.Text);
    }

    [Fact]
    public async Task Final_contract_failure_marks_analysis_failed_without_publishing_function()
    {
        await using var db = CreateContext();
        var (analysis, segment) = await SeedAsync(db);
        var ai = new ScriptedStructuredAiService(ValidFinal(segment.Id), new SpecificationContractException("final", "item-sources"));
        var orchestrator = new SpecificationOrchestrator(db, ai, new SpecificationContractValidator());

        await orchestrator.RunAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), CancellationToken.None);

        db.ChangeTracker.Clear();
        var saved = await db.SpecificationAnalyses.Include(x => x.Functions).Include(x => x.Items).SingleAsync();
        Assert.Equal(SpecificationAnalysisStatus.Failed, saved.Status);
        Assert.Equal("final failed: final/item-sources", saved.Error);
        Assert.Empty(saved.Functions);
        Assert.Empty(saved.Items);
        Assert.NotNull(saved.Stage0RawResponse);
    }

    [Fact]
    public async Task Provider_cancellation_is_propagated_without_marking_failed()
    {
        await using var db = CreateContext();
        var (analysis, segment) = await SeedAsync(db);
        var ai = new ScriptedStructuredAiService(ValidFinal(segment.Id), cancelStage0: true);
        var orchestrator = new SpecificationOrchestrator(db, ai, new SpecificationContractValidator());

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            orchestrator.RunAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), CancellationToken.None));

        db.ChangeTracker.Clear();
        var saved = await db.SpecificationAnalyses.SingleAsync();
        Assert.NotEqual(SpecificationAnalysisStatus.Failed, saved.Status);
        Assert.Equal(SpecificationAnalysisStatus.RunningStage0, saved.Status);
    }

    [Fact]
    public async Task Stale_run_id_leaves_analysis_and_transcript_unchanged()
    {
        await using var db = CreateContext();
        var (analysis, segment) = await SeedAsync(db);
        var orchestrator = new SpecificationOrchestrator(db, new ScriptedStructuredAiService(ValidFinal(segment.Id)), new SpecificationContractValidator());

        await orchestrator.RunAsync(new SpecificationAnalysisJob(analysis.Id, Guid.NewGuid()), CancellationToken.None);

        db.ChangeTracker.Clear();
        var saved = await db.SpecificationAnalyses.SingleAsync();
        var savedSegment = await db.TranscriptSegments.SingleAsync();
        Assert.Equal(SpecificationAnalysisStatus.Queued, saved.Status);
        Assert.Null(saved.Stage0RawResponse);
        Assert.Null(savedSegment.CleanedText);
    }

    private static AppDbContext CreateContext() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

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

    private static FinalSpecificationResponse ValidFinal(Guid segmentId) => new(
        "1.0", "Authentication", "Authentication function",
        [new FinalBusinessContextDto("ctx-1", "Business authentication.", [segmentId])],
        [new FinalRoleDto("role-1", "User", "System user.", [segmentId])],
        [new FinalFunctionalRequirementDto("req-1", "Sign in", "Users can sign in.", SpecificationPriority.Required, [segmentId])],
        [], [], [], [], []);

    private sealed class ScriptedStructuredAiService : IStructuredSpecificationAiService
    {
        private readonly FinalSpecificationResponse _final;
        private readonly Exception? _finalException;
        private readonly bool _cancelStage0;

        public ScriptedStructuredAiService(FinalSpecificationResponse final, Exception? finalException = null, bool cancelStage0 = false)
        {
            _final = final;
            _finalException = finalException;
            _cancelStage0 = cancelStage0;
        }

        public int FinalCalls { get; private set; }
        public int Stage1Calls { get; private set; }
        public int Stage2Calls { get; private set; }
        public int Stage3Calls { get; private set; }
        public FinalSpecificationRequest? FinalRequest { get; private set; }

        public Task<Stage0CleanupResponse> RunStage0Async(Stage0CleanupRequest request, CancellationToken ct)
        {
            if (_cancelStage0) throw new OperationCanceledException();
            return Task.FromResult(new Stage0CleanupResponse("1.0", request.Segments.Select(x => new Stage0CleanedSegmentDto(x.Id, "cleaned transcript")).ToList()));
        }

        public Task<Stage1ExtractionResponse> RunStage1Async(Stage1ExtractionRequest request, CancellationToken ct)
        {
            Stage1Calls++;
            throw new InvalidOperationException("Stage 1 must not be called.");
        }

        public Task<Stage2ReviewResponse> RunStage2Async(Stage1ExtractionResponse request, CancellationToken ct)
        {
            Stage2Calls++;
            throw new InvalidOperationException("Stage 2 must not be called.");
        }

        public Task<Stage3FunctionResponse> RunStage3Async(Stage3FunctionRequest request, CancellationToken ct)
        {
            Stage3Calls++;
            throw new InvalidOperationException("Stage 3 must not be called.");
        }

        public Task<FinalSpecificationResponse> RunFinalSpecificationAsync(FinalSpecificationRequest request, CancellationToken ct)
        {
            FinalCalls++;
            FinalRequest = request;
            if (_finalException is not null) throw _finalException;
            return Task.FromResult(_final);
        }
    }
}
