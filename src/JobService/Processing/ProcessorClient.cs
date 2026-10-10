using Contracts.Processing;

namespace JobService.Processing;

/// <summary>Typed HttpClient; BaseAddress and Timeout come from <see cref="Settings.ProcessorClientOptions"/>.</summary>
public sealed class ProcessorClient(HttpClient httpClient, ILogger<ProcessorClient> logger) : IProcessorClient
{
    public Task<DispatchResult> SubmitAsync(ProcessJobRequest request, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<ProcessingJobStatus?> GetStatusAsync(string jobId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task<bool> CancelAsync(string jobId, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
