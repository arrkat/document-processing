using Contracts.Events;
using JobService.Dispatch;
using JobService.Persistence;

namespace JobService.Events;

public sealed class ProcessingEventHandler(
    IJobRepository repository,
    IDispatchWakeUp dispatchWakeUp,
    ILogger<ProcessingEventHandler> logger) : IProcessingEventHandler
{
    public Task<EventHandlingResult> HandleAsync(ProcessingEvent processingEvent, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
