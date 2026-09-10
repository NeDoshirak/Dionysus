using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface ISpecificationContractValidator
{
    void ValidateStage0(Stage0CleanupRequest input, Stage0CleanupResponse output);
    void ValidateStage1(IReadOnlyCollection<TranscriptSegment> segments, Stage1ExtractionResponse output);
    void ValidateStage2(Stage1ExtractionResponse input, Stage2ReviewResponse output);
    void ValidateStage3(Stage3FunctionRequest input, Stage3FunctionResponse output);
}

public interface ISpecificationPromptFactory
{
    string Stage0Instructions();
    string Stage1Instructions();
    string Stage2Instructions();
    string Stage3Instructions();
}

public interface IStructuredSpecificationAiService
{
    Task<Stage0CleanupResponse> RunStage0Async(Stage0CleanupRequest request, CancellationToken ct);
    Task<Stage1ExtractionResponse> RunStage1Async(Stage1ExtractionRequest request, CancellationToken ct);
    Task<Stage2ReviewResponse> RunStage2Async(Stage1ExtractionResponse request, CancellationToken ct);
    Task<Stage3FunctionResponse> RunStage3Async(Stage3FunctionRequest request, CancellationToken ct);
}

public sealed record SpecificationAnalysisJob(Guid AnalysisId, Guid RunId);

public interface ISpecificationAnalysisQueue
{
    ValueTask EnqueueAsync(SpecificationAnalysisJob job, CancellationToken ct);
    ValueTask<SpecificationAnalysisJob> DequeueAsync(CancellationToken ct);
}

public interface ISpecificationAnalysisOrchestrator
{
    Task RunAsync(SpecificationAnalysisJob job, CancellationToken ct);
}
