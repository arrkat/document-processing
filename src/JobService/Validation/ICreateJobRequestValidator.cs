using Contracts.Jobs;

namespace JobService.Validation;

public interface ICreateJobRequestValidator
{
    /// <returns>Errors keyed by field path (e.g. <c>documents[3].id</c>); empty when the request is valid.</returns>
    IReadOnlyDictionary<string, string[]> Validate(CreateJobRequest? request);
}
