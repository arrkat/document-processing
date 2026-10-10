using Contracts.Documents;
using Contracts.Jobs;
using JobService.Application;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace JobService.Endpoints;

public static class JobsEndpoints
{
    public static IEndpointRouteBuilder MapJobsEndpoints(this IEndpointRouteBuilder app)
    {
        var jobs = app.MapGroup("/api/jobs").WithTags("Jobs");

        jobs.MapPost("/", CreateJob).WithName("CreateJob");
        jobs.MapGet("/", ListJobs).WithName("ListJobs");
        jobs.MapGet("/{jobId}", GetJob).WithName("GetJob");
        jobs.MapGet("/{jobId}/results", GetJobResults).WithName("GetJobResults");
        jobs.MapPost("/{jobId}/cancel", CancelJob).WithName("CancelJob");

        return app;
    }

    private static async Task<Results<Accepted<JobSummary>, ValidationProblem>> CreateJob(
        CreateJobRequest? request, IJobManager manager, CancellationToken ct)
    {
        var outcome = await manager.CreateAsync(request, ct);
        return outcome.Job is { } job
            ? TypedResults.Accepted($"/api/jobs/{job.Id}", job)
            : TypedResults.ValidationProblem(new Dictionary<string, string[]>(outcome.Errors!));
    }

    private static async Task<Ok<PagedResult<JobSummary>>> ListJobs(
        IJobManager manager, CancellationToken ct, JobStatus? status = null, int page = 1, int pageSize = 50) =>
        TypedResults.Ok(await manager.ListAsync(status, page, pageSize, ct));

    private static async Task<Results<Ok<JobDetails>, NotFound>> GetJob(
        string jobId, IJobManager manager, CancellationToken ct) =>
        await manager.GetAsync(jobId, ct) is { } details
            ? TypedResults.Ok(details)
            : TypedResults.NotFound();

    private static async Task<Results<Ok<IReadOnlyList<DocumentResult>>, NotFound>> GetJobResults(
        string jobId, IJobManager manager, CancellationToken ct) =>
        await manager.GetResultsAsync(jobId, ct) is { } results
            ? TypedResults.Ok(results)
            : TypedResults.NotFound();

    private static async Task<Results<Accepted, NotFound, Conflict<ProblemDetails>>> CancelJob(
        string jobId, IJobManager manager, CancellationToken ct) =>
        await manager.CancelAsync(jobId, ct) switch
        {
            CancelJobOutcome.Accepted => TypedResults.Accepted($"/api/jobs/{jobId}"),
            CancelJobOutcome.NotFound => TypedResults.NotFound(),
            _ => TypedResults.Conflict(new ProblemDetails
            {
                Title = "Job already finished",
                Detail = $"Job '{jobId}' is already in a terminal state and cannot be cancelled.",
                Status = StatusCodes.Status409Conflict
            })
        };
}
