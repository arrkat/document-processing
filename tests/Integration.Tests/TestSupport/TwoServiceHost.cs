using System.Net.Http.Json;
using System.Net.WebSockets;
using Contracts;
using Contracts.Jobs;
using JobService.Processing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProcessorService.Analysis;
using ProcessorService.Events;

namespace Integration.Tests.TestSupport;

/// <summary>
/// Both services in one process, wired to each other as in production but without real ports:
/// <list type="bullet">
/// <item>Job Service → Processor (REST): the Job Service's <see cref="ProcessorClient"/> sends through the Processor's test server handler.</item>
/// <item>Processor → Job Service (WebSocket): the Processor's <see cref="IWebSocketConnector"/> connects to the Job Service's test server.</item>
/// <item>Both share one temp blob folder, so the real <c>FileSystemBlobStore</c> carries the claim-check.</item>
/// </list>
/// </summary>
public sealed class TwoServiceHost : IAsyncDisposable
{
    private readonly string _blobRoot = Path.Combine(Path.GetTempPath(), "ta9-e2e-" + Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<JobService.Program> _jobService;
    private readonly WebApplicationFactory<ProcessorService.Program> _processor;

    private TwoServiceHost(int maxConcurrentJobs, int perJobParallelism, IDocumentAnalyzer? analyzer)
    {
        _jobService = new WebApplicationFactory<JobService.Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BlobStorage:RootPath", _blobRoot);
            builder.UseSetting("Dispatch:BusyRetryDelay", "00:00:00.200");
            builder.UseSetting("Dispatch:InitialDelay", "00:00:00.050");
            // The handler is built while the Job Service starts (singletons capture the typed client), so it must not touch the
            // Processor yet: starting the Processor there would connect its socket back to a Job Service that is still starting.
            builder.ConfigureTestServices(services =>
                services.AddHttpClient<IProcessorClient, ProcessorClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => new DeferredTestServerHandler(() => _processor!.Server)));
        });

        _processor = new WebApplicationFactory<ProcessorService.Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BlobStorage:RootPath", _blobRoot);
            builder.UseSetting("Processing:MaxConcurrentJobs", maxConcurrentJobs.ToString());
            builder.UseSetting("Processing:PerJobParallelism", perJobParallelism.ToString());
            builder.UseSetting("JobService:ReconnectMinDelay", "00:00:00.050");
            builder.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<IWebSocketConnector>(new TestServerConnector(_jobService.Server)));
                if (analyzer is not null)
                    services.Replace(ServiceDescriptor.Singleton(analyzer));
            });
        });
    }

    /// <summary>A client of the Job Service, the system's public API.</summary>
    public HttpClient Client { get; private set; } = null!;

    /// <summary>Starts the Job Service, then the Processor, and waits until the Processor's event socket is connected.</summary>
    public static async Task<TwoServiceHost> StartAsync(int maxConcurrentJobs = 10, int perJobParallelism = 4, IDocumentAnalyzer? analyzer = null)
    {
        var host = new TwoServiceHost(maxConcurrentJobs, perJobParallelism, analyzer);
        host.Client = host._jobService.CreateClient();
        _ = host._processor.Server;

        var socketClient = host._processor.Services.GetRequiredService<ProcessingEventsSocketClient>();
        await Eventually.TrueAsync(() => Task.FromResult(socketClient.IsConnected), "the Processor connected to the Job Service");
        return host;
    }

    public async Task<JobSummary> CreateJobAsync(CreateJobRequest request)
    {
        var response = await Client.PostAsJsonAsync("/api/jobs", request, ContractJson.Options);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JobSummary>(ContractJson.Options))!;
    }

    public async Task<JobDetails> GetJobAsync(string jobId) =>
        (await Client.GetFromJsonAsync<JobDetails>($"/api/jobs/{jobId}", ContractJson.Options))!;

    /// <summary>Polls GET /api/jobs/{jobId} until the job reaches <paramref name="status"/>; fails fast if it ends in another final state.</summary>
    public async Task<JobDetails> WaitForStatusAsync(string jobId, JobStatus status)
    {
        JobDetails details = null!;
        await Eventually.TrueAsync(async () =>
        {
            details = await GetJobAsync(jobId);
            if (details.Job.Status != status && details.Job.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
                Assert.Fail($"Job {jobId} ended {details.Job.Status} ({details.Job.ErrorCode}: {details.Job.ErrorMessage}), expected {status}.");
            return details.Job.Status == status;
        }, $"job {jobId} is {status}");
        return details;
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        // The Processor first: it is the WebSocket client, so the Job Service never waits on an open socket.
        await _processor.DisposeAsync();
        await _jobService.DisposeAsync();
        try
        {
            Directory.Delete(_blobRoot, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    /// <summary>Resolves the target test server on the first request, not when the handler is built.</summary>
    private sealed class DeferredTestServerHandler(Func<TestServer> server) : HttpMessageHandler
    {
        private readonly Lazy<HttpMessageInvoker> _inner = new(() => new HttpMessageInvoker(server().CreateHandler()));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            _inner.Value.SendAsync(request, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing && _inner.IsValueCreated)
                _inner.Value.Dispose();
            base.Dispose(disposing);
        }
    }

    private sealed class TestServerConnector(TestServer jobService) : IWebSocketConnector
    {
        public Task<WebSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken) =>
            jobService.CreateWebSocketClient().ConnectAsync(uri, cancellationToken);
    }
}
