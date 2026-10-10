using Contracts.Jobs;

namespace JobService.Validation;

/// <summary>
/// Rules: documents list present and non-empty, at most <see cref="Contracts.DocumentLimits.MaxDocumentsPerJob"/>;
/// each id non-empty, unique, at most <see cref="Contracts.DocumentLimits.MaxDocumentIdLength"/>;
/// content non-null (empty is allowed), at most <see cref="Contracts.DocumentLimits.MaxContentLength"/>.
/// </summary>
public sealed class CreateJobRequestValidator : ICreateJobRequestValidator
{
    public IReadOnlyDictionary<string, string[]> Validate(CreateJobRequest? request) =>
        throw new NotImplementedException();
}
