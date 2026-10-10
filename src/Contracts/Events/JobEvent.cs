using System.Text.Json.Serialization;

namespace Contracts.Events;

/// <summary>
/// Envelope for every Processor -> Job Service WebSocket message. Serialized with a <c>"type"</c> discriminator.
/// <para><see cref="EventId"/> is unique per event (dedupe); <see cref="Sequence"/> increases per job (ordering / stale-event detection).</para>
/// <para>On reconnect the Processor re-sends the latest event of each known job; receivers must be idempotent.</para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(JobStartedEvent), "JobStarted")]
[JsonDerivedType(typeof(JobProgressEvent), "JobProgress")]
[JsonDerivedType(typeof(JobCompletedEvent), "JobCompleted")]
[JsonDerivedType(typeof(JobFailedEvent), "JobFailed")]
[JsonDerivedType(typeof(JobCancelledEvent), "JobCancelled")]
public abstract record JobEvent(Guid EventId, string JobId, long Sequence, DateTimeOffset Timestamp)
{
    [JsonIgnore]
    public bool IsTerminal => this is JobCompletedEvent or JobFailedEvent or JobCancelledEvent;
}

public sealed record JobStartedEvent(Guid EventId, string JobId, long Sequence, DateTimeOffset Timestamp,
    int TotalDocuments)
    : JobEvent(EventId, JobId, Sequence, Timestamp);

/// <summary>Throttled progress. Counts only — results travel through the results blob.</summary>
public sealed record JobProgressEvent(Guid EventId, string JobId, long Sequence, DateTimeOffset Timestamp,
    int ProcessedDocuments, int TotalDocuments)
    : JobEvent(EventId, JobId, Sequence, Timestamp);

/// <summary>All documents were analysed and the results blob at <see cref="ResultsRef"/> is fully written.</summary>
public sealed record JobCompletedEvent(Guid EventId, string JobId, long Sequence, DateTimeOffset Timestamp,
    string ResultsRef)
    : JobEvent(EventId, JobId, Sequence, Timestamp);

public sealed record JobFailedEvent(Guid EventId, string JobId, long Sequence, DateTimeOffset Timestamp,
    ErrorCode ErrorCode, string? ErrorMessage)
    : JobEvent(EventId, JobId, Sequence, Timestamp);

public sealed record JobCancelledEvent(Guid EventId, string JobId, long Sequence, DateTimeOffset Timestamp,
    int ProcessedDocuments)
    : JobEvent(EventId, JobId, Sequence, Timestamp);
