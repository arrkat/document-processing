using System.Net.WebSockets;
using Microsoft.Extensions.Options;
using ProcessorService.Settings;

namespace ProcessorService.Events;

public sealed class ClientWebSocketConnector(IOptions<ProcessingEventsClientOptions> options) : IWebSocketConnector
{
    public async Task<WebSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        socket.Options.KeepAliveInterval = options.Value.KeepAliveInterval;
        try
        {
            await socket.ConnectAsync(uri, cancellationToken);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
