using BlobStorage;
using Contracts.Documents;
using Contracts.Jobs;
using JobService.Dispatch;
using JobService.Persistence;
using JobService.Processing;
using JobService.Validation;

namespace JobService.Application;

public sealed class JobManager(
    ICreateJobRequestValidator validator,
    IJobRepository repository,
    IBlobStore blobStore,
    IJobDispatchQueue dispatchQueue,
    IProcessorClient processorClient,
    TimeProvider timeProvider,
    ILogger<JobManager> logger) : IJobManager
{
    public Task<CreateJobResult> CreateAsync(CreateJobRequest? request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<PagedResult<JobSummary>> ListAsync(JobStatus? status, int page, int pageSize, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<JobDetails?> GetAsync(string jobId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<DocumentResult>?> GetResultsAsync(string jobId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<CancelJobResult> CancelAsync(string jobId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
