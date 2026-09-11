using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public sealed class FinalSpecificationTests
{
    private static readonly Guid SegmentId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    [Fact]
    public void Final_contract_validator_accepts_direct_segment_sources()
    {
        var input = new FinalSpecificationRequest("1.0", [
            new StageSegmentDto(SegmentId, 1.5, 3.25, "Cleaned transcript")
        ]);
        var output = new FinalSpecificationResponse(
            "1.0",
            "Authentication",
            "Users authenticate in the system.",
            [new FinalBusinessContextDto("ctx-1", "The business needs authentication.", [SegmentId])],
            [new FinalRoleDto("role-1", "User", "System user.", [SegmentId])],
            [new FinalFunctionalRequirementDto("req-1", "Sign in", "Users can sign in.", SpecificationPriority.Required, [SegmentId])],
            [], [], [], [], []);

        new SpecificationContractValidator().ValidateFinal(input, output);
    }

    [Fact]
    public void Final_contract_validator_rejects_an_item_without_a_source_segment()
    {
        var input = new FinalSpecificationRequest("1.0", [new StageSegmentDto(SegmentId, 0, 1, "Text")]);
        var output = new FinalSpecificationResponse(
            "1.0", "Title", "Description", [],
            [new FinalRoleDto("role-1", "User", "Description", [])],
            [], [], [], [], [], []);

        var exception = Assert.Throws<SpecificationContractException>(() =>
            new SpecificationContractValidator().ValidateFinal(input, output));

        Assert.Equal("item-sources", exception.Rule);
    }

    [Fact]
    public async Task Structured_service_retries_final_contract_errors_with_diagnostic_code()
    {
        var expected = new FinalSpecificationResponse(
            "1.0", "Authentication", "Description", [], [], [], [], [], [], [], []);
        var transport = new FinalSequenceTextGenerationService(
            "not json",
            JsonSerializer.Serialize(expected, SpecificationJson.Options));
        var service = new StructuredYandexAiService(transport, new FinalTestPrompts(), new SpecificationContractValidator());

        var actual = await service.RunFinalSpecificationAsync(
            new FinalSpecificationRequest("1.0", [new StageSegmentDto(SegmentId, 0, 1, "Text")]),
            CancellationToken.None);

        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(2, transport.CallCount);
        Assert.Contains("provider-response/json", transport.Instructions[1]);
    }

    private sealed class FinalTestPrompts : ISpecificationPromptFactory
    {
        public string Stage0Instructions() => "Stage 0";
        public string Stage1Instructions() => "Stage 1";
        public string Stage2Instructions() => "Stage 2";
        public string Stage3Instructions() => "Stage 3";
        public string FinalInstructions() => "Final specification";
    }

    private sealed class FinalSequenceTextGenerationService(params string[] responses) : ITextGenerationService
    {
        private int _index;
        public int CallCount { get; private set; }
        public System.Collections.Generic.List<string> Instructions { get; } = [];

        public Task<YandexAiResult> RespondAsync(string input, string? instructions, CancellationToken cancellationToken)
        {
            CallCount++;
            Instructions.Add(instructions ?? string.Empty);
            return Task.FromResult(new YandexAiResult("response", "model", responses[Math.Min(_index++, responses.Length - 1)], null));
        }
    }
}
