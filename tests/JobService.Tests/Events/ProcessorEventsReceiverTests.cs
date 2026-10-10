using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Contracts;
using Contracts.Events;
using JobService.Events;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace JobService.Tests.Events;

/// <summary>Transport only: a real WebSocket through the test server, with the event logic faked.</summary>
public sealed class ProcessorEventsReceiverTests : IDisposable
{
    private readonly RecordingHandler _handler = new();
    private readonly WebApplicationFactory<JobService.Program> _factory;

    public ProcessorEventsReceiverTests() =>
        _factory = new WebApplicationFactory<JobService.Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("BlobStorage:RootPath", Path.Combine(Path.GetTempPath(), "ta9-ws-" + Guid.NewGuid().ToString("N")));
            builder.ConfigureTestServices(services => services.AddSingleton<IProcessingEventHandler>(_handler));
        });

    private static byte[] Json(ProcessingEvent processingEvent) =>
        JsonSerializer.SerializeToUtf8Bytes(processingEvent, ContractJson.Options);

    private static ProcessingEvent Progress(long seq) =>
        new ProcessingProgressUpdatedEvent(Guid.NewGuid(), "job-1", seq, DateTimeOffset.UtcNow, (int)seq, 10);

    private Task<WebSocket> ConnectAsync() =>
        _factory.Server.CreateWebSocketClient().ConnectAsync(new Uri("ws://localhost/ws/processor-events"), CancellationToken.None);

    [Fact]
    public async Task Valid_message_reaches_the_handler_as_a_typed_event()
    {
        using var socket = await ConnectAsync();

        await socket.SendAsync(Json(Progress(1)), WebSocketMessageType.Text, true, CancellationToken.None);

        var received = Assert.IsType<ProcessingProgressUpdatedEvent>(await _handler.NextAsync());
        Assert.Equal(1, received.ProcessedDocuments);
    }

    [Fact]
    public async Task Message_split_across_frames_is_reassembled()
    {
        using var socket = await ConnectAsync();
        var payload = Json(Progress(1));

        await socket.SendAsync(payload.AsMemory(0, 10), WebSocketMessageType.Text, endOfMessage: false, CancellationToken.None);
        await socket.SendAsync(payload.AsMemory(10), WebSocketMessageType.Text, endOfMessage: true, CancellationToken.None);

        Assert.IsType<ProcessingProgressUpdatedEvent>(await _handler.NextAsync());
    }

    [Fact]
    public async Task Malformed_message_is_skipped_and_the_connection_stays_open()
    {
        using var socket = await ConnectAsync();

        await socket.SendAsync(Encoding.UTF8.GetBytes("{ not json"), WebSocketMessageType.Text, true, CancellationToken.None);
        await socket.SendAsync(Encoding.UTF8.GetBytes("""{"jobId":"job-1"}"""), WebSocketMessageType.Text, true, CancellationToken.None);
        await socket.SendAsync(Json(Progress(3)), WebSocketMessageType.Text, true, CancellationToken.None);

        var received = Assert.IsType<ProcessingProgressUpdatedEvent>(await _handler.NextAsync());
        Assert.Equal(3, received.Sequence);
        Assert.Equal(WebSocketState.Open, socket.State);
    }

    [Fact]
    public async Task Oversized_message_closes_the_connection_as_too_big()
    {
        using var socket = await ConnectAsync();
        var oversized = Encoding.UTF8.GetBytes(new string('x', ProcessorEventsReceiver.MaxMessageBytes + 1024));

        await socket.SendAsync(oversized, WebSocketMessageType.Text, true, CancellationToken.None);
        var frame = await socket.ReceiveAsync(new byte[256], new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token);

        Assert.Equal(WebSocketMessageType.Close, frame.MessageType);
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, frame.CloseStatus);
    }

    [Fact]
    public async Task Plain_http_request_is_rejected()
    {
        var response = await _factory.CreateClient().GetAsync("/ws/processor-events");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public void Dispose() => _factory.Dispose();

    private sealed class RecordingHandler : IProcessingEventHandler
    {
        private readonly BlockingCollection<ProcessingEvent> _received = new();

        public Task<EventHandlingResult> HandleAsync(ProcessingEvent processingEvent, CancellationToken cancellationToken = default)
        {
            _received.Add(processingEvent, cancellationToken);
            return Task.FromResult(EventHandlingResult.Applied);
        }

        public async Task<ProcessingEvent> NextAsync()
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            ProcessingEvent? next;
            while (!_received.TryTake(out next))
            {
                if (DateTime.UtcNow > deadline)
                    Assert.Fail("No event reached the handler.");
                await Task.Delay(10);
            }
            return next;
        }
    }
}
