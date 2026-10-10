namespace JobService.Settings;

public sealed class ProcessorClientOptions
{
    public const string SectionName = "ProcessorService";

    public string BaseUrl { get; set; } = "http://localhost:5200";
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);
}
