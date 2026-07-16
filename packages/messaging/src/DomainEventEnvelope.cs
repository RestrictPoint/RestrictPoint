using System.Text.Json;
using System.Text.Json.Serialization;

namespace RestrictPoint.Messaging;

/// <summary>
/// The standard event envelope defined in docs/20-Event-Catalog. Every event published to
/// Service Bus uses this shape; no field may be removed.
/// </summary>
public sealed record DomainEventEnvelope
{
    /// <summary>Shared serializer options: camelCase to match the catalog's TypeScript contract.</summary>
    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public required Guid EventId { get; init; }

    /// <summary>Business event name, e.g. <c>UserRegistered</c>.</summary>
    public required string EventType { get; init; }

    /// <summary>Schema version, e.g. <c>1.0</c>.</summary>
    public required string EventVersion { get; init; }

    public required DateTimeOffset OccurredUtc { get; init; }

    /// <summary>Request correlation shared across the business workflow.</summary>
    public required string CorrelationId { get; init; }

    /// <summary>The eventId of the event that caused this one, when applicable.</summary>
    public Guid? CausationId { get; init; }

    /// <summary>Publisher organization scope. Empty GUID for pre-organization events.</summary>
    public required Guid OrganizationId { get; init; }

    /// <summary>Customer tenant, when applicable.</summary>
    public Guid? TenantId { get; init; }

    /// <summary>Publishing service name, e.g. <c>identity</c>.</summary>
    public required string Publisher { get; init; }

    /// <summary>The event payload, serialized as part of the envelope.</summary>
    public required JsonElement Payload { get; init; }

    /// <summary>Creates an envelope for the given payload.</summary>
    public static DomainEventEnvelope Create<TPayload>(
        string eventType,
        string eventVersion,
        string publisher,
        string correlationId,
        Guid organizationId,
        TPayload payload,
        Guid? causationId = null,
        Guid? tenantId = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(publisher);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);
        ArgumentNullException.ThrowIfNull(payload);

        return new DomainEventEnvelope
        {
            EventId = Guid.NewGuid(),
            EventType = eventType,
            EventVersion = eventVersion,
            OccurredUtc = (timeProvider ?? TimeProvider.System).GetUtcNow(),
            CorrelationId = correlationId,
            CausationId = causationId,
            OrganizationId = organizationId,
            TenantId = tenantId,
            Publisher = publisher,
            Payload = JsonSerializer.SerializeToElement(payload, SerializerOptions),
        };
    }
}
