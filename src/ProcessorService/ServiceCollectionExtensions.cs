using System.Threading.Channels;
using Contracts.Events;
using Microsoft.Extensions.Options;
using ProcessorService.Analysis;
using ProcessorService.Events;
using ProcessorService.Application;
using ProcessorService.Jobs;
using ProcessorService.Processing;
using ProcessorService.Settings;
using ProcessorService.Validation;

namespace ProcessorService;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProcessorService(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ProcessingOptions>(configuration.GetSection(ProcessingOptions.SectionName));
        services.Configure<ProcessingEventsClientOptions>(configuration.GetSection(ProcessingEventsClientOptions.SectionName));

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IDocumentAnalyzer, DocumentAnalyzer>();
        services.AddSingleton<IJobRegistry, JobRegistry>();
        services.AddSingleton<IProcessJobRequestValidator, ProcessJobRequestValidator>();
        services.AddSingleton<IProcessingJobManager, ProcessingJobManager>();

        // Outbound events: the worker writes, the WebSocket client is the single reader.
        services.AddSingleton(sp => Channel.CreateBounded<ProcessingEvent>(
            new BoundedChannelOptions(sp.GetRequiredService<IOptions<ProcessingEventsClientOptions>>().Value.EventChannelCapacity)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait
            }));
        services.AddSingleton(sp => sp.GetRequiredService<Channel<ProcessingEvent>>().Writer);
        services.AddSingleton(sp => sp.GetRequiredService<Channel<ProcessingEvent>>().Reader);

        // Registered as singletons too, so they can be resolved directly (IJobRunner, tests, health checks).
        services.AddSingleton<ProcessingWorker>();
        services.AddSingleton<IJobRunner>(sp => sp.GetRequiredService<ProcessingWorker>());
        services.AddHostedService(sp => sp.GetRequiredService<ProcessingWorker>());
        services.AddSingleton<ProcessingEventsSocketClient>();
        services.AddHostedService(sp => sp.GetRequiredService<ProcessingEventsSocketClient>());

        return services;
    }
}
