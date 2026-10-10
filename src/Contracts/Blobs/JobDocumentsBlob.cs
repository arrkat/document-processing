using Contracts.Documents;

namespace Contracts.Blobs;

/// <summary>Payload stored at <c>jobs/{jobId}/documents.json</c>. Written by the Job Service, read by the Processor.</summary>
public sealed record JobDocumentsBlob(int SchemaVersion, string JobId, IReadOnlyList<DocumentInput> Documents)
{
    public const int CurrentSchemaVersion = 1;
}
