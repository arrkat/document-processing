using Contracts.Events;
using JobService.Dispatch;
using JobService.Domain;
using JobService.Persistence;

namespace JobService.Events;

public sealed class ProcessingEventHandler(
    IJobRepository repository,
    IDispatchWakeUp dispatchWakeUp,
    TimeProvider timeProvider,
    ILogger<ProcessingEventHandler> logger) : IProcessingEventHandler
{
    public async Task<EventHandlingResult> HandleAsync(ProcessingEvent processingEvent, CancellationToken cancellationToken = default)
    {
        // The impure shell around the pure Apply: it reads the clock once and passes the time in.
        var receivedAt = timeProvider.GetUtcNow();
        IgnoreReason? ignored = null;

        var update = await repository.TryUpdateStateAsync(processingEvent.JobId, state =>
        {
            var transition = JobStateTransitions.Apply(state, processingEvent, receivedAt);
            ignored = transition.Ignored; // only records the decision; safe if the update is retried
            return transition.NewState;
        }, cancellationToken);

        // The Processor frees its job slot when it sends a terminal event, whether or not this service still cared about the job.
        if (processingEvent.IsTerminal)
            dispatchWakeUp.SlotFreed();

        var result = update.Status switch
        {
            StateUpdateStatus.Updated => EventHandlingResult.Applied,
            StateUpdateStatus.NotFound => EventHandlingResult.UnknownJob,
            _ => ignored == IgnoreReason.Duplicate ? EventHandlingResult.Duplicate : EventHandlingResult.InvalidTransition
        };

        Log(processingEvent, result, update.State);
        return result;
    }

    private void Log(ProcessingEvent processingEvent, EventHandlingResult result, JobState? state)
    {
        var eventType = processingEvent.GetType().Name.Replace("Event", "");
        switch (result)
        {
            case EventHandlingResult.Applied when processingEvent is ProcessingProgressUpdatedEvent:
                logger.LogDebug("{EventType} #{Sequence} for job {JobId} applied", eventType, processingEvent.Sequence, processingEvent.JobId);
                break;
            case EventHandlingResult.Applied:
                logger.LogInformation("{EventType} #{Sequence} for job {JobId} applied; job is {Status}",
                    eventType, processingEvent.Sequence, processingEvent.JobId, state!.Status);
                break;
            case EventHandlingResult.UnknownJob:
                logger.LogWarning("{EventType} #{Sequence} for unknown job {JobId} ignored", eventType, processingEvent.Sequence, processingEvent.JobId);
                break;
            default:
                // Duplicates and events for finished jobs are routine (e.g. a reconnect snapshot), so they are not warnings.
                logger.LogDebug("{EventType} #{Sequence} for job {JobId} ignored: {Reason}",
                    eventType, processingEvent.Sequence, processingEvent.JobId, result);
                break;
        }
    }
}
