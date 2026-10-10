using Contracts.Jobs;
using JobService.Domain;
using JobService.Persistence;
using static JobService.Tests.TestSupport.Jobs;

namespace JobService.Tests.Persistence;

public class InMemoryJobRepositoryTests
{
    private readonly InMemoryJobRepository _repository = new();

    [Fact]
    public async Task Duplicate_id_is_not_added()
    {
        Assert.True(await _repository.TryAddAsync(Metadata("a"), PendingJob()));
        Assert.False(await _repository.TryAddAsync(Metadata("a"), PendingJob()));
    }

    [Fact]
    public async Task Update_reports_updated_declined_and_not_found()
    {
        await _repository.TryAddAsync(Metadata("a"), PendingJob());

        var updated = await _repository.TryUpdateStateAsync("a", s => ((PendingState)s).Start(T0, 0));
        var declined = await _repository.TryUpdateStateAsync("a", _ => null);
        var notFound = await _repository.TryUpdateStateAsync("missing", s => s);

        Assert.Equal(StateUpdateStatus.Updated, updated.Status);
        Assert.Equal(StateUpdateStatus.Declined, declined.Status);
        Assert.IsType<ProcessingState>(declined.State);
        Assert.Equal(StateUpdateStatus.NotFound, notFound.Status);
    }

    [Fact]
    public async Task Concurrent_updates_are_never_lost()
    {
        await _repository.TryAddAsync(Metadata("a"), ProcessingJob());

        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => Task.Run(() => _repository.TryUpdateStateAsync("a",
            s => s is ProcessingState p ? p with { ProcessedDocuments = p.ProcessedDocuments + 1 } : null))));

        var job = await _repository.GetAsync("a");
        Assert.Equal(50, ((ProcessingState)job!.State).ProcessedDocuments);
    }

    [Fact]
    public async Task List_filters_orders_newest_first_and_pages()
    {
        for (var i = 0; i < 5; i++)
            await _repository.TryAddAsync(Metadata($"job-{i}", createdAt: T0.AddMinutes(i)), i % 2 == 0 ? PendingJob() : CompletedJob());

        var pending = await _repository.ListAsync(JobStatus.Pending, page: 1, pageSize: 2);
        Assert.Equal(["job-4", "job-2"], pending.Items.Select(j => j.Metadata.Id));
        Assert.Equal(3, pending.TotalCount);

        var secondPage = await _repository.ListAsync(JobStatus.Pending, page: 2, pageSize: 2);
        Assert.Equal(["job-0"], secondPage.Items.Select(j => j.Metadata.Id));

        Assert.Equal(5, (await _repository.ListAsync(null, 1, 50)).TotalCount);
    }
}
