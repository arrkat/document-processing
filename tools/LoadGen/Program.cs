// Submits synthetic jobs to a running Job Service and reports throughput and job durations.
// Usage: dotnet run --project tools/LoadGen -- --jobs 10 --docs 1000 --words 200..3000 --seed 42 --url http://localhost:5100
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Contracts;
using Contracts.Jobs;
using LoadGen;

var options = LoadGenOptions.Parse(args);
if (options is null)
{
    Console.Error.WriteLine("Usage: LoadGen [--jobs 10] [--docs 1000] [--words 200..3000] [--seed 42] [--url http://localhost:5100] [--timeout 600]");
    return 2;
}

Console.WriteLine($"Generating {options.Jobs} jobs × {options.Docs} documents ({options.MinWords}..{options.MaxWords} words each, seed {options.Seed})...");
var generator = new DocumentGenerator(options.Seed);
var requests = Enumerable.Range(0, options.Jobs)
    .AsParallel().AsOrdered()
    .Select(j => new CreateJobRequest(generator.CreateJob(options.Seed, j, options.Docs, options.MinWords, options.MaxWords)))
    .ToList();
var totalCharacters = requests.Sum(r => r.Documents!.Sum(d => (long)d.Content.Length));
Console.WriteLine($"Generated {totalCharacters:N0} characters ({totalCharacters / options.Jobs:N0} per job).");

using var http = new HttpClient { BaseAddress = new Uri(options.Url), Timeout = TimeSpan.FromMinutes(5) };
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));

Console.WriteLine($"Submitting {options.Jobs} jobs concurrently to {options.Url}...");
var wall = Stopwatch.StartNew();
var created = await Task.WhenAll(requests.Select(request => SubmitAsync(http, request, timeout.Token)));
var submitTime = wall.Elapsed;
requests.Clear(); // the documents are in the blob store now; let them be collected

var jobIds = created.OfType<JobSummary>().Select(j => j.Id).ToList();
Console.WriteLine($"Submitted {jobIds.Count}/{options.Jobs} in {submitTime.TotalSeconds:F2}s; waiting for them to finish...");

var finals = await Task.WhenAll(jobIds.Select(id => WaitForFinalStateAsync(http, id, timeout.Token)));
wall.Stop();

Report(options, totalCharacters, submitTime, wall.Elapsed, finals, rejected: options.Jobs - jobIds.Count);
return finals.All(j => j?.Status == JobStatus.Completed) && jobIds.Count == options.Jobs ? 0 : 1;

static async Task<JobSummary?> SubmitAsync(HttpClient http, CreateJobRequest request, CancellationToken ct)
{
    try
    {
        var response = await http.PostAsJsonAsync("/api/jobs", request, ContractJson.Options, ct);
        if (response.StatusCode == HttpStatusCode.Accepted)
            return await response.Content.ReadFromJsonAsync<JobSummary>(ContractJson.Options, ct);

        Console.Error.WriteLine($"Submit failed: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync(ct)}");
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        Console.Error.WriteLine($"Submit failed: {ex.Message}");
    }
    return null;
}

/// <summary>Polls the job until it is Completed, Failed or Cancelled. Returns null when the run times out first.</summary>
static async Task<JobSummary?> WaitForFinalStateAsync(HttpClient http, string jobId, CancellationToken ct)
{
    try
    {
        while (true)
        {
            var details = await http.GetFromJsonAsync<JobDetails>($"/api/jobs/{jobId}", ContractJson.Options, ct);
            if (details!.Job.Status is JobStatus.Completed or JobStatus.Failed or JobStatus.Cancelled)
                return details.Job;
            await Task.Delay(TimeSpan.FromMilliseconds(250), ct);
        }
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        Console.Error.WriteLine($"Job {jobId} did not finish before the timeout.");
        return null;
    }
}

static void Report(LoadGenOptions options, long totalCharacters, TimeSpan submitTime, TimeSpan wallTime,
    IReadOnlyList<JobSummary?> finals, int rejected)
{
    var completed = finals.OfType<JobSummary>().Where(j => j.Status == JobStatus.Completed).ToList();
    // Server-side durations: from creation (includes waiting in the dispatch line) and from the Processor's start.
    var totalDurations = completed.Select(j => (j.CompletedAt!.Value - j.CreatedAt).TotalSeconds).Order().ToList();
    var processingDurations = completed.Select(j => (j.CompletedAt!.Value - j.StartedAt!.Value).TotalSeconds).Order().ToList();
    var documents = completed.Sum(j => (long)j.TotalDocuments);

    Console.WriteLine();
    Console.WriteLine("=== Results ===");
    Console.WriteLine($"Jobs:        {options.Jobs} × {options.Docs} documents, {options.MinWords}..{options.MaxWords} words, seed {options.Seed}");
    Console.WriteLine($"Status:      {string.Join(", ", finals.GroupBy(j => j?.Status.ToString() ?? "TimedOut").Select(g => $"{g.Key} {g.Count()}"))}"
                      + (rejected > 0 ? $", NotSubmitted {rejected}" : ""));
    Console.WriteLine($"Submit:      {submitTime.TotalSeconds:F2}s for all jobs");
    Console.WriteLine($"Wall time:   {wallTime.TotalSeconds:F2}s (first submit → last job finished)");
    Console.WriteLine($"Throughput:  {documents / wallTime.TotalSeconds:N0} documents/s, {totalCharacters / wallTime.TotalSeconds / 1_000_000:F1}M characters/s");
    if (completed.Count > 0)
    {
        Console.WriteLine($"Job time (created → completed):    p50 {Percentile(totalDurations, 50):F2}s  p95 {Percentile(totalDurations, 95):F2}s  max {totalDurations[^1]:F2}s");
        Console.WriteLine($"Processing (started → completed):  p50 {Percentile(processingDurations, 50):F2}s  p95 {Percentile(processingDurations, 95):F2}s  max {processingDurations[^1]:F2}s");
    }
}

/// <summary>Nearest-rank percentile of an ascending list.</summary>
static double Percentile(IReadOnlyList<double> sorted, int percentile) =>
    sorted[Math.Max(0, (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1)];

internal sealed record LoadGenOptions(int Jobs, int Docs, int MinWords, int MaxWords, int Seed, string Url, int TimeoutSeconds)
{
    public static LoadGenOptions? Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["--jobs"] = "10", ["--docs"] = "1000", ["--words"] = "200..3000", ["--seed"] = "42",
            ["--url"] = "http://localhost:5100", ["--timeout"] = "600"
        };
        for (var i = 0; i < args.Length; i += 2)
        {
            if (!values.ContainsKey(args[i]) || i + 1 >= args.Length)
                return null;
            values[args[i]] = args[i + 1];
        }

        var words = values["--words"].Split("..");
        if (words.Length != 2
            || !int.TryParse(words[0], CultureInfo.InvariantCulture, out var minWords)
            || !int.TryParse(words[1], CultureInfo.InvariantCulture, out var maxWords)
            || minWords < 0 || maxWords < minWords
            || !int.TryParse(values["--jobs"], CultureInfo.InvariantCulture, out var jobs) || jobs < 1
            || !int.TryParse(values["--docs"], CultureInfo.InvariantCulture, out var docs) || docs < 1
            || !int.TryParse(values["--seed"], CultureInfo.InvariantCulture, out var seed)
            || !int.TryParse(values["--timeout"], CultureInfo.InvariantCulture, out var timeoutSeconds) || timeoutSeconds < 1)
            return null;

        return new LoadGenOptions(jobs, docs, minWords, maxWords, seed, values["--url"], timeoutSeconds);
    }
}
