using Contracts.Processing;

namespace ProcessorService.Validation;

public interface IProcessJobRequestValidator
{
    /// <returns>Errors keyed by field name; empty when valid.</returns>
    IReadOnlyDictionary<string, string[]> Validate(ProcessJobRequest? request);
}
