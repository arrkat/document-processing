namespace JobService.Settings;

public sealed class DispatchOptions
{
    public const string SectionName = "Dispatch";

    public int QueueCapacity { get; set; } = 1000;
    public int MaxAttempts { get; set; } = 5;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
}
