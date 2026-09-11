using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public sealed class StructuredYandexAiServiceTests
{
    [Fact]
    public async Task Invalid_model_json_becomes_a_contract_exception()
    {
        var service = Create(new ScriptedTextGenerationService("not json"));

        await Assert.ThrowsAsync<SpecificationContractException>(() =>
            service.RunStage1Async(ValidStage1Request(), CancellationToken.None));
    }

    [Fact]
    public async Task Provider_failure_becomes_a_safe_infrastructure_exception()
    {
        var transport = new ScriptedTextGenerationService(exception: new InvalidOperationException("secret provider detail"));
        var service = Create(transport);

        var exception = await Assert.ThrowsAsync<SpecificationAiInfrastructureException>(() =>
            service.RunStage1Async(ValidStage1Request(), CancellationToken.None));

        Assert.DoesNotContain("secret provider detail", exception.Message);
        Assert.Equal("InvalidOperationException", exception.DiagnosticCode);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task Stage3_retries_invalid_json_and_includes_the_previous_diagnostic()
    {
        var expected = ValidStage3Response();
        var transport = new SequenceTextGenerationService(
            "not json",
            JsonSerializer.Serialize(expected, SpecificationJson.Options));
        var service = Create(transport);

        var actual = await service.RunStage3Async(ValidStage3Request(), CancellationToken.None);

        Assert.Equal(expected.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(expected.Function.Title, actual.Function.Title);
        Assert.Equal(expected.Function.Description, actual.Function.Description);
        Assert.Equal(expected.Function.SourceStatementIds, actual.Function.SourceStatementIds);
        Assert.Empty(actual.Roles);
        Assert.Empty(actual.FunctionalRequirements);
        Assert.Empty(actual.UserScenarios);
        Assert.Empty(actual.Constraints);
        Assert.Empty(actual.Conditions);
        Assert.Empty(actual.Agreements);
        Assert.Empty(actual.KeyQuestions);
        Assert.Equal(2, transport.CallCount);
        Assert.DoesNotContain("provider-response/json", transport.Instructions[0]);
        Assert.Contains("provider-response/json", transport.Instructions[1]);
    }

    [Fact]
    public async Task Stage0_retries_invalid_json_and_includes_the_previous_diagnostic()
    {
        var expected = ValidStage0Response();
        var transport = new SequenceTextGenerationService(
            "not json",
            JsonSerializer.Serialize(expected, SpecificationJson.Options));
        var service = Create(transport);

        var actual = await service.RunStage0Async(ValidStage0Request(), CancellationToken.None);

        Assert.Equal(expected.Segments[0].CleanedText, actual.Segments[0].CleanedText);
        Assert.Equal(2, transport.CallCount);
        Assert.Contains("provider-response/json", transport.Instructions[1]);
    }

    [Fact]
    public async Task Stage1_retries_contract_validation_failures_with_the_previous_rule()
    {
        var invalid = ValidStage1Response() with
        {
            Topics = [new Stage1TopicDto("topic-1", "Authentication", [
                new Stage1StatementDto("st-1", "Users sign in.", [Guid.Parse("00000000-0000-0000-0000-000000000099")])
            ])]
        };
        var transport = new SequenceTextGenerationService(
            JsonSerializer.Serialize(invalid, SpecificationJson.Options),
            JsonSerializer.Serialize(ValidStage1Response(), SpecificationJson.Options));
        var service = Create(transport);

        await service.RunStage1Async(ValidStage1Request(), CancellationToken.None);

        Assert.Equal(2, transport.CallCount);
        Assert.Contains("stage1/statement-sources", transport.Instructions[1]);
    }

    [Fact]
    public async Task Stage2_retries_invalid_json_and_includes_the_previous_diagnostic()
    {
        var expected = ValidStage2Response();
        var transport = new SequenceTextGenerationService(
            "not json",
            JsonSerializer.Serialize(expected, SpecificationJson.Options));
        var service = Create(transport);

        await service.RunStage2Async(ValidStage1Response(), CancellationToken.None);

        Assert.Equal(2, transport.CallCount);
        Assert.Contains("provider-response/json", transport.Instructions[1]);
    }

    [Fact]
    public async Task Stage3_retries_twice_before_succeeding_on_the_third_attempt()
    {
        var expected = ValidStage3Response();
        var transport = new SequenceTextGenerationService(
            "not json",
            "still not json",
            JsonSerializer.Serialize(expected, SpecificationJson.Options));
        var service = Create(transport);

        await service.RunStage3Async(ValidStage3Request(), CancellationToken.None);

        Assert.Equal(3, transport.CallCount);
        Assert.Contains("provider-response/json", transport.Instructions[2]);
    }

    [Fact]
    public async Task Stage3_retries_contract_validation_failures_with_the_previous_rule()
    {
        var invalid = ValidStage3Response() with
        {
            Function = ValidStage3Response().Function with { SourceStatementIds = ["st-404"] }
        };
        var transport = new SequenceTextGenerationService(
            JsonSerializer.Serialize(invalid, SpecificationJson.Options),
            JsonSerializer.Serialize(ValidStage3Response(), SpecificationJson.Options));
        var service = Create(transport);

        await service.RunStage3Async(ValidStage3Request(), CancellationToken.None);

        Assert.Equal(2, transport.CallCount);
        Assert.Contains("stage3/function-sources", transport.Instructions[1]);
    }

    [Fact]
    public async Task Stage3_stops_after_three_retries()
    {
        var transport = new SequenceTextGenerationService(
            "not json",
            "still not json",
            "again not json",
            "finally not json",
            "unexpected fifth attempt");
        var service = Create(transport);

        await Assert.ThrowsAsync<SpecificationContractException>(() =>
            service.RunStage3Async(ValidStage3Request(), CancellationToken.None));

        Assert.Equal(4, transport.CallCount);
    }

    [Theory]
    [InlineData("stage0")]
    [InlineData("stage1")]
    [InlineData("stage2")]
    [InlineData("stage3")]
    public async Task Every_stage_stops_after_three_retries(string stage)
    {
        var transport = new SequenceTextGenerationService("not json", "not json", "not json", "not json");
        var service = Create(transport);

        await Assert.ThrowsAsync<SpecificationContractException>(() => InvokeStageAsync(service, stage));

        Assert.Equal(4, transport.CallCount);
    }

    [Theory]
    [InlineData("stage0")]
    [InlineData("stage1")]
    [InlineData("stage2")]
    [InlineData("stage3")]
    public async Task Infrastructure_failures_are_not_retried_for_any_stage(string stage)
    {
        var transport = new ScriptedTextGenerationService(exception: new InvalidOperationException("provider unavailable"));
        var service = Create(transport);

        await Assert.ThrowsAsync<SpecificationAiInfrastructureException>(() => InvokeStageAsync(service, stage));

        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task Cancellation_is_not_retried()
    {
        var transport = new ScriptedTextGenerationService(exception: new OperationCanceledException());
        var service = Create(transport);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.RunStage0Async(ValidStage0Request(), cancellation.Token));

        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task Http_timeout_is_not_retried_but_becomes_an_infrastructure_error()
    {
        var transport = new ScriptedTextGenerationService(exception: new TaskCanceledException("HTTP timeout"));
        var service = Create(transport);

        var exception = await Assert.ThrowsAsync<SpecificationAiInfrastructureException>(() =>
            service.RunStage0Async(ValidStage0Request(), CancellationToken.None));

        Assert.Equal("TaskCanceledException", exception.DiagnosticCode);
        Assert.Equal(1, transport.CallCount);
    }

    [Fact]
    public async Task Valid_model_response_is_validated_and_request_is_serialized_as_contract_json()
    {
        var response = new Stage1ExtractionResponse("1.0", [], [
            new Stage1TopicDto("topic-1", "Authentication", [
                new Stage1StatementDto("st-1", "Users sign in.", [SegmentId])
            ])
        ]);
        var transport = new ScriptedTextGenerationService(JsonSerializer.Serialize(response, SpecificationJson.Options));
        var service = Create(transport);

        var actual = await service.RunStage1Async(ValidStage1Request(), CancellationToken.None);

        Assert.Equal(response.SchemaVersion, actual.SchemaVersion);
        Assert.Equal(response.Topics[0].Id, actual.Topics[0].Id);
        Assert.Equal(response.Topics[0].Statements[0].SourceSegmentIds, actual.Topics[0].Statements[0].SourceSegmentIds);
        Assert.Contains("\"schemaVersion\":\"1.0\"", transport.LastInput);
        Assert.Equal("Stage 1", transport.LastInstructions);
    }

    private static readonly Guid SegmentId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private static Stage1ExtractionRequest ValidStage1Request() => new("1.0", [
        new StageSegmentDto(SegmentId, 0, 1, "Users sign in.")
    ]);

    private static Stage0CleanupRequest ValidStage0Request() => new("1.0", [
        new StageSegmentDto(SegmentId, 0, 1, "Users sign in.")
    ]);

    private static Stage0CleanupResponse ValidStage0Response() => new("1.0", [
        new Stage0CleanedSegmentDto(SegmentId, "Users sign in.")
    ]);

    private static Stage1ExtractionResponse ValidStage1Response() => new(
        "1.0",
        [],
        [new Stage1TopicDto("topic-1", "Authentication", [
            new Stage1StatementDto("st-1", "Users sign in.", [SegmentId])
        ])]);

    private static Stage2ReviewResponse ValidStage2Response() => new(
        "1.0",
        [],
        [new Stage2TopicDto("topic-1", "Authentication", ["st-1"])],
        [new Stage2StatementDto("st-1", "Users sign in.", AnalysisStatementStatus.Active, [SegmentId])],
        []);

    private static Stage3FunctionRequest ValidStage3Request() => new(
        "1.0",
        [],
        new Stage3FunctionInputDto(
            "topic-1",
            "Authentication",
            [new Stage2StatementDto("st-1", "Users sign in.", AnalysisStatementStatus.Active, [SegmentId])],
            []));

    private static Stage3FunctionResponse ValidStage3Response() => new(
        "1.0",
        new Stage3FunctionDto("Authentication", "Authentication function", ["st-1"]),
        [],
        [],
        [],
        [],
        [],
        [],
        []);

    private static StructuredYandexAiService Create(ITextGenerationService transport) =>
        new(transport, new TestPrompts(), new SpecificationContractValidator());

    private static Task InvokeStageAsync(StructuredYandexAiService service, string stage) => stage switch
    {
        "stage0" => service.RunStage0Async(ValidStage0Request(), CancellationToken.None),
        "stage1" => service.RunStage1Async(ValidStage1Request(), CancellationToken.None),
        "stage2" => service.RunStage2Async(ValidStage1Response(), CancellationToken.None),
        "stage3" => service.RunStage3Async(ValidStage3Request(), CancellationToken.None),
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
    };

    private sealed class TestPrompts : ISpecificationPromptFactory
    {
        public string Stage0Instructions() => "Stage 0";
        public string Stage1Instructions() => "Stage 1";
        public string Stage2Instructions() => "Stage 2";
        public string Stage3Instructions() => "Stage 3";
        public string FinalInstructions() => "Final";
    }

    private sealed class ScriptedTextGenerationService(string? text = null, Exception? exception = null) : ITextGenerationService
    {
        public string LastInput { get; private set; } = string.Empty;
        public string? LastInstructions { get; private set; }
        public int CallCount { get; private set; }

        public Task<YandexAiResult> RespondAsync(string input, string? instructions, CancellationToken cancellationToken)
        {
            CallCount++;
            LastInput = input;
            LastInstructions = instructions;
            if (exception is not null) throw exception;
            return Task.FromResult(new YandexAiResult("response", "model", text ?? string.Empty, null));
        }
    }

    private sealed class SequenceTextGenerationService(params object[] outcomes) : ITextGenerationService
    {
        private int _index;

        public int CallCount { get; private set; }
        public List<string> Instructions { get; } = [];

        public Task<YandexAiResult> RespondAsync(string input, string? instructions, CancellationToken cancellationToken)
        {
            CallCount++;
            Instructions.Add(instructions ?? string.Empty);
            var outcome = outcomes[Math.Min(_index++, outcomes.Length - 1)];
            if (outcome is Exception exception) throw exception;
            return Task.FromResult(new YandexAiResult("response", "model", (string)outcome, null));
        }
    }
}
