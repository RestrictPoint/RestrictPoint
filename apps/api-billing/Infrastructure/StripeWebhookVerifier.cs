using Microsoft.Extensions.Logging;
using RestrictPoint.Api.Billing.Application.Abstractions;
using RestrictPoint.Api.Billing.Domain;
using RestrictPoint.Common;
using Stripe;

namespace RestrictPoint.Api.Billing.Infrastructure;

/// <summary>
/// Verifies Stripe webhook signatures and normalizes payloads. Signature verification is
/// mandatory and happens before any parsing of business data (docs/12 security).
/// </summary>
public sealed partial class StripeWebhookVerifier : IWebhookVerifier
{
    private readonly string _webhookSecret;
    private readonly ILogger<StripeWebhookVerifier> _logger;

    public StripeWebhookVerifier(StripeOptions options, ILogger<StripeWebhookVerifier> logger)
    {
        _webhookSecret = options.WebhookSecret;
        _logger = logger;
    }

    public Result<WebhookEvent> VerifyAndParse(string payload, string signatureHeader)
    {
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(payload, signatureHeader, _webhookSecret);
        }
        catch (StripeException exception)
        {
            LogSignatureFailure(_logger, exception);
            return BillingErrors.InvalidWebhookSignature;
        }

        return new WebhookEvent
        {
            EventId = stripeEvent.Id,
            EventType = stripeEvent.Type,
            Subscription = MapSubscription(stripeEvent),
            Invoice = MapInvoice(stripeEvent),
            Payment = MapPayment(stripeEvent),
        };
    }

    private static SubscriptionEventData? MapSubscription(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is not Stripe.Subscription subscription)
        {
            return null;
        }

        Guid? localId = subscription.Metadata is not null
            && subscription.Metadata.TryGetValue("subscriptionId", out var raw)
            && Guid.TryParse(raw, out var parsed)
                ? parsed
                : null;

        var firstItem = subscription.Items?.Data?.FirstOrDefault();

        return new SubscriptionEventData
        {
            ProviderSubscriptionId = subscription.Id,
            ProviderStatus = subscription.Status,
            LocalSubscriptionId = localId,
            ProviderCustomerId = subscription.CustomerId,
            CurrentPeriodStart = firstItem?.CurrentPeriodStart,
            CurrentPeriodEnd = firstItem?.CurrentPeriodEnd,
            CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
        };
    }

    private static InvoiceEventData? MapInvoice(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is not Stripe.Invoice invoice)
        {
            return null;
        }

        var subscriptionId = invoice.Parent?.SubscriptionDetails?.SubscriptionId;
        if (string.IsNullOrEmpty(subscriptionId))
        {
            return null; // Non-subscription invoices are out of scope.
        }

        return new InvoiceEventData
        {
            ProviderInvoiceId = invoice.Id,
            ProviderSubscriptionId = subscriptionId,
            Amount = invoice.AmountDue / 100m, // Stripe amounts are in minor units.
            Currency = invoice.Currency,
            DueDate = invoice.DueDate,
            PaidUtc = stripeEvent.Type == "invoice.paid" ? stripeEvent.Created : null,
            FailureReason = stripeEvent.Type == "invoice.payment_failed" ? "payment_failed" : null,
        };
    }

    private static PaymentEventData? MapPayment(Event stripeEvent)
    {
        if (stripeEvent.Data.Object is not PaymentIntent paymentIntent)
        {
            return null;
        }

        return new PaymentEventData
        {
            ProviderPaymentIntentId = paymentIntent.Id,
            Amount = paymentIntent.Amount / 100m,
            Currency = paymentIntent.Currency,
            FailureReason = paymentIntent.LastPaymentError?.Message,
        };
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Stripe webhook signature verification failed.")]
    private static partial void LogSignatureFailure(ILogger logger, Exception exception);
}
