using System.Linq;

public static class SpecificationMapper
{
    public static SpecificationDetailsDto ToDetails(SpecificationAnalysis analysis) => new(
        analysis.Id,
        analysis.Status,
        analysis.Error,
        analysis.CreatedAt,
        analysis.CompletedAt,
        analysis.Items
            .Where(x => x.SpecificationFunctionId is null)
            .OrderBy(x => x.SortOrder)
            .Select(ToItem)
            .ToList(),
        analysis.Functions
            .OrderBy(x => x.SortOrder)
            .Select(ToFunction)
            .ToList());

    public static SpecificationFunctionDto ToFunction(SpecificationFunction function) => new(
        function.Id,
        function.Title,
        function.Description,
        function.SortOrder,
        function.AnalysisTopicId is null,
        function.StatementLinks
            .OrderBy(x => x.AnalysisStatement.ExternalId)
            .Select(x => ToStatement(x.AnalysisStatement))
            .ToList(),
        function.Items
            .OrderBy(x => x.SortOrder)
            .Select(ToItem)
            .ToList());

    public static SpecificationItemDto ToItem(SpecificationItem item) => new(
        item.Id,
        item.Kind,
        item.Title,
        item.Description,
        item.Priority,
        item.SortOrder,
        item.IsManual,
        item.StatementLinks
            .OrderBy(x => x.AnalysisStatement.ExternalId)
            .Select(x => ToStatement(x.AnalysisStatement))
            .ToList(),
        item.StatementLinks
            .SelectMany(x => x.AnalysisStatement.SegmentLinks)
            .Select(x => x.TranscriptSegment)
            .DistinctBy(x => x.Id)
            .OrderBy(x => x.StartSeconds)
            .Select(x => new SpecificationSourceSegmentDto(x.Id, x.StartSeconds, x.EndSeconds, x.Text))
            .ToList());

    private static SpecificationSourceStatementDto ToStatement(AnalysisStatement statement) =>
        new(statement.Id, statement.Text, statement.Status);
}
