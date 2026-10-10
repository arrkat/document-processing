using Contracts.Events;

namespace ProcessorService.Events;

/// <summary>Producer side of the outbound event buffer. Never blocks on the network: the WebSocket client sends asynchronously.</summary>
public interface IJobEventPublisher
{
    void Publish(JobEvent jobEvent);
}

/// <summary>Consumer side of the outbound event buffer, read by the single WebSocket sender loop.</summary>
public interface IOutboundEventSource
{
    IAsyncEnumerable<JobEvent> ReadAllAsync(CancellationToken cancellationToken);
}
