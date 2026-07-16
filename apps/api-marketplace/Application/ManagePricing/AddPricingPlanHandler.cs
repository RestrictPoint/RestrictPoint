using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Marketplace.Application.Common;
using RestrictPoint.Api.Marketplace.Application.Events;
using RestrictPoint.Api.Marketplace.Contracts;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Marketplace.Application.ManagePricing;

/// <summary>Input validation for POST /v1/marketplace/listings/{id}/pricing.</summary>
public sealed class AddPricingPlanRequestValidator : AbstractValidator<AddPricingPlanRequest>
{
    private static readonly string[] PricingTypes =
        [nameof(PricingType.Free), nameof(PricingType.OneTimePurchase),
         nameof(PricingType.MonthlySubscription), nameof(PricingType.AnnualSubscription)];

    private static readonly string[] BillingIntervals =
        [nameof(BillingInterval.Monthly), nameof(BillingInterval.Annual)];

    public AddPricingPlanRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(128);
        RuleFor(r => r.PricingType).NotEmpty()
            .Must(t => PricingTypes.Contains(t, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"PricingType must be one of: {string.Join(", ", PricingTypes)}.");
        RuleFor(r => r.Price).NotNull().GreaterThanOrEqualTo(0);
        RuleFor(r => r.Currency).NotEmpty().Length(3);
        RuleFor(r => r.BillingInterval)
            .Must(i => i is null || BillingIntervals.Contains(i, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"BillingInterval must be one of: {string.Join(", ", BillingIntervals)}.");
        RuleFor(r => r.TrialDays).InclusiveBetween(0, 365).When(r => r.TrialDays is not null);
    }
}

/// <summary>
/// POST /v1/marketplace/listings/{id}/pricing — adds a pricing plan to a listing. Requires
/// a publishing role in the owning organization. Domain invariants (Free price=0,
/// subscriptions need an interval) are enforced by <see cref="PricingPlan.Create"/>.
/// </summary>
public sealed class AddPricingPlanHandler
{
    private static readonly string[] PublishingRoles = ["Owner", "Admin", "Developer"];

    private readonly MarketplaceDbContext _dbContext;
    private readonly IOrganizationRoleResolver _roleResolver;
    private readonly IOutboxWriter _outbox;
    private readonly TimeProvider _timeProvider;

    public AddPricingPlanHandler(
        MarketplaceDbContext dbContext,
        IOrganizationRoleResolver roleResolver,
        IOutboxWriter outbox,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _roleResolver = roleResolver;
        _outbox = outbox;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PricingPlanSummary>> HandleAsync(
        RequestContext context,
        string bearerToken,
        Guid listingId,
        AddPricingPlanRequest request,
        CancellationToken cancellationToken)
    {
        var listing = await _dbContext.Listings
            .SingleOrDefaultAsync(l => l.Id == listingId, cancellationToken)
            .ConfigureAwait(false);

        if (listing is null)
        {
            return MarketplaceErrors.ListingNotFound;
        }

        var role = await _roleResolver
            .GetCallerRoleAsync(bearerToken, listing.OrganizationId, cancellationToken)
            .ConfigureAwait(false);

        if (role.IsFailure)
        {
            return role.Error!;
        }

        if (role.Value is null)
        {
            return MarketplaceErrors.ListingNotFound; // No existence disclosure to non-members.
        }

        if (!PublishingRoles.Contains(role.Value, StringComparer.OrdinalIgnoreCase))
        {
            return MarketplaceErrors.NotAuthorizedForOrganization;
        }

        var pricingType = Enum.Parse<PricingType>(request.PricingType!, ignoreCase: true);
        var billingInterval = request.BillingInterval is null
            ? (BillingInterval?)null
            : Enum.Parse<BillingInterval>(request.BillingInterval, ignoreCase: true);

        PricingPlan plan;
        try
        {
            plan = PricingPlan.Create(
                listing.Id,
                request.Name!,
                pricingType,
                request.Price!.Value,
                request.Currency!,
                billingInterval,
                request.TrialDays ?? 0,
                request.LicenseTemplate);
        }
        catch (ArgumentException exception)
        {
            return Error.Validation("Marketplace.InvalidPricingPlan", exception.Message);
        }

        _dbContext.PricingPlans.Add(plan);

        _outbox.Stage(
            Topics.Marketplace,
            DomainEventEnvelope.Create(
                eventType: nameof(PricingModelCreated),
                eventVersion: EventMetadata.Version10,
                publisher: EventMetadata.Publisher,
                correlationId: context.CorrelationId,
                organizationId: listing.OrganizationId,
                payload: new PricingModelCreated
                {
                    PricingModelId = plan.Id,
                    ProjectId = listing.ProjectId,
                    OrganizationId = listing.OrganizationId,
                    PricingType = plan.PricingType.ToString(),
                    CreatedUtc = _timeProvider.GetUtcNow(),
                },
                timeProvider: _timeProvider));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ListingMapper.ToPricingSummary(plan);
    }
}
