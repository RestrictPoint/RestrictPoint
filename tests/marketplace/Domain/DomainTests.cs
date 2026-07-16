using RestrictPoint.Api.Marketplace.Domain;
using Xunit;

namespace RestrictPoint.Api.Marketplace.Tests.Domain;

public sealed class PricingPlanTests
{
    [Fact]
    public void Create_free_plan_with_zero_price_succeeds()
    {
        var plan = PricingPlan.Create(
            Guid.NewGuid(), "Free Tier", PricingType.Free, 0, "usd", null, 0, null);

        Assert.Equal(PricingType.Free, plan.PricingType);
        Assert.Equal(0, plan.Price);
        Assert.Equal("USD", plan.Currency); // Normalized to upper case.
        Assert.True(plan.IsActive);
    }

    [Fact]
    public void Create_free_plan_with_nonzero_price_throws()
    {
        Assert.Throws<ArgumentException>(() => PricingPlan.Create(
            Guid.NewGuid(), "Free", PricingType.Free, 5m, "USD", null, 0, null));
    }

    [Fact]
    public void Create_subscription_without_billing_interval_throws()
    {
        Assert.Throws<ArgumentException>(() => PricingPlan.Create(
            Guid.NewGuid(), "Monthly", PricingType.MonthlySubscription, 9.99m, "USD", null, 0, null));
    }

    [Fact]
    public void Create_with_negative_price_throws()
    {
        Assert.Throws<ArgumentException>(() => PricingPlan.Create(
            Guid.NewGuid(), "Plan", PricingType.OneTimePurchase, -1m, "USD", null, 0, null));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Create_with_invalid_trial_days_throws(int trialDays)
    {
        Assert.Throws<ArgumentException>(() => PricingPlan.Create(
            Guid.NewGuid(), "Plan", PricingType.MonthlySubscription, 9.99m, "USD",
            BillingInterval.Monthly, trialDays, null));
    }

    [Fact]
    public void Create_with_empty_name_throws()
    {
        Assert.Throws<ArgumentException>(() => PricingPlan.Create(
            Guid.NewGuid(), " ", PricingType.Free, 0, "USD", null, 0, null));
    }

    [Fact]
    public void Deactivate_and_activate_toggle_availability()
    {
        var plan = PricingPlan.Create(
            Guid.NewGuid(), "Plan", PricingType.OneTimePurchase, 49m, "USD", null, 0, null);

        plan.Deactivate();
        Assert.False(plan.IsActive);

        plan.Activate();
        Assert.True(plan.IsActive);
    }

    [Fact]
    public void SetStripePriceId_stores_id_and_rejects_empty()
    {
        var plan = PricingPlan.Create(
            Guid.NewGuid(), "Plan", PricingType.AnnualSubscription, 99m, "USD",
            BillingInterval.Annual, 30, null);

        plan.SetStripePriceId("price_abc123");
        Assert.Equal("price_abc123", plan.StripePriceId);

        Assert.Throws<ArgumentException>(() => plan.SetStripePriceId(" "));
    }
}

public sealed class ReviewTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Create_with_invalid_rating_throws(int rating)
    {
        Assert.Throws<ArgumentException>(() => Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), rating, null));
    }

    [Fact]
    public void Create_with_comment_over_4000_characters_throws()
    {
        Assert.Throws<ArgumentException>(() => Review.Create(
            Guid.NewGuid(), Guid.NewGuid(), 5, new string('x', 4001)));
    }

    [Fact]
    public void Create_trims_comment_and_stores_rating()
    {
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), 4, "  Great tool  ");

        Assert.Equal(4, review.Rating);
        Assert.Equal("Great tool", review.Comment);
        Assert.False(review.IsFlagged);
        Assert.Null(review.EditedUtc);
    }

    [Fact]
    public void Update_within_edit_window_succeeds_and_marks_edited()
    {
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), 3, "Okay");

        review.Update(5, "Actually excellent");

        Assert.Equal(5, review.Rating);
        Assert.Equal("Actually excellent", review.Comment);
        Assert.NotNull(review.EditedUtc);
    }

    [Fact]
    public void Flag_and_unflag_toggle_moderation_state()
    {
        var review = Review.Create(Guid.NewGuid(), Guid.NewGuid(), 1, "Spam");

        review.Flag();
        Assert.True(review.IsFlagged);

        review.Unflag();
        Assert.False(review.IsFlagged);
    }
}

public sealed class CategoryTests
{
    [Fact]
    public void Create_generates_url_friendly_slug()
    {
        var category = Category.Create("Data & AI Tools", null, 1);

        Assert.Equal("data-and-ai-tools", category.Slug);
        Assert.Null(category.ParentCategoryId);
    }

    [Fact]
    public void Create_with_empty_name_throws()
    {
        Assert.Throws<ArgumentException>(() => Category.Create(" ", null, 0));
    }
}

public sealed class TagTests
{
    [Fact]
    public void Create_generates_slug_and_zero_usage()
    {
        var tag = Tag.Create("SharePoint Dashboard");

        Assert.Equal("sharepoint-dashboard", tag.Slug);
        Assert.Equal(0, tag.UsageCount);
    }

    [Fact]
    public void Usage_count_increments_and_never_goes_negative()
    {
        var tag = Tag.Create("SPFx");

        tag.IncrementUsage();
        Assert.Equal(1, tag.UsageCount);

        tag.DecrementUsage();
        tag.DecrementUsage();
        Assert.Equal(0, tag.UsageCount);
    }
}
