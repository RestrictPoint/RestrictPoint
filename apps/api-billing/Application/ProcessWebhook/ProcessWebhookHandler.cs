using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestrictPoint.Api.Billing.Application.Abstractions;
using RestrictPoint.Api.Billing.Application.Events;
using RestrictPoint.Api.Billing.Domain;
using RestrictPoint.Api.Billing.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Billing.Application.ProcessWebhook;

/// <summary>
/// POST /v1/billing/webhook — the Stripe webhook processor (docs/12).
///
/// Correctness properties, in order of enforcement:
/// 1. Signature verification happens before anything else (in the verifier).
/// 2. Idempotency: the ProcessedWebhookEvents unique index on StripeEventId makes
///    redelivered events no-ops; the marker commits atomically with the state change.
/// 3. State transitions go through <see cref="SubscriptionStateMachine"/>; Stripe events
///    arriving out of order can never force an illegal transition — they are logged and
///    acknowledged (returning non-2xx would only cause redelivery of the same event).
/// 4. Domain events are staged in the same transaction (outbox), so financial state and
///    emitted events can never diverge.
/// </summary>
public sealed partial class ProcessWebhookHandler
{
    private readonly BillingDbContext _dbContext;
    private readonly IOutboxWriter _outbox;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ProcessWebhookHandler> _logger;

    public ProcessWebhookHandler(
        BillingDbContext dbContext,
        IOutboxWriter outbox,
        TimeProvider timeProvider,
        ILogger<ProcessWebhookHandler> logger)
    {
        _dbContext = dbContext;
        _outbox = outbox;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<Result.Unit>> HandleAsync(
        RequestContext context,
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        var alreadyProcessed = await _dbContext.ProcessedWebhookEvents
            .AnyAsync(e => e.StripeEventId == webhookEvent.EventId, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyProcessed)
        {
            LogDuplicateEvent(_logger, webhookEvent.EventId, webhookEvent.EventType);
            return Result.Success();
        }

        _dbContext.ProcessedWebhookEvents.Add(new ProcessedWebhookEvent
        {
            StripeEventId = webhookEvent.EventId,
            EventType = webhookEvent.EventType,
            ProcessedUtc = _timeProvider.GetUtcNow(),
        });

        switch (webhookEvent.EventType)
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
                await HandleSubscriptionChangeAsync(context, webhookEvent, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case "customer.subscription.deleted":
                await HandleSubscriptionDeletedAsync(context, webhookEvent, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case "invoice.paid":
                await HandleInvoicePaidAsync(context, webhookEvent, cancellationToken)
                    .ConfigureAwait(false);
                break;

            case "invoice.payment_failed":
                await HandleInvoicePaymentFailedAsync(context, webhookEvent, cancellationToken)
                    .ConfigureAwait(false);
                break;

            default:
                // Unhandled types are acknowledged; the idempotency marker still records them.
                LogUnhandledEventType(_logger, webhookEvent.EventType);
                break;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            // A concurrent delivery of the same event won the race; this one is a no-op.
            LogDuplicateEvent(_logger, webhookEvent.EventId, webhookEvent.EventType);
        }

        return Result.Success();
    }

    private async Task HandleSubscriptionChangeAsync(
        RequestContext context,
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        var data = webhookEvent.Subscription;
        if (data is null)
        {
            LogMalformedEvent(_logger, webhookEvent.EventId, webhookEvent.EventType);
            return;
        }

        var subscription = await FindSubscriptionAsync(data, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            LogUnknownSubscription(_logger, data.ProviderSubscriptionId, webhookEvent.EventType);
            return;
        }

        // First contact from Stripe: bind the provider identifiers created at checkout.
        subscription.StripeSubscriptionId ??= data.ProviderSubscriptionId;
        subscription.StripeCustomerId ??= data.ProviderCustomerId;
        subscription.CurrentPeriodStart = data.CurrentPeriodStart ?? subscription.CurrentPeriodStart;
        subscription.CurrentPeriodEnd = data.CurrentPeriodEnd ?? subscription.CurrentPeriodEnd;
        subscription.CancelAtPeriodEnd = data.CancelAtPeriodEnd;

        var targetStatus = MapStripeStatus(data.ProviderStatus);
        if (targetStatus is null)
        {
            LogUnknownStripeStatus(_logger, data.ProviderStatus, webhookEvent.EventId);
            return;
        }

        TransitionSubscription(context, subscription, targetStatus.Value, webhookEvent.EventType);
    }

    private async Task HandleSubscriptionDeletedAsync(
        RequestContext context,
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        var data = webhookEvent.Subscription;
        if (data is null)
        {
            LogMalformedEvent(_logger, webhookEvent.EventId, webhookEvent.EventType);
            return;
        }

        var subscription = await FindSubscriptionAsync(data, cancellationToken).ConfigureAwait(false);
        if (subscription is null)
        {
            LogUnknownSubscription(_logger, data.ProviderSubscriptionId, webhookEvent.EventType);
            return;
        }

        TransitionSubscription(context, subscription, SubscriptionStatus.Canceled, webhookEvent.EventType);
    }

    private async Task HandleInvoicePaidAsync(
        RequestContext context,
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        var data = webhookEvent.Invoice;
        if (data is null)
        {
            LogMalformedEvent(_logger, webhookEvent.EventId, webhookEvent.EventType);
            return;
        }

        var subscription = await _dbContext.Subscriptions
            .SingleOrDefaultAsync(s => s.StripeSubscriptionId == data.ProviderSubscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            LogUnknownSubscription(_logger, data.ProviderSubscriptionId, webhookEvent.EventType);
            return;
        }

        var utcNow = _timeProvider.GetUtcNow();

        var invoice = await UpsertInvoiceAsync(subscription, data, cancellationToken).ConfigureAwait(false);
        invoice.Status = InvoiceStatus.Paid;
        invoice.PaidUtc = data.PaidUtc ?? utcNow;

        var payment = new Payment
        {
            SubscriptionId = subscription.Id,
            InvoiceId = invoice.Id,
            Amount = data.Amount,
            Currency = data.Currency,
            Status = PaymentStatus.Succeeded,
            StripePaymentIntentId = data.ProviderPaymentIntentId ?? $"invoice:{data.ProviderInvoiceId}",
            ProcessedUtc = utcNow,
        };
        _dbContext.Payments.Add(payment);

        Stage(context, nameof(InvoicePaid), EventMetadata.Version10, subscription, new InvoicePaid
        {
            InvoiceId = invoice.Id,
            SubscriptionId = subscription.Id,
            StripeInvoiceId = data.ProviderInvoiceId,
            Amount = data.Amount,
            Currency = data.Currency,
            PaidUtc = invoice.PaidUtc.Value,
        });

        Stage(context, nameof(PaymentSucceeded), EventMetadata.Version10, subscription, new PaymentSucceeded
        {
            PaymentId = payment.Id,
            SubscriptionId = subscription.Id,
            StripePaymentIntentId = payment.StripePaymentIntentId,
            Amount = data.Amount,
            Currency = data.Currency,
            PaidUtc = invoice.PaidUtc.Value,
        });

        // Payment recovery: past-due subscriptions return to Active on successful payment.
        if (subscription.Status == SubscriptionStatus.PastDue)
        {
            TransitionSubscription(context, subscription, SubscriptionStatus.Active, webhookEvent.EventType);
        }
        else if (subscription.Status == SubscriptionStatus.Active)
        {
            Stage(context, nameof(SubscriptionRenewed), EventMetadata.Version10, subscription,
                new SubscriptionRenewed
                {
                    SubscriptionId = subscription.Id,
                    InvoiceId = invoice.Id,
                    StripeInvoiceId = data.ProviderInvoiceId,
                    RenewalPeriodStartUtc = subscription.CurrentPeriodStart,
                    RenewalPeriodEndUtc = subscription.CurrentPeriodEnd,
                    RenewedUtc = utcNow,
                });
        }
    }

    private async Task HandleInvoicePaymentFailedAsync(
        RequestContext context,
        WebhookEvent webhookEvent,
        CancellationToken cancellationToken)
    {
        var data = webhookEvent.Invoice;
        if (data is null)
        {
            LogMalformedEvent(_logger, webhookEvent.EventId, webhookEvent.EventType);
            return;
        }

        var subscription = await _dbContext.Subscriptions
            .SingleOrDefaultAsync(s => s.StripeSubscriptionId == data.ProviderSubscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (subscription is null)
        {
            LogUnknownSubscription(_logger, data.ProviderSubscriptionId, webhookEvent.EventType);
            return;
        }

        var utcNow = _timeProvider.GetUtcNow();

        var invoice = await UpsertInvoiceAsync(subscription, data, cancellationToken).ConfigureAwait(false);
        invoice.Status = InvoiceStatus.Failed;

        var payment = new Payment
        {
            SubscriptionId = subscription.Id,
            InvoiceId = invoice.Id,
            Amount = data.Amount,
            Currency = data.Currency,
            Status = PaymentStatus.Failed,
            StripePaymentIntentId = data.ProviderPaymentIntentId ?? $"invoice:{data.ProviderInvoiceId}",
            ProcessedUtc = utcNow,
            FailureReason = data.FailureReason ?? "payment_failed",
        };
        _dbContext.Payments.Add(payment);

        Stage(context, nameof(PaymentFailed), EventMetadata.Version10, subscription, new PaymentFailed
        {
            PaymentId = payment.Id,
            SubscriptionId = subscription.Id,
            FailureReason = payment.FailureReason!,
            FailedUtc = utcNow,
        });

        // Dunning begins: Active → PastDue (grace period; license stays valid per docs/12).
        if (subscription.Status == SubscriptionStatus.Active)
        {
            TransitionSubscription(context, subscription, SubscriptionStatus.PastDue, webhookEvent.EventType);
        }
    }

    /// <summary>
    /// Applies a state transition through the state machine, staging the corresponding
    /// domain events. Illegal transitions are logged and skipped, never forced.
    /// </summary>
    private void TransitionSubscription(
        RequestContext context,
        Subscription subscription,
        SubscriptionStatus target,
        string sourceEventType)
    {
        var current = subscription.Status;
        if (current == target)
        {
            return;
        }

        if (!SubscriptionStateMachine.CanTransition(current, target))
        {
            LogIllegalTransition(_logger, subscription.Id, current, target, sourceEventType);
            return;
        }

        subscription.Status = target;
        var utcNow = _timeProvider.GetUtcNow();

        switch (target)
        {
            case SubscriptionStatus.Active:
                // The activation event carries the license template — the Licensing
                // service issues from this event alone (docs/12: Billing never issues).
                Stage(context, nameof(SubscriptionActivated), EventMetadata.Version11, subscription,
                    new SubscriptionActivated
                    {
                        SubscriptionId = subscription.Id,
                        CustomerOrganizationId = subscription.CustomerOrganizationId,
                        ProjectId = subscription.ProjectId,
                        StripeSubscriptionId = subscription.StripeSubscriptionId,
                        ActivatedUtc = utcNow,
                        DeveloperOrganizationId = subscription.DeveloperOrganizationId,
                        CustomerTenantId = subscription.CustomerTenantId,
                        CurrentPeriodEnd = subscription.CurrentPeriodEnd,
                        LicenseTemplate = subscription.LicenseTemplate,
                    });
                break;

            case SubscriptionStatus.PastDue:
                Stage(context, nameof(SubscriptionPastDue), EventMetadata.Version10, subscription,
                    new SubscriptionPastDue
                    {
                        SubscriptionId = subscription.Id,
                        PastDueUtc = utcNow,
                    });
                break;

            case SubscriptionStatus.Canceled:
                Stage(context, nameof(SubscriptionCanceled), EventMetadata.Version10, subscription,
                    new SubscriptionCanceled
                    {
                        SubscriptionId = subscription.Id,
                        CancellationReason = sourceEventType,
                        EffectiveUtc = subscription.CancelAtPeriodEnd
                            ? subscription.CurrentPeriodEnd ?? utcNow
                            : utcNow,
                        CanceledUtc = utcNow,
                    });
                break;

            default:
                break;
        }
    }

    private async Task<Subscription?> FindSubscriptionAsync(
        SubscriptionEventData data,
        CancellationToken cancellationToken)
    {
        // Prefer the metadata correlation set at checkout (works before Stripe ids bind).
        if (data.LocalSubscriptionId is not null)
        {
            var byLocalId = await _dbContext.Subscriptions
                .SingleOrDefaultAsync(s => s.Id == data.LocalSubscriptionId, cancellationToken)
                .ConfigureAwait(false);

            if (byLocalId is not null)
            {
                return byLocalId;
            }
        }

        return await _dbContext.Subscriptions
            .SingleOrDefaultAsync(
                s => s.StripeSubscriptionId == data.ProviderSubscriptionId, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Invoice> UpsertInvoiceAsync(
        Subscription subscription,
        InvoiceEventData data,
        CancellationToken cancellationToken)
    {
        var invoice = await _dbContext.Invoices
            .SingleOrDefaultAsync(i => i.StripeInvoiceId == data.ProviderInvoiceId, cancellationToken)
            .ConfigureAwait(false);

        if (invoice is not null)
        {
            return invoice;
        }

        invoice = new Invoice
        {
            CustomerOrganizationId = subscription.CustomerOrganizationId,
            SubscriptionId = subscription.Id,
            Amount = data.Amount,
            Currency = data.Currency,
            StripeInvoiceId = data.ProviderInvoiceId,
            DueDate = data.DueDate,
        };
        _dbContext.Invoices.Add(invoice);
        return invoice;
    }

    private void Stage<TPayload>(
        RequestContext context,
        string eventType,
        string version,
        Subscription subscription,
        TPayload payload)
        where TPayload : notnull =>
        _outbox.Stage(
            Topics.Billing,
            DomainEventEnvelope.Create(
                eventType: eventType,
                eventVersion: version,
                publisher: EventMetadata.Publisher,
                correlationId: context.CorrelationId,
                organizationId: subscription.DeveloperOrganizationId,
                tenantId: subscription.CustomerTenantId,
                payload: payload,
                timeProvider: _timeProvider));

    private static SubscriptionStatus? MapStripeStatus(string stripeStatus) => stripeStatus switch
    {
        "trialing" => SubscriptionStatus.Trialing,
        "active" => SubscriptionStatus.Active,
        "past_due" => SubscriptionStatus.PastDue,
        "unpaid" => SubscriptionStatus.PastDue,
        "paused" => SubscriptionStatus.Paused,
        "canceled" => SubscriptionStatus.Canceled,
        "incomplete" => SubscriptionStatus.Trialing,
        "incomplete_expired" => SubscriptionStatus.Expired,
        _ => null,
    };

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.InnerException?.Message.Contains("ProcessedWebhookEvents", StringComparison.OrdinalIgnoreCase) == true
        || exception.InnerException?.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) == true
        || exception.InnerException?.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase) == true;

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Duplicate webhook event {EventId} ({EventType}) skipped (idempotency).")]
    private static partial void LogDuplicateEvent(ILogger logger, string eventId, string eventType);

    [LoggerMessage(Level = LogLevel.Information, Message = "Unhandled webhook event type {EventType} acknowledged.")]
    private static partial void LogUnhandledEventType(ILogger logger, string eventType);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Webhook event {EventId} ({EventType}) is missing its expected data section.")]
    private static partial void LogMalformedEvent(ILogger logger, string eventId, string eventType);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Webhook for unknown subscription {ProviderSubscriptionId} ({EventType}) acknowledged.")]
    private static partial void LogUnknownSubscription(ILogger logger, string providerSubscriptionId, string eventType);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Unknown Stripe subscription status '{StripeStatus}' in event {EventId}.")]
    private static partial void LogUnknownStripeStatus(ILogger logger, string stripeStatus, string eventId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Illegal transition {Current} -> {Target} for subscription {SubscriptionId} from {SourceEventType}; skipped.")]
    private static partial void LogIllegalTransition(
        ILogger logger, Guid subscriptionId, SubscriptionStatus current, SubscriptionStatus target, string sourceEventType);
}
