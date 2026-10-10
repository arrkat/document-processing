namespace ProcessorService.Jobs;

/// <summary>Unit of work in the intake queue. Documents are loaded from <see cref="DocumentsRef"/> only when a worker picks the job.</summary>
public sealed record ProcessingJob(string JobId, string DocumentsRef, int DocumentCount);
