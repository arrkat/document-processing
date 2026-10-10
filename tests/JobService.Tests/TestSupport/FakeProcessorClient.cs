using System.Collections.Concurrent;
using Contracts.Processing;
using JobService.Processing;

namespace JobService.Tests.TestSupport;

/// <summary>Scriptable Processor: per-job submit answers (default Accepted), a fixed cancel answer, and a status function.</summary>
public sealed class FakeProcessorClient : IProcessorClient
{
    private readonly ConcurrentDictionary<string, Queue<DispatchResult>> _submitScripts = new();

    public ConcurrentQueue<string> Submitted { get; } = new();
    public ConcurrentQueue<string> CancelCommands { get; } = new();
    public ProcessorCancelResult CancelAnswer { get; set; } = ProcessorCancelResult.CancellationRequested;
    public Func<string, ProcessingJobStatus?> Status { get; set; } = _ => null;

    /// <summary>Runs inside SubmitAsync before answering, e.g. to simulate a cancel racing the dispatch.</summary>
    public Func<string, Task>? OnSubmit { get; set; }

    public void ScriptSubmit(string jobId, params DispatchResult[] answers) =>
        _submitScripts[jobId] = new Queue<DispatchResult>(answers);

    public async Task<DispatchResult> SubmitAsync(ProcessJobRequest request, CancellationToken cancellationToken = default)
    {
        Submitted.Enqueue(request.JobId);
        if (OnSubmit is not null)
            await OnSubmit(request.JobId);
        return _submitScripts.TryGetValue(request.JobId, out var script) && script.Count > 0
            ? script.Dequeue()
            : DispatchResult.Accepted;
    }

    public Task<ProcessingJobStatus?> GetStatusAsync(string jobId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Status(jobId));

    public Task<ProcessorCancelResult> CancelAsync(string jobId, CancellationToken cancellationToken = default)
    {
        CancelCommands.Enqueue(jobId);
        return Task.FromResult(CancelAnswer);
    }
}
