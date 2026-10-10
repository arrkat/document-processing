using Contracts.Documents;

namespace Contracts.Blobs;

/// <summary>Payload stored at <c>jobs/{jobId}/results.json</c>. Written by the Processor, read by the Job Service.</summary>
public sealed record JobResultsBlob(int SchemaVersion, string JobId, IReadOnlyList<DocumentResult> Results)
{
    public const int CurrentSchemaVersion = 1;
}
