namespace RestrictPoint.Api.Marketplace.Application.Events;

/// <summary>Service Bus topics this service publishes to.</summary>
public static class Topics
{
    public const string Marketplace = "MarketplaceEvents";
}

/// <summary>Event metadata constants.</summary>
public static class EventMetadata
{
    public const string Publisher = "marketplace";
    public const string Version10 = "1.0";
}

/// <summary>MarketplaceListingCreated v1.0 (docs/20) — listing created, not yet public.</summary>
public sealed record MarketplaceListingCreated
{
    public required Guid ListingId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required Guid CreatedByUserId { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}

/// <summary>MarketplaceListingPublished v1.0 (docs/20) — listing publicly available.</summary>
public sealed record MarketplaceListingPublished
{
    public required Guid ListingId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required Guid PublishedByUserId { get; init; }

    public required DateTimeOffset PublishedUtc { get; init; }
}

/// <summary>PricingModelCreated v1.0 (docs/20) — a pricing plan was created for a listing.</summary>
public sealed record PricingModelCreated
{
    public required Guid PricingModelId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required string PricingType { get; init; }

    public required DateTimeOffset CreatedUtc { get; init; }
}
