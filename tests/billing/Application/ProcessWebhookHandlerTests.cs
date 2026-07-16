using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestrictPoint.Api.Billing.Application.Abstractions;
using RestrictPoint.Api.Billing.Application.ProcessWebhook;
using RestrictPoint.Api.Billing.Domain;
using RestrictPoint.Api.Billing.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;
using Xunit;

namespace RestrictPoint.Api.Billing.Tests.Application;

public sealed class ProcessWebhookHandlerTests : IDisposable
{
    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;
    private readonly Guid _subscriptionId;

    public ProcessWebhookHandlerTests()
    {
        _database = new TestDatabase(_time);

        using var seed = _database.CreateContext();
        var subscription = new Subscription
        {
            CustomerOrganizationId = Guid.NewGuid(),
            DeveloperOrganizationId = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            CustomerTenantId = Guid.NewGuid(),
            Plan = "Monthly",
            LicenseTemplate =
                """{"licenseType":"Monthly","features":{"Export":true},"limits":{"maxUsers":10},"webPartGuids":["7f3f2a4e-6f0a-4b7e-9f0d-1234567890ab"]}""",
        };
        seed.Subscriptions.Add(subscription);
        seed.SaveChanges();
        _subscriptionId = subscription.Id;
    }

    public void Dispose() => _database.Dispose();

    private static RequestContext Context() => new() { CorrelationId = "corr-webhook" };

    private ProcessWebhookHandler CreateHandler(BillingDbContext context) =>
        new(context, new OutboxWriter(context), _time, NullLogger<ProcessWebhookHandler>.Instance);

    private WebhookEvent SubscriptionEvent(
        string type = "customer.subscription.updated",
        string stripeStatus = "active",
        string eventId = "evt_1",
        Guid? localId = null) => new()
    {
        EventId = eventId,
        EventType = type,
        Subscription = new SubscriptionEventData
        {
            ProviderSubscriptionId = "sub_stripe_1",
            ProviderStatus = stripeStatus,
            LocalSubscriptionId = localId ?? _subscriptionId,
            ProviderCustomerId = "cus_stripe_1",
            CurrentPeriodStart = _time.UtcNow,
            CurrentPeriodEnd = _time.UtcNow.AddMonths(1),
        },
    };

    [Fact]
    public async Task Activation_transitions_and_emits_event_with_license_template()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), SubscriptionEvent(), CancellationToken.None);

        Assert.True(result.IsSuccess);

        using var verification = _database.CreateContext();
        var subscription = await verification.Subscriptions.SingleAsync();
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal("sub_stripe_1", subscription.StripeSubscriptionId);
        Assert.Equal("cus_stripe_1", subscription.StripeCustomerId);

        var outbox = await verification.OutboxMessages
            .SingleAsync(m => m.EventType == "SubscriptionActivated");
        var envelope = JsonSerializer.Deserialize<DomainEventEnvelope>(
            outbox.Payload, DomainEventEnvelope.SerializerOptions)!;
        Assert.Equal("1.1", envelope.EventVersion);
        Assert.Contains("licenseType", envelope.Payload.GetProperty("licenseTemplate").GetString());
    }

    [Fact]
    public async Task Duplicate_event_id_is_a_no_op()
    {
        using (var first = _database.CreateContext())
        {
            await CreateHandler(first).HandleAsync(Context(), SubscriptionEvent(), CancellationToken.None);
        }

        using (var second = _database.CreateContext())
        {
            var result = await CreateHandler(second).HandleAsync(
                Context(), SubscriptionEvent(), CancellationToken.None);
            Assert.True(result.IsSuccess);
        }

        using var verification = _database.CreateContext();
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "SubscriptionActivated"));
        Assert.Equal(1, await verification.ProcessedWebhookEvents.CountAsync());
    }

    [Fact]
    public async Task Out_of_order_activation_after_cancellation_is_rejected()
    {
        using (var cancel = _database.CreateContext())
        {
            await CreateHandler(cancel).HandleAsync(
                Context(),
                SubscriptionEvent(type: "customer.subscription.deleted", stripeStatus: "canceled", eventId: "evt_cancel"),
                CancellationToken.None);
        }

        // A stale "active" update arrives after cancellation — must not resurrect.
        using (var stale = _database.CreateContext())
        {
            var result = await CreateHandler(stale).HandleAsync(
                Context(), SubscriptionEvent(eventId: "evt_stale"), CancellationToken.None);
            Assert.True(result.IsSuccess); // Acknowledged, but no transition applied.
        }

        using var verification = _database.CreateContext();
        var subscription = await verification.Subscriptions.SingleAsync();
        Assert.Equal(SubscriptionStatus.Canceled, subscription.Status);
        Assert.Equal(0, await verification.OutboxMessages.CountAsync(m => m.EventType == "SubscriptionActivated"));
    }

    private WebhookEvent InvoiceEvent(string type, string eventId, string? failureReason = null) => new()
    {
        EventId = eventId,
        EventType = type,
        Invoice = new InvoiceEventData
        {
            ProviderInvoiceId = "in_stripe_1",
            ProviderSubscriptionId = "sub_stripe_1",
            Amount = 49.99m,
            Currency = "usd",
            ProviderPaymentIntentId = "pi_stripe_1",
            PaidUtc = type == "invoice.paid" ? _time.UtcNow : null,
            FailureReason = failureReason,
        },
    };

    private async Task ActivateAsync()
    {
        using var context = _database.CreateContext();
        await CreateHandler(context).HandleAsync(Context(), SubscriptionEvent(), CancellationToken.None);
    }

    [Fact]
    public async Task Failed_payment_moves_active_subscription_to_past_due()
    {
        await ActivateAsync();

        using (var context = _database.CreateContext())
        {
            await CreateHandler(context).HandleAsync(
                Context(),
                InvoiceEvent("invoice.payment_failed", "evt_fail", "card_declined"),
                CancellationToken.None);
        }

        using var verification = _database.CreateContext();
        Assert.Equal(SubscriptionStatus.PastDue, (await verification.Subscriptions.SingleAsync()).Status);
        Assert.Equal(PaymentStatus.Failed, (await verification.Payments.SingleAsync()).Status);
        Assert.Equal(InvoiceStatus.Failed, (await verification.Invoices.SingleAsync()).Status);
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "PaymentFailed"));
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "SubscriptionPastDue"));
    }

    [Fact]
    public async Task Payment_recovery_returns_past_due_subscription_to_active()
    {
        await ActivateAsync();

        using (var fail = _database.CreateContext())
        {
            await CreateHandler(fail).HandleAsync(
                Context(), InvoiceEvent("invoice.payment_failed", "evt_fail2", "card_declined"),
                CancellationToken.None);
        }

        using (var recover = _database.CreateContext())
        {
            await CreateHandler(recover).HandleAsync(
                Context(), InvoiceEvent("invoice.paid", "evt_recover"), CancellationToken.None);
        }

        using var verification = _database.CreateContext();
        Assert.Equal(SubscriptionStatus.Active, (await verification.Subscriptions.SingleAsync()).Status);
        // Recovery re-activation emits SubscriptionActivated again (Licensing dedupes by subscription).
        Assert.Equal(2, await verification.OutboxMessages.CountAsync(m => m.EventType == "SubscriptionActivated"));
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "PaymentSucceeded"));
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "InvoicePaid"));
    }

    [Fact]
    public async Task Renewal_payment_on_active_subscription_emits_renewed()
    {
        await ActivateAsync();

        using (var renew = _database.CreateContext())
        {
            await CreateHandler(renew).HandleAsync(
                Context(), InvoiceEvent("invoice.paid", "evt_renew"), CancellationToken.None);
        }

        using var verification = _database.CreateContext();
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "SubscriptionRenewed"));
        Assert.Equal(InvoiceStatus.Paid, (await verification.Invoices.SingleAsync()).Status);
        Assert.Equal(PaymentStatus.Succeeded, (await verification.Payments.SingleAsync()).Status);
    }

    [Fact]
    public async Task Cancellation_emits_canceled_event()
    {
        await ActivateAsync();

        using (var cancel = _database.CreateContext())
        {
            await CreateHandler(cancel).HandleAsync(
                Context(),
                SubscriptionEvent(type: "customer.subscription.deleted", stripeStatus: "canceled", eventId: "evt_del"),
                CancellationToken.None);
        }

        using var verification = _database.CreateContext();
        Assert.Equal(SubscriptionStatus.Canceled, (await verification.Subscriptions.SingleAsync()).Status);
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "SubscriptionCanceled"));
    }

    [Fact]
    public async Task Unknown_subscription_webhook_is_acknowledged_without_changes()
    {
        using var context = _database.CreateContext();

        var unknown = new WebhookEvent
        {
            EventId = "evt_unknown",
            EventType = "customer.subscription.updated",
            Subscription = new SubscriptionEventData
            {
                ProviderSubscriptionId = "sub_never_seen",
                ProviderStatus = "active",
            },
        };

        var result = await CreateHandler(context).HandleAsync(Context(), unknown, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, await context.OutboxMessages.CountAsync());
    }

    [Fact]
    public async Task Unhandled_event_types_are_acknowledged_and_recorded()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(),
            new WebhookEvent { EventId = "evt_other", EventType = "charge.updated" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, await context.ProcessedWebhookEvents.CountAsync());
    }
}
