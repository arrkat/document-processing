using ProcessorService.Jobs;

namespace ProcessorService.Processing;

/// <summary>Starts jobs when capacity allows. An interface so <see cref="Intake.JobIntake"/> tests can simulate "busy" or "already known".</summary>
public interface IJobRunner
{
    /// <summary>
    /// Non-blocking admission: takes a free job slot (at most <see cref="Settings.ProcessingOptions.MaxConcurrentJobs"/>),
    /// registers the job atomically and starts processing it in the background. The slot is released when the job ends.
    /// </summary>
    StartOutcome TryStart(ProcessingJob job);
}
