using Contracts.Processing;
using ProcessorService.Validation;

namespace ProcessorService.Tests.Validation;

public class ProcessJobRequestValidatorTests
{
    private readonly ProcessJobRequestValidator _validator = new();

    [Fact]
    public void Valid_request_has_no_errors()
    {
        Assert.Empty(_validator.Validate(new ProcessJobRequest("job-1", "jobs/job-1/documents.json", 3)));
    }

    [Theory]
    [InlineData(null, "jobs/job-1/documents.json", 3, "jobId")]
    [InlineData("../x", "jobs/x/documents.json", 3, "jobId")]
    [InlineData("a/b", "jobs/a/documents.json", 3, "jobId")]
    [InlineData("job-1", "../../etc/passwd", 3, "documentsRef")]
    [InlineData("job-1", "jobs/other/documents.json", 3, "documentsRef")]
    [InlineData("job-1", "jobs/job-1/documents.json", 0, "documentCount")]
    [InlineData("job-1", "jobs/job-1/documents.json", 1001, "documentCount")]
    public void Each_invalid_field_is_reported(string? jobId, string documentsRef, int documentCount, string expectedField)
    {
        var errors = _validator.Validate(new ProcessJobRequest(jobId!, documentsRef, documentCount));

        Assert.Equal([expectedField], errors.Keys);
    }

    [Fact]
    public void Several_problems_are_reported_together()
    {
        var errors = _validator.Validate(new ProcessJobRequest("", "", 0));

        Assert.Equal(["documentCount", "documentsRef", "jobId"], errors.Keys.Order());
    }

    [Fact]
    public void Missing_body_is_reported()
    {
        Assert.Contains("request", _validator.Validate(null).Keys);
    }
}
