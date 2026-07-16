using FluentAssertions;
using RestrictPoint.Api.Marketplace.Domain;

namespace RestrictPoint.Tests.Marketplace.Domain;

public sealed class PricingPlanTests
{
    [Fact]
    public void Create_WithValidMonthlySubscription_Succeeds()
    {
        var plan = PricingPlan.Create(
            Guid.NewGuid(),
            "Standard",
            PricingType.MonthlySubscription,
            29.99m,
            "USD",
            BillingInterval.Monthly,
            14,
            null);

        plan.Should().NotBeNull();
        plan.Name.Should().Be("Standard");
        plan.PricingType.Should().Be(PricingType.MonthlySubscription);
        plan.Price.Should().Be(29.99m);
        plan.Currency.Should().Be("USD");
        plan.BillingInterval.Should().Be(BillingInterval.Monthly);
        plan.TrialDays.Should().Be(14);
        plan.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Create_WithFreeNonZeroPrice_ThrowsArgumentException()
    {
        var act = () => PricingPlan.Create(
            Guid.NewGuid(),
            "Free",
            PricingType.Free,
            9.99m, // Non-zero price
            "USD",
            null,
            0,
            null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Free pricing*");
    }

    [Fact]
    public void Create_WithSubscriptionWithoutBillingInterval_ThrowsArgumentException()
    {
        var act = () => PricingPlan.Create(
            Guid.NewGuid(),
            "Standard",
            PricingType.MonthlySubscription,
            29.99m,
            "USD",
            null, // Missing billing interval
            0,
            null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*billing interval*");
    }

    [Fact]
    public void Create_WithNegativePrice_ThrowsArgumentException()
    {
        var act = () => PricingPlan.Create(
            Guid.NewGuid(),
            "Standard",
            PricingType.OneTimePurchase,
            -10m,
            "USD",
            null,
            0,
            null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public void Create_WithInvalidTrialDays_ThrowsArgumentException()
    {
        var act = () => PricingPlan.Create(
            Guid.NewGuid(),
            "Standard",
            PricingType.MonthlySubscription,
            29.99m,
            "USD",
            BillingInterval.Monthly,
            400, // Too many trial days
            null);

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Trial days*");
    }

    [Fact]
    public void Update_WithValidData_Succeeds()
    {
        var plan = CreateValidPlan();

        plan.Update("Premium", 49.99m, 30, "{\"features\":[\"unlimited\"]}");

        plan.Name.Should().Be("Premium");
        plan.Price.Should().Be(49.99m);
        plan.TrialDays.Should().Be(30);
        plan.LicenseTemplate.Should().Be("{\"features\":[\"unlimited\"]}");
    }

    [Fact]
    public void Activate_SetsIsActiveToTrue()
    {
        var plan = CreateValidPlan();
        plan.Deactivate();

        plan.Activate();

        plan.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        var plan = CreateValidPlan();

        plan.Deactivate();

        plan.IsActive.Should().BeFalse();
    }

    [Fact]
    public void SetStripePriceId_SetsValue()
    {
        var plan = CreateValidPlan();

        plan.SetStripePriceId("price_1234567890");

        plan.StripePriceId.Should().Be("price_1234567890");
    }

    [Fact]
    public void SetStripePriceId_WithEmptyString_ThrowsArgumentException()
    {
        var plan = CreateValidPlan();

        var act = () => plan.SetStripePriceId("");

        act.Should().Throw<ArgumentException>();
    }

    private static PricingPlan CreateValidPlan() =>
        PricingPlan.Create(
            Guid.NewGuid(),
            "Standard",
            PricingType.MonthlySubscription,
            29.99m,
            "USD",
            BillingInterval.Monthly,
            14,
            null);
}
