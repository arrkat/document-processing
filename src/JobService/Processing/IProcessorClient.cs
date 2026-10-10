using Contracts.Processing;

namespace JobService.Processing;

/// <summary>Typed client for the Processor Service REST API.</summary>
public interface IProcessorClient
{
    /// <summary>POST /api/processing-jobs. Never throws for HTTP/network failures; they are mapped to <see cref="DispatchResult"/>.</summary>
    Task<DispatchResult> SubmitAsync(ProcessJobRequest request, CancellationToken cancellationToken = default);

    /// <summary>GET /api/processing-jobs/{jobId}.</summary>
    /// <returns><c>null</c> if the Processor does not know the job (404).</returns>
    /// <exception cref="HttpRequestException">The Processor could not be reached.</exception>
    Task<ProcessingJobStatus?> GetStatusAsync(string jobId, CancellationToken cancellationToken = default);

    /// <summary>POST /api/processing-jobs/{jobId}/cancel. Never throws for HTTP/network failures.</summary>
    Task<ProcessorCancelResult> CancelAsync(string jobId, CancellationToken cancellationToken = default);
}
