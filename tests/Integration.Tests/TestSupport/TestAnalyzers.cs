using Contracts.Documents;
using ProcessorService.Analysis;

namespace Integration.Tests.TestSupport;

/// <summary>The real analyzer, slowed down so jobs hold their job slots long enough to observe.</summary>
public sealed class SlowAnalyzer(TimeSpan perDocument) : IDocumentAnalyzer
{
    private readonly DocumentAnalyzer _inner = new();

    public DocumentResult Analyze(DocumentInput document)
    {
        Thread.Sleep(perDocument); // synchronous on purpose: Analyze is synchronous CPU work
        return _inner.Analyze(document);
    }
}

/// <summary>
/// Blocks every call until <see cref="Release"/>. <see cref="Entered"/> completes when the first call is in progress,
/// so a test knows a job is mid-processing.
/// </summary>
public sealed class GatedAnalyzer : IDocumentAnalyzer, IDisposable
{
    private readonly DocumentAnalyzer _inner = new();
    private readonly ManualResetEventSlim _gate = new(false);
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;

    public void Release() => _gate.Set();

    public DocumentResult Analyze(DocumentInput document)
    {
        _entered.TrySetResult();
        _gate.Wait(TimeSpan.FromSeconds(10));
        return _inner.Analyze(document);
    }

    public void Dispose() => _gate.Dispose();
}
