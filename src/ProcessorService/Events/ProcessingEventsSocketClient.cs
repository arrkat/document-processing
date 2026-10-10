using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Contracts;
using Contracts.Events;
using Contracts.Processing;
using Microsoft.Extensions.Options;
using ProcessorService.Jobs;
using ProcessorService.Settings;

namespace ProcessorService.Events;

/// <summary>
/// Keeps one WebSocket connection to the Job Service (<see cref="ProcessingEventsClientOptions.EventsUrl"/>) and is the only reader of the
/// outbound event channel (a WebSocket allows one send at a time).
/// <list type="bullet">
/// <item>Connected: send events as they arrive. A listener on the socket notices at once when the server closes it.</item>
/// <item>Disconnected: keep reading the channel and discard events, so it never fills; reconnect with exponential backoff + jitter.</item>
/// <item>On every (re)connect: first send a snapshot, one event per job in <see cref="IJobRegistry.GetAll"/>. The registry is updated
/// before events are published, so the snapshot supersedes anything discarded or lost mid-send.</item>
/// </list>
/// Delivery is at-least-once with possible duplicates; the Job Service is idempotent.
/// </summary>
public sealed class ProcessingEventsSocketClient(
    ChannelReader<ProcessingEvent> events,
    IJobRegistry registry,
    IWebSocketConnector connector,
    TimeProvider timeProvider,
    IOptions<ProcessingEventsClientOptions> options,
    ILogger<ProcessingEventsSocketClient> logger) : BackgroundService
{
    private readonly ProcessingEventsClientOptions _options = options.Value;
    private volatile bool _isConnected;

    /// <summary>Connected right now; exposed for health checks and tests.</summary>
    public bool IsConnected => _isConnected;

    /// <summary>
    /// One event per known job, stating its current status: Processing → ProcessingProgressUpdated (with its count; it also implies
    /// "started"), Finished → ProcessingFinished, Failed → ProcessingFailed, Cancelled → ProcessingCancelled.
    /// </summary>
    public static IReadOnlyList<ProcessingEvent> BuildSnapshot(IEnumerable<ProcessingJobStatus> jobs, Func<string, long> nextSequence, DateTimeOffset now) =>
        jobs.Select(job => (ProcessingEvent)(job.Status switch
        {
            ProcessingStatus.Processing => new ProcessingProgressUpdatedEvent(Guid.NewGuid(), job.JobId, nextSequence(job.JobId), now,
                job.ProcessedDocuments, job.TotalDocuments),
            ProcessingStatus.Finished => new ProcessingFinishedEvent(Guid.NewGuid(), job.JobId, nextSequence(job.JobId), now, job.ResultsRef!),
            ProcessingStatus.Failed => new ProcessingFailedEvent(Guid.NewGuid(), job.JobId, nextSequence(job.JobId), now,
                job.ErrorCode ?? ErrorCode.Unexpected, job.ErrorMessage),
            ProcessingStatus.Cancelled => new ProcessingCancelledEvent(Guid.NewGuid(), job.JobId, nextSequence(job.JobId), now, job.ProcessedDocuments),
            _ => throw new ArgumentOutOfRangeException(nameof(jobs), job.Status, "Unknown processing status.")
        })).ToList();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var uri = new Uri(_options.EventsUrl);
        while (!stoppingToken.IsCancellationRequested)
        {
            WebSocket socket;
            using (var discarding = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken))
            {
                var discardTask = DiscardEventsAsync(discarding.Token);
                try
                {
                    socket = await ConnectWithBackoffAsync(uri, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return; // shutting down
                }
                finally
                {
                    await discarding.CancelAsync();
                    await discardTask;
                }
            }

            try
            {
                _isConnected = true;
                logger.LogInformation("Connected to the Job Service at {Uri}", uri);
                await RunSessionAsync(socket, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Connection to the Job Service lost: {Reason}", ex.Message);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
            finally
            {
                _isConnected = false;
                socket.Dispose();
            }
        }
    }

    private async Task<WebSocket> ConnectWithBackoffAsync(Uri uri, CancellationToken stoppingToken)
    {
        var delay = _options.ReconnectMinDelay;
        while (true)
        {
            try
            {
                return await connector.ConnectAsync(uri, stoppingToken);
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down while a connect attempt failed: not a connection problem, just stop.
                throw new OperationCanceledException(stoppingToken);
            }
            catch (Exception ex)
            {
                var jittered = delay * (0.8 + 0.4 * Random.Shared.NextDouble()); // ±20%, so many clients don't retry in lockstep
                logger.LogWarning("Cannot reach the Job Service ({Reason}); retrying in {Delay}", ex.Message, jittered);
                await Task.Delay(jittered, timeProvider, stoppingToken);
                delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, _options.ReconnectMaxDelay.Ticks));
            }
        }
    }

    private async Task RunSessionAsync(WebSocket socket, CancellationToken stoppingToken)
    {
        using var session = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var listening = ListenForCloseAsync(socket, session);

        try
        {
            foreach (var snapshotEvent in BuildSnapshot(registry.GetAll(), registry.NextSequence, timeProvider.GetUtcNow()))
                await SendAsync(socket, snapshotEvent, session.Token);

            await foreach (var processingEvent in events.ReadAllAsync(session.Token))
                await SendAsync(socket, processingEvent, session.Token);
        }
        finally
        {
            await session.CancelAsync();
            await listening;
        }
    }

    /// <summary>The Job Service never sends data, so any receive result means it closed (or the connection broke): end the session.</summary>
    private static async Task ListenForCloseAsync(WebSocket socket, CancellationTokenSource session)
    {
        try
        {
            var buffer = new byte[256];
            while (!session.IsCancellationRequested)
            {
                var frame = await socket.ReceiveAsync(buffer, session.Token);
                if (frame.MessageType == WebSocketMessageType.Close)
                    break;
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            // Connection broke, or the session is ending anyway.
        }

        await session.CancelAsync();
    }

    private async Task DiscardEventsAsync(CancellationToken cancellationToken)
    {
        var discarded = 0;
        try
        {
            while (await events.WaitToReadAsync(cancellationToken))
                while (events.TryRead(out _))
                    discarded++;
        }
        catch (OperationCanceledException)
        {
            // Connected again (or shutting down).
        }

        if (discarded > 0)
            logger.LogInformation("Discarded {Count} events while disconnected; the snapshot will restate every job", discarded);
    }

    private static Task SendAsync(WebSocket socket, ProcessingEvent processingEvent, CancellationToken cancellationToken)
    {
        // Serialize as the base type so the "type" discriminator is written.
        var payload = JsonSerializer.SerializeToUtf8Bytes<ProcessingEvent>(processingEvent, ContractJson.Options);
        return socket.SendAsync(payload, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
    }
}
