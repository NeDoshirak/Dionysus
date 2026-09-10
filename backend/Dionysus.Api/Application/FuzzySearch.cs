using System.Text;

public sealed record FuzzyMatch<T>(T Value, double Score);

public static class FuzzySearch
{
    public const double MinimumScore = 0.45;

    public static IReadOnlyList<FuzzyMatch<string>> Rank(IEnumerable<string> values, string query) =>
        Rank(values, query, value => value);

    public static IReadOnlyList<FuzzyMatch<T>> Rank<T>(IEnumerable<T> values, string query, Func<T, string> textSelector)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];

        return values
            .Select(value => new FuzzyMatch<T>(value, Similarity(textSelector(value), query)))
            .Where(match => match.Score >= MinimumScore)
            .OrderByDescending(match => match.Score)
            .ToList();
    }

    public static double Similarity(string text, string query)
    {
        var normalizedText = Normalize(text);
        var normalizedQuery = Normalize(query);
        if (normalizedText.Length == 0 || normalizedQuery.Length == 0) return 0;
        if (normalizedText == normalizedQuery) return 1;
        if (normalizedText.Contains(normalizedQuery, StringComparison.Ordinal)) return 0.95;

        var queryWords = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var textWords = normalizedText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordScore = queryWords
            .Select(queryWord => textWords.Select(textWord => WordSimilarity(textWord, queryWord)).DefaultIfEmpty(0).Max())
            .Average();

        return Math.Max(wordScore, WordSimilarity(normalizedText, normalizedQuery));
    }

    private static double WordSimilarity(string left, string right)
    {
        if (left == right) return 1;
        var distance = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++) distance[i, 0] = i;
        for (var j = 0; j <= right.Length; j++) distance[0, j] = j;
        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                distance[i, j] = Math.Min(Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1), distance[i - 1, j - 1] + cost);
            }
        }

        return 1d - (double)distance[left.Length, right.Length] / Math.Max(left.Length, right.Length);
    }

    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.ToLowerInvariant())
            builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
