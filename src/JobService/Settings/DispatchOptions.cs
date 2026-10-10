namespace JobService.Settings;

public sealed class DispatchOptions
{
    public const string SectionName = "Dispatch";

    /// <summary>Attempts while the Processor is unreachable before Failed(ProcessorUnavailable). Busy (503) responses do not count.</summary>
    public int MaxAttempts { get; set; } = 6;

    /// <summary>Backoff for unreachable: InitialDelay, doubled per attempt, capped at MaxDelay (1, 2, 4, 8, 16, 30s ≈ 1 minute).</summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Retry delay while the Processor is busy; cut short when a job-finished event frees a slot.</summary>
    public TimeSpan BusyRetryDelay { get; set; } = TimeSpan.FromSeconds(5);
}
