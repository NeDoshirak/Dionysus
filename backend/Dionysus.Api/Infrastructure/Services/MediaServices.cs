using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

public sealed class FfmpegMediaConverter : IMediaConverter
{
    public async Task<byte[]> ExtractAudioAsync(byte[] video, string extension, CancellationToken ct)
    {
        var input = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}{extension}");
        var output = Path.ChangeExtension(input, ".wav");
        try
        {
            await File.WriteAllBytesAsync(input, video, ct);
            using var process = Process.Start(new ProcessStartInfo("ffmpeg", $"-y -i \"{input}\" -vn -ac 1 -ar 16000 \"{output}\"") { RedirectStandardError = true, UseShellExecute = false }) ?? throw new InvalidOperationException("FFmpeg unavailable");
            await process.WaitForExitAsync(ct);
            if (process.ExitCode != 0 || !File.Exists(output)) throw new InvalidOperationException("Video has no readable audio track");
            return await File.ReadAllBytesAsync(output, ct);
        }
        finally { if (File.Exists(input)) File.Delete(input); if (File.Exists(output)) File.Delete(output); }
    }
}

public static class WhisperTranscriptionParser
{
    public static TranscriptionResult Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var text = root.TryGetProperty("text", out var textElement) ? textElement.GetString() ?? string.Empty : string.Empty;
        var language = root.TryGetProperty("language", out var languageElement) ? languageElement.GetString() : null;
        var segments = new List<TranscriptionSegment>();

        if (root.TryGetProperty("segments", out var segmentElements) && segmentElements.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in segmentElements.EnumerateArray())
            {
                var (start, end) = ReadTimestamps(element);
                var segmentText = element.TryGetProperty("transcript", out var transcript)
                    ? transcript.GetString()
                    : element.TryGetProperty("text", out var textValue) ? textValue.GetString() : null;
                if (start is not null && end is not null && !string.IsNullOrWhiteSpace(segmentText))
                    segments.Add(new TranscriptionSegment(start.Value, end.Value, segmentText.Trim()));
            }
        }

        return new TranscriptionResult(text, language, segments);
    }

    private static (double? Start, double? End) ReadTimestamps(JsonElement element)
    {
        if (element.TryGetProperty("timestamps", out var timestamps) && timestamps.ValueKind == JsonValueKind.Array && timestamps.GetArrayLength() >= 2)
            return (timestamps[0].GetDouble(), timestamps[1].GetDouble());
        if (element.TryGetProperty("start", out var start) && element.TryGetProperty("end", out var end))
            return (start.GetDouble(), end.GetDouble());
        return (null, null);
    }
}

public sealed class WhisperTranscriptionService(IHttpClientFactory clients, IConfiguration configuration) : ITranscriptionService
{
    public async Task<TranscriptionResult> TranscribeAsync(byte[] audio, string fileName, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(audio);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        form.Add(content, "audio_file", fileName);
        form.Add(new StringContent("transcribe"), "task");
        form.Add(new StringContent("json"), "output");
        var query = "asr?output=json&word_timestamps=false";
        var language = configuration["WHISPER_LANGUAGE"];
        if (!string.IsNullOrWhiteSpace(language)) query += $"&language={Uri.EscapeDataString(language)}";
        if (string.Equals(configuration["WHISPER_ENGINE"], "faster_whisper", StringComparison.OrdinalIgnoreCase)) query += "&vad_filter=true";
        var response = await clients.CreateClient("whisper").PostAsync(query, form, ct);
        response.EnsureSuccessStatusCode();
        return WhisperTranscriptionParser.Parse(await response.Content.ReadAsStringAsync(ct));
    }
}
