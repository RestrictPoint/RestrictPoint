using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Identity.Application.Abstractions;

/// <summary>
/// Stages a domain event in the transactional outbox. The event is persisted in the same
/// transaction as the business change and dispatched to Service Bus asynchronously.
/// </summary>
public interface IOutboxWriter
{
    void Stage(string topic, DomainEventEnvelope envelope);
}
