using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Integration.Tests;

public class HealthTests
{
    [Fact]
    public async Task JobService_is_healthy()
    {
        await using var factory = CreateFactory<JobService.Program>();

        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProcessorService_is_healthy()
    {
        await using var factory = CreateFactory<ProcessorService.Program>();

        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static WebApplicationFactory<T> CreateFactory<T>() where T : class =>
        new WebApplicationFactory<T>().WithWebHostBuilder(builder =>
            builder.UseSetting("BlobStorage:RootPath",
                Path.Combine(Path.GetTempPath(), "ta9-it-" + Guid.NewGuid().ToString("N"))));
}
