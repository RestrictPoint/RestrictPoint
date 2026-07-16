using System.Text.Json;
using RestrictPoint.Api.Identity.Application.Abstractions;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Identity.Infrastructure;

/// <summary>
/// Stages outbox rows on the current <see cref="IdentityDbContext"/> unit of work.
/// Rows are committed atomically with the business change when SaveChangesAsync runs.
/// </summary>
public sealed class OutboxWriter : IOutboxWriter
{
    private readonly IdentityDbContext _dbContext;

    public OutboxWriter(IdentityDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Stage(string topic, DomainEventEnvelope envelope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        ArgumentNullException.ThrowIfNull(envelope);

        _dbContext.OutboxMessages.Add(new OutboxMessage
        {
            Id = envelope.EventId,
            Topic = topic,
            EventType = envelope.EventType,
            Payload = JsonSerializer.Serialize(envelope, DomainEventEnvelope.SerializerOptions),
            OccurredUtc = envelope.OccurredUtc,
        });
    }
}
