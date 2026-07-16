using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Marketplace.Application.PublishListing;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using Xunit;

namespace RestrictPoint.Api.Marketplace.Tests.Application;

public sealed class PublishListingHandlerTests : IDisposable
{
    private const string BearerToken = "test-token";

    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;
    private readonly StubRoleResolver _roles = new();
    private readonly Guid _organizationId = Guid.NewGuid();

    public PublishListingHandlerTests()
    {
        _database = new TestDatabase(_time);
    }

    public void Dispose() => _database.Dispose();

    private static RequestContext Context() => new()
    {
        CorrelationId = "corr-publish",
        ExternalObjectId = Guid.NewGuid().ToString(),
    };

    private PublishListingHandler CreateHandler(MarketplaceDbContext context) =>
        new(context, _roles, new OutboxWriter(context), _time);

    private Guid SeedDraftListing(bool withPricing)
    {
        using var context = _database.CreateContext();
        var listing = Listing.Create(
            Guid.NewGuid(), _organizationId, "Dashboard", "Description",
            Guid.NewGuid(), Guid.NewGuid());

        if (withPricing)
        {
            context.PricingPlans.Add(PricingPlan.Create(
                listing.Id, "Standard", PricingType.MonthlySubscription, 9.99m, "USD",
                BillingInterval.Monthly, 14, null));
        }

        context.Listings.Add(listing);
        context.SaveChanges();
        return listing.Id;
    }

    [Fact]
    public async Task Admin_publishes_draft_listing_with_pricing()
    {
        _roles.SetRole(_organizationId, "Admin");
        var listingId = SeedDraftListing(withPricing: true);
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, listingId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Published", result.Value.Status);

        using var verification = _database.CreateContext();
        Assert.Equal(1, await verification.OutboxMessages
            .CountAsync(m => m.EventType == "MarketplaceListingPublished"));
    }

    [Fact]
    public async Task Publish_without_active_pricing_plan_is_rejected()
    {
        _roles.SetRole(_organizationId, "Owner");
        var listingId = SeedDraftListing(withPricing: false);
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, listingId, CancellationToken.None);

        Assert.Equal(MarketplaceErrors.CannotPublishWithoutPricing.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Non_member_receives_not_found()
    {
        var listingId = SeedDraftListing(withPricing: true);
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, listingId, CancellationToken.None);

        // No existence disclosure to non-members.
        Assert.Equal(MarketplaceErrors.ListingNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Developer_role_cannot_publish()
    {
        _roles.SetRole(_organizationId, "Developer");
        var listingId = SeedDraftListing(withPricing: true);
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, listingId, CancellationToken.None);

        Assert.Equal(MarketplaceErrors.NotAuthorizedForOrganization.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Unknown_listing_returns_not_found()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(MarketplaceErrors.ListingNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Already_published_listing_cannot_be_republished()
    {
        _roles.SetRole(_organizationId, "Owner");
        var listingId = SeedDraftListing(withPricing: true);

        using (var context = _database.CreateContext())
        {
            await CreateHandler(context).HandleAsync(
                Context(), BearerToken, listingId, CancellationToken.None);
        }

        using var second = _database.CreateContext();
        var result = await CreateHandler(second).HandleAsync(
            Context(), BearerToken, listingId, CancellationToken.None);

        Assert.Equal(MarketplaceErrors.InvalidStateTransition.Code, result.Error!.Code);
    }
}
