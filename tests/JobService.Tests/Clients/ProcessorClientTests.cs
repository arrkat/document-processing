using System.Net;
using System.Text;
using Contracts.Processing;
using JobService.Processing;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobService.Tests.Clients;

public class ProcessorClientTests
{
    private static readonly ProcessJobRequest Request = new("job-1", "jobs/job-1/documents.json", 3);

    [Theory]
    [InlineData(HttpStatusCode.Accepted, DispatchResult.Accepted)]
    [InlineData(HttpStatusCode.OK, DispatchResult.Accepted)]
    [InlineData(HttpStatusCode.ServiceUnavailable, DispatchResult.Busy)]
    [InlineData(HttpStatusCode.BadRequest, DispatchResult.Rejected)]
    [InlineData(HttpStatusCode.InternalServerError, DispatchResult.Unavailable)]
    public async Task Submit_maps_status_codes(HttpStatusCode status, DispatchResult expected)
    {
        Assert.Equal(expected, await Client(_ => new HttpResponseMessage(status)).SubmitAsync(Request));
    }

    [Fact]
    public async Task Submit_maps_connection_errors_and_timeouts_to_Unavailable()
    {
        Assert.Equal(DispatchResult.Unavailable, await Client(_ => throw new HttpRequestException("refused")).SubmitAsync(Request));
        Assert.Equal(DispatchResult.Unavailable, await Client(_ => throw new TaskCanceledException("timeout")).SubmitAsync(Request));
    }

    [Fact]
    public async Task Status_is_deserialized_404_is_null_and_unreachable_throws()
    {
        const string json = """{"jobId":"job-1","status":"Finished","processedDocuments":3,"totalDocuments":3,"resultsRef":"jobs/job-1/results.json"}""";
        var status = await Client(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") })
            .GetStatusAsync("job-1");

        Assert.Equal(ProcessingStatus.Finished, status!.Status);
        Assert.Null(await Client(_ => new HttpResponseMessage(HttpStatusCode.NotFound)).GetStatusAsync("job-1"));
        await Assert.ThrowsAsync<HttpRequestException>(() => Client(_ => throw new HttpRequestException("refused")).GetStatusAsync("job-1"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted, ProcessorCancelResult.CancellationRequested)]
    [InlineData(HttpStatusCode.Conflict, ProcessorCancelResult.AlreadyFinished)]
    [InlineData(HttpStatusCode.NotFound, ProcessorCancelResult.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError, ProcessorCancelResult.Unavailable)]
    public async Task Cancel_maps_status_codes(HttpStatusCode status, ProcessorCancelResult expected)
    {
        Assert.Equal(expected, await Client(_ => new HttpResponseMessage(status)).CancelAsync("job-1"));
    }

    [Fact]
    public async Task Cancel_maps_connection_errors_to_Unavailable()
    {
        Assert.Equal(ProcessorCancelResult.Unavailable, await Client(_ => throw new HttpRequestException("refused")).CancelAsync("job-1"));
    }

    private static ProcessorClient Client(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        new(new HttpClient(new StubHandler(respond)) { BaseAddress = new Uri("http://processor/") }, NullLogger<ProcessorClient>.Instance);

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
