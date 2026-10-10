using Contracts.Events;
using Microsoft.Extensions.Options;
using ProcessorService.Settings;

namespace ProcessorService.Events;

/// <summary>
/// Bounded buffer between the pipeline and the WebSocket. When full, the oldest <see cref="JobProgressEvent"/> is dropped
/// (a later progress or terminal event supersedes it); terminal and started events are never dropped.
/// </summary>
public sealed class OutboundEventBuffer(IOptions<JobEventsClientOptions> options) : IJobEventPublisher, IOutboundEventSource
{
    public void Publish(JobEvent jobEvent) => throw new NotImplementedException();

    public IAsyncEnumerable<JobEvent> ReadAllAsync(CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
