using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using RestrictPoint.Api.Licensing.Application.ConsumeBillingEvents;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Licensing.Functions;

/// <summary>
/// Service Bus trigger for the BillingEvents topic (subscription: licensing).
/// Delegates to the SubscriptionActivated consumer; failures throw so Service Bus
/// retries and eventually dead-letters the message.
/// </summary>
public sealed class BillingEventsFunction
{
    private readonly SubscriptionActivatedConsumer _consumer;

    public BillingEventsFunction(SubscriptionActivatedConsumer consumer)
    {
        _consumer = consumer;
    }

    [Function("ConsumeBillingEvents")]
    public async Task RunAsync(
        [ServiceBusTrigger("BillingEvents", "licensing", Connection = "ServiceBusConnection")]
        string message,
        FunctionContext functionContext)
    {
        var envelope = JsonSerializer.Deserialize<DomainEventEnvelope>(
            message, DomainEventEnvelope.SerializerOptions)
            ?? throw new JsonException("Billing event envelope deserialized to null.");

        await _consumer.HandleAsync(envelope, functionContext.CancellationToken);
    }
}
