using System.Text.Json.Serialization;
using BlobStorage;
using ProcessorService.Endpoints;

namespace ProcessorService;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddProblemDetails();
        builder.Services.AddHealthChecks();
        builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        builder.Services.AddFileSystemBlobStore(
            builder.Configuration["BlobStorage:RootPath"] ?? throw new InvalidOperationException("BlobStorage:RootPath is not configured."),
            builder.Environment.ContentRootPath);

        builder.Services.AddProcessorService(builder.Configuration);

        var app = builder.Build();

        app.UseExceptionHandler();
        app.UseStatusCodePages();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.MapHealthChecks("/health");
        app.MapProcessingJobsEndpoints();

        app.Run();
    }
}
