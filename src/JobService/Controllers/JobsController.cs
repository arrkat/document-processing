using Contracts.Documents;
using Contracts.Jobs;
using JobService.Application;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace JobService.Controllers;

/// <summary>Create, list, inspect and cancel document-processing jobs.</summary>
[ApiController]
[Route("api/jobs")]
[Produces("application/json")]
public sealed class JobsController(IJobManager manager) : ControllerBase
{
    /// <summary>Create a job.</summary>
    /// <remarks>
    /// Validates the documents, stores them and queues the job for processing. Returns immediately with the job in
    /// <c>Pending</c>; follow the <c>Location</c> header (GET /api/jobs/{jobId}) to track it.
    /// Limits: 1–1000 documents; ids non-empty, unique, at most 128 characters; content non-null, at most 100,000 characters.
    /// </remarks>
    /// <response code="202">Job created and queued.</response>
    /// <response code="400">Invalid request; <c>errors</c> lists every problem by field path (e.g. <c>documents[1].id</c>).</response>
    [HttpPost]
    [ProducesResponseType<JobSummary>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateJobRequest? request, CancellationToken ct)
    {
        var result = await manager.CreateAsync(request, ct);
        return result.Job is { } job
            ? AcceptedAtAction(nameof(Get), new { jobId = job.Id }, job)
            : ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>(result.Errors!)));
    }

    /// <summary>List jobs, newest first.</summary>
    /// <param name="status">Only jobs in this status.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Jobs per page.</param>
    /// <param name="ct">Request cancellation.</param>
    /// <response code="200">One page of job summaries (no results).</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<JobSummary>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] JobStatus? status, CancellationToken ct, [FromQuery] int page = 1, [FromQuery] int pageSize = 50) =>
        Ok(await manager.ListAsync(status, page, pageSize, ct));

    /// <summary>Get a job.</summary>
    /// <remarks>Status and progress; for a <c>Completed</c> job the results are included.</remarks>
    /// <response code="200">The job.</response>
    /// <response code="404">No job with this id.</response>
    [HttpGet("{jobId}")]
    [ProducesResponseType<JobDetails>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(string jobId, CancellationToken ct) =>
        await manager.GetAsync(jobId, ct) is { } details ? Ok(details) : NotFound();

    /// <summary>Get a job's results.</summary>
    /// <remarks>One result per document: word count, unique word count, top 5 words, SHA-256. Available once the job is <c>Completed</c>.</remarks>
    /// <response code="200">The results.</response>
    /// <response code="404">No job with this id, or the job is not Completed.</response>
    [HttpGet("{jobId}/results")]
    [ProducesResponseType<IReadOnlyList<DocumentResult>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResults(string jobId, CancellationToken ct) =>
        await manager.GetResultsAsync(jobId, ct) is { } results ? Ok(results) : NotFound();

    /// <summary>Cancel a job.</summary>
    /// <remarks>
    /// A Pending job is cancelled immediately. For a Processing job a cancel command is sent to the Processor and the job stays
    /// Processing until the Processor confirms; it may still finish as Completed if processing ended first.
    /// </remarks>
    /// <response code="202">Cancelled (Pending job) or cancellation requested (Processing job).</response>
    /// <response code="404">No job with this id.</response>
    /// <response code="409">The job already finished (Completed, Failed or Cancelled).</response>
    /// <response code="503">The job is processing and the Processor could not be reached; retry.</response>
    [HttpPost("{jobId}/cancel")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Cancel(string jobId, CancellationToken ct) =>
        await manager.CancelAsync(jobId, ct) switch
        {
            CancelJobResult.Cancelled or CancelJobResult.CancellationRequested => AcceptedAtAction(nameof(Get), new { jobId }, null),
            CancelJobResult.NotFound => NotFound(),
            CancelJobResult.AlreadyTerminal => Problem(
                title: "Job already finished",
                detail: $"Job '{jobId}' is already in a final state and cannot be cancelled.",
                statusCode: StatusCodes.Status409Conflict),
            CancelJobResult.ProcessorUnavailable => Problem(
                title: "Processor unavailable",
                detail: $"Job '{jobId}' is processing and the cancel command could not be delivered. Retry later.",
                statusCode: StatusCodes.Status503ServiceUnavailable),
            var result => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown cancel result.")
        };
}
