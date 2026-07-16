using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Licensing.Application.Abstractions;
using RestrictPoint.Api.Licensing.Application.Events;
using RestrictPoint.Api.Licensing.Contracts;
using RestrictPoint.Api.Licensing.Domain;
using RestrictPoint.Api.Licensing.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Licensing.Application.RevokeLicense;

/// <summary>Input validation for POST /v1/licenses/revoke.</summary>
public sealed class RevokeLicenseRequestValidator : AbstractValidator<RevokeLicenseRequest>
{
    public RevokeLicenseRequestValidator()
    {
        RuleFor(r => r.LicenseId).NotEmpty();
        RuleFor(r => r.Reason).NotEmpty().MaximumLength(512);
    }
}

/// <summary>
/// POST /v1/licenses/revoke — immediately revokes a license: status change, token
/// revocation, cache invalidation (docs/10 revocation propagation), and LicenseRevoked
/// event. Requires Owner/Admin in the developer organization.
/// </summary>
public sealed class RevokeLicenseHandler
{
    private static readonly string[] RevokingRoles = ["Owner", "Admin"];

    private readonly LicensingDbContext _dbContext;
    private readonly IOrganizationRoleResolver _authorizer;
    private readonly ILicenseCache _cache;
    private readonly IOutboxWriter _outbox;
    private readonly TimeProvider _timeProvider;

    public RevokeLicenseHandler(
        LicensingDbContext dbContext,
        IOrganizationRoleResolver authorizer,
        ILicenseCache cache,
        IOutboxWriter outbox,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _authorizer = authorizer;
        _cache = cache;
        _outbox = outbox;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LicenseSummary>> HandleAsync(
        RequestContext context,
        string bearerToken,
        RevokeLicenseRequest request,
        CancellationToken cancellationToken)
    {
        var license = await _dbContext.Licenses
            .Include(l => l.Features)
            .Include(l => l.Limits)
            .Include(l => l.WebParts)
            .SingleOrDefaultAsync(l => l.Id == request.LicenseId!.Value, cancellationToken)
            .ConfigureAwait(false);

        if (license is null)
        {
            return LicensingErrors.LicenseNotFound;
        }

        var role = await _authorizer
            .GetCallerRoleAsync(bearerToken, license.DeveloperOrganizationId, cancellationToken)
            .ConfigureAwait(false);

        if (role.IsFailure)
        {
            return role.Error!;
        }

        if (role.Value is null || !RevokingRoles.Contains(role.Value, StringComparer.OrdinalIgnoreCase))
        {
            // Non-members receive NotFound to avoid disclosing license existence.
            return role.Value is null
                ? LicensingErrors.LicenseNotFound
                : LicensingErrors.NotAuthorizedForOrganization;
        }

        if (license.Status == LicenseStatus.Revoked)
        {
            return LicensingErrors.AlreadyRevoked;
        }

        var utcNow = _timeProvider.GetUtcNow();

        license.Status = LicenseStatus.Revoked;
        license.RevokedUtc = utcNow;

        var tokens = await _dbContext.LicenseTokens
            .Where(t => t.LicenseId == license.Id && !t.Revoked)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var token in tokens)
        {
            token.Revoked = true;
        }

        _outbox.Stage(
            Topics.License,
            DomainEventEnvelope.Create(
                eventType: nameof(LicenseRevoked),
                eventVersion: EventMetadata.Version10,
                publisher: EventMetadata.Publisher,
                correlationId: context.CorrelationId,
                organizationId: license.DeveloperOrganizationId,
                tenantId: license.CustomerTenantId,
                payload: new LicenseRevoked
                {
                    LicenseId = license.Id,
                    ProjectId = license.ProjectId,
                    Reason = request.Reason!.Trim(),
                    RevokedByUserId = context.UserId,
                    RevokedUtc = utcNow,
                },
                timeProvider: _timeProvider));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Cache invalidation after commit: the next validation sees the revoked state.
        await _cache.InvalidateLicenseAsync(license.Id, cancellationToken).ConfigureAwait(false);

        return LicenseMapper.ToSummary(license);
    }
}

/// <summary>Maps license aggregates to API summaries.</summary>
public static class LicenseMapper
{
    public static LicenseSummary ToSummary(License license)
    {
        ArgumentNullException.ThrowIfNull(license);

        return new LicenseSummary
        {
            Id = license.Id,
            ProjectId = license.ProjectId,
            CustomerOrganizationId = license.CustomerOrganizationId,
            CustomerTenantId = license.CustomerTenantId,
            LicenseType = license.LicenseType.ToString(),
            Status = license.Status.ToString(),
            IssuedUtc = license.IssuedUtc,
            ExpiresUtc = license.ExpiresUtc,
            RevokedUtc = license.RevokedUtc,
            Version = license.Version,
            Features = license.Features.Where(f => !f.IsDeleted).ToDictionary(f => f.FeatureKey, f => f.Enabled),
            Limits = license.Limits.Where(l => !l.IsDeleted).ToDictionary(l => l.LimitKey, l => l.Value),
            WebPartGuids = license.WebParts.Where(w => !w.IsDeleted).Select(w => w.WebPartGuid).ToList(),
        };
    }
}
