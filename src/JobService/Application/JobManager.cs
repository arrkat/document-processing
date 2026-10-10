using System.Threading.Channels;
using BlobStorage;
using Contracts;
using Contracts.Blobs;
using Contracts.Documents;
using Contracts.Jobs;
using JobService.Dispatch;
using JobService.Domain;
using JobService.Persistence;
using JobService.Processing;
using JobService.Validation;

namespace JobService.Application;

public sealed class JobManager(
    ICreateJobRequestValidator validator,
    IJobRepository repository,
    IBlobStore blobStore,
    ChannelWriter<DispatchRequest> dispatchQueue,
    IProcessorClient processorClient,
    TimeProvider timeProvider,
    ILogger<JobManager> logger) : IJobManager
{
    public async Task<CreateJobResult> CreateAsync(CreateJobRequest? request, CancellationToken cancellationToken = default)
    {
        var errors = validator.Validate(request);
        if (errors.Count > 0)
            return CreateJobResult.Invalid(errors);

        var documents = request!.Documents!;
        var jobId = Guid.NewGuid().ToString("N");
        var documentsRef = BlobKey.JobDocuments(jobId);

        // Order matters: the blob before the job (the Processor must never get a job without documents),
        // the job before the queue (the dispatcher must always find the job it reads).
        await blobStore.PutJsonAsync(documentsRef,
            new JobDocumentsBlob(JobDocumentsBlob.CurrentSchemaVersion, jobId, documents), ContractJson.Options, cancellationToken);

        var now = timeProvider.GetUtcNow();
        var job = new Job(new JobMetadata(jobId, documents.Count, documentsRef, now), PendingState.Create(now));
        await repository.TryAddAsync(job.Metadata, job.State, cancellationToken);

        // The dispatch channel is unbounded: the job is already stored, so refusing it here would strand it.
        dispatchQueue.TryWrite(new DispatchRequest(jobId));

        logger.LogInformation("Job {JobId} created with {DocumentCount} documents", jobId, documents.Count);
        return CreateJobResult.Created(job.ToSummary());
    }

    public async Task<PagedResult<JobSummary>> ListAsync(JobStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var jobs = await repository.ListAsync(status, page, pageSize, cancellationToken);
        return new PagedResult<JobSummary>(jobs.Items.Select(job => job.ToSummary()).ToList(), jobs.Page, jobs.PageSize, jobs.TotalCount);
    }

    public async Task<JobDetails?> GetAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var job = await repository.GetAsync(jobId, cancellationToken);
        if (job is null)
            return null;

        var results = job.State is CompletedState ? await ReadResultsAsync(jobId, cancellationToken) : null;
        return new JobDetails(job.ToSummary(), results);
    }

    public async Task<IReadOnlyList<DocumentResult>?> GetResultsAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var job = await repository.GetAsync(jobId, cancellationToken);
        return job?.State is CompletedState ? await ReadResultsAsync(jobId, cancellationToken) : null;
    }

    public async Task<CancelJobResult> CancelAsync(string jobId, CancellationToken cancellationToken = default)
    {
        // States only move forward, so this loop runs at most a couple of times (Pending → Processing → terminal).
        while (true)
        {
            var job = await repository.GetAsync(jobId, cancellationToken);
            switch (job?.State)
            {
                case null:
                    return CancelJobResult.NotFound;

                case PendingState:
                {
                    var now = timeProvider.GetUtcNow();
                    var update = await repository.TryUpdateStateAsync(jobId,
                        state => state is PendingState pending ? pending.Cancel(now) : null, cancellationToken);
                    if (update.Status == StateUpdateStatus.Updated)
                    {
                        logger.LogInformation("Job {JobId} cancelled while waiting to be dispatched", jobId);
                        return CancelJobResult.Cancelled;
                    }

                    continue; // it moved on (e.g. the Processor just accepted it): decide again
                }

                case ProcessingState:
                    return await CancelOnProcessorAsync(jobId, cancellationToken);

                default:
                    return CancelJobResult.AlreadyTerminal;
            }
        }
    }

    private async Task<CancelJobResult> CancelOnProcessorAsync(string jobId, CancellationToken cancellationToken)
    {
        var answer = await processorClient.CancelAsync(jobId, cancellationToken);
        logger.LogInformation("Cancel command for job {JobId} sent to the Processor: {Answer}", jobId, answer);

        switch (answer)
        {
            case ProcessorCancelResult.CancellationRequested:
                return CancelJobResult.CancellationRequested;
            case ProcessorCancelResult.AlreadyFinished:
                return CancelJobResult.AlreadyTerminal;
            case ProcessorCancelResult.NotFound:
                // A fact, not a guess: the Processor says it no longer has the job.
                var now = timeProvider.GetUtcNow();
                await repository.TryUpdateStateAsync(jobId,
                    state => state is ProcessingState processing
                        ? processing.Fail(ErrorCode.ProcessorTimeout, "The Processor no longer knows this job (it probably restarted).", now)
                        : null,
                    cancellationToken);
                return CancelJobResult.AlreadyTerminal;
            case ProcessorCancelResult.Unavailable:
                return CancelJobResult.ProcessorUnavailable;
            default:
                throw new ArgumentOutOfRangeException(nameof(answer), answer, "Unknown cancel answer.");
        }
    }

    private async Task<IReadOnlyList<DocumentResult>> ReadResultsAsync(string jobId, CancellationToken cancellationToken)
    {
        var blob = await blobStore.GetJsonAsync<JobResultsBlob>(BlobKey.JobResults(jobId), ContractJson.Options, cancellationToken);
        return blob.Results;
    }
}
