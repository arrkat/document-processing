using System.Net;
using System.Net.Http.Json;
using Contracts;
using Contracts.Processing;

namespace JobService.Processing;

/// <summary>
/// Typed HttpClient; BaseAddress and Timeout come from <see cref="Settings.ProcessorClientOptions"/>.
/// Maps the Processor's HTTP answers onto results the Job Service reasons about; a timeout counts as "unreachable".
/// </summary>
public sealed class ProcessorClient(HttpClient httpClient, ILogger<ProcessorClient> logger) : IProcessorClient
{
    public async Task<DispatchResult> SubmitAsync(ProcessJobRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsJsonAsync("api/processing-jobs", request, ContractJson.Options, cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.Accepted or HttpStatusCode.OK => DispatchResult.Accepted,
                HttpStatusCode.ServiceUnavailable or HttpStatusCode.TooManyRequests => DispatchResult.Busy,
                >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError => DispatchResult.Rejected,
                _ => DispatchResult.Unavailable
            };
        }
        catch (Exception ex) when (IsUnreachable(ex, cancellationToken))
        {
            logger.LogWarning("Processor unreachable while submitting job {JobId}: {Reason}", request.JobId, ex.Message);
            return DispatchResult.Unavailable;
        }
    }

    public async Task<ProcessingJobStatus?> GetStatusAsync(string jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync($"api/processing-jobs/{Uri.EscapeDataString(jobId)}", cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ProcessingJobStatus>(ContractJson.Options, cancellationToken);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new HttpRequestException("The Processor did not answer in time.", ex);
        }
    }

    public async Task<ProcessorCancelResult> CancelAsync(string jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.PostAsync($"api/processing-jobs/{Uri.EscapeDataString(jobId)}/cancel", content: null, cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.Accepted => ProcessorCancelResult.CancellationRequested,
                HttpStatusCode.Conflict => ProcessorCancelResult.AlreadyFinished,
                HttpStatusCode.NotFound => ProcessorCancelResult.NotFound,
                _ => ProcessorCancelResult.Unavailable
            };
        }
        catch (Exception ex) when (IsUnreachable(ex, cancellationToken))
        {
            logger.LogWarning("Processor unreachable while cancelling job {JobId}: {Reason}", jobId, ex.Message);
            return ProcessorCancelResult.Unavailable;
        }
    }

    // Connection failures, and timeouts (a TaskCanceledException the caller did not ask for).
    private static bool IsUnreachable(Exception ex, CancellationToken cancellationToken) =>
        ex is HttpRequestException || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested);
}
