using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

public sealed class TranscriptionQueue : ITranscriptionQueue
{
    private readonly Channel<TranscriptionJob> _channel = Channel.CreateUnbounded<TranscriptionJob>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask EnqueueAsync(TranscriptionJob job, CancellationToken ct) =>
        _channel.Writer.WriteAsync(job, ct);

    public ValueTask<TranscriptionJob> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}

public sealed class TranscriptionWorker(
    IServiceScopeFactory scopeFactory,
    ITranscriptionQueue queue,
    ILogger<TranscriptionWorker>? logger = null) : BackgroundService
{
    private readonly ILogger<TranscriptionWorker> _logger = logger ?? NullLogger<TranscriptionWorker>.Instance;

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await RecoverAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = await db.VoiceRecordings
            .Where(recording => recording.Status == "processing")
            .Select(recording => recording.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in ids)
            await queue.EnqueueAsync(new TranscriptionJob(id), cancellationToken);
    }

    public async Task ProcessAsync(TranscriptionJob job, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var converter = scope.ServiceProvider.GetRequiredService<IMediaConverter>();
        var transcription = scope.ServiceProvider.GetRequiredService<ITranscriptionService>();
        var specificationQueue = scope.ServiceProvider.GetRequiredService<ISpecificationAnalysisQueue>();
        var recording = await db.VoiceRecordings
            .SingleOrDefaultAsync(item => item.Id == job.RecordingId && item.Status == "processing", cancellationToken);
        if (recording is null) return;

        var audio = recording.AudioData;
        if (recording.SourceType == "video")
        {
            try
            {
                audio = await converter.ExtractAudioAsync(audio, Path.GetExtension(recording.FileName), cancellationToken);
                recording.AudioData = audio;
                recording.FileName = Path.ChangeExtension(recording.FileName, ".wav");
                recording.ContentType = "audio/wav";
                recording.SizeBytes = audio.LongLength;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                await MarkFailedAsync(db, recording, "Conversion failed.", cancellationToken);
                _logger.LogError(exception, "Conversion failed for recording {RecordingId}.", recording.Id);
                return;
            }
        }

        TranscriptionResult result;
        try
        {
            result = await transcription.TranscribeAsync(audio, recording.FileName, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            await MarkFailedAsync(db, recording, "Transcription failed.", cancellationToken);
            _logger.LogError(exception, "Transcription failed for recording {RecordingId}.", recording.Id);
            return;
        }

        recording.Transcript = result.Text;
        recording.Language = result.Language;
        recording.Error = null;
        recording.Segments = result.Segments.Select(segment => new TranscriptSegment
        {
            VoiceRecordingId = recording.Id,
            StartSeconds = segment.StartSeconds,
            EndSeconds = segment.EndSeconds,
            Text = segment.Text
        }).ToList();
        db.TranscriptSegments.AddRange(recording.Segments);
        recording.Status = "completed";
        var analysis = new SpecificationAnalysis { ProjectEntityId = recording.ProjectEntityId };
        db.SpecificationAnalyses.Add(analysis);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            await specificationQueue.EnqueueAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unable to queue specification analysis {AnalysisId}.", analysis.Id);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessAsync(await queue.DequeueAsync(stoppingToken), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Transcription worker failed to process a job.");
            }
        }
    }

    private static async Task MarkFailedAsync(AppDbContext db, VoiceRecording recording, string error, CancellationToken cancellationToken)
    {
        recording.Status = "failed";
        recording.Error = error;
        await db.SaveChangesAsync(cancellationToken);
    }
}
