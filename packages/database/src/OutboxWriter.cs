using Microsoft.EntityFrameworkCore;
using RestrictPoint.Messaging;

namespace RestrictPoint.Database;

/// <summary>
/// Stages a domain event in the transactional outbox. The event is persisted in the same
/// transaction as the business change and dispatched to Service Bus asynchronously.
/// </summary>
public interface IOutboxWriter
{
    void Stage(string topic, DomainEventEnvelope envelope);
}

/// <summary>
/// Outbox writer bound to any <see cref="DbContext"/> that maps <see cref="OutboxMessage"/>.
/// </summary>
public sealed class OutboxWriter : IOutboxWriter
{
    private readonly DbContext _dbContext;

    public OutboxWriter(DbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Stage(string topic, DomainEventEnvelope envelope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(envelope);

        _dbContext.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = envelope.EventId,
            Topic = topic,
            EventType = envelope.EventType,
            Payload = System.Text.Json.JsonSerializer.Serialize(
                envelope, DomainEventEnvelope.SerializerOptions),
            OccurredUtc = envelope.OccurredUtc,
        });
    }
}
