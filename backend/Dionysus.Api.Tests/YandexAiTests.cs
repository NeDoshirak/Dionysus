using Xunit;

public sealed class YandexAiTests
{
    [Fact]
    public void Parses_responses_api_output_text()
    {
        const string json = """
        {
          "id": "response-1",
          "model": "gpt://folder/deepseek-v4-flash/latest",
          "output_text": "Готово",
          "usage": { "input_tokens": 4, "output_tokens": 2, "total_tokens": 6 }
        }
        """;

        var result = YandexResponsesParser.Parse(json);

        Assert.Equal("response-1", result.ResponseId);
        Assert.Equal("gpt://folder/deepseek-v4-flash/latest", result.Model);
        Assert.Equal("Готово", result.Text);
        Assert.Equal(6, result.TotalTokens);
    }
}
