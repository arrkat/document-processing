using System.Net.WebSockets;

namespace ProcessorService.Events;

/// <summary>Opens a WebSocket to a URI. A seam so tests can connect to an in-process server instead of a real port.</summary>
public interface IWebSocketConnector
{
    Task<WebSocket> ConnectAsync(Uri uri, CancellationToken cancellationToken);
}
