using System.Globalization;
using System.Text;

public static class SpecificationMarkdownExporter
{
    public static string Export(SpecificationAnalysis analysis)
    {
        var function = analysis.Functions.OrderBy(x => x.SortOrder).FirstOrDefault();
        if (function is null)
            return "# Specification\n\nNo specification content is available.\n";

        var markdown = new StringBuilder();
        markdown.Append("# ").AppendLine(function.Title);
        markdown.AppendLine();
        markdown.AppendLine(function.Description);
        markdown.AppendLine();

        foreach (var item in function.Items.OrderBy(x => x.SortOrder))
        {
            markdown.Append("## ").AppendLine(DisplayKind(item.Kind));
            if (!string.IsNullOrWhiteSpace(item.Title))
            {
                markdown.Append("### ").AppendLine(item.Title);
            }

            markdown.AppendLine(item.Description);
            if (!string.IsNullOrWhiteSpace(item.Priority))
            {
                markdown.Append("Priority: ").AppendLine(item.Priority);
            }

            AppendSources(markdown, item.StatementLinks.SelectMany(x => x.AnalysisStatement.SegmentLinks).Select(x => x.TranscriptSegment));
            markdown.AppendLine();
        }

        return markdown.ToString();
    }

    private static void AppendSources(StringBuilder markdown, IEnumerable<TranscriptSegment> segments)
    {
        var sources = segments.DistinctBy(x => x.Id).OrderBy(x => x.StartSeconds).ToList();
        if (sources.Count == 0) return;

        markdown.AppendLine("Sources:");
        foreach (var segment in sources)
        {
            markdown.Append("- [")
                .Append(FormatTime(segment.StartSeconds))
                .Append(" - ")
                .Append(FormatTime(segment.EndSeconds))
                .Append("] ")
                .AppendLine(segment.Text);
        }
    }

    private static string FormatTime(double seconds)
    {
        var totalMilliseconds = Math.Max(0, (long)Math.Round(seconds * 1000, MidpointRounding.AwayFromZero));
        var time = TimeSpan.FromMilliseconds(totalMilliseconds);
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds:000}";
    }

    private static string DisplayKind(SpecificationItemKind kind) => kind switch
    {
        SpecificationItemKind.BusinessContext => "Business context",
        SpecificationItemKind.FunctionalRequirement => "Functional requirement",
        SpecificationItemKind.UserScenario => "User scenario",
        SpecificationItemKind.KeyQuestion => "Key question",
        _ => kind.ToString()
    };
}
