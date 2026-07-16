using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestrictPoint.Api.Marketplace.Application.CreateListing;
using RestrictPoint.Api.Marketplace.Application.PublishListing;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Common;
using RestrictPoint.Tests.Shared;

namespace RestrictPoint.Tests.Marketplace.Application;

public sealed class PublishListingHandlerTests : IDisposable
{
    private readonly MarketplaceDbContext _db;
    private readonly TestOutboxWriter _outbox;
    private readonly StubOrganizationRoleResolver _roleResolver;
    private readonly PublishListingHandler _handler;

    public PublishListingHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new MarketplaceDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _outbox = new TestOutboxWriter();
        _roleResolver = new StubOrganizationRoleResolver();
        _handler = new PublishListingHandler(_db, _roleResolver, _outbox, NullLogger<PublishListingHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidListingAndPricing_PublishesListing()
    {
        var organizationId = Guid.NewGuid();
        var listing = await SeedDraftListingWithPricingAsync(organizationId);
        var principal = new RestrictPointPrincipal(Guid.NewGuid(), "test@example.com", organizationId);

        _roleResolver.SetRole(OrganizationRole.Owner);

        var result = await ExecuteHandlerAsync(principal, listing.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("Published");

        var updated = await _db.Listings.FindAsync(listing.Id);
        updated!.Status.Should().Be(ListingStatus.Published);

        _outbox.Messages.Should().ContainSingle();
        _outbox.Messages[0].EventType.Should().Be("ListingPublished");
    }

    [Fact]
    public async Task ExecuteAsync_WithoutPricingPlan_ReturnsFailure()
    {
        var organizationId = Guid.NewGuid();
        var listing = await SeedDraftListingAsync(organizationId);
        var principal = new RestrictPointPrincipal(Guid.NewGuid(), "test@example.com", organizationId);

        _roleResolver.SetRole(OrganizationRole.Owner);

        var result = await ExecuteHandlerAsync(principal, listing.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("PublishFailed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotOwner_ReturnsNotListingOwner()
    {
        var organizationId = Guid.NewGuid();
        var listing = await SeedDraftListingWithPricingAsync(organizationId);
        var principal = new RestrictPointPrincipal(Guid.NewGuid(), "test@example.com", Guid.NewGuid()); // Different org

        _roleResolver.SetRole(OrganizationRole.Member);

        var result = await ExecuteHandlerAsync(principal, listing.Id);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MarketplaceErrors.NotListingOwner);
    }

    [Fact]
    public async Task ExecuteAsync_WhenListingNotFound_ReturnsListingNotFound()
    {
        var principal = new RestrictPointPrincipal(Guid.NewGuid(), "test@example.com", Guid.NewGuid());

        _roleResolver.SetRole(OrganizationRole.Owner);

        var result = await ExecuteHandlerAsync(principal, Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MarketplaceErrors.ListingNotFound);
    }

    private async Task<Listing> SeedDraftListingAsync(Guid organizationId)
    {
        var category = Category.Create("Productivity", null, 1);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var listing = Listing.Create(
            Guid.NewGuid(),
            organizationId,
            "Test Listing",
            "Description",
            category.Id,
            Guid.NewGuid());

        _db.Listings.Add(listing);
        await _db.SaveChangesAsync();
        return listing;
    }

    private async Task<Listing> SeedDraftListingWithPricingAsync(Guid organizationId)
    {
        var listing = await SeedDraftListingAsync(organizationId);

        var plan = PricingPlan.Create(
            listing.Id,
            "Standard",
            PricingType.MonthlySubscription,
            29.99m,
            "USD",
            BillingInterval.Monthly,
            14,
            null);

        _db.PricingPlans.Add(plan);
        await _db.SaveChangesAsync();

        return listing;
    }

    private Task<Result<ListingDto>> ExecuteHandlerAsync(RestrictPointPrincipal principal, Guid listingId)
    {
        var method = _handler.GetType().GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (Task<Result<ListingDto>>)method!.Invoke(_handler, [principal, listingId, CancellationToken.None])!;
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
