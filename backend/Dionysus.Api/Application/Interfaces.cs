public interface IEmailSender { Task SendAsync(string to, string subject, string body); }
public interface ITranscriptionService { Task<string> TranscribeAsync(byte[] audio, string fileName, CancellationToken cancellationToken); }
public interface IMediaConverter { Task<byte[]> ExtractAudioAsync(byte[] video, string extension, CancellationToken cancellationToken); }
