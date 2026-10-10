using Contracts.Events;

namespace JobService.Events;

/// <summary>
/// Applies one Processor event: <see cref="Domain.JobStateTransitions.Apply"/> inside <see cref="Persistence.IJobRepository.TryUpdateStateAsync"/>,
/// then side effects (on JobCompleted: enqueue results ingestion) and logging of ignored events.
/// </summary>
public interface IJobEventHandler
{
    Task<EventHandlingResult> HandleAsync(JobEvent jobEvent, CancellationToken cancellationToken = default);
}
