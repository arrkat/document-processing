using BlobStorage;

namespace BlobStorage.Tests;

public class BlobKeyTests
{
    [Fact]
    public void Builds_job_keys()
    {
        Assert.Equal("jobs/abc-123/documents.json", BlobKey.JobDocuments("abc-123"));
        Assert.Equal("jobs/abc-123/results.json", BlobKey.JobResults("abc-123"));
    }

    [Theory]
    [InlineData("..")]
    [InlineData("a/b")]
    public void Rejects_job_ids_that_would_change_the_path(string jobId)
    {
        Assert.Throws<ArgumentException>(() => BlobKey.JobDocuments(jobId));
    }
}
