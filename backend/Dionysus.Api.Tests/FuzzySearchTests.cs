using Xunit;

public sealed class FuzzySearchTests
{
    [Fact]
    public void Ranks_similar_project_names_above_unrelated_names()
    {
        var results = FuzzySearch.Rank(
            ["Говновоз", "Командное совещание", "Отчёт по продажам"],
            "Говновос");

        Assert.Equal("Говновоз", results[0].Value);
        Assert.True(results[0].Score >= FuzzySearch.MinimumScore);
    }

    [Fact]
    public void Finds_transcription_phrase_with_typo()
    {
        var score = FuzzySearch.Similarity("Сегодня обсуждаем запуск проекта", "обсуждаем запусок");

        Assert.True(score >= FuzzySearch.MinimumScore);
    }

    [Fact]
    public void Ignores_results_that_are_not_similar()
    {
        var results = FuzzySearch.Rank(["Совещание", "Отчёт"], "банан");

        Assert.Empty(results);
    }
}
