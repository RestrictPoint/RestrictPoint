using FluentValidation;
using RestrictPoint.Api.Licensing.Application.Abstractions;
using RestrictPoint.Api.Licensing.Application.Common;
using RestrictPoint.Api.Licensing.Application.Events;
using RestrictPoint.Api.Licensing.Contracts;
using RestrictPoint.Api.Licensing.Domain;
using RestrictPoint.Api.Licensing.Infrastructure;
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
    private readonly LicenseTokenService _tokenService;
    private readonly IOrganizationAuthorizer _authorizer;
    private readonly IOutboxWriter _outbox;
    private readonly TimeProvider _timeProvider;

    public IssueLicenseHandler(
        LicensingDbContext dbContext,
        LicenseTokenService tokenService,
        IOrganizationAuthorizer authorizer,
        IOutboxWriter outbox,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _authorizer = authorizer;
        _outbox = outbox;
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

        var license = new License
        {
            ProjectId = request.ProjectId!.Value,
            DeveloperOrganizationId = request.DeveloperOrganizationId!.Value,
            CustomerOrganizationId = request.CustomerOrganizationId!.Value,
            CustomerTenantId = request.CustomerTenantId!.Value,
            LicenseType = licenseType,
            IssuedUtc = utcNow,
            ExpiresUtc = licenseType == LicenseType.Lifetime ? null : request.ExpiresUtc,
            SubscriptionId = request.SubscriptionId,
        };

        foreach (var (key, enabled) in request.Features ?? new Dictionary<string, bool>())
        {
            license.Features.Add(new LicenseFeature { LicenseId = license.Id, FeatureKey = key, Enabled = enabled });
        }

        foreach (var (key, value) in request.Limits ?? new Dictionary<string, int>())
        {
            license.Limits.Add(new LicenseLimit { LicenseId = license.Id, LimitKey = key, Value = value });
        }

        foreach (var webPartGuid in request.WebPartGuids!)
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
                correlationId: context.CorrelationId,
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
