namespace ProcessorService.Settings;

public sealed class ProcessingEventsClientOptions
{
    public const string SectionName = "JobService";

    public string EventsUrl { get; set; } = "ws://localhost:5100/ws/processor-events";

    /// <summary>Capacity of the outbound event channel. It drains even while disconnected (events are discarded), so it only absorbs bursts.</summary>
    public int EventChannelCapacity { get; set; } = 10_000;

    public TimeSpan ReconnectMinDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan ReconnectMaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(15);
}
