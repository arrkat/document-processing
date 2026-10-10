using Contracts.Documents;

namespace Contracts.Jobs;

/// <summary>Body of POST /api/jobs. Nullable so that a missing list is reported by validation, not by model binding.</summary>
public sealed record CreateJobRequest(IReadOnlyList<DocumentInput>? Documents);
