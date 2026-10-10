using JobService.Domain;

namespace JobService.Persistence;

public enum StateUpdateStatus
{
    Updated,

    /// <summary>The updater returned null; the stored state is unchanged.</summary>
    Declined,
    NotFound
}

/// <param name="State">The stored state after the call (new state if Updated, current state if Declined, null if NotFound).</param>
public sealed record StateUpdateResult(StateUpdateStatus Status, JobState? State);
