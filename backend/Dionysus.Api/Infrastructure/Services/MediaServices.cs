using System.Diagnostics;
using System.Net.Http.Headers;

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

public sealed class WhisperTranscriptionService(IHttpClientFactory clients) : ITranscriptionService
{
    public async Task<string> TranscribeAsync(byte[] audio, string fileName, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(audio);
        content.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
        form.Add(content, "audio_file", fileName);
        form.Add(new StringContent("transcribe"), "task");
        form.Add(new StringContent("json"), "output");
        var response = await clients.CreateClient("whisper").PostAsync("asr", form, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }
}
