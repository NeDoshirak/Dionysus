using System.Text.Json;

public sealed class SpecificationAiInfrastructureException : Exception
{
    public SpecificationAiInfrastructureException(Exception innerException)
        : base("The specification AI provider is temporarily unavailable.", innerException)
    {
        DiagnosticCode = innerException switch
        {
            YandexAiResponseFormatException formatException => formatException.DiagnosticCode,
            YandexAiException yandexException => $"yandex-http-{yandexException.StatusCode}",
            _ => innerException.GetType().Name
        };
    }

    public string DiagnosticCode { get; }
}

public sealed class StructuredYandexAiService(
    ITextGenerationService textGeneration,
    ISpecificationPromptFactory prompts,
    ISpecificationContractValidator validator,
    ILogger<StructuredYandexAiService>? logger = null) : IStructuredSpecificationAiService
{
    private const int MaxAttempts = 4;

    public Task<Stage0CleanupResponse> RunStage0Async(Stage0CleanupRequest request, CancellationToken ct) =>
        RunWithContractRetriesAsync<Stage0CleanupRequest, Stage0CleanupResponse>(
            request,
            prompts.Stage0Instructions,
            response => validator.ValidateStage0(request, response),
            ct);

    public Task<Stage1ExtractionResponse> RunStage1Async(Stage1ExtractionRequest request, CancellationToken ct) =>
        RunWithContractRetriesAsync<Stage1ExtractionRequest, Stage1ExtractionResponse>(
            request,
            prompts.Stage1Instructions,
            response => validator.ValidateStage1(
                request.Segments.Select(segment => new TranscriptSegment
                {
                    Id = segment.Id,
                    StartSeconds = segment.StartSeconds,
                    EndSeconds = segment.EndSeconds,
                    Text = segment.Text
                }).ToArray(),
                response),
            ct);

    public Task<Stage2ReviewResponse> RunStage2Async(Stage1ExtractionResponse request, CancellationToken ct) =>
        RunWithContractRetriesAsync<Stage1ExtractionResponse, Stage2ReviewResponse>(
            request,
            prompts.Stage2Instructions,
            response => validator.ValidateStage2(request, response),
            ct);

    public Task<Stage3FunctionResponse> RunStage3Async(Stage3FunctionRequest request, CancellationToken ct) =>
        RunWithContractRetriesAsync<Stage3FunctionRequest, Stage3FunctionResponse>(
            request,
            prompts.Stage3Instructions,
            response => validator.ValidateStage3(request, response),
            ct);

    public Task<FinalSpecificationResponse> RunFinalSpecificationAsync(FinalSpecificationRequest request, CancellationToken ct) =>
        RunWithContractRetriesAsync<FinalSpecificationRequest, FinalSpecificationResponse>(
            request,
            prompts.FinalInstructions,
            response => validator.ValidateFinal(request, response),
            ct);

    private async Task<TResponse> RunWithContractRetriesAsync<TRequest, TResponse>(
        TRequest request,
        Func<string> instructionsFactory,
        Action<TResponse> validate,
        CancellationToken ct)
        where TRequest : notnull
    {
        string? previousError = null;
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            try
            {
                var instructions = instructionsFactory();
                if (previousError is not null)
                {
                    instructions += $"\n\nThe previous attempt failed contract validation with diagnostic code '{previousError}'. Return a complete corrected JSON response and fix this exact issue.";
                }

                return await RunAsync<TRequest, TResponse>(
                    request,
                    instructions,
                    validate,
                    ct);
            }
            catch (SpecificationContractException exception) when (attempt < MaxAttempts - 1)
            {
                previousError = $"{exception.Stage}/{exception.Rule}";
                logger?.LogWarning(
                    "Retrying specification AI request. RequestType: {RequestType}; Attempt: {Attempt}; MaxAttempts: {MaxAttempts}; DiagnosticCode: {DiagnosticCode}",
                    typeof(TRequest).Name,
                    attempt + 2,
                    MaxAttempts,
                    previousError);
            }
        }

        throw new InvalidOperationException("Specification AI retry loop completed without a result.");
    }

    private async Task<TResponse> RunAsync<TRequest, TResponse>(
        TRequest request,
        string instructions,
        Action<TResponse> validate,
        CancellationToken ct = default)
        where TRequest : notnull
    {
        string input;
        try
        {
            input = JsonSerializer.Serialize(request, SpecificationJson.Options);
            var result = await textGeneration.RespondAsync(input, instructions, ct);
            var response = SpecificationJson.Deserialize<TResponse>(result.Text);
            validate(response);
            return response;
        }
        catch (SpecificationContractException exception)
        {
            logger?.LogWarning(
                "Specification AI contract violation. Stage: {Stage}; ContractStage: {ContractStage}; Rule: {Rule}",
                typeof(TRequest).Name,
                exception.Stage,
                exception.Rule);
            throw;
        }
        catch (JsonException)
        {
            logger?.LogWarning("Specification AI returned invalid contract JSON. Stage: {Stage}", typeof(TRequest).Name);
            throw new SpecificationContractException("provider-response", "json");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            var infrastructureException = new SpecificationAiInfrastructureException(exception);
            logger?.LogError(
                "Specification AI request timed out. Stage: {Stage}; DiagnosticCode: {DiagnosticCode}",
                typeof(TRequest).Name,
                infrastructureException.DiagnosticCode);
            throw infrastructureException;
        }
        catch (Exception exception)
        {
            logger?.LogError(
                "Specification AI stage failed. Stage: {Stage}; FailureType: {FailureType}; DiagnosticCode: {DiagnosticCode}",
                typeof(TRequest).Name,
                exception.GetType().Name,
                new SpecificationAiInfrastructureException(exception).DiagnosticCode);
            throw new SpecificationAiInfrastructureException(exception);
        }
    }
}
