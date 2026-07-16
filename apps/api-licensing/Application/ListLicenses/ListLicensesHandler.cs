using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Licensing.Application.Abstractions;
using RestrictPoint.Api.Licensing.Application.RevokeLicense;
using RestrictPoint.Api.Licensing.Contracts;
using RestrictPoint.Api.Licensing.Domain;
using RestrictPoint.Api.Licensing.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Common;

namespace RestrictPoint.Api.Licensing.Application.ListLicenses;

/// <summary>
/// GET /v1/licenses?organizationId= and GET /v1/licenses/{id} — developer-organization
/// scoped reads. The caller must be an active member of the developer organization.
/// </summary>
public sealed class ListLicensesHandler
{
    private readonly LicensingDbContext _dbContext;
    private readonly IOrganizationRoleResolver _authorizer;

    public ListLicensesHandler(LicensingDbContext dbContext, IOrganizationRoleResolver authorizer)
    {
        _dbContext = dbContext;
        _authorizer = authorizer;
    }

    public async Task<Result<IReadOnlyList<LicenseSummary>>> ListAsync(
        string bearerToken,
        Guid developerOrganizationId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        var membership = await RequireMembershipAsync(bearerToken, developerOrganizationId, cancellationToken)
            .ConfigureAwait(false);

        if (membership.IsFailure)
        {
            return membership.Error!;
        }

        var query = _dbContext.Licenses
            .AsNoTracking()
            .Include(l => l.Features)
            .Include(l => l.Limits)
            .Include(l => l.WebParts)
            .Where(l => l.DeveloperOrganizationId == developerOrganizationId);

        if (projectId is not null)
        {
            query = query.Where(l => l.ProjectId == projectId);
        }

        var licenses = await query
            .OrderByDescending(l => l.IssuedUtc)
            .Take(200)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<LicenseSummary> summaries = licenses.Select(LicenseMapper.ToSummary).ToList();
        return Result.Success(summaries);
    }

    public async Task<Result<LicenseSummary>> GetAsync(
        string bearerToken,
        Guid licenseId,
        CancellationToken cancellationToken)
    {
        var license = await _dbContext.Licenses
            .AsNoTracking()
            .Include(l => l.Features)
            .Include(l => l.Limits)
            .Include(l => l.WebParts)
            .SingleOrDefaultAsync(l => l.Id == licenseId, cancellationToken)
            .ConfigureAwait(false);

        if (license is null)
        {
            return LicensingErrors.LicenseNotFound;
        }

        var membership = await RequireMembershipAsync(
            bearerToken, license.DeveloperOrganizationId, cancellationToken).ConfigureAwait(false);

        if (membership.IsFailure)
        {
            // Non-members receive NotFound to avoid disclosing license existence.
            return membership.Error!.Kind == ErrorKind.Forbidden
                ? LicensingErrors.LicenseNotFound
                : membership.Error!;
        }

        return LicenseMapper.ToSummary(license);
    }

    private async Task<Result<Result.Unit>> RequireMembershipAsync(
        string bearerToken,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var role = await _authorizer.GetCallerRoleAsync(bearerToken, organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (role.IsFailure)
        {
            return role.Error!;
        }

        return role.Value is not null
            ? Result.Success()
            : LicensingErrors.NotAuthorizedForOrganization;
    }
}
