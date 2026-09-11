using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class TranscriptionQueueTests
{
    [Fact]
    public async Task Worker_completes_processing_recording_and_queues_specification_analysis()
    {
        var databaseName = Guid.NewGuid().ToString();
        var recording = await AddProcessingRecordingAsync(databaseName);
        var transcription = new SuccessfulTranscription();
        var specificationQueue = new RecordingSpecificationQueue();
        var worker = CreateWorker(databaseName, transcription, specificationQueue, new RecordingTranscriptionQueue());

        await worker.ProcessAsync(new TranscriptionJob(recording.Id), CancellationToken.None);

        await using var db = CreateDb(databaseName);
        var saved = await db.VoiceRecordings.Include(item => item.Segments).SingleAsync();
        Assert.Equal("completed", saved.Status);
        Assert.Equal("transcript", saved.Transcript);
        Assert.Equal("en", saved.Language);
        Assert.Single(saved.Segments);
        Assert.Single(await db.SpecificationAnalyses.ToListAsync());
        Assert.Single(specificationQueue.Jobs);
    }

    [Fact]
    public async Task Worker_marks_failed_transcription_with_safe_error()
    {
        var databaseName = Guid.NewGuid().ToString();
        var recording = await AddProcessingRecordingAsync(databaseName);
        var worker = CreateWorker(databaseName, new FailingTranscription(), new RecordingSpecificationQueue(), new RecordingTranscriptionQueue());

        await worker.ProcessAsync(new TranscriptionJob(recording.Id), CancellationToken.None);

        await using var db = CreateDb(databaseName);
        var saved = await db.VoiceRecordings.SingleAsync();
        Assert.Equal("failed", saved.Status);
        Assert.Equal("Transcription failed.", saved.Error);
        Assert.DoesNotContain("provider secret", saved.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(await db.SpecificationAnalyses.ToListAsync());
    }

    [Fact]
    public async Task Recovery_enqueues_persisted_processing_recordings()
    {
        var databaseName = Guid.NewGuid().ToString();
        var recording = await AddProcessingRecordingAsync(databaseName);
        var queue = new RecordingTranscriptionQueue();
        var worker = CreateWorker(databaseName, new SuccessfulTranscription(), new RecordingSpecificationQueue(), queue);

        await worker.RecoverAsync(CancellationToken.None);

        Assert.Equal(new TranscriptionJob(recording.Id), Assert.Single(queue.Jobs));
    }

    private static TranscriptionWorker CreateWorker(string databaseName, ITranscriptionService transcription, ISpecificationAnalysisQueue specificationQueue, ITranscriptionQueue queue)
    {
        var services = new ServiceCollection()
            .AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName))
            .AddSingleton<IMediaConverter, PassthroughMediaConverter>()
            .AddSingleton<ITranscriptionService>(transcription)
            .AddSingleton<ISpecificationAnalysisQueue>(specificationQueue)
            .BuildServiceProvider();

        return new TranscriptionWorker(services.GetRequiredService<IServiceScopeFactory>(), queue);
    }

    private static AppDbContext CreateDb(string databaseName) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName).Options);

    private static async Task<VoiceRecording> AddProcessingRecordingAsync(string databaseName)
    {
        await using var db = CreateDb(databaseName);
        var project = new ProjectEntity { OwnerId = "user-1", Name = "Project" };
        var recording = new VoiceRecording
        {
            ProjectEntityId = project.Id,
            FileName = "voice.wav",
            ContentType = "audio/wav",
            SourceType = "audio",
            AudioData = [1, 2, 3],
            SizeBytes = 3,
            Status = "processing"
        };
        project.Recordings.Add(recording);
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return recording;
    }

    private sealed class SuccessfulTranscription : ITranscriptionService
    {
        public Task<TranscriptionResult> TranscribeAsync(byte[] audio, string fileName, CancellationToken cancellationToken) =>
            Task.FromResult(new TranscriptionResult("transcript", "en", [new TranscriptionSegment(0, 1, "segment")]));
    }

    private sealed class FailingTranscription : ITranscriptionService
    {
        public Task<TranscriptionResult> TranscribeAsync(byte[] audio, string fileName, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("provider secret");
    }

    private sealed class PassthroughMediaConverter : IMediaConverter
    {
        public Task<byte[]> ExtractAudioAsync(byte[] video, string extension, CancellationToken cancellationToken) =>
            Task.FromResult(video);
    }

    private sealed class RecordingTranscriptionQueue : ITranscriptionQueue
    {
        public List<TranscriptionJob> Jobs { get; } = [];

        public ValueTask EnqueueAsync(TranscriptionJob job, CancellationToken ct)
        {
            Jobs.Add(job);
            return ValueTask.CompletedTask;
        }

        public ValueTask<TranscriptionJob> DequeueAsync(CancellationToken ct) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingSpecificationQueue : ISpecificationAnalysisQueue
    {
        public List<SpecificationAnalysisJob> Jobs { get; } = [];

        public ValueTask EnqueueAsync(SpecificationAnalysisJob job, CancellationToken ct)
        {
            Jobs.Add(job);
            return ValueTask.CompletedTask;
        }

        public ValueTask<SpecificationAnalysisJob> DequeueAsync(CancellationToken ct) =>
            throw new NotSupportedException();
    }
}
