using JobService.Application;
using JobService.Dispatch;
using JobService.Events;
using JobService.Monitoring;
using JobService.Persistence;
using JobService.Processing;
using JobService.Results;
using JobService.Settings;
using JobService.Validation;
using Microsoft.Extensions.Options;

namespace JobService;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddJobService(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ProcessorClientOptions>(configuration.GetSection(ProcessorClientOptions.SectionName));
        services.Configure<DispatchOptions>(configuration.GetSection(DispatchOptions.SectionName));
        services.Configure<ResultsIngestionOptions>(configuration.GetSection(ResultsIngestionOptions.SectionName));
        services.Configure<WatchdogOptions>(configuration.GetSection(WatchdogOptions.SectionName));

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IJobRepository, InMemoryJobRepository>();
        services.AddSingleton<ICreateJobRequestValidator, CreateJobRequestValidator>();
        services.AddSingleton<IJobManager, JobManager>();

        services.AddSingleton<IJobDispatchQueue, ChannelJobDispatchQueue>();
        services.AddSingleton<IResultsIngestionQueue, ChannelResultsIngestionQueue>();

        services.AddSingleton<IJobEventHandler, JobEventHandler>();
        services.AddSingleton<ProcessorEventsSocketHandler>();

        // Retries are owned by JobDispatcher (long outages, Busy backoff) — no resilience handler here to avoid nested retries.
        services.AddHttpClient<IProcessorClient, ProcessorClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessorClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = options.Timeout;
        });

        // Registered as singletons too, so tests (and the WebSocket handler) can resolve them directly.
        services.AddSingleton<JobDispatcher>();
        services.AddHostedService(sp => sp.GetRequiredService<JobDispatcher>());
        services.AddSingleton<ResultsIngestor>();
        services.AddHostedService(sp => sp.GetRequiredService<ResultsIngestor>());
        services.AddSingleton<JobWatchdog>();
        services.AddHostedService(sp => sp.GetRequiredService<JobWatchdog>());

        return services;
    }
}
