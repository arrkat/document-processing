using System.Net.WebSockets;

namespace JobService.Events;

/// <summary>
/// Owns one accepted Processor WebSocket connection until it closes: receives (possibly fragmented) text messages,
/// deserializes <see cref="Contracts.Events.ProcessingEvent"/> with <see cref="Contracts.ContractJson.Options"/> and passes them to
/// <see cref="IProcessingEventHandler"/>. Malformed messages are logged and dropped without closing the socket.
/// On a new connection it should trigger a reconciliation pass (<see cref="Monitoring.JobWatchdog.ScanAsync"/>).
/// </summary>
public sealed class ProcessorEventsSocketHandler(IProcessingEventHandler eventHandler, ILogger<ProcessorEventsSocketHandler> logger)
{
    public Task HandleAsync(WebSocket socket, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
