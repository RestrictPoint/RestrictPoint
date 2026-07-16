using RestrictPoint.Api.Licensing.Application.Events;
using RestrictPoint.Api.Licensing.Domain;
using RestrictPoint.Api.Licensing.Infrastructure;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Licensing.Application.Common;

/// <summary>Inputs for issuing a license, independent of the trigger (HTTP or billing event).</summary>
public sealed record LicenseIssuanceSpec
{
    public required Guid ProjectId { get; init; }

    public required Guid DeveloperOrganizationId { get; init; }

    public required Guid CustomerOrganizationId { get; init; }

    public required Guid CustomerTenantId { get; init; }

    public required LicenseType LicenseType { get; init; }

    public DateTimeOffset? ExpiresUtc { get; init; }

    public Guid? SubscriptionId { get; init; }

    public required IReadOnlyDictionary<string, bool> Features { get; init; }

    public required IReadOnlyDictionary<string, int> Limits { get; init; }

    public required IReadOnlyList<Guid> WebPartGuids { get; init; }
}

/// <summary>
/// Core license issuance: creates the aggregate, signs the token, and stages the
/// LicenseIssued event. Shared by the HTTP issue endpoint and the SubscriptionActivated
/// consumer. The caller owns SaveChanges.
/// </summary>
public sealed class LicenseIssuanceService
{
    private readonly LicensingDbContext _dbContext;
    private readonly LicenseTokenService _tokenService;
    private readonly IOutboxWriter _outbox;
    private readonly TimeProvider _timeProvider;

    public LicenseIssuanceService(
        LicensingDbContext dbContext,
        LicenseTokenService tokenService,
        IOutboxWriter outbox,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _outbox = outbox;
        _timeProvider = timeProvider;
    }

    /// <summary>Creates the license and its signed token; stages LicenseIssued.</summary>
    public async Task<(License License, string Token)> IssueAsync(
        LicenseIssuanceSpec spec,
        string correlationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        var utcNow = _timeProvider.GetUtcNow();

        var license = new License
        {
            ProjectId = spec.ProjectId,
            DeveloperOrganizationId = spec.DeveloperOrganizationId,
            CustomerOrganizationId = spec.CustomerOrganizationId,
            CustomerTenantId = spec.CustomerTenantId,
            LicenseType = spec.LicenseType,
            IssuedUtc = utcNow,
            ExpiresUtc = spec.LicenseType == LicenseType.Lifetime ? null : spec.ExpiresUtc,
            SubscriptionId = spec.SubscriptionId,
        };

        foreach (var (key, enabled) in spec.Features)
        {
            license.Features.Add(new LicenseFeature { LicenseId = license.Id, FeatureKey = key, Enabled = enabled });
        }

        foreach (var (key, value) in spec.Limits)
        {
            license.Limits.Add(new LicenseLimit { LicenseId = license.Id, LimitKey = key, Value = value });
        }

        foreach (var webPartGuid in spec.WebPartGuids)
        {
            license.WebParts.Add(new LicenseWebPart { LicenseId = license.Id, WebPartGuid = webPartGuid });
        }

        var payload = LicensePayloadFactory.Create(license, tokenId: Guid.NewGuid().ToString("N"));
        var token = await _tokenService.CreateTokenAsync(payload, cancellationToken).ConfigureAwait(false);

        _dbContext.Licenses.Add(license);
        _dbContext.LicenseTokens.Add(new LicenseToken
        {
            LicenseId = license.Id,
            TokenId = payload.TokenId,
            KeyId = _tokenService.SignerKeyId,
            IssuedUtc = utcNow,
            ExpiresUtc = license.ExpiresUtc,
        });

        _outbox.Stage(
            Topics.License,
            DomainEventEnvelope.Create(
                eventType: nameof(LicenseIssued),
                eventVersion: EventMetadata.Version10,
                publisher: EventMetadata.Publisher,
                correlationId: correlationId,
                organizationId: license.DeveloperOrganizationId,
                tenantId: license.CustomerTenantId,
                payload: new LicenseIssued
                {
                    LicenseId = license.Id,
                    ProjectId = license.ProjectId,
                    CustomerOrganizationId = license.CustomerOrganizationId,
                    DeveloperOrganizationId = license.DeveloperOrganizationId,
                    SubscriptionId = license.SubscriptionId,
                    LicenseType = license.LicenseType.ToString(),
                    ExpiresUtc = license.ExpiresUtc,
                    IssuedUtc = license.IssuedUtc,
                },
                timeProvider: _timeProvider));

        return (license, token);
    }
}
