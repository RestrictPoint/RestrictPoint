using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RestrictPoint.Api.Licensing.Application.Common;
using RestrictPoint.Api.Licensing.Application.ConsumeBillingEvents;
using RestrictPoint.Api.Licensing.Domain;
using RestrictPoint.Api.Licensing.Infrastructure;
using RestrictPoint.Database;
using RestrictPoint.Messaging;
using Xunit;

namespace RestrictPoint.Api.Licensing.Tests.Application;

public sealed class SubscriptionActivatedConsumerTests : IDisposable
{
    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;
    private readonly InMemoryLicenseSigning _signing = new();
    private readonly Guid _subscriptionId = Guid.NewGuid();
    private readonly Guid _webPartGuid = Guid.NewGuid();

    public SubscriptionActivatedConsumerTests()
    {
        _database = new TestDatabase(_time);
    }

    public void Dispose()
    {
        _database.Dispose();
        _signing.Dispose();
    }

    private SubscriptionActivatedConsumer CreateConsumer(LicensingDbContext context) =>
        new(
            context,
            new LicenseIssuanceService(
                context,
                new LicenseTokenService(_signing, _signing),
                new OutboxWriter(context),
                _time),
            NullLogger<SubscriptionActivatedConsumer>.Instance);

    private DomainEventEnvelope ActivationEnvelope(
        string? licenseType = "Monthly",
        DateTimeOffset? periodEnd = null)
    {
        var template = JsonSerializer.Serialize(new
        {
            licenseType,
            features = new Dictionary<string, bool> { ["Export"] = true },
            limits = new Dictionary<string, int> { ["maxUsers"] = 10 },
            webPartGuids = new[] { _webPartGuid },
        });

        return DomainEventEnvelope.Create(
            eventType: "SubscriptionActivated",
            eventVersion: "1.1",
            publisher: "billing",
            correlationId: "corr-saga",
            organizationId: Guid.NewGuid(),
            payload: new
            {
                subscriptionId = _subscriptionId,
                customerOrganizationId = Guid.NewGuid(),
                projectId = Guid.NewGuid(),
                developerOrganizationId = Guid.NewGuid(),
                customerTenantId = Guid.NewGuid(),
                currentPeriodEnd = periodEnd ?? _time.UtcNow.AddMonths(1),
                licenseTemplate = template,
            },
            timeProvider: _time);
    }

    [Fact]
    public async Task Activation_issues_license_bound_to_subscription()
    {
        using var context = _database.CreateContext();

        await CreateConsumer(context).HandleAsync(ActivationEnvelope(), CancellationToken.None);

        using var verification = _database.CreateContext();
        var license = await verification.Licenses
            .Include(l => l.Features)
            .Include(l => l.WebParts)
            .SingleAsync();

        Assert.Equal(_subscriptionId, license.SubscriptionId);
        Assert.Equal(LicenseType.Monthly, license.LicenseType);
        Assert.Equal(LicenseStatus.Active, license.Status);
        Assert.NotNull(license.ExpiresUtc);
        Assert.Single(license.WebParts, w => w.WebPartGuid == _webPartGuid);

        Assert.Equal(1, await verification.LicenseTokens.CountAsync());
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "LicenseIssued"));
    }

    [Fact]
    public async Task Duplicate_activation_does_not_double_issue()
    {
        using (var first = _database.CreateContext())
        {
            await CreateConsumer(first).HandleAsync(ActivationEnvelope(), CancellationToken.None);
        }

        using (var second = _database.CreateContext())
        {
            await CreateConsumer(second).HandleAsync(ActivationEnvelope(), CancellationToken.None);
        }

        using var verification = _database.CreateContext();
        Assert.Equal(1, await verification.Licenses.CountAsync());
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "LicenseIssued"));
    }

    [Fact]
    public async Task Non_activation_events_are_ignored()
    {
        using var context = _database.CreateContext();

        var envelope = DomainEventEnvelope.Create(
            eventType: "PaymentSucceeded",
            eventVersion: "1.0",
            publisher: "billing",
            correlationId: "corr-x",
            organizationId: Guid.NewGuid(),
            payload: new { paymentId = Guid.NewGuid() },
            timeProvider: _time);

        await CreateConsumer(context).HandleAsync(envelope, CancellationToken.None);

        Assert.Equal(0, await context.Licenses.CountAsync());
    }

    [Fact]
    public async Task Unknown_license_type_throws_for_dead_lettering()
    {
        using var context = _database.CreateContext();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateConsumer(context).HandleAsync(
                ActivationEnvelope(licenseType: "Forever"), CancellationToken.None));

        Assert.Equal(0, await context.Licenses.CountAsync());
    }
}
