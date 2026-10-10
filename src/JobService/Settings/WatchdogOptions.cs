namespace JobService.Settings;

public sealed class WatchdogOptions
{
    public const string SectionName = "Watchdog";

    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromSeconds(60);
}
