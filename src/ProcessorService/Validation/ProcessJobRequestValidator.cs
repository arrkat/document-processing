using BlobStorage;
using Contracts;
using Contracts.Processing;

namespace ProcessorService.Validation;

/// <summary>
/// Rules: jobId is a safe blob-key segment (letters, digits, '-', '_', at most <see cref="MaxJobIdLength"/>);
/// documentsRef is exactly this job's documents key (<see cref="BlobKey.JobDocuments"/>), so a request can never point
/// at another job's documents or outside the store; documentCount is 1..<see cref="DocumentLimits.MaxDocumentsPerJob"/>.
/// </summary>
public sealed class ProcessJobRequestValidator : IProcessJobRequestValidator
{
    public const int MaxJobIdLength = 64;

    public IReadOnlyDictionary<string, string[]> Validate(ProcessJobRequest? request)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var messages))
                errors[field] = messages = [];
            messages.Add(message);
        }

        if (request is null)
        {
            Add("request", "Request body is required.");
            return ToResult(errors);
        }

        var jobIdIsValid = request.JobId is { Length: <= MaxJobIdLength } && BlobKey.IsValidSegment(request.JobId);
        if (string.IsNullOrEmpty(request.JobId))
            Add("jobId", "Job id is required.");
        else if (!jobIdIsValid)
            Add("jobId", $"Job id may contain only letters, digits, '-' and '_', and at most {MaxJobIdLength} characters.");

        if (string.IsNullOrEmpty(request.DocumentsRef))
            Add("documentsRef", "Documents reference is required.");
        else if (jobIdIsValid && request.DocumentsRef != BlobKey.JobDocuments(request.JobId))
            Add("documentsRef", $"Documents reference must be '{BlobKey.JobDocuments(request.JobId)}'.");

        if (request.DocumentCount is < 1 or > DocumentLimits.MaxDocumentsPerJob)
            Add("documentCount", $"Document count must be between 1 and {DocumentLimits.MaxDocumentsPerJob}.");

        return ToResult(errors);
    }

    private static Dictionary<string, string[]> ToResult(Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
}
