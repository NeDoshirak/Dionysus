using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

public sealed class SpecificationContractException : Exception
{
    public SpecificationContractException(string stage, string rule)
        : base($"Specification contract violation: {stage}/{rule}.")
    {
        Stage = stage;
        Rule = rule;
    }

    public string Stage { get; }
    public string Rule { get; }
}

public sealed class SpecificationContractValidator : ISpecificationContractValidator
{
    private const string SchemaVersion = "1.0";
    private static readonly Regex ContextId = new("^ctx-[1-9][0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex TopicId = new("^topic-[1-9][0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex StatementId = new("^st-[1-9][0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex RelationId = new("^rel-[1-9][0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex RoleId = new("^role-[1-9][0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex RequirementId = new("^req-[1-9][0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex ScenarioId = new("^scenario-[1-9][0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex ConstraintId = new("^constraint-[1-9][0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex ConditionId = new("^condition-[1-9][0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex AgreementId = new("^agreement-[1-9][0-9]*$", RegexOptions.CultureInvariant);
    private static readonly Regex QuestionId = new("^question-[1-9][0-9]*$", RegexOptions.CultureInvariant);

    public void ValidateStage0(Stage0CleanupRequest input, Stage0CleanupResponse output)
    {
        const string stage = "stage0";
        RequireVersion(input.SchemaVersion, stage);
        RequireVersion(output.SchemaVersion, stage);
        var inputIds = input.Segments.Select(x => x.Id).ToArray();
        RequireNonEmptyUniqueGuids(inputIds, stage, "input-segment-ids");

        var outputIds = output.Segments.Select(x => x.SegmentId).ToArray();
        RequireNonEmptyUniqueGuids(outputIds, stage, "output-segment-ids");
        if (!inputIds.SequenceEqual(outputIds))
        {
            Fail(stage, "segment-identity");
        }

        if (output.Segments.Any(x => string.IsNullOrWhiteSpace(x.CleanedText)))
        {
            Fail(stage, "cleaned-text");
        }
    }

    public void ValidateStage1(IReadOnlyCollection<TranscriptSegment> segments, Stage1ExtractionResponse output)
    {
        const string stage = "stage1";
        RequireVersion(output.SchemaVersion, stage);
        var inputSegmentIds = segments.Select(x => x.Id).ToArray();
        RequireNonEmptyUniqueGuids(inputSegmentIds, stage, "input-segment-ids");
        var knownSegmentIds = inputSegmentIds.ToHashSet();
        ValidateBusinessContext(output.BusinessContext, knownSegmentIds, stage);
        RequireUniqueMatchingIds(output.Topics.Select(x => x.Id), TopicId, stage, "topic-ids");

        var statements = output.Topics.SelectMany(x => x.Statements).ToArray();
        RequireUniqueMatchingIds(statements.Select(x => x.Id), StatementId, stage, "statement-ids");
        foreach (var topic in output.Topics)
        {
            RequireText(topic.Name, stage, "topic-name");
        }

        foreach (var statement in statements)
        {
            RequireText(statement.Text, stage, "statement-text");
            RequireKnownSources(statement.SourceSegmentIds, knownSegmentIds, stage, "statement-sources");
        }
    }

    public void ValidateStage2(Stage1ExtractionResponse input, Stage2ReviewResponse output)
    {
        const string stage = "stage2";
        RequireVersion(input.SchemaVersion, stage);
        RequireVersion(output.SchemaVersion, stage);
        var inputStatementList = input.Topics.SelectMany(x => x.Statements).ToArray();
        RequireUniqueMatchingIds(inputStatementList.Select(x => x.Id), StatementId, stage, "input-statement-ids");
        var inputStatements = inputStatementList.ToDictionary(x => x.Id, StringComparer.Ordinal);
        ValidateImmutableBusinessContext(input.BusinessContext, output.BusinessContext, stage);

        RequireUniqueMatchingIds(output.Statements.Select(x => x.Id), StatementId, stage, "statement-ids");
        if (output.Statements.Count != inputStatements.Count)
        {
            Fail(stage, "statement-identity");
        }

        foreach (var statement in output.Statements)
        {
            if (!inputStatements.TryGetValue(statement.Id, out var original)
                || !string.Equals(statement.Text, original.Text, StringComparison.Ordinal)
                || !SameSet(statement.SourceSegmentIds, original.SourceSegmentIds))
            {
                Fail(stage, "statement-identity");
            }

            if (!Enum.IsDefined(statement.Status))
            {
                Fail(stage, "statement-status");
            }
        }

        var statementsById = output.Statements.ToDictionary(x => x.Id, StringComparer.Ordinal);
        RequireUniqueMatchingIds(output.Topics.Select(x => x.Id), TopicId, stage, "topic-ids");
        if (output.Topics.Count > input.Topics.Count)
        {
            Fail(stage, "topic-identity");
        }
        var topicAssignments = output.Topics.SelectMany(x => x.StatementIds).ToArray();
        if (topicAssignments.Length != statementsById.Count
            || topicAssignments.Distinct(StringComparer.Ordinal).Count() != statementsById.Count
            || topicAssignments.Any(id => !statementsById.ContainsKey(id)))
        {
            Fail(stage, "topic-assignment");
        }

        foreach (var topic in output.Topics)
        {
            RequireText(topic.Name, stage, "topic-name");
        }

        RequireUniqueMatchingIds(output.Relations.Select(x => x.Id), RelationId, stage, "relation-ids");
        foreach (var relation in output.Relations)
        {
            if (!Enum.IsDefined(relation.Type))
            {
                Fail(stage, "relation-type");
            }

            RequireRelationEndpoints(relation, statementsById, stage);
            ValidateRelationStatuses(relation, statementsById, stage);
        }
    }

    public void ValidateStage3(Stage3FunctionRequest input, Stage3FunctionResponse output)
    {
        const string stage = "stage3";
        RequireVersion(input.SchemaVersion, stage);
        RequireVersion(output.SchemaVersion, stage);
        var inputStatementIds = input.Function.Statements.Select(x => x.Id).ToArray();
        RequireUniqueMatchingIds(inputStatementIds, StatementId, stage, "input-statement-ids");
        var functionStatementIds = inputStatementIds.ToHashSet(StringComparer.Ordinal);

        ValidateSourceLinkedItem(output.Function.SourceStatementIds, functionStatementIds, stage, "function-sources");
        RequireText(output.Function.Title, stage, "function-title");
        RequireText(output.Function.Description, stage, "function-description");
        ValidateSourceLinkedItems(output.Roles, x => x.Id, x => x.SourceStatementIds, RoleId, functionStatementIds, stage, "role-ids");
        ValidateSourceLinkedItems(output.FunctionalRequirements, x => x.Id, x => x.SourceStatementIds, RequirementId, functionStatementIds, stage, "requirement-ids");
        ValidateSourceLinkedItems(output.UserScenarios, x => x.Id, x => x.SourceStatementIds, ScenarioId, functionStatementIds, stage, "scenario-ids");
        ValidateSourceLinkedItems(output.Constraints, x => x.Id, x => x.SourceStatementIds, ConstraintId, functionStatementIds, stage, "constraint-ids");
        ValidateSourceLinkedItems(output.Conditions, x => x.Id, x => x.SourceStatementIds, ConditionId, functionStatementIds, stage, "condition-ids");
        ValidateSourceLinkedItems(output.Agreements, x => x.Id, x => x.SourceStatementIds, AgreementId, functionStatementIds, stage, "agreement-ids");
        ValidateSourceLinkedItems(output.KeyQuestions, x => x.Id, x => x.SourceStatementIds, QuestionId, functionStatementIds, stage, "question-ids");

        if (output.FunctionalRequirements.Any(x => !Enum.IsDefined(x.Priority)))
        {
            Fail(stage, "requirement-priority");
        }

        if (output.KeyQuestions.Any(x => !Enum.IsDefined(x.Reason)))
        {
            Fail(stage, "question-reason");
        }
    }

    private static void ValidateBusinessContext(IEnumerable<StageBusinessContextDto> context, HashSet<Guid> knownSegmentIds, string stage)
    {
        RequireUniqueMatchingIds(context.Select(x => x.Id), ContextId, stage, "context-ids");
        foreach (var item in context)
        {
            RequireText(item.Text, stage, "context-text");
            RequireKnownSources(item.SourceSegmentIds, knownSegmentIds, stage, "context-sources");
        }
    }

    private static void ValidateImmutableBusinessContext(IReadOnlyList<StageBusinessContextDto> input, IReadOnlyList<StageBusinessContextDto> output, string stage)
    {
        var expected = input.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        var actual = output.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();
        if (expected.Length != actual.Length || expected.Zip(actual).Any(pair =>
                !string.Equals(pair.First.Id, pair.Second.Id, StringComparison.Ordinal)
                || !string.Equals(pair.First.Text, pair.Second.Text, StringComparison.Ordinal)
                || !SameSet(pair.First.SourceSegmentIds, pair.Second.SourceSegmentIds)))
        {
            Fail(stage, "context-identity");
        }
    }

    private static void RequireRelationEndpoints(StageRelationDto relation, IReadOnlyDictionary<string, Stage2StatementDto> statements, string stage)
    {
        RequireUniqueNonEmptyStrings(relation.SourceStatementIds, stage, "relation-source-ids");
        RequireUniqueNonEmptyStrings(relation.TargetStatementIds, stage, "relation-target-ids");
        if (relation.SourceStatementIds.Any(id => !statements.ContainsKey(id))
            || relation.TargetStatementIds.Any(id => !statements.ContainsKey(id))
            || relation.SourceStatementIds.Intersect(relation.TargetStatementIds, StringComparer.Ordinal).Any())
        {
            Fail(stage, "relation-endpoints");
        }
    }

    private static void ValidateRelationStatuses(StageRelationDto relation, IReadOnlyDictionary<string, Stage2StatementDto> statements, string stage)
    {
        if (relation.Type == AnalysisRelationType.Supersedes
            && (!relation.SourceStatementIds.All(id => statements[id].Status == AnalysisStatementStatus.Active)
                || !relation.TargetStatementIds.All(id => statements[id].Status == AnalysisStatementStatus.Superseded)))
        {
            Fail(stage, "supersedes-status");
        }

        if (relation.Type == AnalysisRelationType.Contradicts
            && (!relation.SourceStatementIds.All(id => statements[id].Status == AnalysisStatementStatus.Active)
                || !relation.TargetStatementIds.All(id => statements[id].Status == AnalysisStatementStatus.Active)))
        {
            Fail(stage, "contradicts-status");
        }
    }

    private static void ValidateSourceLinkedItems<T>(IReadOnlyList<T> items, Func<T, string> id, Func<T, IReadOnlyList<string>> sources, Regex idFormat, HashSet<string> knownIds, string stage, string rule)
    {
        RequireUniqueMatchingIds(items.Select(id), idFormat, stage, rule);
        foreach (var item in items)
        {
            ValidateSourceLinkedItem(sources(item), knownIds, stage, "item-sources");
        }
    }

    private static void ValidateSourceLinkedItem(IReadOnlyList<string> sourceIds, HashSet<string> knownIds, string stage, string rule)
    {
        RequireUniqueNonEmptyStrings(sourceIds, stage, rule);
        if (sourceIds.Any(id => !knownIds.Contains(id)))
        {
            Fail(stage, rule);
        }
    }

    private static void RequireKnownSources(IReadOnlyList<Guid> sourceIds, HashSet<Guid> knownIds, string stage, string rule)
    {
        RequireNonEmptyUniqueGuids(sourceIds, stage, rule);
        if (sourceIds.Any(id => !knownIds.Contains(id)))
        {
            Fail(stage, rule);
        }
    }

    private static void RequireVersion(string version, string stage)
    {
        if (!string.Equals(version, SchemaVersion, StringComparison.Ordinal))
        {
            Fail(stage, "schema-version");
        }
    }

    private static void RequireUniqueMatchingIds(IEnumerable<string> ids, Regex pattern, string stage, string rule)
    {
        var values = ids.ToArray();
        if (values.Any(string.IsNullOrWhiteSpace) || values.Any(value => !pattern.IsMatch(value)) || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            Fail(stage, rule);
        }
    }

    private static void RequireUniqueNonEmptyStrings(IEnumerable<string> ids, string stage, string rule)
    {
        var values = ids.ToArray();
        if (values.Length == 0 || values.Any(string.IsNullOrWhiteSpace) || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            Fail(stage, rule);
        }
    }

    private static void RequireNonEmptyUniqueGuids(IEnumerable<Guid> ids, string stage, string rule)
    {
        var values = ids.ToArray();
        if (values.Any(id => id == Guid.Empty) || values.Distinct().Count() != values.Length)
        {
            Fail(stage, rule);
        }
    }

    private static void RequireText(string value, string stage, string rule)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Fail(stage, rule);
        }
    }

    private static bool SameSet(IEnumerable<Guid> first, IEnumerable<Guid> second) => first.OrderBy(x => x).SequenceEqual(second.OrderBy(x => x));

    private static void Fail(string stage, string rule) => throw new SpecificationContractException(stage, rule);
}
