using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
