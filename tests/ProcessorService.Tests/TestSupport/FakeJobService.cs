using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using ProcessorService.Events;

namespace ProcessorService.Tests.TestSupport;

/// <summary>
/// An in-process WebSocket server standing in for the Job Service: records every message with its connection number,
/// and can drop a connection after a given number of messages.
/// </summary>
public sealed class FakeJobService : IAsyncDisposable
{
    private readonly WebApplication _app;
    private int _connections;

    private FakeJobService(WebApplication app) => _app = app;

    public Channel<(int Connection, string Message)> Received { get; } = Channel.CreateUnbounded<(int, string)>();

    /// <summary>(connection number, messages received on it) → drop the connection now?</summary>
    public Func<int, int, bool> DropWhen { get; set; } = (_, _) => false;

    public int Connections => Volatile.Read(ref _connections);

    public IWebSocketConnector Connector => new TestServerConnector(_app.GetTestServer());

    public static async Task<FakeJobService> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        var fake = new FakeJobService(app);

        app.UseWebSockets();
        app.Map("/ws", fake.AcceptAsync);
        await app.StartAsync();
        return fake;
    }

    private async Task AcceptAsync(HttpContext context)
    {
        var connection = Interlocked.Increment(ref _connections);
        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var buffer = new byte[64 * 1024];
        var count = 0;

        while (socket.State == WebSocketState.Open)
        {
            var frame = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (frame.MessageType == WebSocketMessageType.Close)
                break;

            await Received.Writer.WriteAsync((connection, Encoding.UTF8.GetString(buffer, 0, frame.Count)));
            if (DropWhen(connection, ++count))
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.EndpointUnavailable, "dropped by test", CancellationToken.None);
                break;
            }
        }
    }

    public async ValueTask DisposeAsync() => await _app.DisposeAsync();

    private sealed class TestServerConnector(TestServer server) : IWebSocketConnector
    {
        public Task<WebSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken) =>
            server.CreateWebSocketClient().ConnectAsync(uri, cancellationToken);
    }
}
