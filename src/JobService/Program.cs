using BlobStorage;
using JobService.Endpoints;

namespace JobService;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddApiControllers();
        builder.Services.AddSwagger("Job Service",
            "Accepts document-processing jobs, tracks their state and serves the results.");
        builder.Services.AddProblemDetails();
        builder.Services.AddHealthChecks();

        builder.Services.AddFileSystemBlobStore(
            builder.Configuration["BlobStorage:RootPath"] ?? throw new InvalidOperationException("BlobStorage:RootPath is not configured."),
            builder.Environment.ContentRootPath);

        builder.Services.AddJobService(builder.Configuration);

        var app = builder.Build();

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseWebSockets();

        // Always on: Swagger is the documented way to try the POC.
        app.UseSwagger();
        app.UseSwaggerUI();

        app.MapHealthChecks("/health");
        app.MapControllers();
        app.MapProcessorEventsEndpoint();

        app.Run();
    }
}
