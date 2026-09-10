using System;
using System.Collections.Generic;
using Xunit;

public sealed class SpecificationContractValidatorTests
{
    private static readonly Guid SegmentOne = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid SegmentTwo = Guid.Parse("00000000-0000-0000-0000-000000000002");

    private readonly SpecificationContractValidator _validator = new();

    [Fact]
    public void Stage0_rejects_a_missing_input_segment_id()
    {
        var input = new Stage0CleanupRequest("1.0", [Segment(SegmentOne), Segment(SegmentTwo)]);
        var output = new Stage0CleanupResponse("1.0", [new Stage0CleanedSegmentDto(SegmentOne, "First cleaned segment.")]);

        Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage0(input, output));
    }

    [Fact]
    public void Stage0_rejects_a_duplicate_input_segment_id()
    {
        var input = new Stage0CleanupRequest("1.0", [Segment(SegmentOne), Segment(SegmentOne)]);
        var output = new Stage0CleanupResponse("1.0", [
            new Stage0CleanedSegmentDto(SegmentOne, "First cleaned segment."),
            new Stage0CleanedSegmentDto(SegmentOne, "Second cleaned segment.")
        ]);

        Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage0(input, output));
    }

    [Fact]
    public void Stage0_rejects_an_unknown_output_segment_id()
    {
        var input = new Stage0CleanupRequest("1.0", [Segment(SegmentOne)]);
        var output = new Stage0CleanupResponse("1.0", [new Stage0CleanedSegmentDto(SegmentTwo, "Unexpected segment.")]);

        Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage0(input, output));
    }

    [Fact]
    public void Stage1_rejects_a_statement_source_outside_the_recording()
    {
        var output = ValidStage1() with
        {
            Topics = [new Stage1TopicDto("topic-1", "Authentication", [
                new Stage1StatementDto("st-1", "Users sign in with corporate accounts.", [SegmentTwo])
            ])]
        };

        Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage1([TranscriptSegment(SegmentOne)], output));
    }

    [Fact]
    public void Stage1_rejects_duplicate_recording_segment_ids_before_building_the_known_set()
    {
        var exception = Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage1([
            TranscriptSegment(SegmentOne),
            TranscriptSegment(SegmentOne)
        ], ValidStage1()));

        Assert.Equal("input-segment-ids", exception.Rule);
    }

    [Fact]
    public void Stage1_rejects_duplicate_statement_ids_with_safe_contract_diagnostics()
    {
        var output = ValidStage1() with
        {
            Topics = [new Stage1TopicDto("topic-1", "Authentication", [
                new Stage1StatementDto("st-1", "First fact.", [SegmentOne]),
                new Stage1StatementDto("st-1", "Second fact.", [SegmentOne])
            ])]
        };

        var exception = Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage1([TranscriptSegment(SegmentOne)], output));

        Assert.Equal("stage1", exception.Stage);
        Assert.Equal("statement-ids", exception.Rule);
        Assert.DoesNotContain("First fact.", exception.Message);
        Assert.DoesNotContain("Second fact.", exception.Message);
    }

    [Fact]
    public void Stage2_rejects_changed_statement_text()
    {
        var output = ValidStage2() with
        {
            Statements = [new Stage2StatementDto("st-1", "A different fact.", AnalysisStatementStatus.Active, [SegmentOne])]
        };

        Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage2(ValidStage1(), output));
    }

    [Fact]
    public void Stage2_rejects_duplicate_input_statement_ids_before_building_the_dictionary()
    {
        var input = new Stage1ExtractionResponse("1.0", [], [
            new Stage1TopicDto("topic-1", "Authentication", [new Stage1StatementDto("st-1", "First fact.", [SegmentOne])]),
            new Stage1TopicDto("topic-2", "Authorization", [new Stage1StatementDto("st-1", "Second fact.", [SegmentOne])])
        ]);

        var exception = Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage2(input, ValidStage2()));

        Assert.Equal("input-statement-ids", exception.Rule);
    }

    [Fact]
    public void Stage2_rejects_changed_statement_source_segments()
    {
        var output = ValidStage2() with
        {
            Statements = [new Stage2StatementDto("st-1", "Users sign in with corporate accounts.", AnalysisStatementStatus.Active, [SegmentTwo])]
        };

        Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage2(ValidStage1(), output));
    }

    [Fact]
    public void Stage2_rejects_a_statement_that_is_not_assigned_to_one_topic()
    {
        var output = ValidStage2() with { Topics = [new Stage2TopicDto("topic-1", "Authentication", [])] };

        Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage2(ValidStage1(), output));
    }

    [Fact]
    public void Stage2_rejects_a_contradiction_between_nonactive_statements()
    {
        var input = new Stage1ExtractionResponse("1.0", [], [new Stage1TopicDto("topic-1", "Authentication", [
            new Stage1StatementDto("st-1", "First active fact.", [SegmentOne]),
            new Stage1StatementDto("st-2", "Second active fact.", [SegmentOne])
        ])]);
        var output = new Stage2ReviewResponse("1.0", [], [new Stage2TopicDto("topic-1", "Authentication", ["st-1", "st-2"])], [
            new Stage2StatementDto("st-1", "First active fact.", AnalysisStatementStatus.Superseded, [SegmentOne]),
            new Stage2StatementDto("st-2", "Second active fact.", AnalysisStatementStatus.Active, [SegmentOne])
        ], [new StageRelationDto("rel-1", AnalysisRelationType.Contradicts, ["st-1"], ["st-2"], "They conflict.")]);

        var exception = Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage2(input, output));

        Assert.Equal("contradicts-status", exception.Rule);
    }

    [Fact]
    public void Stage2_rejects_a_supersedes_relation_with_incompatible_statuses()
    {
        var input = new Stage1ExtractionResponse("1.0", [], [new Stage1TopicDto("topic-1", "Authentication", [
            new Stage1StatementDto("st-1", "The old decision.", [SegmentOne]),
            new Stage1StatementDto("st-2", "The replacement decision.", [SegmentOne])
        ])]);
        var output = new Stage2ReviewResponse("1.0", [], [new Stage2TopicDto("topic-1", "Authentication", ["st-1", "st-2"])], [
            new Stage2StatementDto("st-1", "The old decision.", AnalysisStatementStatus.Active, [SegmentOne]),
            new Stage2StatementDto("st-2", "The replacement decision.", AnalysisStatementStatus.Active, [SegmentOne])
        ], [new StageRelationDto("rel-1", AnalysisRelationType.Supersedes, ["st-2"], ["st-1"], "Replacement.")]);

        Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage2(input, output));
    }

    [Fact]
    public void Stage3_rejects_sources_outside_its_input_function()
    {
        var input = new Stage3FunctionRequest("1.0", [], new Stage3FunctionInputDto("topic-1", "Authentication", [
            new Stage2StatementDto("st-1", "Users sign in with corporate accounts.", AnalysisStatementStatus.Active, [SegmentOne])
        ], []));
        var output = ValidStage3() with
        {
            Roles = [new Stage3RoleDto("role-1", "Administrator", "Manages access.", ["st-2"])]
        };

        Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage3(input, output));
    }

    [Fact]
    public void Stage3_rejects_duplicate_input_statement_ids_before_building_the_known_set()
    {
        var input = new Stage3FunctionRequest("1.0", [], new Stage3FunctionInputDto("topic-1", "Authentication", [
            new Stage2StatementDto("st-1", "First fact.", AnalysisStatementStatus.Active, [SegmentOne]),
            new Stage2StatementDto("st-1", "Second fact.", AnalysisStatementStatus.Active, [SegmentOne])
        ], []));

        var exception = Assert.Throws<SpecificationContractException>(() => _validator.ValidateStage3(input, ValidStage3()));

        Assert.Equal("input-statement-ids", exception.Rule);
    }

    private static StageSegmentDto Segment(Guid id) => new(id, 0, 1, "Transcript text stays out of diagnostics.");

    private static TranscriptSegment TranscriptSegment(Guid id) => new() { Id = id, StartSeconds = 0, EndSeconds = 1, Text = "Transcript text stays out of diagnostics." };

    private static Stage1ExtractionResponse ValidStage1() => new("1.0", [], [new Stage1TopicDto("topic-1", "Authentication", [
        new Stage1StatementDto("st-1", "Users sign in with corporate accounts.", [SegmentOne])
    ])]);

    private static Stage2ReviewResponse ValidStage2() => new("1.0", [], [new Stage2TopicDto("topic-1", "Authentication", ["st-1"])], [
        new Stage2StatementDto("st-1", "Users sign in with corporate accounts.", AnalysisStatementStatus.Active, [SegmentOne])
    ], []);

    private static Stage3FunctionResponse ValidStage3() => new("1.0",
        new Stage3FunctionDto("Authentication", "Corporate sign-in.", ["st-1"]), [], [], [], [], [], [], []);
}
