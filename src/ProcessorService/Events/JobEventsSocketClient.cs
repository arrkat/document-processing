using Microsoft.Extensions.Options;
using ProcessorService.Jobs;
using ProcessorService.Settings;

namespace ProcessorService.Events;

/// <summary>
/// Keeps one WebSocket connection to the Job Service (<see cref="JobEventsClientOptions.EventsUrl"/>):
/// connect → send a snapshot of every known job (<see cref="IJobEventFactory.Snapshot"/>) → single sender loop draining
/// <see cref="IOutboundEventSource"/> (ClientWebSocket allows one SendAsync at a time).
/// On failure: keep the in-flight event, reconnect with exponential backoff + jitter, resend it. Receivers are idempotent,
/// so at-least-once delivery with duplicates is acceptable.
/// </summary>
public sealed class JobEventsSocketClient(
    IOutboundEventSource eventSource,
    IJobRegistry registry,
    IJobEventFactory eventFactory,
    TimeProvider timeProvider,
    IOptions<JobEventsClientOptions> options,
    ILogger<JobEventsSocketClient> logger) : BackgroundService
{
    /// <summary>Connected (to <see cref="JobEventsClientOptions.EventsUrl"/>) right now; exposed for health checks and tests.</summary>
    public bool IsConnected => false;

    // TODO: connect/send/reconnect loop.
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.CompletedTask;
}
