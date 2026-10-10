using System.Net;
using System.Net.Http.Json;
using Contracts.Processing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ProcessorService.Application;
using ProcessorService.Jobs;

namespace ProcessorService.Tests.Controllers;

/// <summary>HTTP mapping only: the manager is faked, so these tests check status codes, headers and bodies.</summary>
public class ProcessingJobsControllerTests
{
    private static readonly ProcessJobRequest Request = new("job-1", "jobs/job-1/documents.json", 3);

    [Fact]
    public async Task Accepted_returns_202_with_location()
    {
        using var client = CreateClient(new SubmitProcessingJobResult(SubmitProcessingJobStatus.Accepted));

        var response = await client.PostAsJsonAsync("/api/processing-jobs", Request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("/api/processing-jobs/job-1", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Invalid_returns_400_with_field_errors()
    {
        using var client = CreateClient(new SubmitProcessingJobResult(SubmitProcessingJobStatus.Invalid,
            new Dictionary<string, string[]> { ["documentCount"] = ["Document count must be between 1 and 1000."] }));

        var response = await client.PostAsJsonAsync("/api/processing-jobs", Request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("documentCount", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Busy_returns_503_with_retry_after()
    {
        using var client = CreateClient(new SubmitProcessingJobResult(SubmitProcessingJobStatus.Busy));

        var response = await client.PostAsJsonAsync("/api/processing-jobs", Request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.NotNull(response.Headers.RetryAfter);
    }

    private static HttpClient CreateClient(SubmitProcessingJobResult result)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BlobStorage:RootPath", Path.Combine(Path.GetTempPath(), "ta9-ctrl-" + Guid.NewGuid().ToString("N")));
            builder.ConfigureTestServices(services => services.AddSingleton<IProcessingJobManager>(new FixedManager(result)));
        });
        return factory.CreateClient();
    }

    private sealed class FixedManager(SubmitProcessingJobResult result) : IProcessingJobManager
    {
        public SubmitProcessingJobResult Submit(ProcessJobRequest? request) => result;
        public ProcessingJobStatus? GetStatus(string jobId) => null;
        public CancelProcessingJobResult Cancel(string jobId) => CancelProcessingJobResult.NotFound;
    }
}
