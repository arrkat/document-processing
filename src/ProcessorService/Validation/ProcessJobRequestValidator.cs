using Contracts.Processing;

namespace ProcessorService.Validation;

/// <summary>Rules: jobId non-empty and blob-key safe; documentsRef a valid <see cref="BlobStorage.BlobKey"/>; documentCount in 1..<see cref="Contracts.DocumentLimits.MaxDocumentsPerJob"/>.</summary>
public sealed class ProcessJobRequestValidator : IProcessJobRequestValidator
{
    public IReadOnlyDictionary<string, string[]> Validate(ProcessJobRequest? request) =>
        throw new NotImplementedException();
}
