using System;
using Xunit;

public sealed class SpecificationMarkdownExporterTests
{
    [Fact]
    public void Exports_title_description_items_and_timestamped_sources()
    {
        var segment = new TranscriptSegment
        {
            Id = Guid.NewGuid(),
            StartSeconds = 1.25,
            EndSeconds = 4.5,
            Text = "Пользователь входит в систему."
        };
        var statement = new AnalysisStatement
        {
            Id = Guid.NewGuid(),
            ExternalId = "segment-1",
            Text = "Пользователь входит в систему.",
            SegmentLinks = [new AnalysisStatementSegment { TranscriptSegment = segment }]
        };
        var function = new SpecificationFunction
        {
            Title = "Авторизация",
            Description = "Пользователь должен иметь возможность войти.",
            StatementLinks = [new SpecificationFunctionStatement { AnalysisStatement = statement }],
            Items = [new SpecificationItem
            {
                Kind = SpecificationItemKind.FunctionalRequirement,
                Title = "Вход в систему",
                Description = "Система принимает корректные учётные данные.",
                Priority = "Required",
                StatementLinks = [new SpecificationItemStatement { AnalysisStatement = statement }]
            }]
        };
        var analysis = new SpecificationAnalysis { Functions = [function] };

        var markdown = SpecificationMarkdownExporter.Export(analysis);

        Assert.Contains("# Авторизация", markdown);
        Assert.Contains("Пользователь должен иметь возможность войти.", markdown);
        Assert.Contains("## Functional requirement", markdown);
        Assert.Contains("### Вход в систему", markdown);
        Assert.Contains("Priority: Required", markdown);
        Assert.Contains("[00:01.250 - 00:04.500] Пользователь входит в систему.", markdown);
    }
}
