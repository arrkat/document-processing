using Contracts.Documents;
using Contracts.Jobs;
using JobService.Validation;

namespace JobService.Tests.Validation;

public class CreateJobRequestValidatorTests
{
    private readonly CreateJobRequestValidator _validator = new();

    private static CreateJobRequest Request(params DocumentInput[] documents) => new(documents);

    [Fact]
    public void Valid_request_has_no_errors()
    {
        Assert.Empty(_validator.Validate(Request(new DocumentInput("a", "hello"), new DocumentInput("b", "world"))));
    }

    [Fact]
    public void Empty_content_is_valid()
    {
        Assert.Empty(_validator.Validate(Request(new DocumentInput("a", ""), new DocumentInput("b", "   "))));
    }

    public static TheoryData<string, CreateJobRequest?, string> InvalidCases => new()
    {
        { "missing body", null, "request" },
        { "missing list", new CreateJobRequest(null), "documents" },
        { "empty list", Request(), "documents" },
        { "too many documents", new CreateJobRequest(Enumerable.Range(0, 1001).Select(i => new DocumentInput($"d{i}", "x")).ToList()), "documents" },
        { "empty id", Request(new DocumentInput("", "x")), "documents[0].id" },
        { "id too long", Request(new DocumentInput(new string('a', 129), "x")), "documents[0].id" },
        { "duplicate id", Request(new DocumentInput("a", "x"), new DocumentInput("b", "y"), new DocumentInput("a", "z")), "documents[2].id" },
        { "null content", Request(new DocumentInput("a", null!)), "documents[0].content" },
        { "content too long", Request(new DocumentInput("a", new string('x', 100_001))), "documents[0].content" },
    };

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void Each_rule_reports_its_field(string scenario, CreateJobRequest? request, string expectedField)
    {
        var errors = _validator.Validate(request);

        Assert.True(errors.Keys.SequenceEqual([expectedField]), $"{scenario}: got [{string.Join(", ", errors.Keys)}]");
    }

    [Fact]
    public void Several_problems_are_reported_together()
    {
        var errors = _validator.Validate(Request(new DocumentInput("", null!), new DocumentInput("b", new string('x', 100_001))));

        Assert.Equal(["documents[0].content", "documents[0].id", "documents[1].content"], errors.Keys.Order());
    }
}
