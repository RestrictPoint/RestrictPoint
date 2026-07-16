using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Marketplace.Application.SubmitReview;
using RestrictPoint.Api.Marketplace.Contracts;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Common;
using Xunit;

namespace RestrictPoint.Api.Marketplace.Tests.Application;

public sealed class SubmitReviewHandlerTests : IDisposable
{
    private const string BearerToken = "test-token";

    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;
    private readonly StubRoleResolver _roles = new();
    private readonly Guid _organizationId = Guid.NewGuid();

    public SubmitReviewHandlerTests()
    {
        _database = new TestDatabase(_time);
    }

    public void Dispose() => _database.Dispose();

    private static RequestContext Context(Guid? userId = null) => new()
    {
        CorrelationId = "corr-review",
        ExternalObjectId = (userId ?? Guid.NewGuid()).ToString(),
    };

    private SubmitReviewHandler CreateHandler(MarketplaceDbContext context) =>
        new(context, _roles);

    private Guid SeedListing(ListingStatus status = ListingStatus.Published)
    {
        using var context = _database.CreateContext();
        var listing = Listing.Create(
            Guid.NewGuid(), _organizationId, "Dashboard", "Description",
            Guid.NewGuid(), Guid.NewGuid());

        if (status == ListingStatus.Published)
        {
            listing.PricingPlans.Add(PricingPlan.Create(
                listing.Id, "Standard", PricingType.Free, 0, "USD", null, 0, null));
            listing.Publish();
        }

        context.Listings.Add(listing);
        context.SaveChanges();
        return listing.Id;
    }

    private static SubmitReviewRequest Request(int rating = 5, string? comment = "Great") =>
        new() { Rating = rating, Comment = comment };

    [Fact]
    public async Task Customer_reviews_published_listing_and_rating_recalculates()
    {
        var listingId = SeedListing();
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, listingId, Request(4, "Solid"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.Rating);

        using var verification = _database.CreateContext();
        var listing = await verification.Listings.SingleAsync();
        Assert.Equal(4m, listing.AverageRating);
        Assert.Equal(1, listing.ReviewCount);
    }

    [Fact]
    public async Task Rating_averages_across_multiple_reviewers()
    {
        var listingId = SeedListing();

        using (var context = _database.CreateContext())
        {
            await CreateHandler(context).HandleAsync(
                Context(), BearerToken, listingId, Request(5), CancellationToken.None);
        }

        using (var context = _database.CreateContext())
        {
            await CreateHandler(context).HandleAsync(
                Context(), BearerToken, listingId, Request(3), CancellationToken.None);
        }

        using var verification = _database.CreateContext();
        var listing = await verification.Listings.SingleAsync();
        Assert.Equal(4m, listing.AverageRating);
        Assert.Equal(2, listing.ReviewCount);
    }

    [Fact]
    public async Task Unpublished_listing_cannot_be_reviewed()
    {
        var listingId = SeedListing(ListingStatus.Draft);
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, listingId, Request(), CancellationToken.None);

        Assert.Equal(MarketplaceErrors.ListingNotPublished.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Organization_member_cannot_review_own_listing()
    {
        _roles.SetRole(_organizationId, "Developer");
        var listingId = SeedListing();
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, listingId, Request(), CancellationToken.None);

        Assert.Equal(MarketplaceErrors.CannotReviewOwnListing.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Duplicate_review_by_same_user_is_rejected()
    {
        var listingId = SeedListing();
        var userId = Guid.NewGuid();

        using (var context = _database.CreateContext())
        {
            var first = await CreateHandler(context).HandleAsync(
                Context(userId), BearerToken, listingId, Request(), CancellationToken.None);
            Assert.True(first.IsSuccess);
        }

        using var second = _database.CreateContext();
        var result = await CreateHandler(second).HandleAsync(
            Context(userId), BearerToken, listingId, Request(), CancellationToken.None);

        Assert.Equal(MarketplaceErrors.ReviewAlreadyExists.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Unknown_listing_returns_not_found()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, Guid.NewGuid(), Request(), CancellationToken.None);

        Assert.Equal(MarketplaceErrors.ListingNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Caller_without_resolvable_identity_is_rejected()
    {
        var listingId = SeedListing();
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            new RequestContext { CorrelationId = "corr", ExternalObjectId = "not-a-guid" },
            BearerToken, listingId, Request(), CancellationToken.None);

        Assert.Equal(MarketplaceErrors.MissingUserIdentity.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Identity_outage_fails_closed()
    {
        _roles.FailNextCall = true;
        var listingId = SeedListing();
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, listingId, Request(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, await context.Reviews.CountAsync());
    }
}
