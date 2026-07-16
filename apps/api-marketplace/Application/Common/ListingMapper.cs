using RestrictPoint.Api.Marketplace.Contracts;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Common;

namespace RestrictPoint.Api.Marketplace.Application.Common;

/// <summary>Maps domain entities to response contracts.</summary>
public static class ListingMapper
{
    public static ListingSummary ToSummary(Listing listing) => new()
    {
        Id = listing.Id,
        ProjectId = listing.ProjectId,
        OrganizationId = listing.OrganizationId,
        Title = listing.Title,
        Status = listing.Status.ToString(),
        CategoryId = listing.CategoryId,
        IsFeatured = listing.IsFeatured,
        InstallCount = listing.InstallCount,
        AverageRating = listing.AverageRating,
        ReviewCount = listing.ReviewCount,
        LogoUrl = listing.LogoUrl,
    };

    public static ListingDetail ToDetail(
        Listing listing,
        IReadOnlyList<string> tags,
        IReadOnlyList<Review> recentReviews) => new()
    {
        Id = listing.Id,
        ProjectId = listing.ProjectId,
        OrganizationId = listing.OrganizationId,
        Title = listing.Title,
        Description = listing.Description,
        Status = listing.Status.ToString(),
        CategoryId = listing.CategoryId,
        WebPartGuid = listing.WebPartGuid,
        IsFeatured = listing.IsFeatured,
        InstallCount = listing.InstallCount,
        AverageRating = listing.AverageRating,
        ReviewCount = listing.ReviewCount,
        LogoUrl = listing.LogoUrl,
        SupportUrl = listing.SupportUrl,
        DocumentationUrl = listing.DocumentationUrl,
        PricingPlans = listing.PricingPlans
            .Where(p => p.IsActive)
            .Select(ToPricingSummary)
            .ToList(),
        Tags = tags,
        RecentReviews = recentReviews.Select(ToReviewSummary).ToList(),
        CreatedUtc = listing.CreatedUtc,
        UpdatedUtc = listing.UpdatedUtc,
    };

    public static PricingPlanSummary ToPricingSummary(PricingPlan plan) => new()
    {
        Id = plan.Id,
        Name = plan.Name,
        PricingType = plan.PricingType.ToString(),
        Price = plan.Price,
        Currency = plan.Currency,
        BillingInterval = plan.BillingInterval?.ToString(),
        TrialDays = plan.TrialDays,
        IsActive = plan.IsActive,
    };

    public static ReviewSummary ToReviewSummary(Review review) => new()
    {
        Id = review.Id,
        Rating = review.Rating,
        Comment = review.Comment,
        CreatedUtc = review.CreatedUtc,
    };
}

/// <summary>Caller identity helpers.</summary>
public static class RequestContextExtensions
{
    /// <summary>
    /// The caller's stable user id. Entra object ids are GUIDs; an unparseable value
    /// indicates a token defect and yields <see cref="Guid.Empty"/> (rejected by callers
    /// that require a user identity).
    /// </summary>
    public static Guid CallerUserId(this RequestContext context) =>
        context.UserId
        ?? (Guid.TryParse(context.ExternalObjectId, out var objectId) ? objectId : Guid.Empty);
}
