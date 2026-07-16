using RestrictPoint.Common;

namespace RestrictPoint.Api.Billing.Application.Abstractions;

/// <summary>Provider-agnostic webhook event, normalized from the Stripe payload.</summary>
public sealed record WebhookEvent
{
    public required string EventId { get; init; }

    /// <summary>Stripe event type, e.g. <c>customer.subscription.updated</c>.</summary>
    public required string EventType { get; init; }

    public SubscriptionEventData? Subscription { get; init; }

    public InvoiceEventData? Invoice { get; init; }

    public PaymentEventData? Payment { get; init; }
}

public sealed record SubscriptionEventData
{
    public required string ProviderSubscriptionId { get; init; }

    /// <summary>Stripe subscription status, e.g. active, past_due, canceled, trialing.</summary>
    public required string ProviderStatus { get; init; }

    /// <summary>Our subscription id, from Stripe metadata["subscriptionId"] set at checkout.</summary>
    public Guid? LocalSubscriptionId { get; init; }

    public string? ProviderCustomerId { get; init; }

    public DateTimeOffset? CurrentPeriodStart { get; init; }

    public DateTimeOffset? CurrentPeriodEnd { get; init; }

    public bool CancelAtPeriodEnd { get; init; }
}

public sealed record InvoiceEventData
{
    public required string ProviderInvoiceId { get; init; }

    public required string ProviderSubscriptionId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    /// <summary>The payment intent that settled (or failed to settle) this invoice.</summary>
    public string? ProviderPaymentIntentId { get; init; }

    public string? FailureReason { get; init; }

    public DateTimeOffset? DueDate { get; init; }

    public DateTimeOffset? PaidUtc { get; init; }
}

public sealed record PaymentEventData
{
    public required string ProviderPaymentIntentId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public string? FailureReason { get; init; }
}

/// <summary>
/// Verifies webhook authenticity and normalizes payloads. Signature verification is
/// mandatory (docs/12 security) — unsigned or tampered payloads never reach handlers.
/// </summary>
public interface IWebhookVerifier
{
    Result<WebhookEvent> VerifyAndParse(string payload, string signatureHeader);
}
