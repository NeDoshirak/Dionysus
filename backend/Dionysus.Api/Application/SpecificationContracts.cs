using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

public static class SpecificationJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static T Deserialize<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new JsonException("AI response is empty");
        }

        using var document = JsonDocument.Parse(json);
        RejectNullValues(document.RootElement);

        return JsonSerializer.Deserialize<T>(json, Options)
            ?? throw new JsonException("AI response is empty");
    }

    private static void RejectNullValues(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            throw new JsonException("AI response contains a null value.");
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                RejectNullValues(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                RejectNullValues(item);
            }
        }
    }
}

public sealed record Stage0CleanupRequest(
    [property: JsonRequired] string SchemaVersion,
    [property: JsonRequired] IReadOnlyList<StageSegmentDto> Segments);

public sealed record Stage0CleanupResponse(
    [property: JsonRequired] string SchemaVersion,
    [property: JsonRequired] IReadOnlyList<Stage0CleanedSegmentDto> Segments);

public sealed record StageSegmentDto(
    [property: JsonRequired] Guid Id,
    [property: JsonRequired] double StartSeconds,
    [property: JsonRequired] double EndSeconds,
    [property: JsonRequired] string Text);

public sealed record Stage0CleanedSegmentDto(
    [property: JsonRequired] Guid SegmentId,
    [property: JsonRequired] string CleanedText);

public sealed record Stage1ExtractionRequest(
    [property: JsonRequired] string SchemaVersion,
    [property: JsonRequired] IReadOnlyList<StageSegmentDto> Segments);

public sealed record Stage1ExtractionResponse(
    [property: JsonRequired] string SchemaVersion,
    [property: JsonRequired] IReadOnlyList<StageBusinessContextDto> BusinessContext,
    [property: JsonRequired] IReadOnlyList<Stage1TopicDto> Topics);

public sealed record StageBusinessContextDto(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Text,
    [property: JsonRequired] IReadOnlyList<Guid> SourceSegmentIds);

public sealed record Stage1TopicDto(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Name,
    [property: JsonRequired] IReadOnlyList<Stage1StatementDto> Statements);

public sealed record Stage1StatementDto(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Text,
    [property: JsonRequired] IReadOnlyList<Guid> SourceSegmentIds);

public sealed record Stage2ReviewResponse(
    [property: JsonRequired] string SchemaVersion,
    [property: JsonRequired] IReadOnlyList<StageBusinessContextDto> BusinessContext,
    [property: JsonRequired] IReadOnlyList<Stage2TopicDto> Topics,
    [property: JsonRequired] IReadOnlyList<Stage2StatementDto> Statements,
    [property: JsonRequired] IReadOnlyList<StageRelationDto> Relations);

public sealed record Stage2TopicDto(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Name,
    [property: JsonRequired] IReadOnlyList<string> StatementIds);

public sealed record Stage2StatementDto(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Text,
    [property: JsonRequired] AnalysisStatementStatus Status,
    [property: JsonRequired] IReadOnlyList<Guid> SourceSegmentIds);

public sealed record StageRelationDto(
    [property: JsonRequired] string Id,
    [property: JsonRequired] AnalysisRelationType Type,
    [property: JsonRequired] IReadOnlyList<string> SourceStatementIds,
    [property: JsonRequired] IReadOnlyList<string> TargetStatementIds,
    [property: JsonRequired] string Reason);

public sealed record Stage3FunctionRequest(
    [property: JsonRequired] string SchemaVersion,
    [property: JsonRequired] IReadOnlyList<StageBusinessContextDto> BusinessContext,
    [property: JsonRequired] Stage3FunctionInputDto Function);

public sealed record Stage3FunctionInputDto(
    [property: JsonRequired] string TopicId,
    [property: JsonRequired] string Name,
    [property: JsonRequired] IReadOnlyList<Stage2StatementDto> Statements,
    [property: JsonRequired] IReadOnlyList<StageRelationDto> Relations);

public sealed record Stage3FunctionResponse(
    [property: JsonRequired] string SchemaVersion,
    [property: JsonRequired] Stage3FunctionDto Function,
    [property: JsonRequired] IReadOnlyList<Stage3RoleDto> Roles,
    [property: JsonRequired] IReadOnlyList<Stage3FunctionalRequirementDto> FunctionalRequirements,
    [property: JsonRequired] IReadOnlyList<Stage3UserScenarioDto> UserScenarios,
    [property: JsonRequired] IReadOnlyList<Stage3DescriptionItemDto> Constraints,
    [property: JsonRequired] IReadOnlyList<Stage3DescriptionItemDto> Conditions,
    [property: JsonRequired] IReadOnlyList<Stage3DescriptionItemDto> Agreements,
    [property: JsonRequired] IReadOnlyList<Stage3KeyQuestionDto> KeyQuestions);

public sealed record Stage3FunctionDto(
    [property: JsonRequired] string Title,
    [property: JsonRequired] string Description,
    [property: JsonRequired] IReadOnlyList<string> SourceStatementIds);

public sealed record Stage3RoleDto(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Name,
    [property: JsonRequired] string Description,
    [property: JsonRequired] IReadOnlyList<string> SourceStatementIds);

public sealed record Stage3FunctionalRequirementDto(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string Description,
    [property: JsonRequired] string Priority,
    [property: JsonRequired] IReadOnlyList<string> SourceStatementIds);

public sealed record Stage3UserScenarioDto(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string Actor,
    [property: JsonRequired] string Description,
    [property: JsonRequired] IReadOnlyList<string> SourceStatementIds);

public sealed record Stage3DescriptionItemDto(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Description,
    [property: JsonRequired] IReadOnlyList<string> SourceStatementIds);

public sealed record Stage3KeyQuestionDto(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Title,
    [property: JsonRequired] string Description,
    [property: JsonRequired] string Reason,
    [property: JsonRequired] IReadOnlyList<string> SourceStatementIds);

public sealed record SpecificationDetailsDto(
    Guid Id,
    SpecificationAnalysisStatus Status,
    string? Error,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<SpecificationItemDto> BusinessContext,
    IReadOnlyList<SpecificationFunctionDto> Functions);

public sealed record SpecificationFunctionDto(
    Guid Id,
    string Title,
    string Description,
    int SortOrder,
    bool IsManual,
    IReadOnlyList<SpecificationSourceStatementDto> SourceStatements,
    IReadOnlyList<SpecificationItemDto> Items);

public sealed record SpecificationItemDto(
    Guid Id,
    SpecificationItemKind Kind,
    string? Title,
    string Description,
    string? Priority,
    int SortOrder,
    bool IsManual,
    IReadOnlyList<SpecificationSourceStatementDto> SourceStatements,
    IReadOnlyList<SpecificationSourceSegmentDto> SourceSegments);

public sealed record SpecificationSourceStatementDto(Guid Id, string Text, AnalysisStatementStatus Status);

public sealed record SpecificationSourceSegmentDto(Guid Id, double StartSeconds, double EndSeconds, string Text);

public sealed record CreateSpecificationFunctionRequest(string Title, string Description, int SortOrder, IReadOnlyList<Guid> SourceStatementIds);

public sealed record UpdateSpecificationFunctionRequest(string Title, string Description, int SortOrder, IReadOnlyList<Guid>? SourceStatementIds);

public sealed record CreateSpecificationItemRequest(SpecificationItemKind Kind, string? Title, string Description, string? Priority, IReadOnlyList<Guid> SourceStatementIds);

public sealed record UpdateSpecificationItemRequest(string? Title, string Description, string? Priority, IReadOnlyList<Guid>? SourceStatementIds);
