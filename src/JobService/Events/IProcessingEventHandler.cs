using Contracts.Events;

namespace JobService.Events;

/// <summary>
/// Applies one Processor event: <see cref="Domain.JobStateTransitions.Apply"/> inside <see cref="Persistence.IJobRepository.TryUpdateStateAsync"/>,
/// then side effects: on ProcessingFinished enqueue results ingestion; on any terminal event call <see cref="Dispatch.IDispatchWakeUp.SlotFreed"/>;
/// log ignored events.
/// </summary>
public interface IProcessingEventHandler
{
    Task<EventHandlingResult> HandleAsync(ProcessingEvent processingEvent, CancellationToken cancellationToken = default);
}
