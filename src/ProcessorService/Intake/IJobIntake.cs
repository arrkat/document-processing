using Contracts.Processing;
using ProcessorService.Jobs;

namespace ProcessorService.Intake;

/// <summary>The Processor's front door: use cases behind the /api/processing-jobs endpoints.</summary>
public interface IJobIntake
{
    /// <summary>
    /// Order matters: validate (Invalid) → already known? (AlreadyExists, even when all slots are busy, so a retry of a
    /// running job never blocks the sender) → <see cref="Processing.IJobRunner.TryStart"/> (Accepted / Busy).
    /// </summary>
    SubmitOutcome Submit(ProcessJobRequest? request);

    ProcessingJobStatus? GetStatus(string jobId);

    CancelOutcome Cancel(string jobId);
}
