using FluentValidation;
using RestrictPoint.Api.Licensing.Application.Abstractions;
using RestrictPoint.Api.Licensing.Application.Common;
using RestrictPoint.Api.Licensing.Application.Events;
using RestrictPoint.Api.Licensing.Contracts;
using RestrictPoint.Api.Licensing.Domain;
using RestrictPoint.Api.Licensing.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Licensing.Application.IssueLicense;

/// <summary>Input validation for POST /v1/licenses/issue.</summary>
public sealed class IssueLicenseRequestValidator : AbstractValidator<IssueLicenseRequest>
{
    public IssueLicenseRequestValidator()
    {
        RuleFor(r => r.ProjectId).NotEmpty();
        RuleFor(r => r.DeveloperOrganizationId).NotEmpty();
        RuleFor(r => r.CustomerOrganizationId).NotEmpty();
        RuleFor(r => r.CustomerTenantId).NotEmpty();
        RuleFor(r => r.LicenseType).NotEmpty();
        RuleFor(r => r.WebPartGuids).NotEmpty()
            .WithMessage("At least one web part GUID is required for installation binding.");
    }
}

/// <summary>
/// POST /v1/licenses/issue — creates a license and returns its signed token. Requires the
/// caller to hold an issuing role (Owner/Admin/Developer) in the developer organization,
/// verified against the Identity service.
/// </summary>
public sealed class IssueLicenseHandler
{
    private static readonly string[] IssuingRoles = ["Owner", "Admin", "Developer"];

    private readonly LicensingDbContext _dbContext;
    private readonly LicenseIssuanceService _issuance;
    private readonly IOrganizationRoleResolver _authorizer;
    private readonly TimeProvider _timeProvider;

    public IssueLicenseHandler(
        LicensingDbContext dbContext,
        LicenseIssuanceService issuance,
        IOrganizationRoleResolver authorizer,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _issuance = issuance;
        _authorizer = authorizer;
        _timeProvider = timeProvider;
    }

    public async Task<Result<LicenseIssuedResponse>> HandleAsync(
        RequestContext context,
        string bearerToken,
        IssueLicenseRequest request,
        CancellationToken cancellationToken)
    {
        var authorization = await AuthorizeAsync(
            bearerToken, request.DeveloperOrganizationId!.Value, cancellationToken).ConfigureAwait(false);

        if (authorization.IsFailure)
        {
            return authorization.Error!;
        }

        if (!Enum.TryParse<LicenseType>(request.LicenseType, ignoreCase: true, out var licenseType)
            || !Enum.IsDefined(licenseType))
        {
            return LicensingErrors.InvalidLicenseType;
        }

        var utcNow = _timeProvider.GetUtcNow();

        if (licenseType != LicenseType.Lifetime
            && (request.ExpiresUtc is null || request.ExpiresUtc <= utcNow))
        {
            return LicensingErrors.ExpiryRequired;
        }

        var (license, token) = await _issuance.IssueAsync(
            new LicenseIssuanceSpec
            {
                ProjectId = request.ProjectId!.Value,
                DeveloperOrganizationId = request.DeveloperOrganizationId!.Value,
                CustomerOrganizationId = request.CustomerOrganizationId!.Value,
                CustomerTenantId = request.CustomerTenantId!.Value,
                LicenseType = licenseType,
                ExpiresUtc = request.ExpiresUtc,
                SubscriptionId = request.SubscriptionId,
                Features = request.Features ?? new Dictionary<string, bool>(),
                Limits = request.Limits ?? new Dictionary<string, int>(),
                WebPartGuids = request.WebPartGuids!,
            },
            context.CorrelationId,
            cancellationToken).ConfigureAwait(false);

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new LicenseIssuedResponse
        {
            LicenseId = license.Id,
            LicenseToken = token,
            LicenseType = license.LicenseType.ToString(),
            Status = license.Status.ToString(),
            ExpiresUtc = license.ExpiresUtc,
            IssuedUtc = license.IssuedUtc,
        };
    }

    private async Task<Result<Result.Unit>> AuthorizeAsync(
        string bearerToken,
        Guid developerOrganizationId,
        CancellationToken cancellationToken)
    {
        var role = await _authorizer.GetCallerRoleAsync(bearerToken, developerOrganizationId, cancellationToken)
            .ConfigureAwait(false);

        if (role.IsFailure)
        {
            return role.Error!;
        }

        return role.Value is not null && IssuingRoles.Contains(role.Value, StringComparer.OrdinalIgnoreCase)
            ? Result.Success()
            : LicensingErrors.NotAuthorizedForOrganization;
    }
}
