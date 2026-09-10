using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

public sealed class SpecificationAnalysisQueueTests
{
    [Fact]
    public async Task Queue_delivers_a_job_with_its_analysis_and_run_ids()
    {
        var queue = new SpecificationAnalysisQueue();
        var job = new SpecificationAnalysisJob(Guid.NewGuid(), Guid.NewGuid());

        await queue.EnqueueAsync(job, CancellationToken.None);

        Assert.Equal(job, await queue.DequeueAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Recovery_requeues_queued_and_interrupted_running_analyses()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        await using (var db = new AppDbContext(options))
        {
            var queued = new SpecificationAnalysis { Status = SpecificationAnalysisStatus.Queued };
            var running = new SpecificationAnalysis { Status = SpecificationAnalysisStatus.RunningStage2 };
            db.SpecificationAnalyses.AddRange(queued, running);
            await db.SaveChangesAsync();

            var queue = new RecordingQueue();
            var services = new ServiceCollection()
                .AddDbContext<AppDbContext>(builder => builder.UseInMemoryDatabase(databaseName))
                .AddSingleton<ISpecificationAnalysisQueue>(queue)
                .BuildServiceProvider();
            var worker = new SpecificationAnalysisWorker(services.GetRequiredService<IServiceScopeFactory>(), queue);

            var oldRunId = running.RunId;
            await worker.RecoverAsync(CancellationToken.None);
            await db.Entry(running).ReloadAsync();

            Assert.Equal(SpecificationAnalysisStatus.Queued, running.Status);
            Assert.NotEqual(oldRunId, running.RunId);
            Assert.Equal(2, queue.Jobs.Count);
            Assert.Contains(queue.Jobs, job => job.AnalysisId == queued.Id && job.RunId == queued.RunId);
            Assert.Contains(queue.Jobs, job => job.AnalysisId == running.Id && job.RunId == running.RunId);
        }
    }

    [Fact]
    public async Task Claim_allows_only_one_worker_to_claim_a_run()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(databaseName).Options;
        await using (var db = new AppDbContext(options))
        {
            var analysis = new SpecificationAnalysis { Status = SpecificationAnalysisStatus.Queued };
            db.SpecificationAnalyses.Add(analysis);
            await db.SaveChangesAsync();

            var services = new ServiceCollection()
                .AddDbContext<AppDbContext>(builder => builder.UseInMemoryDatabase(databaseName))
                .BuildServiceProvider();
            var first = new SpecificationAnalysisWorker(services.GetRequiredService<IServiceScopeFactory>(), new RecordingQueue());
            var second = new SpecificationAnalysisWorker(services.GetRequiredService<IServiceScopeFactory>(), new RecordingQueue());
            var job = new SpecificationAnalysisJob(analysis.Id, analysis.RunId);

            var results = await Task.WhenAll(first.TryClaimAsync(job, CancellationToken.None), second.TryClaimAsync(job, CancellationToken.None));

            Assert.Single(results, claimed => claimed is not null);
            await db.Entry(analysis).ReloadAsync();
            Assert.Equal(SpecificationAnalysisStatus.RunningStage0, analysis.Status);
        }
    }

    [Fact]
    public async Task RunAsync_failure_is_logged_and_next_job_is_processed()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options))
        {
            var first = new SpecificationAnalysis();
            var second = new SpecificationAnalysis();
            db.SpecificationAnalyses.AddRange(first, second);
            await db.SaveChangesAsync();
        }

        using var stopping = new CancellationTokenSource();
        var queue = new SpecificationAnalysisQueue();
        var orchestrator = new ThrowOnceOrchestrator(stopping);
        var logger = new RecordingLogger();
        var services = new ServiceCollection()
            .AddDbContext<AppDbContext>(builder => builder.UseInMemoryDatabase(databaseName))
            .AddSingleton<ISpecificationAnalysisOrchestrator>(orchestrator)
            .BuildServiceProvider();
        var worker = new SpecificationAnalysisWorker(
            services.GetRequiredService<IServiceScopeFactory>(), queue, logger);

        await queue.EnqueueAsync(await GetJobAsync(databaseName, 0), CancellationToken.None);
        await queue.EnqueueAsync(await GetJobAsync(databaseName, 1), CancellationToken.None);
        await worker.StartAsync(stopping.Token);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(2, orchestrator.ProcessedJobs.Count);
        Assert.Contains(logger.Errors, message => message.Contains("Specification analysis job"));
    }

    [Fact]
    public async Task Host_cancellation_stops_worker_without_logging_an_error()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using (var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options))
        {
            var analysis = new SpecificationAnalysis();
            db.SpecificationAnalyses.Add(analysis);
            await db.SaveChangesAsync();
        }

        using var stopping = new CancellationTokenSource();
        var queue = new SpecificationAnalysisQueue();
        var orchestrator = new CancelingOrchestrator(stopping);
        var logger = new RecordingLogger();
        var services = new ServiceCollection()
            .AddDbContext<AppDbContext>(builder => builder.UseInMemoryDatabase(databaseName))
            .AddSingleton<ISpecificationAnalysisOrchestrator>(orchestrator)
            .BuildServiceProvider();
        var worker = new SpecificationAnalysisWorker(
            services.GetRequiredService<IServiceScopeFactory>(), queue, logger);

        await queue.EnqueueAsync(await GetJobAsync(databaseName, 0), CancellationToken.None);
        await worker.StartAsync(stopping.Token);
        await worker.StopAsync(CancellationToken.None);

        Assert.Single(orchestrator.ProcessedJobs);
        Assert.Empty(logger.Errors);
    }

    private static async Task<SpecificationAnalysisJob> GetJobAsync(string databaseName, int index)
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);
        var analysis = await db.SpecificationAnalyses.OrderBy(x => x.Id).Skip(index).FirstAsync();
        return new SpecificationAnalysisJob(analysis.Id, analysis.RunId);
    }

    private sealed class ThrowOnceOrchestrator(CancellationTokenSource stopping) : ISpecificationAnalysisOrchestrator
    {
        public List<SpecificationAnalysisJob> ProcessedJobs { get; } = [];

        public Task RunAsync(SpecificationAnalysisJob job, CancellationToken ct)
        {
            ProcessedJobs.Add(job);
            if (ProcessedJobs.Count == 1) throw new OperationCanceledException("transient timeout");
            stopping.Cancel();
            return Task.CompletedTask;
        }
    }

    private sealed class CancelingOrchestrator(CancellationTokenSource stopping) : ISpecificationAnalysisOrchestrator
    {
        public List<SpecificationAnalysisJob> ProcessedJobs { get; } = [];

        public Task RunAsync(SpecificationAnalysisJob job, CancellationToken ct)
        {
            ProcessedJobs.Add(job);
            stopping.Cancel();
            throw new OperationCanceledException(ct);
        }
    }

    private sealed class RecordingLogger : ILogger<SpecificationAnalysisWorker>
    {
        public List<string> Errors { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error) Errors.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new();
            public void Dispose() { }
        }
    }

    private sealed class RecordingQueue : ISpecificationAnalysisQueue
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
