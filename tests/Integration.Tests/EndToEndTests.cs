using System.Net;
using System.Net.Http.Json;
using Contracts;
using Contracts.Documents;
using Contracts.Jobs;
using Integration.Tests.TestSupport;
using ProcessorService.Analysis;

namespace Integration.Tests;

/// <summary>The whole flow across both services: REST dispatch, blob claim-check, WebSocket events, results read from the blob.</summary>
public class EndToEndTests
{
    [Fact]
    public async Task A_job_flows_through_both_services_and_completes_with_results()
    {
        await using var host = await TwoServiceHost.StartAsync();
        var documents = new List<DocumentInput>
        {
            new("english", "The quick brown fox jumps over the lazy dog. The dog sleeps."),
            new("hebrew", "שָׁלוֹם עולם, שלום לכולם"),
            new("empty", "")
        };

        var created = await host.CreateJobAsync(new CreateJobRequest(documents));
        var completed = await host.WaitForStatusAsync(created.Id, JobStatus.Completed);

        Assert.Equal(3, completed.Job.TotalDocuments);
        Assert.Equal(3, completed.Job.ProcessedDocuments);
        Assert.NotNull(completed.Job.StartedAt);
        Assert.NotNull(completed.Job.CompletedAt);
        Assert.Null(completed.Job.ErrorCode);

        var analyzer = new DocumentAnalyzer();
        var expected = documents.Select(analyzer.Analyze).ToList();
        Assert.Equivalent(expected, completed.Results, strict: true);

        var results = await host.Client.GetFromJsonAsync<List<DocumentResult>>($"/api/jobs/{created.Id}/results", ContractJson.Options);
        Assert.Equivalent(expected, results, strict: true);
    }

    [Fact]
    public async Task Jobs_beyond_the_Processor_slots_wait_as_Pending_and_all_complete()
    {
        // 2 job slots, ~300 ms per job (3 documents × 100 ms, one at a time): 6 jobs need three rounds.
        await using var host = await TwoServiceHost.StartAsync(
            maxConcurrentJobs: 2, perJobParallelism: 1, analyzer: new SlowAnalyzer(TimeSpan.FromMilliseconds(100)));

        var jobs = new List<JobSummary>();
        for (var i = 0; i < 6; i++)
            jobs.Add(await host.CreateJobAsync(new CreateJobRequest(
                [new DocumentInput("a", $"job {i} first"), new DocumentInput("b", "second"), new DocumentInput("c", "third")])));

        var pending = await host.Client.GetFromJsonAsync<PagedResult<JobSummary>>("/api/jobs?status=Pending", ContractJson.Options);
        Assert.NotEmpty(pending!.Items);

        foreach (var job in jobs)
        {
            var completed = await host.WaitForStatusAsync(job.Id, JobStatus.Completed);
            Assert.Equal(3, completed.Results!.Count);
        }
    }

    [Fact]
    public async Task Cancelling_a_processing_job_waits_for_the_Processor_then_ends_Cancelled()
    {
        using var analyzer = new GatedAnalyzer();
        await using var host = await TwoServiceHost.StartAsync(perJobParallelism: 1, analyzer: analyzer);

        var created = await host.CreateJobAsync(new CreateJobRequest(
            [new DocumentInput("a", "one"), new DocumentInput("b", "two"), new DocumentInput("c", "three")]));
        await analyzer.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        await host.WaitForStatusAsync(created.Id, JobStatus.Processing);

        var cancel = await host.Client.PostAsync($"/api/jobs/{created.Id}/cancel", content: null);

        // The first document is still being analysed, so the Processor has not confirmed: the job must not show Cancelled yet.
        Assert.Equal(HttpStatusCode.Accepted, cancel.StatusCode);
        Assert.Equal(JobStatus.Processing, (await host.GetJobAsync(created.Id)).Job.Status);

        analyzer.Release();
        var cancelled = await host.WaitForStatusAsync(created.Id, JobStatus.Cancelled);
        Assert.Null(cancelled.Results);
        Assert.True(cancelled.Job.ProcessedDocuments < 3);
    }
}
