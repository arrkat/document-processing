using System.Net.WebSockets;
using System.Threading.Channels;
using Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ProcessorService.Events;
using ProcessorService.Jobs;
using ProcessorService.Settings;
using ProcessorService.Tests.TestSupport;

namespace ProcessorService.Tests.Events;

public sealed class ProcessingEventsSocketClientTests : IAsyncDisposable
{
    private readonly Channel<ProcessingEvent> _events = Channel.CreateUnbounded<ProcessingEvent>();
    private readonly JobRegistry _registry = new();
    private ProcessingEventsSocketClient? _client;

    public ProcessingEventsSocketClientTests()
    {
        // One finished job, so every snapshot contains exactly one ProcessingFinished for "done".
        _registry.TryRegister(new ProcessingJob("done", "jobs/done/documents.json", 1));
        _registry.TryMarkFinished("done", "jobs/done/results.json");
    }

    private async Task StartClientAsync(IWebSocketConnector connector)
    {
        _client = new ProcessingEventsSocketClient(_events.Reader, _registry, connector, TimeProvider.System,
            Options.Create(new ProcessingEventsClientOptions
            {
                EventsUrl = "ws://localhost/ws",
                ReconnectMinDelay = TimeSpan.FromMilliseconds(10),
                ReconnectMaxDelay = TimeSpan.FromMilliseconds(50)
            }),
            NullLogger<ProcessingEventsSocketClient>.Instance);
        await _client.StartAsync(CancellationToken.None);
    }

    private static ProcessingEvent Progress(string jobId, int processed) =>
        new ProcessingProgressUpdatedEvent(Guid.NewGuid(), jobId, processed, DateTimeOffset.UtcNow, processed, 10);

    [Fact]
    public async Task Sends_the_snapshot_first_then_events_in_order()
    {
        await using var server = await FakeJobService.StartAsync();
        await StartClientAsync(server.Connector);

        var snapshot = await NextMessageAsync(server);
        await _events.Writer.WriteAsync(Progress("job-1", 1));
        await _events.Writer.WriteAsync(Progress("job-1", 2));

        Assert.Contains("\"type\":\"ProcessingFinished\"", snapshot.Message);
        Assert.Contains("\"processedDocuments\":1", (await NextMessageAsync(server)).Message);
        Assert.Contains("\"processedDocuments\":2", (await NextMessageAsync(server)).Message);
    }

    [Fact]
    public async Task Events_are_discarded_while_the_Job_Service_is_unreachable()
    {
        var connector = new FailingConnector();
        await StartClientAsync(connector);

        for (var i = 0; i < 100; i++)
            await _events.Writer.WriteAsync(Progress("job-1", i));

        await WaitUntilAsync(() => _events.Reader.Count == 0 && connector.Attempts >= 2, "events drained and retries made");
        Assert.False(_client!.IsConnected);
    }

    [Fact]
    public async Task Reconnects_after_a_drop_and_resends_the_snapshot()
    {
        await using var server = await FakeJobService.StartAsync();
        server.DropWhen = (connection, received) => connection == 1 && received == 1;
        await StartClientAsync(server.Connector);

        var first = await NextMessageAsync(server);
        var second = await NextMessageAsync(server);

        Assert.Equal((1, 2), (first.Connection, second.Connection));
        Assert.Contains("\"type\":\"ProcessingFinished\"", second.Message);
    }

    private static async Task<(int Connection, string Message)> NextMessageAsync(FakeJobService server)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await server.Received.Reader.ReadAsync(timeout.Token);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string because)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                Assert.Fail($"Timed out waiting until {because}.");
            await Task.Delay(10);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.StopAsync(CancellationToken.None);
            _client.Dispose();
        }
    }

    private sealed class FailingConnector : IWebSocketConnector
    {
        private int _attempts;
        public int Attempts => Volatile.Read(ref _attempts);

        public Task<WebSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _attempts);
            throw new WebSocketException("connection refused");
        }
    }
}
