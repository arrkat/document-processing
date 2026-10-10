namespace JobService.Domain;

/// <summary>Facts about a job that never change after creation. The documents themselves live in the blob at <see cref="DocumentsRef"/>.</summary>
public sealed record JobMetadata(string Id, int TotalDocuments, string DocumentsRef, DateTimeOffset CreatedAt);
