using System.Threading.Channels;
using Contracts.Events;
using Microsoft.Extensions.Options;
using ProcessorService.Jobs;
using ProcessorService.Settings;

namespace ProcessorService.Events;

/// <summary>
/// Keeps one WebSocket connection to the Job Service (<see cref="ProcessingEventsClientOptions.EventsUrl"/>) and is the only reader of the
/// outbound event channel (ClientWebSocket allows one SendAsync at a time).
/// <list type="bullet">
/// <item>Connected: send events as they arrive.</item>
/// <item>Disconnected: keep reading the channel and discard events, so it never fills; reconnect with exponential backoff + jitter.</item>
/// <item>On every (re)connect: send a snapshot event per job in <see cref="IJobRegistry.GetAll"/>, built from its current status with
/// the job's next sequence. The registry is updated before events are published, so the snapshot supersedes anything discarded.</item>
/// </list>
/// Delivery is at-least-once with possible duplicates; the Job Service is idempotent.
/// </summary>
public sealed class ProcessingEventsSocketClient(
    ChannelReader<ProcessingEvent> events,
    IJobRegistry registry,
    TimeProvider timeProvider,
    IOptions<ProcessingEventsClientOptions> options,
    ILogger<ProcessingEventsSocketClient> logger) : BackgroundService
{
    /// <summary>Connected right now; exposed for health checks and tests.</summary>
    public bool IsConnected => false;

    // TODO stage 5: connect / snapshot / send loop with reconnect.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
