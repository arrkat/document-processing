using Contracts.Processing;
using ProcessorService.Processing;
using ProcessorService.Tests.TestSupport;

namespace ProcessorService.Tests.Processing;

/// <summary>Admission rules owned by <see cref="ProcessingWorker.TryStart"/>.</summary>
public sealed class ProcessingWorkerAdmissionTests : IAsyncDisposable
{
    private readonly GatedAnalyzer _analyzer = new();
    private readonly WorkerHarness _harness;

    public ProcessingWorkerAdmissionTests() =>
        _harness = new WorkerHarness(_analyzer, o => o.MaxConcurrentJobs = 2);

    [Fact]
    public async Task Known_job_is_AlreadyKnown_even_when_all_slots_are_busy()
    {
        Assert.Equal(StartJobResult.Started, _harness.Worker.TryStart(await _harness.CreateJobAsync("a", 1)));
        Assert.Equal(StartJobResult.Started, _harness.Worker.TryStart(await _harness.CreateJobAsync("b", 1)));

        Assert.Equal(StartJobResult.AlreadyKnown, _harness.Worker.TryStart(await _harness.CreateJobAsync("a", 1)));
    }

    [Fact]
    public async Task Busy_when_all_slots_are_taken_and_nothing_is_registered()
    {
        _harness.Worker.TryStart(await _harness.CreateJobAsync("a", 1));
        _harness.Worker.TryStart(await _harness.CreateJobAsync("b", 1));

        Assert.Equal(StartJobResult.Busy, _harness.Worker.TryStart(await _harness.CreateJobAsync("c", 1)));
        Assert.Null(_harness.Registry.GetStatus("c"));
    }

    [Fact]
    public async Task Slot_is_released_when_a_job_ends()
    {
        _harness.Worker.TryStart(await _harness.CreateJobAsync("a", 1));
        _harness.Worker.TryStart(await _harness.CreateJobAsync("b", 1));

        _analyzer.Release();
        await WorkerHarness.WaitUntilAsync(() => _harness.Worker.RunningJobs == 0, "both jobs ended");

        Assert.Equal(StartJobResult.Started, _harness.Worker.TryStart(await _harness.CreateJobAsync("c", 1)));
    }

    [Fact]
    public async Task Concurrent_submits_of_the_same_job_start_it_once()
    {
        _analyzer.Release();
        var job = await _harness.CreateJobAsync("a", 1);
        var results = new StartJobResult[50];

        Parallel.For(0, results.Length, i => results[i] = _harness.Worker.TryStart(job));

        Assert.Equal(1, results.Count(o => o == StartJobResult.Started));
        Assert.Equal(49, results.Count(o => o == StartJobResult.AlreadyKnown));
        await WorkerHarness.WaitUntilAsync(() => _harness.Registry.GetStatus("a")!.Status == ProcessingStatus.Finished, "the job finished");
    }

    public async ValueTask DisposeAsync()
    {
        _analyzer.Release();
        await _harness.DisposeAsync();
        _analyzer.Dispose();
    }
}
