using System.Text.Json;
using Azure.Messaging.ServiceBus;

namespace RestrictPoint.Messaging;

/// <summary>Publishes domain events to a Service Bus topic.</summary>
public interface IEventPublisher
{
    Task PublishAsync(string topicName, DomainEventEnvelope envelope, CancellationToken cancellationToken);
}

/// <summary>
/// Service Bus event publisher. Senders are cached per topic; the client authenticates
/// with Managed Identity (no connection strings, per platform security requirements).
/// Message id is the event id, enabling Service Bus duplicate detection.
/// </summary>
public sealed class ServiceBusEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly Dictionary<string, ServiceBusSender> _senders = [];
    private readonly SemaphoreSlim _senderLock = new(1, 1);

    public ServiceBusEventPublisher(ServiceBusClient client)
    {
        _client = client;
    }

    public async Task PublishAsync(
        string topicName,
        DomainEventEnvelope envelope,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topicName);
        ArgumentNullException.ThrowIfNull(envelope);

        var sender = await GetSenderAsync(topicName, cancellationToken).ConfigureAwait(false);

        var message = new ServiceBusMessage(
            JsonSerializer.SerializeToUtf8Bytes(envelope, DomainEventEnvelope.SerializerOptions))
        {
            MessageId = envelope.EventId.ToString(),
            CorrelationId = envelope.CorrelationId,
            Subject = envelope.EventType,
            ContentType = "application/json",
            ApplicationProperties =
            {
                ["eventType"] = envelope.EventType,
                ["eventVersion"] = envelope.EventVersion,
                ["publisher"] = envelope.Publisher,
                ["organizationId"] = envelope.OrganizationId.ToString(),
            },
        };

        await sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ServiceBusSender> GetSenderAsync(string topicName, CancellationToken cancellationToken)
    {
        if (_senders.TryGetValue(topicName, out var existing))
        {
            return existing;
        }

        await _senderLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_senders.TryGetValue(topicName, out var sender))
            {
                sender = _client.CreateSender(topicName);
                _senders[topicName] = sender;
            }

            return sender;
        }
        finally
        {
            _senderLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sender in _senders.Values)
        {
            await sender.DisposeAsync().ConfigureAwait(false);
        }

        _senderLock.Dispose();
    }
}
