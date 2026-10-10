using ProcessorService.Analysis;
using ProcessorService.Application;
using ProcessorService.Concurrency;
using ProcessorService.Events;
using ProcessorService.Jobs;
using ProcessorService.Pipeline;
using ProcessorService.Queue;
using ProcessorService.Settings;
using ProcessorService.Validation;

namespace ProcessorService;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProcessorService(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ProcessingOptions>(configuration.GetSection(ProcessingOptions.SectionName));
        services.Configure<JobEventsClientOptions>(configuration.GetSection(JobEventsClientOptions.SectionName));

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IDocumentAnalyzer, DocumentAnalyzer>();
        services.AddSingleton<IJobRegistry, JobRegistry>();
        services.AddSingleton<IProcessingQueue, ChannelProcessingQueue>();
        services.AddSingleton<IDocumentConcurrencyLimiter, SemaphoreDocumentConcurrencyLimiter>();
        services.AddSingleton<IJobProcessor, JobProcessor>();
        services.AddSingleton<IProcessJobRequestValidator, ProcessJobRequestValidator>();
        services.AddSingleton<IProcessingService, ProcessingService>();

        services.AddSingleton<IJobEventFactory, JobEventFactory>();
        services.AddSingleton<OutboundEventBuffer>();
        services.AddSingleton<IJobEventPublisher>(sp => sp.GetRequiredService<OutboundEventBuffer>());
        services.AddSingleton<IOutboundEventSource>(sp => sp.GetRequiredService<OutboundEventBuffer>());

        // Registered as singletons too, so tests (and health checks) can resolve them directly.
        services.AddSingleton<ProcessingWorkerPool>();
        services.AddHostedService(sp => sp.GetRequiredService<ProcessingWorkerPool>());
        services.AddSingleton<JobEventsSocketClient>();
        services.AddHostedService(sp => sp.GetRequiredService<JobEventsSocketClient>());

        return services;
    }
}
