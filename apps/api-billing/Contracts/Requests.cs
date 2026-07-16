namespace RestrictPoint.Api.Billing.Contracts;

/// <summary>Request for POST /v1/billing/checkout.</summary>
public sealed record CreateCheckoutRequest
{
    public Guid? ProjectId { get; init; }

    public Guid? DeveloperOrganizationId { get; init; }

    public Guid? CustomerOrganizationId { get; init; }

    /// <summary>The customer SharePoint tenant the resulting license binds to.</summary>
    public Guid? CustomerTenantId { get; init; }

    /// <summary>Stripe price id for the plan.</summary>
    public string? PlanPriceId { get; init; }

    /// <summary>Display plan name, e.g. Monthly, Annual.</summary>
    public string? Plan { get; init; }

    public string? SuccessUrl { get; init; }

    public string? CancelUrl { get; init; }

    /// <summary>License template applied on activation: { licenseType, features, limits, webPartGuids }.</summary>
    public LicenseTemplateDto? LicenseTemplate { get; init; }
}

/// <summary>License template DTO (validated and serialized onto the subscription).</summary>
public sealed record LicenseTemplateDto
{
    public string? LicenseType { get; init; }

    public IReadOnlyDictionary<string, bool>? Features { get; init; }

    public IReadOnlyDictionary<string, int>? Limits { get; init; }

    public IReadOnlyList<Guid>? WebPartGuids { get; init; }
}

/// <summary>Request for POST /v1/billing/subscriptions/cancel.</summary>
public sealed record CancelSubscriptionRequest
{
    public Guid? SubscriptionId { get; init; }

    /// <summary>True (default): cancel at period end. False: cancel immediately.</summary>
    public bool CancelAtPeriodEnd { get; init; } = true;

    public string? Reason { get; init; }
}

/// <summary>Request for POST /v1/billing/stripe/connect.</summary>
public sealed record ConnectOnboardingRequest
{
    public Guid? DeveloperOrganizationId { get; init; }

    public string? ReturnUrl { get; init; }

    public string? RefreshUrl { get; init; }
}
