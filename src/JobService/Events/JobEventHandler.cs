using Contracts.Events;
using JobService.Persistence;
using JobService.Results;

namespace JobService.Events;

public sealed class JobEventHandler(
    IJobRepository repository,
    IResultsIngestionQueue resultsQueue,
    ILogger<JobEventHandler> logger) : IJobEventHandler
{
    public Task<EventHandlingResult> HandleAsync(JobEvent jobEvent, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
