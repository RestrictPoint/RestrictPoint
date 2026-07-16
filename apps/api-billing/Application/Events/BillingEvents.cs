namespace RestrictPoint.Api.Billing.Application.Events;

/// <summary>Service Bus topics this service publishes to.</summary>
public static class Topics
{
    public const string Billing = "BillingEvents";
}

/// <summary>Event metadata constants.</summary>
public static class EventMetadata
{
    public const string Publisher = "billing";
    public const string Version10 = "1.0";
    public const string Version11 = "1.1";
}

/// <summary>SubscriptionCreated v1.0 (docs/20) — created, payment not yet confirmed.</summary>
public sealed record SubscriptionCreated
{
    public required Guid SubscriptionId { get; init; }

    public required Guid CustomerOrganizationId { get; init; }

    public required Guid DeveloperOrganizationId { get; init; }

    public required Guid ProjectId { get; init; }

    public string? StripeSubscriptionId { get; init; }

    public required string Plan { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}

/// <summary>
/// SubscriptionActivated v1.1 — payment satisfied; triggers license issuance. Extends the
/// catalog v1.0 payload with the license template and organization/tenant context so the
/// Licensing consumer can issue without a synchronous callback (docs/20: payloads are
/// self-contained). No v1.0 fields are removed.
/// </summary>
public sealed record SubscriptionActivated
{
    public required Guid SubscriptionId { get; init; }

    public required Guid CustomerOrganizationId { get; init; }

    public required Guid ProjectId { get; init; }

    public string? StripeSubscriptionId { get; init; }

    public required DateTimeOffset ActivatedUtc { get; init; }

    // v1.1 additions:
    public required Guid DeveloperOrganizationId { get; init; }

    public required Guid CustomerTenantId { get; init; }

    public DateTimeOffset? CurrentPeriodEnd { get; init; }

    /// <summary>Serialized <c>LicenseTemplate</c> JSON.</summary>
    public required string LicenseTemplate { get; init; }
}

/// <summary>SubscriptionCanceled v1.0 (docs/20).</summary>
public sealed record SubscriptionCanceled
{
    public required Guid SubscriptionId { get; init; }

    public required string CancellationReason { get; init; }

    public required DateTimeOffset EffectiveUtc { get; init; }

    public required DateTimeOffset CanceledUtc { get; init; }
}

/// <summary>SubscriptionPastDue v1.0 (docs/12 billing events) — grace period begins.</summary>
public sealed record SubscriptionPastDue
{
    public required Guid SubscriptionId { get; init; }

    public required DateTimeOffset PastDueUtc { get; init; }
}

/// <summary>SubscriptionRenewed v1.0 (docs/20).</summary>
public sealed record SubscriptionRenewed
{
    public required Guid SubscriptionId { get; init; }

    public required Guid InvoiceId { get; init; }

    public required string StripeInvoiceId { get; init; }

    public DateTimeOffset? RenewalPeriodStartUtc { get; init; }

    public DateTimeOffset? RenewalPeriodEndUtc { get; init; }

    public required DateTimeOffset RenewedUtc { get; init; }
}

/// <summary>PaymentSucceeded v1.0 (docs/20). Only Billing may publish this.</summary>
public sealed record PaymentSucceeded
{
    public required Guid PaymentId { get; init; }

    public required Guid SubscriptionId { get; init; }

    public required string StripePaymentIntentId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required DateTimeOffset PaidUtc { get; init; }
}

/// <summary>PaymentFailed v1.0 (docs/20) — may eventually suspend the license.</summary>
public sealed record PaymentFailed
{
    public required Guid PaymentId { get; init; }

    public required Guid SubscriptionId { get; init; }

    public required string FailureReason { get; init; }

    public DateTimeOffset? RetryScheduledUtc { get; init; }

    public required DateTimeOffset FailedUtc { get; init; }
}

/// <summary>InvoicePaid v1.0 (docs/12 billing events).</summary>
public sealed record InvoicePaid
{
    public required Guid InvoiceId { get; init; }

    public required Guid SubscriptionId { get; init; }

    public required string StripeInvoiceId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required DateTimeOffset PaidUtc { get; init; }
}
