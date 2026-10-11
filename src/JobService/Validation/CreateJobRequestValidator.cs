using Contracts;
using Contracts.Jobs;

namespace JobService.Validation;

/// <summary>
/// Rules: documents list present and non-empty, at most <see cref="DocumentLimits.MaxDocumentsPerJob"/>;
/// each id non-empty, unique, at most <see cref="DocumentLimits.MaxDocumentIdLength"/>;
/// content non-null (empty is allowed), at most <see cref="DocumentLimits.MaxContentLength"/> characters;
/// all contents together at most <see cref="DocumentLimits.MaxTotalContentLength"/> characters.
/// Errors are keyed by field path, e.g. <c>documents[2].id</c>, and all of them are reported at once.
/// </summary>
public sealed class CreateJobRequestValidator : ICreateJobRequestValidator
{
    public IReadOnlyDictionary<string, string[]> Validate(CreateJobRequest? request)
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

        var documents = request.Documents;
        if (documents is null || documents.Count == 0)
        {
            Add("documents", "At least one document is required.");
            return ToResult(errors);
        }

        if (documents.Count > DocumentLimits.MaxDocumentsPerJob)
            Add("documents", $"At most {DocumentLimits.MaxDocumentsPerJob} documents are allowed per job.");

        var firstIndexById = new Dictionary<string, int>(StringComparer.Ordinal);
        long totalContentLength = 0;
        for (var i = 0; i < documents.Count; i++)
        {
            var document = documents[i];
            if (document is null)
            {
                Add($"documents[{i}]", "Document is required.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(document.Id))
                Add($"documents[{i}].id", "Document id is required.");
            else if (document.Id.Length > DocumentLimits.MaxDocumentIdLength)
                Add($"documents[{i}].id", $"Document id must be at most {DocumentLimits.MaxDocumentIdLength} characters.");
            else if (!firstIndexById.TryAdd(document.Id, i))
                Add($"documents[{i}].id", $"Duplicate document id '{document.Id}' (also at documents[{firstIndexById[document.Id]}]).");

            if (document.Content is null)
                Add($"documents[{i}].content", "Content is required (it may be empty).");
            else if (document.Content.Length > DocumentLimits.MaxContentLength)
                Add($"documents[{i}].content", $"Content must be at most {DocumentLimits.MaxContentLength} characters.");

            totalContentLength += document.Content?.Length ?? 0;
        }

        if (totalContentLength > DocumentLimits.MaxTotalContentLength)
            Add("documents", $"Total content must be at most {DocumentLimits.MaxTotalContentLength:N0} characters per job (got {totalContentLength:N0}).");

        return ToResult(errors);
    }

    private static Dictionary<string, string[]> ToResult(Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
}
