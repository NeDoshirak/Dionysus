using Xunit;

public sealed class SpecificationPromptFactoryTests
{
    private readonly SpecificationPromptFactory _factory = new();

    [Fact]
    public void Stage0_instructions_preserve_segments_and_require_strict_json()
    {
        var instructions = _factory.Stage0Instructions();

        Assert.Contains("transcript editor", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("schemaVersion\": \"1.0\"", instructions);
        Assert.Contains("one JSON object only", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no Markdown", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("count, order, and IDs", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("actual input segmentId values", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Never generate, replace, shorten, reformat, or omit", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("empty segments array", instructions, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Stage1_instructions_define_the_extraction_role_and_source_boundaries()
    {
        var instructions = _factory.Stage1Instructions();

        Assert.Contains("business analyst", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("schemaVersion\": \"1.0\"", instructions);
        Assert.Contains("one JSON object only", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no Markdown", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("businessContext", instructions);
        Assert.Contains("topics", instructions);
        Assert.Contains("source segment IDs", instructions, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Stage2_instructions_forbid_new_facts()
    {
        var instructions = _factory.Stage2Instructions();

        Assert.Contains("strict reviewer", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not invent facts", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("businessContext", instructions);
        Assert.Contains("unchanged", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("or reorder", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one JSON object only", instructions, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Stage3_instructions_require_all_safety_clauses()
    {
        var instructions = _factory.Stage3Instructions();

        Assert.Contains("system analyst", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("schemaVersion\": \"1.0\"", instructions);
        Assert.Contains("source statements", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Synthesize only from", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("keyQuestions", instructions);
        Assert.Contains("one JSON object only", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no Markdown", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Every generated item", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("may be an empty array", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"title\"", instructions);
        Assert.Contains("not \"name\"", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Russian", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Do not invent", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("do not create it", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("keyQuestion", instructions, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("role-N", instructions);
        Assert.Contains("req-N", instructions);
        Assert.Contains("scenario-N", instructions);
        Assert.Contains("constraint-N", instructions);
        Assert.Contains("condition-N", instructions);
        Assert.Contains("agreement-N", instructions);
        Assert.Contains("question-N", instructions);
        Assert.Contains("required|desirable|future|unknown", instructions);
        Assert.Contains("contradiction|unresolved|missingInformation", instructions);
    }
}
