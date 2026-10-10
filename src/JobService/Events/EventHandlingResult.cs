namespace JobService.Events;

public enum EventHandlingResult
{
    Applied,
    Duplicate,
    InvalidTransition,
    UnknownJob
}
