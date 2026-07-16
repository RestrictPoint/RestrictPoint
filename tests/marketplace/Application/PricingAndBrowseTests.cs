using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Marketplace.Application.BrowseListings;
using RestrictPoint.Api.Marketplace.Application.ManagePricing;
using RestrictPoint.Api.Marketplace.Contracts;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using Xunit;

namespace RestrictPoint.Api.Marketplace.Tests.Application;

public sealed class AddPricingPlanHandlerTests : IDisposable
{
    private const string BearerToken = "test-token";

    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;
    private readonly StubRoleResolver _roles = new();
    private readonly Guid _organizationId = Guid.NewGuid();

    public AddPricingPlanHandlerTests()
    {
        _database = new TestDatabase(_time);
    }

    public void Dispose() => _database.Dispose();

    private static RequestContext Context() => new()
    {
        CorrelationId = "corr-pricing",
        ExternalObjectId = Guid.NewGuid().ToString(),
    };

    private AddPricingPlanHandler CreateHandler(MarketplaceDbContext context) =>
        new(context, _roles, new OutboxWriter(context), _time);

    private Guid SeedDraftListing()
    {
        using var context = _database.CreateContext();
        var listing = Listing.Create(
            Guid.NewGuid(), _organizationId, "Dashboard", "Description",
            Guid.NewGuid(), Guid.NewGuid());
        context.Listings.Add(listing);
        context.SaveChanges();
        return listing.Id;
    }

    private static AddPricingPlanRequest Request() => new()
    {
        Name = "Standard",
        PricingType = "MonthlySubscription",
        Price = 9.99m,
        Currency = "USD",
        BillingInterval = "Monthly",
        TrialDays = 14,
    };

    [Fact]
    public async Task Developer_adds_pricing_plan_with_event()
    {
        _roles.SetRole(_organizationId, "Developer");
        var listingId = SeedDraftListing();
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, listingId, Request(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("MonthlySubscription", result.Value.PricingType);
        Assert.True(result.Value.IsActive);

        using var verification = _database.CreateContext();
        Assert.Equal(1, await verification.PricingPlans.CountAsync());
        Assert.Equal(1, await verification.OutboxMessages
            .CountAsync(m => m.EventType == "PricingModelCreated"));
    }

    [Fact]
    public async Task Domain_invariant_violation_returns_validation_error()
    {
        _roles.SetRole(_organizationId, "Owner");
        var listingId = SeedDraftListing();
        using var context = _database.CreateContext();

        // Free pricing with a non-zero price violates the domain invariant.
        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, listingId,
            Request() with { PricingType = "Free", Price = 5m, BillingInterval = null },
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorKind.Validation, result.Error!.Kind);
        Assert.Equal(0, await context.PricingPlans.CountAsync());
    }

    [Fact]
    public async Task Non_member_receives_not_found()
    {
        var listingId = SeedDraftListing();
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, listingId, Request(), CancellationToken.None);

        Assert.Equal(MarketplaceErrors.ListingNotFound.Code, result.Error!.Code);
    }
}

public sealed class BrowseListingsHandlerTests : IDisposable
{
    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;

    public BrowseListingsHandlerTests()
    {
        _database = new TestDatabase(_time);
    }

    public void Dispose() => _database.Dispose();

    private Listing SeedListing(
        string title,
        ListingStatus status = ListingStatus.Published,
        bool featured = false,
        string? tag = null,
        Guid? categoryId = null)
    {
        using var context = _database.CreateContext();
        var listing = Listing.Create(
            Guid.NewGuid(), Guid.NewGuid(), title, $"Description for {title}",
            categoryId ?? Guid.NewGuid(), Guid.NewGuid());

        if (status is ListingStatus.Published or ListingStatus.Deprecated or ListingStatus.Suspended)
        {
            listing.PricingPlans.Add(PricingPlan.Create(
                listing.Id, "Standard", PricingType.Free, 0, "USD", null, 0, null));
            listing.Publish();

            if (status == ListingStatus.Deprecated)
            {
                listing.Deprecate();
            }
            else if (status == ListingStatus.Suspended)
            {
                listing.Suspend("test");
            }

            if (featured && status == ListingStatus.Published)
            {
                listing.MarkAsFeatured();
            }
        }

        if (tag is not null)
        {
            var tagEntity = Tag.Create(tag);
            context.Tags.Add(tagEntity);
            listing.Tags.Add(new ListingTag
            {
                ListingId = listing.Id,
                TagId = tagEntity.Id,
                Listing = listing,
                Tag = tagEntity,
            });
        }

        context.Listings.Add(listing);
        context.SaveChanges();
        return listing;
    }

    [Fact]
    public async Task List_returns_only_visible_statuses()
    {
        SeedListing("Published App");
        SeedListing("Deprecated App", ListingStatus.Deprecated);
        SeedListing("Draft App", ListingStatus.Draft);
        SeedListing("Suspended App", ListingStatus.Suspended);

        using var context = _database.CreateContext();
        var result = await new BrowseListingsHandler(context)
            .ListAsync(new BrowseListingsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.DoesNotContain(result.Value, l => l.Title == "Draft App");
        Assert.DoesNotContain(result.Value, l => l.Title == "Suspended App");
    }

    [Fact]
    public async Task Featured_listings_rank_first()
    {
        SeedListing("Organic App");
        SeedListing("Featured App", featured: true);

        using var context = _database.CreateContext();
        var result = await new BrowseListingsHandler(context)
            .ListAsync(new BrowseListingsQuery(), CancellationToken.None);

        Assert.Equal("Featured App", result.Value[0].Title);
    }

    [Fact]
    public async Task Tag_filter_narrows_results()
    {
        SeedListing("Tagged App", tag: "Dashboard");
        SeedListing("Other App");

        using var context = _database.CreateContext();
        var result = await new BrowseListingsHandler(context)
            .ListAsync(new BrowseListingsQuery { Tag = "Dashboard" }, CancellationToken.None);

        var summary = Assert.Single(result.Value);
        Assert.Equal("Tagged App", summary.Title);
    }

    [Fact]
    public async Task Get_returns_detail_with_pricing_and_hides_invisible_listings()
    {
        var published = SeedListing("Visible App");
        var draft = SeedListing("Hidden App", ListingStatus.Draft);

        using var context = _database.CreateContext();
        var handler = new BrowseListingsHandler(context);

        var visible = await handler.GetAsync(published.Id, CancellationToken.None);
        Assert.True(visible.IsSuccess);
        Assert.Single(visible.Value.PricingPlans);

        var hidden = await handler.GetAsync(draft.Id, CancellationToken.None);
        Assert.Equal(MarketplaceErrors.ListingNotFound.Code, hidden.Error!.Code);
    }

    [Fact]
    public async Task Search_matches_title_and_description()
    {
        SeedListing("Analytics Dashboard");
        SeedListing("CRM Connector");

        using var context = _database.CreateContext();
        var handler = new BrowseListingsHandler(context);

        var byTitle = await handler.SearchAsync("Analytics", null, null, CancellationToken.None);
        var match = Assert.Single(byTitle.Value);
        Assert.Equal("Analytics Dashboard", match.Title);

        var byDescription = await handler.SearchAsync("Description for CRM", null, null, CancellationToken.None);
        Assert.Single(byDescription.Value);
    }

    [Fact]
    public async Task Search_excludes_deprecated_listings()
    {
        SeedListing("Old Analytics", ListingStatus.Deprecated);

        using var context = _database.CreateContext();
        var result = await new BrowseListingsHandler(context)
            .SearchAsync("Analytics", null, null, CancellationToken.None);

        Assert.Empty(result.Value);
    }
}
