using Contracts.Processing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using ProcessorService.Intake;
using ProcessorService.Jobs;

namespace ProcessorService.Controllers;

/// <summary>Processing jobs, called by the Job Service.</summary>
[ApiController]
[Route("api/processing-jobs")]
[Produces("application/json")]
public sealed class ProcessingJobsController(IJobIntake intake) : ControllerBase
{
    private const int RetryAfterSeconds = 5;

    /// <summary>Start processing a job.</summary>
    /// <remarks>
    /// The documents are read from <c>documentsRef</c> in the shared blob store. Idempotent on <c>jobId</c>:
    /// resubmitting a known job returns 200 and starts nothing. There is no queue here: when every job slot is busy
    /// the answer is 503 and the caller retries later.
    /// </remarks>
    /// <response code="202">Processing started.</response>
    /// <response code="200">This jobId is already known; nothing new was started.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="503">All job slots are busy; retry after the <c>Retry-After</c> seconds.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult Submit([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ProcessJobRequest? request)
    {
        var outcome = intake.Submit(request);
        switch (outcome.Status)
        {
            case SubmitStatus.Accepted:
                return AcceptedAtAction(nameof(GetStatus), new { jobId = request!.JobId }, null);
            case SubmitStatus.AlreadyExists:
                return Ok();
            case SubmitStatus.Invalid:
                return ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]>(outcome.Errors!)));
            case SubmitStatus.Busy:
                Response.Headers.RetryAfter = RetryAfterSeconds.ToString();
                return Problem(
                    title: "Processor is busy",
                    detail: "All job slots are busy. Retry later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            default:
                throw new ArgumentOutOfRangeException(nameof(request), outcome.Status, "Unknown submit status.");
        }
    }

    /// <summary>Get a processing job's status.</summary>
    /// <remarks>Used by the Job Service to reconcile jobs whose events were lost.</remarks>
    /// <response code="200">The job's processing status.</response>
    /// <response code="404">This Processor does not know the job (e.g. it restarted).</response>
    [HttpGet("{jobId}")]
    [ProducesResponseType<ProcessingJobStatus>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public IActionResult GetStatus(string jobId) =>
        intake.GetStatus(jobId) is { } status ? Ok(status) : NotFound();

    /// <summary>Cancel a processing job.</summary>
    /// <response code="202">Cancellation requested; a ProcessingCancelled event follows.</response>
    /// <response code="404">This Processor does not know the job.</response>
    /// <response code="409">The job already reached a final state.</response>
    [HttpPost("{jobId}/cancel")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public IActionResult Cancel(string jobId) =>
        intake.Cancel(jobId) switch
        {
            CancelOutcome.CancellationRequested => AcceptedAtAction(nameof(GetStatus), new { jobId }, null),
            CancelOutcome.NotFound => NotFound(),
            CancelOutcome.AlreadyFinished => Problem(
                title: "Job already finished",
                detail: $"Job '{jobId}' already reached a final state.",
                statusCode: StatusCodes.Status409Conflict),
            var outcome => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown cancel outcome.")
        };
}
