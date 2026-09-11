using System.Threading.Channels;

public sealed class TranscriptionQueue : ITranscriptionQueue
{
    private readonly Channel<TranscriptionJob> _channel = Channel.CreateUnbounded<TranscriptionJob>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask EnqueueAsync(TranscriptionJob job, CancellationToken ct) =>
        _channel.Writer.WriteAsync(job, ct);

    public ValueTask<TranscriptionJob> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}
