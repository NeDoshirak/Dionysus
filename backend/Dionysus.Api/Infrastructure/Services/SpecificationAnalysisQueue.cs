using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

public sealed class SpecificationAnalysisQueue : ISpecificationAnalysisQueue
{
    private readonly Channel<SpecificationAnalysisJob> _channel = Channel.CreateUnbounded<SpecificationAnalysisJob>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask EnqueueAsync(SpecificationAnalysisJob job, CancellationToken ct) =>
        _channel.Writer.WriteAsync(job, ct);

    public ValueTask<SpecificationAnalysisJob> DequeueAsync(CancellationToken ct) =>
        _channel.Reader.ReadAsync(ct);
}

public sealed class SpecificationAnalysisWorker(
    IServiceScopeFactory scopeFactory,
    ISpecificationAnalysisQueue queue) : BackgroundService
{
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await RecoverAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var analyses = await db.SpecificationAnalyses
            .Where(analysis => analysis.Status == SpecificationAnalysisStatus.Queued ||
                (analysis.Status >= SpecificationAnalysisStatus.RunningStage0 &&
                 analysis.Status <= SpecificationAnalysisStatus.RunningStage3))
            .ToListAsync(cancellationToken);

        foreach (var analysis in analyses)
        {
            if (analysis.Status != SpecificationAnalysisStatus.Queued)
            {
                analysis.Status = SpecificationAnalysisStatus.Queued;
                analysis.RunId = Guid.NewGuid();
            }
        }

        if (analyses.Count > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
            foreach (var analysis in analyses)
                await queue.EnqueueAsync(new SpecificationAnalysisJob(analysis.Id, analysis.RunId), cancellationToken);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in ReadJobs(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<ISpecificationAnalysisOrchestrator>();
            try
            {
                await orchestrator.RunAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async IAsyncEnumerable<SpecificationAnalysisJob> ReadJobs(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
            yield return await queue.DequeueAsync(cancellationToken);
    }
}
