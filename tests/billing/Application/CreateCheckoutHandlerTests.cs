using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Billing.Application.Abstractions;
using RestrictPoint.Api.Billing.Application.Common;
using RestrictPoint.Api.Billing.Application.CreateCheckout;
using RestrictPoint.Api.Billing.Contracts;
using RestrictPoint.Api.Billing.Domain;
using RestrictPoint.Api.Billing.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using Xunit;

namespace RestrictPoint.Api.Billing.Tests.Application;

/// <summary>Stub payment provider capturing requests.</summary>
public sealed class StubPaymentProvider : IPaymentProvider
{
    public CheckoutSessionRequest? LastCheckout { get; private set; }

    public bool FailNextCall { get; set; }

    public Task<Result<string>> CreateCheckoutSessionAsync(
        CheckoutSessionRequest request, CancellationToken cancellationToken)
    {
        LastCheckout = request;
        return Task.FromResult(FailNextCall
            ? Result.Failure<string>(BillingErrors.PaymentProviderUnavailable)
            : Result.Success("https://checkout.stripe.com/session/test"));
    }

    public Task<Result<Result.Unit>> CancelSubscriptionAsync(
        string providerSubscriptionId, bool cancelAtPeriodEnd, CancellationToken cancellationToken) =>
        Task.FromResult(FailNextCall
            ? Result.Failure(BillingErrors.PaymentProviderUnavailable)
            : Result.Success());

    public Task<Result<string>> CreateConnectOnboardingLinkAsync(
        Guid developerOrganizationId, string email, string returnUrl, string refreshUrl,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success("https://connect.stripe.com/onboarding/test"));
}

public sealed class CreateCheckoutHandlerTests : IDisposable
{
    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;
    private readonly StubPaymentProvider _provider = new();

    public CreateCheckoutHandlerTests()
    {
        _database = new TestDatabase(_time);
    }

    public void Dispose() => _database.Dispose();

    private static RequestContext Context() => new()
    {
        CorrelationId = "corr-checkout",
        Email = "buyer@customer.com",
    };

    private CreateCheckoutHandler CreateHandler(BillingDbContext context) =>
        new(context, _provider, new OutboxWriter(context), _time,
            new BillingOptions { PlatformFeePercent = 10m });

    private static CreateCheckoutRequest Request(LicenseTemplateDto? template = null) => new()
    {
        ProjectId = Guid.NewGuid(),
        DeveloperOrganizationId = Guid.NewGuid(),
        CustomerOrganizationId = Guid.NewGuid(),
        CustomerTenantId = Guid.NewGuid(),
        PlanPriceId = "price_123",
        Plan = "Monthly",
        SuccessUrl = "https://portal.restrictpoint.com/success",
        CancelUrl = "https://portal.restrictpoint.com/cancel",
        LicenseTemplate = template ?? new LicenseTemplateDto
        {
            LicenseType = "Monthly",
            Features = new Dictionary<string, bool> { ["Export"] = true },
            Limits = new Dictionary<string, int> { ["maxUsers"] = 10 },
            WebPartGuids = [Guid.NewGuid()],
        },
    };

    [Fact]
    public async Task Creates_pending_subscription_with_template_and_event()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(Context(), Request(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("https://checkout.stripe.com/session/test", result.Value.CheckoutUrl);

        using var verification = _database.CreateContext();
        var subscription = await verification.Subscriptions.SingleAsync();
        Assert.Equal(SubscriptionStatus.Trialing, subscription.Status); // Pending payment.
        Assert.Null(subscription.StripeSubscriptionId); // Bound later via webhook.

        var template = LicenseTemplate.Parse(subscription.LicenseTemplate);
        Assert.True(template.IsSuccess);
        Assert.Equal("Monthly", template.Value.LicenseType);

        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "SubscriptionCreated"));
        Assert.Equal(subscription.Id, _provider.LastCheckout!.SubscriptionId); // Metadata correlation.
    }

    [Fact]
    public async Task Invalid_license_template_is_rejected_before_provider_call()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(),
            Request(new LicenseTemplateDto { LicenseType = "Forever", WebPartGuids = [Guid.NewGuid()] }),
            CancellationToken.None);

        Assert.Equal(BillingErrors.InvalidLicenseTemplate.Code, result.Error!.Code);
        Assert.Null(_provider.LastCheckout);
        Assert.Equal(0, await context.Subscriptions.CountAsync());
    }

    [Fact]
    public async Task Template_without_webparts_is_rejected()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(),
            Request(new LicenseTemplateDto { LicenseType = "Monthly", WebPartGuids = [] }),
            CancellationToken.None);

        Assert.Equal(BillingErrors.InvalidLicenseTemplate.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Provider_failure_leaves_no_local_state()
    {
        _provider.FailNextCall = true;
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(Context(), Request(), CancellationToken.None);

        Assert.Equal(BillingErrors.PaymentProviderUnavailable.Code, result.Error!.Code);
        Assert.Equal(0, await context.Subscriptions.CountAsync());
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public void LicenseTemplate_roundtrips_serialization()
    {
        var template = new LicenseTemplate
        {
            LicenseType = "Annual",
            Features = new Dictionary<string, bool> { ["AI"] = false },
            Limits = new Dictionary<string, int> { ["maxSites"] = 3 },
            WebPartGuids = [Guid.NewGuid()],
        };

        var parsed = LicenseTemplate.Parse(template.Serialize());

        Assert.True(parsed.IsSuccess);
        Assert.Equal(template.LicenseType, parsed.Value.LicenseType);
        Assert.Equal(template.WebPartGuids, parsed.Value.WebPartGuids);
    }
}
