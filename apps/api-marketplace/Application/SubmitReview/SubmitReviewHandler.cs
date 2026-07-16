using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Marketplace.Application.Common;
using RestrictPoint.Api.Marketplace.Contracts;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Common;

namespace RestrictPoint.Api.Marketplace.Application.SubmitReview;

/// <summary>Input validation for POST /v1/marketplace/listings/{id}/review.</summary>
public sealed class SubmitReviewRequestValidator : AbstractValidator<SubmitReviewRequest>
{
    public SubmitReviewRequestValidator()
    {
        RuleFor(r => r.Rating).NotNull().InclusiveBetween(1, 5);
        RuleFor(r => r.Comment).MaximumLength(4000);
    }
}

/// <summary>
/// POST /v1/marketplace/listings/{id}/review — submits a review on a published listing.
/// Rules (docs/13): one review per user per listing, rating 1–5, and members of the owning
/// organization cannot review their own listing (fraud prevention).
/// </summary>
public sealed class SubmitReviewHandler
{
    private readonly MarketplaceDbContext _dbContext;
    private readonly IOrganizationRoleResolver _roleResolver;

    public SubmitReviewHandler(MarketplaceDbContext dbContext, IOrganizationRoleResolver roleResolver)
    {
        _dbContext = dbContext;
        _roleResolver = roleResolver;
    }

    public async Task<Result<ReviewSummary>> HandleAsync(
        RequestContext context,
        string bearerToken,
        Guid listingId,
        SubmitReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userId = context.CallerUserId();
        if (userId == Guid.Empty)
        {
            return MarketplaceErrors.MissingUserIdentity;
        }

        var listing = await _dbContext.Listings
            .Include(l => l.Reviews)
            .SingleOrDefaultAsync(l => l.Id == listingId, cancellationToken)
            .ConfigureAwait(false);

        if (listing is null)
        {
            return MarketplaceErrors.ListingNotFound;
        }

        if (listing.Status != ListingStatus.Published)
        {
            return MarketplaceErrors.ListingNotPublished;
        }

        // Fraud prevention: members of the owning organization cannot review their own listing.
        var role = await _roleResolver
            .GetCallerRoleAsync(bearerToken, listing.OrganizationId, cancellationToken)
            .ConfigureAwait(false);

        if (role.IsFailure)
        {
            return role.Error!;
        }

        if (role.Value is not null)
        {
            return MarketplaceErrors.CannotReviewOwnListing;
        }

        if (listing.Reviews.Any(r => r.UserId == userId))
        {
            return MarketplaceErrors.ReviewAlreadyExists;
        }

        Review review;
        try
        {
            review = Review.Create(listing.Id, userId, request.Rating!.Value, request.Comment);
        }
        catch (ArgumentException exception)
        {
            return Error.Validation("Marketplace.InvalidReview", exception.Message);
        }

        // Explicit Add: entities discovered via navigation from a tracked parent would be
        // marked Modified (client-set GUID key), producing an UPDATE instead of an INSERT.
        // EF navigation fixup places the review into listing.Reviews before recalculation.
        _dbContext.Reviews.Add(review);
        listing.RecalculateRating();

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ListingMapper.ToReviewSummary(review);
    }
}
