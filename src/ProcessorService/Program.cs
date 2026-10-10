using BlobStorage;

namespace ProcessorService;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddApiControllers();
        builder.Services.AddSwagger("Processor Service",
            "Analyses the documents of a job (word count, unique words, top 5 words, SHA-256) and reports progress over WebSocket.");
        builder.Services.AddProblemDetails();
        builder.Services.AddHealthChecks();

        builder.Services.AddFileSystemBlobStore(
            builder.Configuration["BlobStorage:RootPath"] ?? throw new InvalidOperationException("BlobStorage:RootPath is not configured."),
            builder.Environment.ContentRootPath);

        builder.Services.AddProcessorService(builder.Configuration);

        var app = builder.Build();

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        // Always on: Swagger is the documented way to try the POC.
        app.UseSwagger();
        app.UseSwaggerUI();

        app.MapHealthChecks("/health");
        app.MapControllers();

        app.Run();
    }
}
