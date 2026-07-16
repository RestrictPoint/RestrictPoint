namespace RestrictPoint.Api.Marketplace.Contracts;

/// <summary>Request body for POST /v1/marketplace/listings.</summary>
public sealed record CreateListingRequest
{
    public Guid? ProjectId { get; init; }

    public Guid? OrganizationId { get; init; }

    public string? Title { get; init; }

    public string? Description { get; init; }

    public Guid? CategoryId { get; init; }

    public Guid? WebPartGuid { get; init; }

    public string? LogoUrl { get; init; }

    public string? SupportUrl { get; init; }

    public string? DocumentationUrl { get; init; }

    public IReadOnlyList<string>? Tags { get; init; }
}

/// <summary>Request body for POST /v1/marketplace/listings/{id}/pricing.</summary>
public sealed record AddPricingPlanRequest
{
    public string? Name { get; init; }

    /// <summary>Free | OneTimePurchase | MonthlySubscription | AnnualSubscription.</summary>
    public string? PricingType { get; init; }

    public decimal? Price { get; init; }

    /// <summary>ISO 4217 code, e.g. USD.</summary>
    public string? Currency { get; init; }

    /// <summary>Monthly | Annual. Required for subscription pricing types.</summary>
    public string? BillingInterval { get; init; }

    public int? TrialDays { get; init; }

    /// <summary>Serialized license template applied on purchase (docs/13 license integration).</summary>
    public string? LicenseTemplate { get; init; }
}

/// <summary>Request body for POST /v1/marketplace/listings/{id}/review.</summary>
public sealed record SubmitReviewRequest
{
    public int? Rating { get; init; }

    public string? Comment { get; init; }
}

/// <summary>Compact listing shape for catalog/search responses.</summary>
public sealed record ListingSummary
{
    public required Guid Id { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required string Title { get; init; }

    public required string Status { get; init; }

    public required Guid CategoryId { get; init; }

    public required bool IsFeatured { get; init; }

    public required int InstallCount { get; init; }

    public required decimal AverageRating { get; init; }

    public required int ReviewCount { get; init; }

    public string? LogoUrl { get; init; }
}

/// <summary>Full listing detail including pricing plans, tags, and recent reviews.</summary>
public sealed record ListingDetail
{
    public required Guid Id { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required string Title { get; init; }

    public required string Description { get; init; }

    public required string Status { get; init; }

    public required Guid CategoryId { get; init; }

    public required Guid WebPartGuid { get; init; }

    public required bool IsFeatured { get; init; }

    public required int InstallCount { get; init; }

    public required decimal AverageRating { get; init; }

    public required int ReviewCount { get; init; }

    public string? LogoUrl { get; init; }

    public string? SupportUrl { get; init; }

    public string? DocumentationUrl { get; init; }

    public required IReadOnlyList<PricingPlanSummary> PricingPlans { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    public required IReadOnlyList<ReviewSummary> RecentReviews { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }

    public required DateTimeOffset UpdatedUtc { get; init; }
}

/// <summary>Pricing plan shape returned to clients.</summary>
public sealed record PricingPlanSummary
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string PricingType { get; init; }

    public required decimal Price { get; init; }

    public required string Currency { get; init; }

    public string? BillingInterval { get; init; }

    public required int TrialDays { get; init; }

    public required bool IsActive { get; init; }
}

/// <summary>Review shape returned to clients. User identity is never exposed beyond an opaque id.</summary>
public sealed record ReviewSummary
{
    public required Guid Id { get; init; }

    public required int Rating { get; init; }

    public string? Comment { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}
