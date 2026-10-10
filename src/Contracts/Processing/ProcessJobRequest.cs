namespace Contracts.Processing;

/// <summary>Body of POST /api/processing-jobs. Carries a reference to the documents blob, not the documents (claim-check).</summary>
public sealed record ProcessJobRequest(string JobId, string DocumentsRef, int DocumentCount);
