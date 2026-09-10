using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public sealed class RecordingSpecificationQueue : ISpecificationAnalysisQueue
{
    private readonly ConcurrentQueue<SpecificationAnalysisJob> _jobs = new();
    public IReadOnlyCollection<SpecificationAnalysisJob> Jobs => _jobs.ToArray();
    public void Clear()
    {
        while (_jobs.TryDequeue(out _)) { }
    }

    public ValueTask EnqueueAsync(SpecificationAnalysisJob job, CancellationToken ct)
    {
        _jobs.Enqueue(job);
        return ValueTask.CompletedTask;
    }

    public ValueTask<SpecificationAnalysisJob> DequeueAsync(CancellationToken ct) =>
        _jobs.TryDequeue(out var job)
            ? ValueTask.FromResult(job)
            : ValueTask.FromException<SpecificationAnalysisJob>(new InvalidOperationException("No queued specification job."));
}
