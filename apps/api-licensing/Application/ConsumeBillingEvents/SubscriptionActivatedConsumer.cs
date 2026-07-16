using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestrictPoint.Api.Licensing.Application.Common;
using RestrictPoint.Api.Licensing.Domain;
using RestrictPoint.Api.Licensing.Infrastructure;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Licensing.Application.ConsumeBillingEvents;

/// <summary>
/// Consumes SubscriptionActivated from the Billing service and issues the license
/// (docs/12: Billing never issues licenses directly). Idempotent by subscription id —
/// Service Bus at-least-once delivery can never double-issue.
/// </summary>
public sealed partial class SubscriptionActivatedConsumer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly LicensingDbContext _dbContext;
    private readonly LicenseIssuanceService _issuance;
    private readonly ILogger<SubscriptionActivatedConsumer> _logger;

    public SubscriptionActivatedConsumer(
        LicensingDbContext dbContext,
        LicenseIssuanceService issuance,
        ILogger<SubscriptionActivatedConsumer> logger)
    {
        _dbContext = dbContext;
        _issuance = issuance;
        _logger = logger;
    }

    /// <summary>
    /// Processes one billing-topic envelope. Non-activation events and duplicates are
    /// acknowledged as no-ops. Malformed activation events throw so Service Bus retries
    /// and eventually dead-letters them for investigation.
    /// </summary>
    public async Task HandleAsync(DomainEventEnvelope envelope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.EventType != "SubscriptionActivated")
        {
            return;
        }

        var payload = envelope.Payload.Deserialize<SubscriptionActivatedPayload>(JsonOptions)
            ?? throw new JsonException("SubscriptionActivated payload deserialized to null.");

        // Idempotency: at-least-once delivery must never issue twice for one subscription.
        var existing = await _dbContext.Licenses
            .AnyAsync(l => l.SubscriptionId == payload.SubscriptionId, cancellationToken)
            .ConfigureAwait(false);

        if (existing)
        {
            LogAlreadyIssued(_logger, payload.SubscriptionId);
            return;
        }

        var template = JsonSerializer.Deserialize<LicenseTemplatePayload>(payload.LicenseTemplate, JsonOptions)
            ?? throw new JsonException("License template deserialized to null.");

        if (!Enum.TryParse<LicenseType>(template.LicenseType, ignoreCase: true, out var licenseType)
            || !Enum.IsDefined(licenseType))
        {
            throw new InvalidOperationException(
                $"SubscriptionActivated for {payload.SubscriptionId} carries unknown license type " +
                $"'{template.LicenseType}'.");
        }

        if (licenseType != LicenseType.Lifetime && payload.CurrentPeriodEnd is null)
        {
            throw new InvalidOperationException(
                $"SubscriptionActivated for {payload.SubscriptionId} has no period end for a " +
                $"non-lifetime license.");
        }

        var (license, _) = await _issuance.IssueAsync(
            new LicenseIssuanceSpec
            {
                ProjectId = payload.ProjectId,
                DeveloperOrganizationId = payload.DeveloperOrganizationId,
                CustomerOrganizationId = payload.CustomerOrganizationId,
                CustomerTenantId = payload.CustomerTenantId,
                LicenseType = licenseType,
                ExpiresUtc = payload.CurrentPeriodEnd,
                SubscriptionId = payload.SubscriptionId,
                Features = template.Features ?? new Dictionary<string, bool>(),
                Limits = template.Limits ?? new Dictionary<string, int>(),
                WebPartGuids = template.WebPartGuids ?? [],
            },
            envelope.CorrelationId,
            cancellationToken).ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        LogLicenseIssued(_logger, license.Id, payload.SubscriptionId);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "License already issued for subscription {SubscriptionId}; duplicate activation skipped.")]
    private static partial void LogAlreadyIssued(ILogger logger, Guid subscriptionId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "License {LicenseId} issued for subscription {SubscriptionId}.")]
    private static partial void LogLicenseIssued(ILogger logger, Guid licenseId, Guid subscriptionId);

    /// <summary>Consumer-side view of the Billing SubscriptionActivated v1.1 payload.</summary>
    internal sealed record SubscriptionActivatedPayload
    {
        public required Guid SubscriptionId { get; init; }

        public required Guid CustomerOrganizationId { get; init; }

        public required Guid ProjectId { get; init; }

        public required Guid DeveloperOrganizationId { get; init; }

        public required Guid CustomerTenantId { get; init; }

        public DateTimeOffset? CurrentPeriodEnd { get; init; }

        public required string LicenseTemplate { get; init; }
    }

    /// <summary>Consumer-side view of the license template JSON.</summary>
    internal sealed record LicenseTemplatePayload
    {
        public required string LicenseType { get; init; }

        public Dictionary<string, bool>? Features { get; init; }

        public Dictionary<string, int>? Limits { get; init; }

        public List<Guid>? WebPartGuids { get; init; }
    }
}
