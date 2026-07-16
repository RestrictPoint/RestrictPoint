using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Marketplace.Application.CreateListing;
using RestrictPoint.Api.Marketplace.Contracts;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using Xunit;

namespace RestrictPoint.Api.Marketplace.Tests.Application;

public sealed class CreateListingHandlerTests : IDisposable
{
    private const string BearerToken = "test-token";

    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;
    private readonly StubRoleResolver _roles = new();
    private readonly Guid _organizationId = Guid.NewGuid();
    private readonly Guid _categoryId;

    public CreateListingHandlerTests()
    {
        _database = new TestDatabase(_time);
        _categoryId = SeedCategory();
    }

    public void Dispose() => _database.Dispose();

    private Guid SeedCategory()
    {
        using var context = _database.CreateContext();
        var category = Category.Create("Productivity", null, 1);
        context.Categories.Add(category);
        context.SaveChanges();
        return category.Id;
    }

    private static RequestContext Context() => new()
    {
        CorrelationId = "corr-create-listing",
        ExternalObjectId = Guid.NewGuid().ToString(),
    };

    private CreateListingHandler CreateHandler(MarketplaceDbContext context) =>
        new(context, _roles, new OutboxWriter(context), _time);

    private CreateListingRequest Request(Guid? projectId = null) => new()
    {
        ProjectId = projectId ?? Guid.NewGuid(),
        OrganizationId = _organizationId,
        Title = "Analytics Dashboard",
        Description = "A powerful analytics dashboard.",
        CategoryId = _categoryId,
        WebPartGuid = Guid.NewGuid(),
        LogoUrl = "https://cdn.restrictpoint.com/logo.png",
        Tags = ["SPFx", "Dashboard"],
    };

    [Fact]
    public async Task Member_with_developer_role_creates_draft_listing_with_tags_and_event()
    {
        _roles.SetRole(_organizationId, "Developer");
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, Request(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Draft", result.Value.Status);
        Assert.Equal(2, result.Value.Tags.Count);

        using var verification = _database.CreateContext();
        var listing = await verification.Listings.Include(l => l.Tags).SingleAsync();
        Assert.Equal(ListingStatus.Draft, listing.Status);
        Assert.Equal(2, listing.Tags.Count);
        Assert.Equal(1, await verification.OutboxMessages
            .CountAsync(m => m.EventType == "MarketplaceListingCreated"));
    }

    [Fact]
    public async Task Non_member_is_rejected()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, Request(), CancellationToken.None);

        Assert.Equal(MarketplaceErrors.NotAuthorizedForOrganization.Code, result.Error!.Code);
        Assert.Equal(0, await context.Listings.CountAsync());
    }

    [Fact]
    public async Task Member_without_publishing_role_is_rejected()
    {
        _roles.SetRole(_organizationId, "Viewer");
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, Request(), CancellationToken.None);

        Assert.Equal(MarketplaceErrors.NotAuthorizedForOrganization.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Identity_outage_fails_closed()
    {
        _roles.FailNextCall = true;
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, Request(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, await context.Listings.CountAsync());
    }

    [Fact]
    public async Task Unknown_category_is_rejected()
    {
        _roles.SetRole(_organizationId, "Owner");
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), BearerToken, Request() with { CategoryId = Guid.NewGuid() }, CancellationToken.None);

        Assert.Equal(MarketplaceErrors.CategoryNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Duplicate_project_listing_is_rejected()
    {
        _roles.SetRole(_organizationId, "Owner");
        var projectId = Guid.NewGuid();

        using (var context = _database.CreateContext())
        {
            var first = await CreateHandler(context).HandleAsync(
                Context(), BearerToken, Request(projectId), CancellationToken.None);
            Assert.True(first.IsSuccess);
        }

        using var second = _database.CreateContext();
        var result = await CreateHandler(second).HandleAsync(
            Context(), BearerToken, Request(projectId), CancellationToken.None);

        Assert.Equal(MarketplaceErrors.ListingAlreadyExists.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Existing_tags_are_reused_and_usage_incremented()
    {
        _roles.SetRole(_organizationId, "Owner");

        using (var context = _database.CreateContext())
        {
            await CreateHandler(context).HandleAsync(
                Context(), BearerToken, Request(), CancellationToken.None);
        }

        using (var context = _database.CreateContext())
        {
            await CreateHandler(context).HandleAsync(
                Context(), BearerToken, Request(), CancellationToken.None);
        }

        using var verification = _database.CreateContext();
        Assert.Equal(2, await verification.Tags.CountAsync()); // No duplicate tag rows.
        var tag = await verification.Tags.SingleAsync(t => t.Name == "SPFx");
        Assert.Equal(2, tag.UsageCount);
    }
}
