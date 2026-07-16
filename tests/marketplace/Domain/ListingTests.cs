using FluentAssertions;
using RestrictPoint.Api.Marketplace.Domain;

namespace RestrictPoint.Tests.Marketplace.Domain;

public sealed class ListingTests
{
    [Fact]
    public void Create_WithValidData_CreatesListing()
    {
        var projectId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var webPartGuid = Guid.NewGuid();

        var listing = Listing.Create(
            projectId,
            organizationId,
            "My Web Part",
            "A great web part for SharePoint",
            categoryId,
            webPartGuid);

        listing.Should().NotBeNull();
        listing.Title.Should().Be("My Web Part");
        listing.Description.Should().Be("A great web part for SharePoint");
        listing.Status.Should().Be(ListingStatus.Draft);
        listing.ProjectId.Should().Be(projectId);
        listing.OrganizationId.Should().Be(organizationId);
        listing.CategoryId.Should().Be(categoryId);
        listing.WebPartGuid.Should().Be(webPartGuid);
        listing.IsFeatured.Should().BeFalse();
        listing.InstallCount.Should().Be(0);
        listing.AverageRating.Should().Be(0);
        listing.ReviewCount.Should().Be(0);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidTitle_ThrowsArgumentException(string? title)
    {
        var act = () => Listing.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            title!,
            "Description",
            Guid.NewGuid(),
            Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Title*");
    }

    [Fact]
    public void Create_WithTitleTooLong_ThrowsArgumentException()
    {
        var longTitle = new string('A', 257);

        var act = () => Listing.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            longTitle,
            "Description",
            Guid.NewGuid(),
            Guid.NewGuid());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Title*");
    }

    [Fact]
    public void Publish_FromDraft_WithPricingPlans_Succeeds()
    {
        var listing = CreateDraftListing();
        var plan = PricingPlan.Create(
            listing.Id,
            "Standard",
            PricingType.MonthlySubscription,
            29.99m,
            "USD",
            BillingInterval.Monthly,
            14,
            null);
        listing.PricingPlans.Add(plan);

        listing.Publish();

        listing.Status.Should().Be(ListingStatus.Published);
    }

    [Fact]
    public void Publish_WithoutPricingPlans_ThrowsInvalidOperationException()
    {
        var listing = CreateDraftListing();

        var act = () => listing.Publish();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*pricing plan*");
    }

    [Fact]
    public void Publish_FromRemoved_ThrowsInvalidOperationException()
    {
        var listing = CreateDraftListing();
        listing.Remove();

        var act = () => listing.Publish();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*transition*");
    }

    [Fact]
    public void Suspend_FromPublished_Succeeds()
    {
        var listing = CreatePublishedListing();

        listing.Suspend("Terms of service violation");

        listing.Status.Should().Be(ListingStatus.Suspended);
    }

    [Fact]
    public void Deprecate_FromPublished_Succeeds()
    {
        var listing = CreatePublishedListing();

        listing.Deprecate();

        listing.Status.Should().Be(ListingStatus.Deprecated);
    }

    [Fact]
    public void Remove_FromAnyState_Succeeds()
    {
        var draft = CreateDraftListing();
        var published = CreatePublishedListing();

        draft.Remove();
        published.Remove();

        draft.Status.Should().Be(ListingStatus.Removed);
        published.Status.Should().Be(ListingStatus.Removed);
    }

    [Fact]
    public void MarkAsFeatured_WhenPublished_Succeeds()
    {
        var listing = CreatePublishedListing();

        listing.MarkAsFeatured();

        listing.IsFeatured.Should().BeTrue();
    }

    [Fact]
    public void MarkAsFeatured_WhenDraft_ThrowsInvalidOperationException()
    {
        var listing = CreateDraftListing();

        var act = () => listing.MarkAsFeatured();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*published*");
    }

    [Fact]
    public void IncrementInstallCount_IncreasesCount()
    {
        var listing = CreatePublishedListing();
        var initialCount = listing.InstallCount;

        listing.IncrementInstallCount();

        listing.InstallCount.Should().Be(initialCount + 1);
    }

    [Fact]
    public void RecalculateRating_WithNoReviews_SetsRatingToZero()
    {
        var listing = CreatePublishedListing();

        listing.RecalculateRating();

        listing.AverageRating.Should().Be(0);
        listing.ReviewCount.Should().Be(0);
    }

    [Fact]
    public void RecalculateRating_WithReviews_CalculatesAverage()
    {
        var listing = CreatePublishedListing();
        listing.Reviews.Add(Review.Create(listing.Id, Guid.NewGuid(), 5, "Great!"));
        listing.Reviews.Add(Review.Create(listing.Id, Guid.NewGuid(), 3, "OK"));
        listing.Reviews.Add(Review.Create(listing.Id, Guid.NewGuid(), 4, "Good"));

        listing.RecalculateRating();

        listing.AverageRating.Should().Be(4m); // (5+3+4)/3 = 4
        listing.ReviewCount.Should().Be(3);
    }

    [Fact]
    public void Update_WhenDraft_Succeeds()
    {
        var listing = CreateDraftListing();
        var newCategoryId = Guid.NewGuid();

        listing.Update("Updated Title", "Updated Description", newCategoryId, "logo.png", "support.com", "docs.com");

        listing.Title.Should().Be("Updated Title");
        listing.Description.Should().Be("Updated Description");
        listing.CategoryId.Should().Be(newCategoryId);
        listing.LogoUrl.Should().Be("logo.png");
        listing.SupportUrl.Should().Be("support.com");
        listing.DocumentationUrl.Should().Be("docs.com");
    }

    [Fact]
    public void Update_WhenRemoved_ThrowsInvalidOperationException()
    {
        var listing = CreateDraftListing();
        listing.Remove();

        var act = () => listing.Update("New Title", "New Description", Guid.NewGuid(), null, null, null);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Removed*");
    }

    private static Listing CreateDraftListing()
    {
        return Listing.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Listing",
            "Test Description",
            Guid.NewGuid(),
            Guid.NewGuid());
    }

    private static Listing CreatePublishedListing()
    {
        var listing = CreateDraftListing();
        var plan = PricingPlan.Create(
            listing.Id,
            "Standard",
            PricingType.MonthlySubscription,
            29.99m,
            "USD",
            BillingInterval.Monthly,
            14,
            null);
        listing.PricingPlans.Add(plan);
        listing.Publish();
        return listing;
    }
}
