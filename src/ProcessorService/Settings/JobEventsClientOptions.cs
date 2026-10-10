namespace ProcessorService.Settings;

public sealed class JobEventsClientOptions
{
    public const string SectionName = "JobService";

    public string EventsUrl { get; set; } = "ws://localhost:5100/ws/processor-events";
    public int OutboundBufferCapacity { get; set; } = 10_000;
    public TimeSpan ReconnectMinDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan ReconnectMaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(15);
}
