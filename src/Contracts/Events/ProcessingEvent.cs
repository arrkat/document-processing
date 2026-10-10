using System.Text.Json.Serialization;

namespace Contracts.Events;

/// <summary>
/// Envelope for every Processor -> Job Service WebSocket message. Serialized with a <c>"type"</c> discriminator.
/// <para>Events describe the Processor's work ("processing"), not the job's lifecycle: <see cref="ProcessingFinishedEvent"/>
/// means "processing is done, the results blob is fully written"; on receiving it the Job Service marks the job Completed.</para>
/// <para><see cref="EventId"/> is unique per event (dedupe); <see cref="Sequence"/> increases per job (ordering / stale-event detection).</para>
/// <para>On reconnect the Processor sends a snapshot event for each known job; receivers must be idempotent.</para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ProcessingStartedEvent), "ProcessingStarted")]
[JsonDerivedType(typeof(ProcessingProgressUpdatedEvent), "ProcessingProgressUpdated")]
[JsonDerivedType(typeof(ProcessingFinishedEvent), "ProcessingFinished")]
[JsonDerivedType(typeof(ProcessingFailedEvent), "ProcessingFailed")]
[JsonDerivedType(typeof(ProcessingCancelledEvent), "ProcessingCancelled")]
public abstract record ProcessingEvent(Guid EventId, string JobId, long Sequence, DateTimeOffset Timestamp)
{
    [JsonIgnore]
    public bool IsTerminal => this is ProcessingFinishedEvent or ProcessingFailedEvent or ProcessingCancelledEvent;
}

public sealed record ProcessingStartedEvent(Guid EventId, string JobId, long Sequence, DateTimeOffset Timestamp,
    int TotalDocuments)
    : ProcessingEvent(EventId, JobId, Sequence, Timestamp);

/// <summary>Throttled progress. Counts only — results travel through the results blob.</summary>
public sealed record ProcessingProgressUpdatedEvent(Guid EventId, string JobId, long Sequence, DateTimeOffset Timestamp,
    int ProcessedDocuments, int TotalDocuments)
    : ProcessingEvent(EventId, JobId, Sequence, Timestamp);

/// <summary>All documents were analysed and the results blob at <see cref="ResultsRef"/> is fully written.</summary>
public sealed record ProcessingFinishedEvent(Guid EventId, string JobId, long Sequence, DateTimeOffset Timestamp,
    string ResultsRef)
    : ProcessingEvent(EventId, JobId, Sequence, Timestamp);

public sealed record ProcessingFailedEvent(Guid EventId, string JobId, long Sequence, DateTimeOffset Timestamp,
    ErrorCode ErrorCode, string? ErrorMessage)
    : ProcessingEvent(EventId, JobId, Sequence, Timestamp);

public sealed record ProcessingCancelledEvent(Guid EventId, string JobId, long Sequence, DateTimeOffset Timestamp,
    int ProcessedDocuments)
    : ProcessingEvent(EventId, JobId, Sequence, Timestamp);
