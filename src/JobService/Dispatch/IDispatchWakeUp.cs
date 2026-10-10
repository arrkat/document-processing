namespace JobService.Dispatch;

/// <summary>
/// Signals the dispatcher that a Processor job slot was probably freed (a terminal event arrived),
/// so a job waiting after a 503 Busy is retried immediately instead of after the full busy delay.
/// </summary>
public interface IDispatchWakeUp
{
    void SlotFreed();
}
