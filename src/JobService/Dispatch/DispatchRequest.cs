namespace JobService.Dispatch;

/// <summary>A job waiting in the dispatch line (the Job Service's single waiting line). Its own type so the channel is unambiguous in DI.</summary>
public sealed record DispatchRequest(string JobId);
