public interface IEmailSender { Task SendAsync(string to, string subject, string body); }
public interface ITranscriptionService { Task<TranscriptionResult> TranscribeAsync(byte[] audio, string fileName, CancellationToken cancellationToken); }
public interface IMediaConverter { Task<byte[]> ExtractAudioAsync(byte[] video, string extension, CancellationToken cancellationToken); }
public sealed record TranscriptionJob(Guid RecordingId);
public interface ITranscriptionQueue
{
    ValueTask EnqueueAsync(TranscriptionJob job, CancellationToken ct);
    ValueTask<TranscriptionJob> DequeueAsync(CancellationToken ct);
}
public interface ITextGenerationService { Task<YandexAiResult> RespondAsync(string input, string? instructions, CancellationToken cancellationToken); }
