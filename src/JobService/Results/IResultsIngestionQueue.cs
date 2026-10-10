namespace JobService.Results;

/// <summary>Job ids whose results blob (<see cref="BlobStorage.BlobKey.JobResults"/>) should be ingested. Fed by ProcessingFinished events and the watchdog.</summary>
public interface IResultsIngestionQueue
{
    bool TryEnqueue(string jobId);

    IAsyncEnumerable<string> ReadAllAsync(CancellationToken cancellationToken);
}
