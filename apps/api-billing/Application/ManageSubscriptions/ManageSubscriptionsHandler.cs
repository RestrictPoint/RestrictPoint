using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Billing.Application.Abstractions;
using RestrictPoint.Api.Billing.Application.Events;
using RestrictPoint.Api.Billing.Contracts;
using RestrictPoint.Api.Billing.Domain;
using RestrictPoint.Api.Billing.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Billing.Application.ManageSubscriptions;

/// <summary>Input validation for POST /v1/billing/subscriptions/cancel.</summary>
public sealed class CancelSubscriptionRequestValidator : AbstractValidator<CancelSubscriptionRequest>
{
    public CancelSubscriptionRequestValidator()
    {
        RuleFor(r => r.SubscriptionId).NotEmpty();
        RuleFor(r => r.Reason).MaximumLength(512);
    }
}

/// <summary>
/// Subscription management: cancel (customer org Owner/Admin/Billing), list (member of
/// customer or developer org), and invoice listing. Cancellation requests Stripe first;
/// the definitive state change arrives via webhook, but we optimistically stage the
/// cancellation for immediate-cancel requests.
/// </summary>
public sealed class ManageSubscriptionsHandler
{
    private static readonly string[] CancelingRoles = ["Owner", "Admin", "Billing"];

    private readonly BillingDbContext _dbContext;
    private readonly IPaymentProvider _paymentProvider;
    private readonly IOrganizationRoleResolver _roleResolver;
    private readonly IOutboxWriter _outbox;
    private readonly TimeProvider _timeProvider;

    public ManageSubscriptionsHandler(
        BillingDbContext dbContext,
        IPaymentProvider paymentProvider,
        IOrganizationRoleResolver roleResolver,
        IOutboxWriter outbox,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _paymentProvider = paymentProvider;
        _roleResolver = roleResolver;
        _outbox = outbox;
        _timeProvider = timeProvider;
    }

    public async Task<Result<SubscriptionSummary>> CancelAsync(
        RequestContext context,
        string bearerToken,
        CancelSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var subscription = await _dbContext.Subscriptions
            .SingleOrDefaultAsync(s => s.Id == request.SubscriptionId!.Value, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            return BillingErrors.SubscriptionNotFound;
        }

        var role = await _roleResolver
            .GetCallerRoleAsync(bearerToken, subscription.CustomerOrganizationId, cancellationToken)
            .ConfigureAwait(false);

        if (role.IsFailure)
        {
            return role.Error!;
        }

        if (role.Value is null)
        {
            return BillingErrors.SubscriptionNotFound; // No existence disclosure to non-members.
        }

        if (!CancelingRoles.Contains(role.Value, StringComparer.OrdinalIgnoreCase))
        {
            return BillingErrors.NotAuthorizedForOrganization;
        }

        if (subscription.Status is SubscriptionStatus.Canceled or SubscriptionStatus.Expired)
        {
            return BillingErrors.AlreadyCanceled;
        }

        // Stripe first: if the provider call fails, our state is untouched.
        if (subscription.StripeSubscriptionId is not null)
        {
            var providerResult = await _paymentProvider.CancelSubscriptionAsync(
                subscription.StripeSubscriptionId, request.CancelAtPeriodEnd, cancellationToken)
                .ConfigureAwait(false);

            if (providerResult.IsFailure)
            {
                return providerResult.Error!;
            }
        }

        var utcNow = _timeProvider.GetUtcNow();

        if (request.CancelAtPeriodEnd)
        {
            // Definitive state change arrives via the customer.subscription.updated webhook.
            subscription.CancelAtPeriodEnd = true;
        }
        else if (SubscriptionStateMachine.CanTransition(subscription.Status, SubscriptionStatus.Canceled))
        {
            subscription.Status = SubscriptionStatus.Canceled;

            _outbox.Stage(
                Topics.Billing,
                DomainEventEnvelope.Create(
                    eventType: nameof(SubscriptionCanceled),
                    eventVersion: EventMetadata.Version10,
                    publisher: EventMetadata.Publisher,
                    correlationId: context.CorrelationId,
                    organizationId: subscription.DeveloperOrganizationId,
                    tenantId: subscription.CustomerTenantId,
                    payload: new SubscriptionCanceled
                    {
                        SubscriptionId = subscription.Id,
                        CancellationReason = string.IsNullOrWhiteSpace(request.Reason)
                            ? "customer_request"
                            : request.Reason.Trim(),
                        EffectiveUtc = utcNow,
                        CanceledUtc = utcNow,
                    },
                    timeProvider: _timeProvider));
        }

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return ToSummary(subscription);
    }

    public async Task<Result<IReadOnlyList<SubscriptionSummary>>> ListAsync(
        string bearerToken,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var membership = await RequireMembershipAsync(bearerToken, organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (membership.IsFailure)
        {
            return membership.Error!;
        }

        var subscriptions = await _dbContext.Subscriptions
            .AsNoTracking()
            .Where(s => s.CustomerOrganizationId == organizationId
                || s.DeveloperOrganizationId == organizationId)
            .OrderByDescending(s => s.CreatedUtc)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<SubscriptionSummary> summaries = subscriptions.Select(ToSummary).ToList();
        return Result.Success(summaries);
    }

    public async Task<Result<IReadOnlyList<InvoiceSummary>>> ListInvoicesAsync(
        string bearerToken,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var membership = await RequireMembershipAsync(bearerToken, organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (membership.IsFailure)
        {
            return membership.Error!;
        }

        var invoices = await _dbContext.Invoices
            .AsNoTracking()
            .Where(i => i.CustomerOrganizationId == organizationId)
            .OrderByDescending(i => i.CreatedUtc)
            .Take(200)
            .Select(i => new InvoiceSummary
            {
                Id = i.Id,
                SubscriptionId = i.SubscriptionId,
                Amount = i.Amount,
                Currency = i.Currency,
                Status = i.Status.ToString(),
                DueDate = i.DueDate,
                PaidUtc = i.PaidUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result.Success<IReadOnlyList<InvoiceSummary>>(invoices);
    }

    private async Task<Result<Result.Unit>> RequireMembershipAsync(
        string bearerToken,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var role = await _roleResolver.GetCallerRoleAsync(bearerToken, organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (role.IsFailure)
        {
            return role.Error!;
        }

        return role.Value is not null
            ? Result.Success()
            : BillingErrors.NotAuthorizedForOrganization;
    }

    private static SubscriptionSummary ToSummary(Subscription subscription) => new()
    {
        Id = subscription.Id,
        ProjectId = subscription.ProjectId,
        CustomerOrganizationId = subscription.CustomerOrganizationId,
        DeveloperOrganizationId = subscription.DeveloperOrganizationId,
        Status = subscription.Status.ToString(),
        Plan = subscription.Plan,
        CurrentPeriodStart = subscription.CurrentPeriodStart,
        CurrentPeriodEnd = subscription.CurrentPeriodEnd,
        CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
        LicenseId = subscription.LicenseId,
    };
}
