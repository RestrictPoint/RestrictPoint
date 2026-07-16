using RestrictPoint.Api.Marketplace.Domain;
using Xunit;

namespace RestrictPoint.Api.Marketplace.Tests.Domain;

public sealed class ListingTests
{
    private static Listing CreateDraftListing() => Listing.Create(
        Guid.NewGuid(), Guid.NewGuid(), "Analytics Dashboard",
        "A powerful analytics dashboard web part.", Guid.NewGuid(), Guid.NewGuid());

    private static Listing CreatePublishedListing()
    {
        var listing = CreateDraftListing();
        listing.PricingPlans.Add(PricingPlan.Create(
            listing.Id, "Standard", PricingType.MonthlySubscription, 9.99m, "USD",
            BillingInterval.Monthly, 14, null));
        listing.Publish();
        return listing;
    }

    [Fact]
    public void Create_produces_draft_with_trimmed_fields()
    {
        var listing = Listing.Create(
            Guid.NewGuid(), Guid.NewGuid(), "  Title  ", "  Description  ",
            Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(ListingStatus.Draft, listing.Status);
        Assert.Equal("Title", listing.Title);
        Assert.Equal("Description", listing.Description);
        Assert.False(listing.IsFeatured);
        Assert.Equal(0, listing.InstallCount);
        Assert.Equal(0, listing.ReviewCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_title(string title)
    {
        Assert.Throws<ArgumentException>(() => Listing.Create(
            Guid.NewGuid(), Guid.NewGuid(), title, "Description", Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void Create_rejects_title_over_256_characters()
    {
        Assert.Throws<ArgumentException>(() => Listing.Create(
            Guid.NewGuid(), Guid.NewGuid(), new string('x', 257), "Description",
            Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void Create_rejects_empty_description()
    {
        Assert.Throws<ArgumentException>(() => Listing.Create(
            Guid.NewGuid(), Guid.NewGuid(), "Title", "  ", Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public void Publish_from_draft_with_pricing_succeeds()
    {
        var listing = CreatePublishedListing();

        Assert.Equal(ListingStatus.Published, listing.Status);
    }

    [Fact]
    public void Publish_without_pricing_plans_throws()
    {
        var listing = CreateDraftListing();

        Assert.Throws<InvalidOperationException>(listing.Publish);
    }

    [Fact]
    public void Publish_from_removed_throws()
    {
        var listing = CreateDraftListing();
        listing.Remove();

        Assert.Throws<InvalidOperationException>(listing.Publish);
    }

    [Fact]
    public void Suspend_published_listing_succeeds()
    {
        var listing = CreatePublishedListing();

        listing.Suspend("Policy violation");

        Assert.Equal(ListingStatus.Suspended, listing.Status);
    }

    [Fact]
    public void Suspended_listing_can_be_republished()
    {
        var listing = CreatePublishedListing();
        listing.Suspend("Investigation");

        listing.Publish();

        Assert.Equal(ListingStatus.Published, listing.Status);
    }

    [Fact]
    public void Deprecate_published_listing_succeeds()
    {
        var listing = CreatePublishedListing();

        listing.Deprecate();

        Assert.Equal(ListingStatus.Deprecated, listing.Status);
    }

    [Fact]
    public void Deprecated_listing_can_only_be_removed()
    {
        var listing = CreatePublishedListing();
        listing.Deprecate();

        Assert.Throws<InvalidOperationException>(listing.Publish);

        listing.Remove();
        Assert.Equal(ListingStatus.Removed, listing.Status);
    }

    [Fact]
    public void Suspend_draft_listing_throws()
    {
        var listing = CreateDraftListing();

        Assert.Throws<InvalidOperationException>(() => listing.Suspend("reason"));
    }

    [Fact]
    public void MarkAsFeatured_requires_published_status()
    {
        var draft = CreateDraftListing();
        Assert.Throws<InvalidOperationException>(draft.MarkAsFeatured);

        var published = CreatePublishedListing();
        published.MarkAsFeatured();
        Assert.True(published.IsFeatured);
    }

    [Fact]
    public void IncrementInstallCount_increases_count()
    {
        var listing = CreatePublishedListing();

        listing.IncrementInstallCount();
        listing.IncrementInstallCount();

        Assert.Equal(2, listing.InstallCount);
    }

    [Fact]
    public void RecalculateRating_averages_reviews()
    {
        var listing = CreatePublishedListing();
        listing.Reviews.Add(Review.Create(listing.Id, Guid.NewGuid(), 5, "Excellent"));
        listing.Reviews.Add(Review.Create(listing.Id, Guid.NewGuid(), 3, "Decent"));

        listing.RecalculateRating();

        Assert.Equal(4m, listing.AverageRating);
        Assert.Equal(2, listing.ReviewCount);
    }

    [Fact]
    public void RecalculateRating_with_no_reviews_resets_to_zero()
    {
        var listing = CreatePublishedListing();
        listing.Reviews.Add(Review.Create(listing.Id, Guid.NewGuid(), 5, null));
        listing.RecalculateRating();

        listing.Reviews.Clear();
        listing.RecalculateRating();

        Assert.Equal(0m, listing.AverageRating);
        Assert.Equal(0, listing.ReviewCount);
    }
}

public sealed class ListingStateMachineTests
{
    [Theory]
    [InlineData(ListingStatus.Draft, ListingStatus.Published, true)]
    [InlineData(ListingStatus.Draft, ListingStatus.Removed, true)]
    [InlineData(ListingStatus.Draft, ListingStatus.Suspended, false)]
    [InlineData(ListingStatus.Draft, ListingStatus.Deprecated, false)]
    [InlineData(ListingStatus.Published, ListingStatus.Suspended, true)]
    [InlineData(ListingStatus.Published, ListingStatus.Deprecated, true)]
    [InlineData(ListingStatus.Published, ListingStatus.Removed, true)]
    [InlineData(ListingStatus.Published, ListingStatus.Draft, false)]
    [InlineData(ListingStatus.Suspended, ListingStatus.Published, true)]
    [InlineData(ListingStatus.Suspended, ListingStatus.Removed, true)]
    [InlineData(ListingStatus.Suspended, ListingStatus.Deprecated, false)]
    [InlineData(ListingStatus.Deprecated, ListingStatus.Removed, true)]
    [InlineData(ListingStatus.Deprecated, ListingStatus.Published, false)]
    [InlineData(ListingStatus.Removed, ListingStatus.Published, false)]
    [InlineData(ListingStatus.Removed, ListingStatus.Draft, false)]
    public void CanTransition_enforces_docs13_lifecycle(ListingStatus from, ListingStatus to, bool expected)
    {
        Assert.Equal(expected, ListingStateMachine.CanTransition(from, to));
    }
}
