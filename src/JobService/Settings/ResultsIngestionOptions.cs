namespace JobService.Settings;

public sealed class ResultsIngestionOptions
{
    public const string SectionName = "ResultsIngestion";

    public int MaxAttempts { get; set; } = 5;
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}
