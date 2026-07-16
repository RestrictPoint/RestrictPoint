namespace RestrictPoint.Api.Billing.Contracts;

/// <summary>Response for POST /v1/billing/checkout (docs/16).</summary>
public sealed record CheckoutResponse
{
    public required Guid SubscriptionId { get; init; }

    public required string CheckoutUrl { get; init; }
}

/// <summary>Response for POST /v1/billing/stripe/connect.</summary>
public sealed record ConnectOnboardingResponse
{
    public required string OnboardingUrl { get; init; }
}

/// <summary>Subscription summary for list/cancel endpoints.</summary>
public sealed record SubscriptionSummary
{
    public required Guid Id { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid CustomerOrganizationId { get; init; }

    public required Guid DeveloperOrganizationId { get; init; }

    public required string Status { get; init; }

    public required string Plan { get; init; }

    public DateTimeOffset? CurrentPeriodStart { get; init; }

    public DateTimeOffset? CurrentPeriodEnd { get; init; }

    public required bool CancelAtPeriodEnd { get; init; }

    public Guid? LicenseId { get; init; }
}

/// <summary>Invoice summary for GET /v1/billing/invoices.</summary>
public sealed record InvoiceSummary
{
    public required Guid Id { get; init; }

    public required Guid SubscriptionId { get; init; }

    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string Status { get; init; }

    public DateTimeOffset? DueDate { get; init; }

    public DateTimeOffset? PaidUtc { get; init; }
}
