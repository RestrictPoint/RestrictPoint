using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestrictPoint.Api.Marketplace.Application.CreateReview;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Common;
using RestrictPoint.Tests.Shared;

namespace RestrictPoint.Tests.Marketplace.Application;

public sealed class CreateReviewHandlerTests : IDisposable
{
    private readonly MarketplaceDbContext _db;
    private readonly TestOutboxWriter _outbox;
    private readonly CreateReviewHandler _handler;

    public CreateReviewHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new MarketplaceDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _outbox = new TestOutboxWriter();
        _handler = new CreateReviewHandler(_db, _outbox, NullLogger<CreateReviewHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidData_CreatesReview()
    {
        var listing = await SeedPublishedListingAsync();
        var userId = Guid.NewGuid();
        var principal = new RestrictPointPrincipal(userId, "test@example.com", Guid.NewGuid()); // Different org

        var request = new CreateReviewRequest(5, "Excellent web part!");

        var result = await ExecuteHandlerAsync(principal, listing.Id, request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rating.Should().Be(5);
        result.Value.Comment.Should().Be("Excellent web part!");

        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.ListingId == listing.Id);
        review.Should().NotBeNull();
        review!.UserId.Should().Be(userId);

        _outbox.Messages.Should().ContainSingle();
        _outbox.Messages[0].EventType.Should().Be("ReviewCreated");
    }

    [Fact]
    public async Task ExecuteAsync_WhenListingNotPublished_ReturnsListingNotPublished()
    {
        var listing = await SeedDraftListingAsync();
        var principal = new RestrictPointPrincipal(Guid.NewGuid(), "test@example.com", Guid.NewGuid());

        var request = new CreateReviewRequest(5, "Good");

        var result = await ExecuteHandlerAsync(principal, listing.Id, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MarketplaceErrors.ListingNotPublished);
    }

    [Fact]
    public async Task ExecuteAsync_WhenReviewingOwnListing_ReturnsCannotReviewOwnListing()
    {
        var organizationId = Guid.NewGuid();
        var listing = await SeedPublishedListingAsync(organizationId);
        var principal = new RestrictPointPrincipal(Guid.NewGuid(), "test@example.com", organizationId); // Same org

        var request = new CreateReviewRequest(5, "Self-review attempt");

        var result = await ExecuteHandlerAsync(principal, listing.Id, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MarketplaceErrors.CannotReviewOwnListing);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDuplicateReview_ReturnsReviewAlreadyExists()
    {
        var listing = await SeedPublishedListingAsync();
        var userId = Guid.NewGuid();
        var principal = new RestrictPointPrincipal(userId, "test@example.com", Guid.NewGuid());

        // Create first review
        var existingReview = Review.Create(listing.Id, userId, 4, "First review");
        _db.Reviews.Add(existingReview);
        await _db.SaveChangesAsync();

        // Try to create duplicate
        var request = new CreateReviewRequest(5, "Second review");

        var result = await ExecuteHandlerAsync(principal, listing.Id, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MarketplaceErrors.ReviewAlreadyExists);
    }

    [Fact]
    public async Task ExecuteAsync_RecalculatesListingRating()
    {
        var listing = await SeedPublishedListingAsync();
        
        // Add first review
        var review1 = Review.Create(listing.Id, Guid.NewGuid(), 5, "Great!");
        _db.Reviews.Add(review1);
        await _db.SaveChangesAsync();

        // Add second review via handler
        var principal = new RestrictPointPrincipal(Guid.NewGuid(), "test@example.com", Guid.NewGuid());
        var request = new CreateReviewRequest(3, "OK");

        await ExecuteHandlerAsync(principal, listing.Id, request);

        var updated = await _db.Listings
            .Include(l => l.Reviews)
            .FirstAsync(l => l.Id == listing.Id);

        updated.ReviewCount.Should().Be(2);
        updated.AverageRating.Should().Be(4m); // (5+3)/2 = 4
    }

    private async Task<Listing> SeedDraftListingAsync(Guid? organizationId = null)
    {
        var category = Category.Create("Productivity", null, 1);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var listing = Listing.Create(
            Guid.NewGuid(),
            organizationId ?? Guid.NewGuid(),
            "Test Listing",
            "Description",
            category.Id,
            Guid.NewGuid());

        _db.Listings.Add(listing);
        await _db.SaveChangesAsync();
        return listing;
    }

    private async Task<Listing> SeedPublishedListingAsync(Guid? organizationId = null)
    {
        var listing = await SeedDraftListingAsync(organizationId);

        var plan = PricingPlan.Create(
            listing.Id,
            "Standard",
            PricingType.Free,
            0m,
            "USD",
            null,
            0,
            null);

        _db.PricingPlans.Add(plan);
        await _db.SaveChangesAsync();

        listing.Publish();
        await _db.SaveChangesAsync();

        return listing;
    }

    private Task<Result<ReviewDto>> ExecuteHandlerAsync(RestrictPointPrincipal principal, Guid listingId, CreateReviewRequest request)
    {
        var method = _handler.GetType().GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (Task<Result<ReviewDto>>)method!.Invoke(_handler, [principal, listingId, request, CancellationToken.None])!;
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
