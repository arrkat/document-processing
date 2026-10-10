using System.Threading.Channels;
using JobService.Application;
using JobService.Dispatch;
using JobService.Events;
using JobService.Monitoring;
using JobService.Persistence;
using JobService.Processing;
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
        services.Configure<WatchdogOptions>(configuration.GetSection(WatchdogOptions.SectionName));

        services.AddSingleton(TimeProvider.System);

        services.AddSingleton<IJobRepository, InMemoryJobRepository>();
        services.AddSingleton<ICreateJobRequestValidator, CreateJobRequestValidator>();
        services.AddSingleton<IJobManager, JobManager>();

        // The dispatch line: JobManager writes, JobDispatcher is the single reader. Unbounded on purpose: a job is stored before
        // it is queued, so a full queue would strand it. Admission control belongs to rate limiting (documented, not implemented).
        services.AddSingleton(_ => Channel.CreateUnbounded<DispatchRequest>(new UnboundedChannelOptions { SingleReader = true }));
        services.AddSingleton(sp => sp.GetRequiredService<Channel<DispatchRequest>>().Writer);
        services.AddSingleton(sp => sp.GetRequiredService<Channel<DispatchRequest>>().Reader);

        services.AddSingleton<IProcessingEventHandler, ProcessingEventHandler>();
        services.AddSingleton<ProcessorEventsReceiver>();

        // Retries are owned by JobDispatcher (long outages, Busy backoff) — no resilience handler here to avoid nested retries.
        services.AddHttpClient<IProcessorClient, ProcessorClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ProcessorClientOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = options.Timeout;
        });

        // Registered as singletons too, so tests (and the WebSocket handler) can resolve them directly.
        services.AddSingleton<JobDispatcher>();
        services.AddSingleton<IDispatchWakeUp>(sp => sp.GetRequiredService<JobDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<JobDispatcher>());
        services.AddSingleton<JobWatchdog>();
        services.AddHostedService(sp => sp.GetRequiredService<JobWatchdog>());

        return services;
    }
}
