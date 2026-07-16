namespace RestrictPoint.Database;

/// <summary>
/// Transactional outbox row. Domain events are written to this table inside the same
/// transaction as the business change, then dispatched to Service Bus by a background
/// dispatcher. This guarantees events are never lost and never published for rolled-back work.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }

    /// <summary>Destination Service Bus topic, e.g. <c>IdentityEvents</c>.</summary>
    public required string Topic { get; set; }

    /// <summary>Business event name, duplicated from the envelope for observability queries.</summary>
    public required string EventType { get; set; }

    /// <summary>The complete serialized <c>DomainEventEnvelope</c> JSON.</summary>
    public required string Payload { get; set; }

    public DateTimeOffset OccurredUtc { get; set; }

    /// <summary>Null until successfully dispatched to Service Bus.</summary>
    public DateTimeOffset? DispatchedUtc { get; set; }

    /// <summary>Dispatch attempts so far; rows exceeding the max are surfaced by monitoring.</summary>
    public int AttemptCount { get; set; }

    /// <summary>Last dispatch error, for diagnostics. Cleared on success.</summary>
    public string? LastError { get; set; }
}
