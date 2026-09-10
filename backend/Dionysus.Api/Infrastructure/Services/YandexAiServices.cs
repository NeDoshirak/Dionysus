using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public sealed class YandexAiResponseFormatException(string diagnosticCode) : Exception("Yandex AI returned an unusable response format.")
{
    public string DiagnosticCode { get; } = diagnosticCode;
}

public sealed class YandexResponsesParser
{
    public static YandexAiResult Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var responseId = root.GetProperty("id").GetString() ?? string.Empty;
        var model = root.GetProperty("model").GetString() ?? string.Empty;
        var text = root.TryGetProperty("output_text", out var outputText)
            ? outputText.GetString() ?? string.Empty
            : ReadOutputText(root);
        var totalTokens = root.TryGetProperty("usage", out var usage) && usage.TryGetProperty("total_tokens", out var total)
            ? (int?)total.GetInt32()
            : null;

        return new YandexAiResult(responseId, model, text, totalTokens);
    }

    private static string ReadOutputText(JsonElement root)
    {
        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array) return string.Empty;

        var text = string.Join("\n", output.EnumerateArray()
            .Where(item => item.TryGetProperty("content", out var content)
                           && content.ValueKind == JsonValueKind.Array)
            .SelectMany(item => item.GetProperty("content").EnumerateArray())
            .Where(content => content.TryGetProperty("type", out var type)
                              && type.ValueKind == JsonValueKind.String
                              && type.GetString() == "output_text"
                              && content.TryGetProperty("text", out _))
            .Select(content => content.GetProperty("text").GetString())
            .Where(text => !string.IsNullOrWhiteSpace(text)));

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new YandexAiResponseFormatException("missing-output-text");
        }

        return text;
    }
}

public sealed class YandexAiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public sealed class YandexAiService(
    IHttpClientFactory clients,
    IConfiguration configuration,
    ILogger<YandexAiService> logger) : ITextGenerationService
{
    public async Task<YandexAiResult> RespondAsync(string input, string? instructions, CancellationToken cancellationToken)
    {
        var apiKey = configuration["YANDEX_AI_API_KEY"];
        var folderId = configuration["YANDEX_AI_FOLDER_ID"];
        var model = configuration["YANDEX_AI_MODEL"] ?? $"gpt://{folderId}/deepseek-v4-flash/latest";
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(folderId))
            throw new InvalidOperationException("Yandex AI is not configured");

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Api-Key", apiKey);
        request.Headers.Add("OpenAI-Project", folderId);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            model,
            instructions = instructions ?? string.Empty,
            input
        }), Encoding.UTF8, "application/json");

        var response = await clients.CreateClient("yandex-ai").SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw new YandexAiException((int)response.StatusCode, "Yandex AI request failed");
        try
        {
            return YandexResponsesParser.Parse(body);
        }
        catch (YandexAiResponseFormatException exception)
        {
            logger.LogError(
                exception,
                "Yandex AI response could not be parsed. DiagnosticCode: {DiagnosticCode}; ResponseLength: {ResponseLength}",
                exception.DiagnosticCode,
                body.Length);
            throw;
        }
    }
}
