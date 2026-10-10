using System.Net;
using System.Net.Http.Json;
using Contracts.Documents;
using Contracts.Jobs;
using JobService.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using static JobService.Tests.TestSupport.Jobs;

namespace JobService.Tests.Controllers;

/// <summary>HTTP mapping only: the manager is faked, so these tests check status codes, headers and bodies.</summary>
public class JobsControllerTests
{
    private static readonly JobSummary Summary = new("job-1", JobStatus.Pending, 1, 0, T0, null, null, T0, null, null);
    private static readonly CreateJobRequest Request = new([new DocumentInput("a", "hello")]);

    [Fact]
    public async Task Create_returns_202_with_location()
    {
        using var client = CreateClient(new FakeManager { Create = CreateJobResult.Created(Summary) });

        var response = await client.PostAsJsonAsync("/api/jobs", Request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("/api/jobs/job-1", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Invalid_create_returns_400_with_field_errors()
    {
        using var client = CreateClient(new FakeManager
        {
            Create = CreateJobResult.Invalid(new Dictionary<string, string[]> { ["documents[0].id"] = ["Document id is required."] })
        });

        var response = await client.PostAsJsonAsync("/api/jobs", Request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("documents[0].id", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Unknown_job_returns_404()
    {
        using var client = CreateClient(new FakeManager());

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/jobs/missing")).StatusCode);
    }

    [Theory]
    [InlineData(CancelJobResult.Cancelled, HttpStatusCode.Accepted)]
    [InlineData(CancelJobResult.CancellationRequested, HttpStatusCode.Accepted)]
    [InlineData(CancelJobResult.NotFound, HttpStatusCode.NotFound)]
    [InlineData(CancelJobResult.AlreadyTerminal, HttpStatusCode.Conflict)]
    [InlineData(CancelJobResult.ProcessorUnavailable, HttpStatusCode.ServiceUnavailable)]
    public async Task Cancel_maps_results_to_status_codes(CancelJobResult result, HttpStatusCode expected)
    {
        using var client = CreateClient(new FakeManager { Cancel = result });

        Assert.Equal(expected, (await client.PostAsync("/api/jobs/job-1/cancel", null)).StatusCode);
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task Invalid_paging_returns_400(string query)
    {
        using var client = CreateClient(new FakeManager());

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/jobs?{query}")).StatusCode);
    }

    private static HttpClient CreateClient(IJobManager manager)
    {
        var factory = new WebApplicationFactory<JobService.Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BlobStorage:RootPath", Path.Combine(Path.GetTempPath(), "ta9-js-ctrl-" + Guid.NewGuid().ToString("N")));
            builder.ConfigureTestServices(services => services.AddSingleton(manager));
        });
        return factory.CreateClient();
    }

    private sealed class FakeManager : IJobManager
    {
        public CreateJobResult Create { get; init; } = CreateJobResult.Created(Summary);
        public CancelJobResult Cancel { get; init; } = CancelJobResult.NotFound;

        public Task<CreateJobResult> CreateAsync(CreateJobRequest? request, CancellationToken cancellationToken = default) => Task.FromResult(Create);

        public Task<PagedResult<JobSummary>> ListAsync(JobStatus? status, int page, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult(new PagedResult<JobSummary>([], page, pageSize, 0));

        public Task<JobDetails?> GetAsync(string jobId, CancellationToken cancellationToken = default) => Task.FromResult<JobDetails?>(null);

        public Task<IReadOnlyList<DocumentResult>?> GetResultsAsync(string jobId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DocumentResult>?>(null);

        public Task<CancelJobResult> CancelAsync(string jobId, CancellationToken cancellationToken = default) => Task.FromResult(Cancel);
    }
}
