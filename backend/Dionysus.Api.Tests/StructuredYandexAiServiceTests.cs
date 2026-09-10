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
        var service = Create(new ScriptedTextGenerationService(exception: new InvalidOperationException("secret provider detail")));

        var exception = await Assert.ThrowsAsync<SpecificationAiInfrastructureException>(() =>
            service.RunStage1Async(ValidStage1Request(), CancellationToken.None));

        Assert.DoesNotContain("secret provider detail", exception.Message);
        Assert.Equal("InvalidOperationException", exception.DiagnosticCode);
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

    private static StructuredYandexAiService Create(ITextGenerationService transport) =>
        new(transport, new TestPrompts(), new SpecificationContractValidator());

    private sealed class TestPrompts : ISpecificationPromptFactory
    {
        public string Stage0Instructions() => "Stage 0";
        public string Stage1Instructions() => "Stage 1";
        public string Stage2Instructions() => "Stage 2";
        public string Stage3Instructions() => "Stage 3";
    }

    private sealed class ScriptedTextGenerationService(string? text = null, Exception? exception = null) : ITextGenerationService
    {
        public string LastInput { get; private set; } = string.Empty;
        public string? LastInstructions { get; private set; }

        public Task<YandexAiResult> RespondAsync(string input, string? instructions, CancellationToken cancellationToken)
        {
            LastInput = input;
            LastInstructions = instructions;
            if (exception is not null) throw exception;
            return Task.FromResult(new YandexAiResult("response", "model", text ?? string.Empty, null));
        }
    }
}
