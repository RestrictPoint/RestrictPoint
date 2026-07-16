using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Marketplace.Application.Common;
using RestrictPoint.Api.Marketplace.Application.Events;
using RestrictPoint.Api.Marketplace.Contracts;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Marketplace.Application.PublishListing;

/// <summary>
/// POST /v1/marketplace/listings/{id}/publish — transitions Draft/Suspended → Published.
/// Requires Owner or Admin role in the owning organization; the listing must carry at
/// least one active pricing plan (docs/13). Non-members receive 404 (no existence disclosure).
/// </summary>
public sealed class PublishListingHandler
{
    private static readonly string[] PublishingRoles = ["Owner", "Admin"];

    private readonly MarketplaceDbContext _dbContext;
    private readonly IOrganizationRoleResolver _roleResolver;
    private readonly IOutboxWriter _outbox;
    private readonly TimeProvider _timeProvider;

    public PublishListingHandler(
        MarketplaceDbContext dbContext,
        IOrganizationRoleResolver roleResolver,
        IOutboxWriter outbox,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _roleResolver = roleResolver;
        _outbox = outbox;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ListingSummary>> HandleAsync(
        RequestContext context,
        string bearerToken,
        Guid listingId,
        CancellationToken cancellationToken)
    {
        var listing = await _dbContext.Listings
            .Include(l => l.PricingPlans)
            .SingleOrDefaultAsync(l => l.Id == listingId, cancellationToken)
            .ConfigureAwait(false);

        if (listing is null)
        {
            return MarketplaceErrors.ListingNotFound;
        }

        var role = await _roleResolver
            .GetCallerRoleAsync(bearerToken, listing.OrganizationId, cancellationToken)
            .ConfigureAwait(false);

        if (role.IsFailure)
        {
            return role.Error!;
        }

        if (role.Value is null)
        {
            return MarketplaceErrors.ListingNotFound; // No existence disclosure to non-members.
        }

        if (!PublishingRoles.Contains(role.Value, StringComparer.OrdinalIgnoreCase))
        {
            return MarketplaceErrors.NotAuthorizedForOrganization;
        }

        if (!ListingStateMachine.CanTransition(listing.Status, ListingStatus.Published))
        {
            return MarketplaceErrors.InvalidStateTransition;
        }

        if (!listing.PricingPlans.Any(p => p.IsActive))
        {
            return MarketplaceErrors.CannotPublishWithoutPricing;
        }

        listing.Publish();

        _outbox.Stage(
            Topics.Marketplace,
            DomainEventEnvelope.Create(
                eventType: nameof(MarketplaceListingPublished),
                eventVersion: EventMetadata.Version10,
                publisher: EventMetadata.Publisher,
                correlationId: context.CorrelationId,
                organizationId: listing.OrganizationId,
                payload: new MarketplaceListingPublished
                {
                    ListingId = listing.Id,
                    ProjectId = listing.ProjectId,
                    OrganizationId = listing.OrganizationId,
                    PublishedByUserId = context.CallerUserId(),
                    PublishedUtc = _timeProvider.GetUtcNow(),
                },
                timeProvider: _timeProvider));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ListingMapper.ToSummary(listing);
    }
}
