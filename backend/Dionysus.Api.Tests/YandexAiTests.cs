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

    [Fact]
    public void Parses_only_final_output_text_from_nested_response_content()
    {
        const string json = """
        {
          "id": "response-2",
          "model": "gpt://folder/deepseek-v4-flash/latest",
          "output": [
            {
              "type": "reasoning",
              "content": [{ "type": "summary_text", "text": "Internal reasoning that is not contract JSON." }]
            },
            {
              "type": "message",
              "content": [{ "type": "output_text", "text": "{\"schemaVersion\":\"1.0\"}" }]
            }
          ],
          "usage": { "total_tokens": 6 }
        }
        """;

        var result = YandexResponsesParser.Parse(json);

        Assert.Equal("{\"schemaVersion\":\"1.0\"}", result.Text);
    }

    [Fact]
    public void Rejects_provider_response_without_final_output_text()
    {
        const string json = """
        {
          "id": "response-3",
          "model": "gpt://folder/deepseek-v4-flash/latest",
          "output": [{ "type": "reasoning", "content": [{ "type": "summary_text", "text": "Internal reasoning." }] }]
        }
        """;

        var exception = Assert.Throws<YandexAiResponseFormatException>(() => YandexResponsesParser.Parse(json));

        Assert.Equal("missing-output-text", exception.DiagnosticCode);
        Assert.DoesNotContain("Internal reasoning.", exception.Message);
    }

    [Fact]
    public void Ignores_null_reasoning_content_before_final_output_text()
    {
        const string json = """
        {
          "id": "response-4",
          "model": "gpt://folder/gpt-oss-120b/latest",
          "output": [
            { "type": "reasoning", "content": null },
            { "type": "message", "content": [{ "type": "output_text", "text": "{\"schemaVersion\":\"1.0\"}" }] }
          ]
        }
        """;

        var result = YandexResponsesParser.Parse(json);

        Assert.Equal("{\"schemaVersion\":\"1.0\"}", result.Text);
    }
}
