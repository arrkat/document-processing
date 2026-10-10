using Contracts.Events;
using JobService.Dispatch;
using JobService.Persistence;
using JobService.Results;

namespace JobService.Events;

public sealed class ProcessingEventHandler(
    IJobRepository repository,
    IResultsIngestionQueue resultsQueue,
    IDispatchWakeUp dispatchWakeUp,
    ILogger<ProcessingEventHandler> logger) : IProcessingEventHandler
{
    public Task<EventHandlingResult> HandleAsync(ProcessingEvent processingEvent, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
