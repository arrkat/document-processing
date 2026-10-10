namespace ProcessorService.Jobs;

/// <summary>A job accepted for processing. Documents are read from <see cref="DocumentsRef"/> when processing starts.</summary>
public sealed record ProcessingJob(string JobId, string DocumentsRef, int DocumentCount);
