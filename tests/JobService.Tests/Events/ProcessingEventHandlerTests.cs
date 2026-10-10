using Contracts;
using Contracts.Events;
using JobService.Dispatch;
using JobService.Domain;
using JobService.Events;
using JobService.Persistence;
using JobService.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using static JobService.Tests.TestSupport.Jobs;

namespace JobService.Tests.Events;

public class ProcessingEventHandlerTests
{
    private readonly InMemoryJobRepository _repository = new();
    private readonly RecordingWakeUp _wakeUp = new();
    private readonly ListLogger<ProcessingEventHandler> _logger = new();
    private readonly ProcessingEventHandler _handler;

    public ProcessingEventHandlerTests() =>
        _handler = new ProcessingEventHandler(_repository, _wakeUp, TimeProvider.System, _logger);

    private static ProcessingEvent Started(string jobId, long seq) => new ProcessingStartedEvent(Guid.NewGuid(), jobId, seq, T0, 3);
    private static ProcessingEvent Progress(string jobId, long seq) => new ProcessingProgressUpdatedEvent(Guid.NewGuid(), jobId, seq, T0, 1, 3);
    private static ProcessingEvent Finished(string jobId, long seq) => new ProcessingFinishedEvent(Guid.NewGuid(), jobId, seq, T0, "jobs/x/results.json");

    [Fact]
    public async Task Applied_event_updates_the_job()
    {
        await AddAsync(_repository, "a", ProcessingJob(sequence: 1));

        Assert.Equal(EventHandlingResult.Applied, await _handler.HandleAsync(Finished("a", 2)));
        Assert.IsType<CompletedState>((await _repository.GetAsync("a"))!.State);
    }

    [Fact]
    public async Task Unknown_duplicate_and_invalid_events_are_reported()
    {
        await AddAsync(_repository, "running", ProcessingJob(sequence: 5));
        await AddAsync(_repository, "done", CompletedJob(sequence: 5));

        Assert.Equal(EventHandlingResult.UnknownJob, await _handler.HandleAsync(Progress("missing", 1)));
        Assert.Equal(EventHandlingResult.Duplicate, await _handler.HandleAsync(Progress("running", 5)));
        Assert.Equal(EventHandlingResult.InvalidTransition, await _handler.HandleAsync(Progress("done", 6)));
    }

    [Fact]
    public async Task Every_terminal_event_wakes_the_dispatcher_even_when_ignored()
    {
        await AddAsync(_repository, "running", ProcessingJob(sequence: 1));
        await AddAsync(_repository, "done", CompletedJob(sequence: 5));

        await _handler.HandleAsync(Progress("running", 2));
        Assert.Equal(0, _wakeUp.Calls);

        await _handler.HandleAsync(Finished("running", 3)); // applied
        await _handler.HandleAsync(Finished("done", 6));    // ignored: the job already finished, but the Processor's slot is free
        Assert.Equal(2, _wakeUp.Calls);
    }

    [Fact]
    public async Task Lifecycle_events_are_logged_at_information_level()
    {
        await AddAsync(_repository, "a", PendingJob());

        await _handler.HandleAsync(Started("a", 1));

        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("ProcessingStarted"));
    }

    private sealed class RecordingWakeUp : IDispatchWakeUp
    {
        private int _calls;
        public int Calls => _calls;
        public void SlotFreed() => Interlocked.Increment(ref _calls);
    }
}
