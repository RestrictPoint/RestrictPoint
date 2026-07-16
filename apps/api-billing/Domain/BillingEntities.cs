using RestrictPoint.Database;

namespace RestrictPoint.Api.Billing.Domain;

public enum InvoiceStatus
{
    Open,
    Paid,
    Failed,
    Void,
}

/// <summary>An invoice mirrored from Stripe (docs/12 invoice model).</summary>
public sealed class Invoice : BaseEntity
{
    public required Guid CustomerOrganizationId { get; set; }

    public required Guid SubscriptionId { get; set; }

    /// <summary>Amount in the smallest currency unit is avoided: stored as decimal per docs/09.</summary>
    public required decimal Amount { get; set; }

    public required string Currency { get; set; }

    public InvoiceStatus Status { get; set; } = InvoiceStatus.Open;

    public required string StripeInvoiceId { get; set; }

    public DateTimeOffset? DueDate { get; set; }

    public DateTimeOffset? PaidUtc { get; set; }
}

public enum PaymentStatus
{
    Succeeded,
    Failed,
    Refunded,
}

/// <summary>A payment attempt mirrored from Stripe (docs/12 payment model).</summary>
public sealed class Payment : BaseEntity
{
    public required Guid SubscriptionId { get; set; }

    public Guid? InvoiceId { get; set; }

    public required decimal Amount { get; set; }

    public required string Currency { get; set; }

    public required PaymentStatus Status { get; set; }

    public required string StripePaymentIntentId { get; set; }

    public required DateTimeOffset ProcessedUtc { get; set; }

    public string? FailureReason { get; set; }
}

/// <summary>
/// Processed Stripe webhook events. The unique StripeEventId constraint is the idempotency
/// guarantee (docs/12): a redelivered event inserts a duplicate key and is skipped.
/// </summary>
public sealed class ProcessedWebhookEvent
{
    public const int StripeEventIdMaxLength = 256;
    public const int EventTypeMaxLength = 128;

    public Guid Id { get; set; } = Guid.NewGuid();

    public required string StripeEventId { get; set; }

    public required string EventType { get; set; }

    public required DateTimeOffset ProcessedUtc { get; set; }
}
