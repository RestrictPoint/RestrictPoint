using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestrictPoint.Api.Marketplace.Application.CreateListing;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Common;
using RestrictPoint.Messaging;
using RestrictPoint.Tests.Shared;

namespace RestrictPoint.Tests.Marketplace.Application;

public sealed class CreateListingHandlerTests : IDisposable
{
    private readonly MarketplaceDbContext _db;
    private readonly TestOutboxWriter _outbox;
    private readonly StubOrganizationRoleResolver _roleResolver;
    private readonly CreateListingHandler _handler;

    public CreateListingHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        _db = new MarketplaceDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();

        _outbox = new TestOutboxWriter();
        _roleResolver = new StubOrganizationRoleResolver();
        _handler = new CreateListingHandler(_db, _roleResolver, _outbox, NullLogger<CreateListingHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidData_CreatesListing()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var categoryId = await SeedCategoryAsync();
        var principal = new RestrictPointPrincipal(Guid.NewGuid(), "test@example.com", organizationId);

        _roleResolver.SetRole(OrganizationRole.Owner);

        var request = new CreateListingRequest(
            organizationId,
            projectId,
            "My SPFx Web Part",
            "A great web part for productivity",
            categoryId,
            Guid.NewGuid());

        var result = await ExecuteHandlerAsync(principal, request);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Title.Should().Be("My SPFx Web Part");
        result.Value.Status.Should().Be("Draft");

        var listing = await _db.Listings.FirstOrDefaultAsync(l => l.ProjectId == projectId);
        listing.Should().NotBeNull();
        listing!.Title.Should().Be("My SPFx Web Part");

        _outbox.Messages.Should().ContainSingle();
        _outbox.Messages[0].EventType.Should().Be("ListingCreated");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotOwnerOrAdmin_ReturnsForbidden()
    {
        var organizationId = Guid.NewGuid();
        var categoryId = await SeedCategoryAsync();
        var principal = new RestrictPointPrincipal(Guid.NewGuid(), "test@example.com", organizationId);

        _roleResolver.SetRole(OrganizationRole.Member); // Not owner/admin

        var request = new CreateListingRequest(
            organizationId,
            Guid.NewGuid(),
            "My Web Part",
            "Description",
            categoryId,
            Guid.NewGuid());

        var result = await ExecuteHandlerAsync(principal, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Contain("Forbidden");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDuplicateProject_ReturnsListingAlreadyExists()
    {
        var organizationId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var categoryId = await SeedCategoryAsync();
        var principal = new RestrictPointPrincipal(Guid.NewGuid(), "test@example.com", organizationId);

        _roleResolver.SetRole(OrganizationRole.Owner);

        // Create first listing
        var existingListing = Listing.Create(projectId, organizationId, "Existing", "Desc", categoryId, Guid.NewGuid());
        _db.Listings.Add(existingListing);
        await _db.SaveChangesAsync();

        // Try to create duplicate
        var request = new CreateListingRequest(
            organizationId,
            projectId, // Same project
            "Duplicate",
            "Description",
            categoryId,
            Guid.NewGuid());

        var result = await ExecuteHandlerAsync(principal, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MarketplaceErrors.ListingAlreadyExists);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCategoryNotFound_ReturnsCategoryNotFound()
    {
        var organizationId = Guid.NewGuid();
        var principal = new RestrictPointPrincipal(Guid.NewGuid(), "test@example.com", organizationId);

        _roleResolver.SetRole(OrganizationRole.Owner);

        var request = new CreateListingRequest(
            organizationId,
            Guid.NewGuid(),
            "My Web Part",
            "Description",
            Guid.NewGuid(), // Non-existent category
            Guid.NewGuid());

        var result = await ExecuteHandlerAsync(principal, request);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(MarketplaceErrors.CategoryNotFound);
    }

    private async Task<Guid> SeedCategoryAsync()
    {
        var category = Category.Create("Productivity", null, 1);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();
        return category.Id;
    }

    private Task<Result<ListingDto>> ExecuteHandlerAsync(RestrictPointPrincipal principal, CreateListingRequest request)
    {
        // Reflection to call private ExecuteAsync
        var method = _handler.GetType().GetMethod("ExecuteAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return (Task<Result<ListingDto>>)method!.Invoke(_handler, [principal, request, CancellationToken.None])!;
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }
}
