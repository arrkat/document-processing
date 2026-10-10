using System.Diagnostics;
using System.Threading.Channels;
using Contracts;
using JobService.Dispatch;
using JobService.Domain;
using JobService.Persistence;
using JobService.Processing;
using JobService.Settings;
using JobService.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using static JobService.Tests.TestSupport.Jobs;

namespace JobService.Tests.Dispatch;

public class JobDispatcherTests
{
    private readonly InMemoryJobRepository _repository = new();
    private readonly Channel<DispatchRequest> _queue = Channel.CreateUnbounded<DispatchRequest>();
    private readonly FakeProcessorClient _processor = new();

    private JobDispatcher CreateDispatcher(Action<DispatchOptions>? configure = null)
    {
        var options = new DispatchOptions
        {
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(1),
            MaxDelay = TimeSpan.FromMilliseconds(5),
            BusyRetryDelay = TimeSpan.FromMilliseconds(1)
        };
        configure?.Invoke(options);
        return new JobDispatcher(_queue.Reader, _repository, _processor, TimeProvider.System, Options.Create(options),
            NullLogger<JobDispatcher>.Instance);
    }

    private async Task<JobState> StateOf(string jobId) => (await _repository.GetAsync(jobId))!.State;

    [Fact]
    public async Task Accepted_job_becomes_Processing()
    {
        await AddAsync(_repository, "a", PendingJob());

        await CreateDispatcher().DispatchAsync(new DispatchRequest("a"), CancellationToken.None);

        Assert.IsType<ProcessingState>(await StateOf("a"));
    }

    [Fact]
    public async Task Job_that_is_no_longer_Pending_is_not_sent()
    {
        await AddAsync(_repository, "a", CancelledJob());

        await CreateDispatcher().DispatchAsync(new DispatchRequest("a"), CancellationToken.None);

        Assert.Empty(_processor.Submitted);
    }

    [Fact]
    public async Task Busy_is_not_a_failed_attempt()
    {
        await AddAsync(_repository, "a", PendingJob());
        _processor.ScriptSubmit("a", Enumerable.Repeat(DispatchResult.Busy, 10).Append(DispatchResult.Accepted).ToArray());

        await CreateDispatcher(o => o.MaxAttempts = 2).DispatchAsync(new DispatchRequest("a"), CancellationToken.None);

        Assert.Equal(11, _processor.Submitted.Count);
        Assert.IsType<ProcessingState>(await StateOf("a"));
    }

    [Fact]
    public async Task Freed_slot_ends_the_busy_wait_early()
    {
        await AddAsync(_repository, "a", PendingJob());
        _processor.ScriptSubmit("a", DispatchResult.Busy, DispatchResult.Accepted);
        var dispatcher = CreateDispatcher(o => o.BusyRetryDelay = TimeSpan.FromSeconds(30));
        var stopwatch = Stopwatch.StartNew();

        var dispatch = dispatcher.DispatchAsync(new DispatchRequest("a"), CancellationToken.None);
        await WaitUntilAsync(() => _processor.Submitted.Count == 1);
        dispatcher.SlotFreed();
        await dispatch.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.IsType<ProcessingState>(await StateOf("a"));
    }

    [Fact]
    public async Task Unreachable_for_every_attempt_fails_the_job()
    {
        await AddAsync(_repository, "a", PendingJob());
        _processor.ScriptSubmit("a", DispatchResult.Unavailable, DispatchResult.Unavailable, DispatchResult.Unavailable);

        await CreateDispatcher().DispatchAsync(new DispatchRequest("a"), CancellationToken.None);

        Assert.Equal(3, _processor.Submitted.Count);
        Assert.Equal(ErrorCode.ProcessorUnavailable, Assert.IsType<FailedState>(await StateOf("a")).ErrorCode);
    }

    [Fact]
    public async Task Processor_coming_back_within_the_window_starts_the_job()
    {
        await AddAsync(_repository, "a", PendingJob());
        _processor.ScriptSubmit("a", DispatchResult.Unavailable, DispatchResult.Unavailable, DispatchResult.Accepted);

        await CreateDispatcher().DispatchAsync(new DispatchRequest("a"), CancellationToken.None);

        Assert.IsType<ProcessingState>(await StateOf("a"));
    }

    [Fact]
    public async Task Rejected_job_fails_without_retry()
    {
        await AddAsync(_repository, "a", PendingJob());
        _processor.ScriptSubmit("a", DispatchResult.Rejected);

        await CreateDispatcher().DispatchAsync(new DispatchRequest("a"), CancellationToken.None);

        Assert.Single(_processor.Submitted);
        Assert.Equal(ErrorCode.ProcessorRejected, Assert.IsType<FailedState>(await StateOf("a")).ErrorCode);
    }

    [Fact]
    public async Task Cancel_racing_the_dispatch_sends_a_compensating_cancel()
    {
        await AddAsync(_repository, "a", PendingJob());
        // The client cancels while the POST is on its way to the Processor.
        _processor.OnSubmit = jobId => _repository.TryUpdateStateAsync(jobId, s => s is PendingState p ? p.Cancel(T0) : null);

        await CreateDispatcher().DispatchAsync(new DispatchRequest("a"), CancellationToken.None);

        Assert.IsType<CancelledState>(await StateOf("a"));
        Assert.Equal(["a"], _processor.CancelCommands);
    }

    [Fact]
    public async Task Jobs_are_sent_one_at_a_time_in_order()
    {
        await AddAsync(_repository, "a", PendingJob());
        await AddAsync(_repository, "b", PendingJob());
        _processor.ScriptSubmit("a", DispatchResult.Busy, DispatchResult.Accepted);
        using var dispatcher = CreateDispatcher();

        await dispatcher.StartAsync(CancellationToken.None);
        await _queue.Writer.WriteAsync(new DispatchRequest("a"));
        await _queue.Writer.WriteAsync(new DispatchRequest("b"));
        await WaitUntilAsync(() => _processor.Submitted.Count == 3);
        await dispatcher.StopAsync(CancellationToken.None);

        Assert.Equal(["a", "a", "b"], _processor.Submitted);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail("Timed out waiting for the condition.");
            await Task.Delay(10);
        }
    }
}
