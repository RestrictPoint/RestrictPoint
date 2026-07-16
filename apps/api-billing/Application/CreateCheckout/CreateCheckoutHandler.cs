using FluentValidation;
using RestrictPoint.Api.Billing.Application.Abstractions;
using RestrictPoint.Api.Billing.Application.Common;
using RestrictPoint.Api.Billing.Application.Events;
using RestrictPoint.Api.Billing.Contracts;
using RestrictPoint.Api.Billing.Domain;
using RestrictPoint.Api.Billing.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Billing.Application.CreateCheckout;

/// <summary>Input validation for POST /v1/billing/checkout.</summary>
public sealed class CreateCheckoutRequestValidator : AbstractValidator<CreateCheckoutRequest>
{
    public CreateCheckoutRequestValidator()
    {
        RuleFor(r => r.ProjectId).NotEmpty();
        RuleFor(r => r.DeveloperOrganizationId).NotEmpty();
        RuleFor(r => r.CustomerOrganizationId).NotEmpty();
        RuleFor(r => r.CustomerTenantId).NotEmpty();
        RuleFor(r => r.PlanPriceId).NotEmpty().MaximumLength(256);
        RuleFor(r => r.Plan).NotEmpty().MaximumLength(Subscription.PlanMaxLength);
        RuleFor(r => r.SuccessUrl).NotEmpty().Must(BeHttps)
            .WithMessage("SuccessUrl must be an absolute https URL.");
        RuleFor(r => r.CancelUrl).NotEmpty().Must(BeHttps)
            .WithMessage("CancelUrl must be an absolute https URL.");
        RuleFor(r => r.LicenseTemplate).NotNull();

        static bool BeHttps(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }
}

/// <summary>
/// POST /v1/billing/checkout — creates a pending subscription (Trialing) and a Stripe
/// checkout session. The license template is validated and captured now; activation via
/// webhook carries it to the Licensing service.
/// </summary>
public sealed class CreateCheckoutHandler
{
    private readonly BillingDbContext _dbContext;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IOutboxWriter _outbox;
    private readonly TimeProvider _timeProvider;
    private readonly decimal _platformFeePercent;

    public CreateCheckoutHandler(
        BillingDbContext dbContext,
        IPaymentProvider paymentProvider,
        IOutboxWriter outbox,
        TimeProvider timeProvider,
        BillingOptions options)
    {
        _dbContext = dbContext;
        _paymentProvider = paymentProvider;
        _outbox = outbox;
        _timeProvider = timeProvider;
        _platformFeePercent = options.PlatformFeePercent;
    }

    public async Task<Result<CheckoutResponse>> HandleAsync(
        RequestContext context,
        CreateCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var template = new LicenseTemplate
        {
            LicenseType = request.LicenseTemplate!.LicenseType ?? string.Empty,
            Features = request.LicenseTemplate.Features ?? new Dictionary<string, bool>(),
            Limits = request.LicenseTemplate.Limits ?? new Dictionary<string, int>(),
            WebPartGuids = request.LicenseTemplate.WebPartGuids ?? [],
        };

        if (!template.IsValid())
        {
            return BillingErrors.InvalidLicenseTemplate;
        }

        var subscription = new Subscription
        {
            CustomerOrganizationId = request.CustomerOrganizationId!.Value,
            DeveloperOrganizationId = request.DeveloperOrganizationId!.Value,
            ProjectId = request.ProjectId!.Value,
            CustomerTenantId = request.CustomerTenantId!.Value,
            Plan = request.Plan!.Trim(),
            LicenseTemplate = template.Serialize(),
        };

        var checkout = await _paymentProvider.CreateCheckoutSessionAsync(
            new CheckoutSessionRequest
            {
                SubscriptionId = subscription.Id,
                PlanPriceId = request.PlanPriceId!,
                CustomerEmail = context.Email ?? string.Empty,
                SuccessUrl = request.SuccessUrl!,
                CancelUrl = request.CancelUrl!,
                PlatformFeePercent = _platformFeePercent,
            },
            cancellationToken).ConfigureAwait(false);

        if (checkout.IsFailure)
        {
            return checkout.Error!;
        }

        _dbContext.Subscriptions.Add(subscription);

        _outbox.Stage(
            Topics.Billing,
            DomainEventEnvelope.Create(
                eventType: nameof(SubscriptionCreated),
                eventVersion: EventMetadata.Version10,
                publisher: EventMetadata.Publisher,
                correlationId: context.CorrelationId,
                organizationId: subscription.DeveloperOrganizationId,
                tenantId: subscription.CustomerTenantId,
                payload: new SubscriptionCreated
                {
                    SubscriptionId = subscription.Id,
                    CustomerOrganizationId = subscription.CustomerOrganizationId,
                    DeveloperOrganizationId = subscription.DeveloperOrganizationId,
                    ProjectId = subscription.ProjectId,
                    StripeSubscriptionId = subscription.StripeSubscriptionId,
                    Plan = subscription.Plan,
                    CreatedUtc = _timeProvider.GetUtcNow(),
                },
                timeProvider: _timeProvider));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new CheckoutResponse
        {
            SubscriptionId = subscription.Id,
            CheckoutUrl = checkout.Value,
        };
    }
}

/// <summary>Billing configuration (docs/12 revenue model).</summary>
public sealed record BillingOptions
{
    /// <summary>Platform fee percentage (5–15 per docs/12).</summary>
    public required decimal PlatformFeePercent { get; init; }
}
