using Contracts.Processing;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using ProcessorService.Application;
using ProcessorService.Jobs;

namespace ProcessorService.Endpoints;

public static class ProcessingJobsEndpoints
{
    private const int RetryAfterSeconds = 5;

    public static IEndpointRouteBuilder MapProcessingJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/processing-jobs").WithTags("Processing jobs");

        jobs.MapPost("/", Submit).WithName("SubmitProcessingJob");
        jobs.MapGet("/{jobId}", GetStatus).WithName("GetProcessingJobStatus");
        jobs.MapPost("/{jobId}/cancel", Cancel).WithName("CancelProcessingJob");

        return app;
    }

    private static Results<Accepted, Ok, ValidationProblem, ProblemHttpResult> Submit(
        ProcessJobRequest? request, IProcessingService service, HttpContext context)
    {
        var outcome = service.Submit(request);
        switch (outcome.Status)
        {
            case SubmitStatus.Accepted:
                return TypedResults.Accepted($"/api/processing-jobs/{request!.JobId}");
            case SubmitStatus.AlreadyExists:
                return TypedResults.Ok();
            case SubmitStatus.Invalid:
                return TypedResults.ValidationProblem(new Dictionary<string, string[]>(outcome.Errors!));
            default:
                context.Response.Headers.RetryAfter = RetryAfterSeconds.ToString();
                return TypedResults.Problem(
                    title: "Processor is busy",
                    detail: "The processing queue is full. Retry later.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static Results<Ok<ProcessingJobStatus>, NotFound> GetStatus(string jobId, IProcessingService service) =>
        service.GetStatus(jobId) is { } status
            ? TypedResults.Ok(status)
            : TypedResults.NotFound();

    private static Results<Accepted, NotFound, Conflict<ProblemDetails>> Cancel(string jobId, IProcessingService service) =>
        service.Cancel(jobId) switch
        {
            CancelOutcome.CancellationRequested => TypedResults.Accepted($"/api/processing-jobs/{jobId}"),
            CancelOutcome.NotFound => TypedResults.NotFound(),
            _ => TypedResults.Conflict(new ProblemDetails
            {
                Title = "Job already finished",
                Detail = $"Job '{jobId}' already reached a terminal state.",
                Status = StatusCodes.Status409Conflict
            })
        };
}
