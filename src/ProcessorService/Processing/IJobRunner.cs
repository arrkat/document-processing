using ProcessorService.Jobs;

namespace ProcessorService.Processing;

/// <summary>
/// Admission: the single owner of the decision whether a job starts. An interface so <see cref="Application.ProcessingJobManager"/>
/// tests can substitute it.
/// </summary>
public interface IJobRunner
{
    /// <summary>
    /// Decides atomically, in this order:
    /// <list type="number">
    /// <item>jobId already known → <see cref="StartJobResult.AlreadyKnown"/> (checked first, so a retry of a running job is never told Busy);</item>
    /// <item>all job slots taken (<see cref="Settings.ProcessingOptions.MaxConcurrentJobs"/>) → <see cref="StartJobResult.Busy"/>, nothing registered;</item>
    /// <item>otherwise register, take a slot, start processing in the background → <see cref="StartJobResult.Started"/>.</item>
    /// </list>
    /// Never blocks. The slot is released when the job ends.
    /// </summary>
    StartJobResult TryStart(ProcessingJob job);
}
