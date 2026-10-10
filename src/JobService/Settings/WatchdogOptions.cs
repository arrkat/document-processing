namespace JobService.Settings;

public sealed class WatchdogOptions
{
    public const string SectionName = "Watchdog";

    /// <summary>How often the watchdog scans for silent Processing jobs.</summary>
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>A Processing job with no activity for this long is checked. Healthy jobs report progress at least every 500 ms.</summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromSeconds(60);
}
