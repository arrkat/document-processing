using Contracts.Documents;
using ProcessorService.Analysis;

namespace ProcessorService.Tests.TestSupport;

/// <summary>Returns a fixed result per document; records nothing.</summary>
public sealed class StubAnalyzer : IDocumentAnalyzer
{
    public DocumentResult Analyze(DocumentInput document) =>
        new(document.Id, WordCount: 1, UniqueWordCount: 1, TopWords: [], Sha256: "stub");
}

/// <summary>Simulates slow CPU work and records the peak number of concurrent calls.</summary>
public sealed class ConcurrencyProbeAnalyzer(TimeSpan workDuration) : IDocumentAnalyzer
{
    private int _current;
    private int _peak;

    public int Peak => Volatile.Read(ref _peak);

    public DocumentResult Analyze(DocumentInput document)
    {
        var now = Interlocked.Increment(ref _current);
        int peak;
        while (now > (peak = Volatile.Read(ref _peak)) && Interlocked.CompareExchange(ref _peak, now, peak) != peak) { }

        Thread.Sleep(workDuration);
        Interlocked.Decrement(ref _current);
        return new StubAnalyzer().Analyze(document);
    }
}

/// <summary>
/// Blocks every call until <see cref="Release"/> (a synchronous block on purpose: <c>Analyze</c> is synchronous CPU work).
/// <see cref="Entered"/> completes when the first call is in progress, so tests can <c>await</c> it instead of blocking.
/// </summary>
public sealed class GatedAnalyzer : IDocumentAnalyzer, IDisposable
{
    private readonly ManualResetEventSlim _gate = new(false);
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => _entered.Task;

    public void Release() => _gate.Set();

    public DocumentResult Analyze(DocumentInput document)
    {
        _entered.TrySetResult();
        _gate.Wait(TimeSpan.FromSeconds(10));
        return new StubAnalyzer().Analyze(document);
    }

    public void Dispose() => _gate.Dispose();
}

public sealed class ThrowingAnalyzer : IDocumentAnalyzer
{
    public DocumentResult Analyze(DocumentInput document) => throw new InvalidOperationException("analyzer bug");
}
