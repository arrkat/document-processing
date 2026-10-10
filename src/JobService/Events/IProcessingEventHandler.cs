using Contracts.Events;

namespace JobService.Events;

/// <summary>
/// Applies one Processor event: <see cref="Domain.JobStateTransitions.Apply"/> inside <see cref="Persistence.IJobRepository.TryUpdateStateAsync"/>,
/// then side effects: logs every applied or ignored event (including ProcessingStarted) and, on any terminal event,
/// calls <see cref="Dispatch.IDispatchWakeUp.SlotFreed"/> so a job waiting after a 503 is sent at once.
/// </summary>
public interface IProcessingEventHandler
{
    Task<EventHandlingResult> HandleAsync(ProcessingEvent processingEvent, CancellationToken cancellationToken = default);
}
