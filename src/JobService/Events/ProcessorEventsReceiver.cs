using System.Net.WebSockets;
using System.Text.Json;
using Contracts;
using Contracts.Events;

namespace JobService.Events;

/// <summary>
/// Transport only: owns one accepted Processor WebSocket connection until it closes. It reassembles (possibly fragmented)
/// text messages, deserializes each into a <see cref="ProcessingEvent"/> and hands it to <see cref="IProcessingEventHandler"/>,
/// which owns what the event means. A malformed message is logged and skipped without closing the socket; a message over
/// <see cref="MaxMessageBytes"/> closes it (events are well under 1 KB, so that is a bug or abuse).
/// </summary>
public sealed class ProcessorEventsReceiver(IProcessingEventHandler eventHandler, ILogger<ProcessorEventsReceiver> logger)
{
    public const int MaxMessageBytes = 64 * 1024;

    public async Task HandleAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processor events connection opened");
        var buffer = new byte[4096];
        using var message = new MemoryStream();

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var frame = await socket.ReceiveAsync(buffer, cancellationToken);
                if (frame.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, cancellationToken);
                    break;
                }

                message.Write(buffer, 0, frame.Count);
                if (message.Length > MaxMessageBytes)
                {
                    logger.LogWarning("Processor sent a message over {Limit} bytes; closing the connection", MaxMessageBytes);
                    await socket.CloseOutputAsync(WebSocketCloseStatus.MessageTooBig, $"Messages are limited to {MaxMessageBytes} bytes.", cancellationToken);
                    break;
                }

                if (!frame.EndOfMessage)
                    continue;

                if (frame.MessageType == WebSocketMessageType.Text)
                    await DeliverAsync(message.ToArray(), cancellationToken);
                else
                    logger.LogWarning("Ignored a binary message from the Processor");

                message.SetLength(0);
            }
        }
        catch (WebSocketException ex)
        {
            logger.LogWarning("Processor events connection dropped: {Reason}", ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The request was aborted (shutdown or client gone).
        }

        logger.LogInformation("Processor events connection closed");
    }

    private async Task DeliverAsync(byte[] payload, CancellationToken cancellationToken)
    {
        ProcessingEvent? processingEvent;
        try
        {
            processingEvent = JsonSerializer.Deserialize<ProcessingEvent>(payload, ContractJson.Options);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            // NotSupportedException: no (or an unknown) "type" discriminator, so the abstract event type can't be created.
            logger.LogWarning("Skipped a malformed message from the Processor: {Reason}", ex.Message);
            return;
        }

        if (processingEvent is null)
        {
            logger.LogWarning("Skipped an empty message from the Processor");
            return;
        }

        try
        {
            await eventHandler.HandleAsync(processingEvent, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One bad event must not take down the connection for every other job.
            logger.LogError(ex, "Handling {EventType} for job {JobId} failed", processingEvent.GetType().Name, processingEvent.JobId);
        }
    }
}
