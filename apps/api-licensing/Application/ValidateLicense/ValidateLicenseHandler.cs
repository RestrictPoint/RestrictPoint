using FluentValidation;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Licensing.Application.Abstractions;
using RestrictPoint.Api.Licensing.Application.Common;
using RestrictPoint.Api.Licensing.Application.Events;
using RestrictPoint.Api.Licensing.Contracts;
using RestrictPoint.Api.Licensing.Domain;
using RestrictPoint.Api.Licensing.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

namespace RestrictPoint.Api.Licensing.Application.ValidateLicense;

/// <summary>Input validation for POST /v1/licenses/validate.</summary>
public sealed class ValidateLicenseRequestValidator : AbstractValidator<ValidateLicenseRequest>
{
    public ValidateLicenseRequestValidator()
    {
        RuleFor(r => r.LicenseToken).NotEmpty();
        RuleFor(r => r.TenantId).NotEmpty();
        RuleFor(r => r.ProjectId).NotEmpty();
        RuleFor(r => r.WebPartGuid).NotEmpty();
        RuleFor(r => r.InstallationId).NotEmpty();
        RuleFor(r => r.Nonce).NotEmpty().MaximumLength(128);
        RuleFor(r => r.TimestampUtc).NotEmpty();
    }
}

/// <summary>
/// POST /v1/licenses/validate — the SDK online-validation critical path (docs/10).
/// The signed license token is the credential; the endpoint is anonymous by design.
/// Order of checks: replay protection → signature → binding → revocation/expiry state.
/// Every outcome emits an asynchronous validation event; the caller never waits on events.
/// </summary>
public sealed class ValidateLicenseHandler
{
    /// <summary>Timestamp tolerance for replay protection (docs/10: ±5 minutes).</summary>
    private static readonly TimeSpan TimestampWindow = TimeSpan.FromMinutes(5);

    /// <summary>Nonce deduplication window: 2× the timestamp window covers all replays.</summary>
    private static readonly TimeSpan NonceWindow = TimeSpan.FromMinutes(10);

    /// <summary>License state cache TTL (docs/10: 12h).</summary>
    private static readonly TimeSpan LicenseCacheTtl = TimeSpan.FromHours(12);

    private readonly LicensingDbContext _dbContext;
    private readonly LicenseTokenService _tokenService;
    private readonly ILicenseCache _cache;
    private readonly IOutboxWriter _outbox;
    private readonly TimeProvider _timeProvider;

    public ValidateLicenseHandler(
        LicensingDbContext dbContext,
        LicenseTokenService tokenService,
        ILicenseCache cache,
        IOutboxWriter outbox,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _cache = cache;
        _outbox = outbox;
        _timeProvider = timeProvider;
    }

    public async Task<Result<ValidateLicenseResponse>> HandleAsync(
        RequestContext context,
        ValidateLicenseRequest request,
        CancellationToken cancellationToken)
    {
        var utcNow = _timeProvider.GetUtcNow();

        // 1. Replay protection (docs/10): timestamp window, then nonce uniqueness.
        if ((utcNow - request.TimestampUtc!.Value).Duration() > TimestampWindow)
        {
            return await FailAsync(context, request, "stale_timestamp", utcNow, cancellationToken)
                .ConfigureAwait(false) ?? LicensingErrors.StaleTimestamp;
        }

        var nonceIsFresh = await _cache
            .TryRegisterNonceAsync(request.Nonce!, NonceWindow, cancellationToken)
            .ConfigureAwait(false);

        if (!nonceIsFresh)
        {
            return await FailAsync(context, request, "nonce_reuse", utcNow, cancellationToken)
                .ConfigureAwait(false) ?? LicensingErrors.ReplayDetected;
        }

        // 2. Cryptographic verification.
        var verification = await _tokenService.VerifyTokenAsync(request.LicenseToken!, cancellationToken)
            .ConfigureAwait(false);

        if (verification.IsFailure)
        {
            return await FailAsync(context, request, verification.Error!.Code, utcNow, cancellationToken)
                .ConfigureAwait(false) ?? verification.Error!;
        }

        var payload = verification.Value;

        // 3. Installation binding (docs/10): tenant, project, and web part must all match.
        if (payload.TenantId != request.TenantId!.Value)
        {
            return await FailAsync(context, request, "tenant_mismatch", utcNow, cancellationToken, payload.LicenseId)
                .ConfigureAwait(false) ?? LicensingErrors.TenantMismatch;
        }

        if (payload.ProjectId != request.ProjectId!.Value)
        {
            return await FailAsync(context, request, "project_mismatch", utcNow, cancellationToken, payload.LicenseId)
                .ConfigureAwait(false) ?? LicensingErrors.ProjectMismatch;
        }

        if (!payload.WebPartGuids.Contains(request.WebPartGuid!.Value))
        {
            return await FailAsync(context, request, "webpart_mismatch", utcNow, cancellationToken, payload.LicenseId)
                .ConfigureAwait(false) ?? LicensingErrors.WebPartMismatch;
        }

        // 4. Revocation and expiry state — cache first, database on miss.
        var state = await ResolveLicenseStateAsync(payload.LicenseId, cancellationToken).ConfigureAwait(false);
        if (state is null)
        {
            return await FailAsync(context, request, "license_not_found", utcNow, cancellationToken, payload.LicenseId)
                .ConfigureAwait(false) ?? LicensingErrors.LicenseNotFound;
        }

        var status = EffectiveStatus(state, utcNow);
        if (status is not LicenseStatus.Active)
        {
            var reason = status.ToString().ToLowerInvariant();
            await StageValidationFailedAsync(context, request, reason, utcNow, payload.LicenseId)
                .ConfigureAwait(false);
            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return new ValidateLicenseResponse
            {
                IsValid = false,
                Status = reason,
                Features = payload.Features,
                Limits = payload.Limits,
                ExpiresAt = state.ExpiresUtc,
                FailureReason = reason,
            };
        }

        // 5. Installation tracking: first contact from an installation id is activation.
        await TrackInstallationAsync(context, request, payload, utcNow, cancellationToken).ConfigureAwait(false);

        _outbox.Stage(
            Topics.License,
            DomainEventEnvelope.Create(
                eventType: nameof(LicenseValidationSucceeded),
                eventVersion: EventMetadata.Version10,
                publisher: EventMetadata.Publisher,
                correlationId: context.CorrelationId,
                organizationId: payload.CustomerId,
                tenantId: payload.TenantId,
                payload: new LicenseValidationSucceeded
                {
                    LicenseId = payload.LicenseId,
                    InstallationId = request.InstallationId!.Value,
                    ProjectId = payload.ProjectId,
                    ValidationMethod = "online",
                    ValidatedUtc = utcNow,
                },
                timeProvider: _timeProvider));

        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return new ValidateLicenseResponse
        {
            IsValid = true,
            Status = "active",
            Features = payload.Features,
            Limits = payload.Limits,
            ExpiresAt = state.ExpiresUtc,
        };
    }

    private static LicenseStatus EffectiveStatus(CachedLicenseState state, DateTimeOffset utcNow)
    {
        if (!Enum.TryParse<LicenseStatus>(state.Status, out var status))
        {
            return LicenseStatus.Revoked; // Unknown persisted status: fail closed.
        }

        return status == LicenseStatus.Active && state.ExpiresUtc is not null && utcNow >= state.ExpiresUtc
            ? LicenseStatus.Expired
            : status;
    }

    private async Task<CachedLicenseState?> ResolveLicenseStateAsync(
        Guid licenseId,
        CancellationToken cancellationToken)
    {
        var cached = await _cache.GetLicenseAsync(licenseId, cancellationToken).ConfigureAwait(false);
        if (cached is not null)
        {
            return cached;
        }

        var license = await _dbContext.Licenses
            .AsNoTracking()
            .SingleOrDefaultAsync(l => l.Id == licenseId, cancellationToken)
            .ConfigureAwait(false);

        if (license is null)
        {
            return null;
        }

        var state = new CachedLicenseState
        {
            LicenseId = license.Id,
            Status = license.Status.ToString(),
            ExpiresUtc = license.ExpiresUtc,
        };

        await _cache.SetLicenseAsync(state, cancellationToken).ConfigureAwait(false);
        return state;
    }

    private async Task TrackInstallationAsync(
        RequestContext context,
        ValidateLicenseRequest request,
        LicensePayload payload,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken)
    {
        var installation = await _dbContext.Installations
            .SingleOrDefaultAsync(
                i => i.LicenseId == payload.LicenseId && i.InstallationId == request.InstallationId!.Value,
                cancellationToken)
            .ConfigureAwait(false);

        if (installation is not null)
        {
            installation.LastValidatedUtc = utcNow;
            installation.SdkVersion = request.SdkVersion ?? installation.SdkVersion;
            return;
        }

        _dbContext.Installations.Add(new Installation
        {
            LicenseId = payload.LicenseId,
            TenantId = request.TenantId!.Value,
            WebPartGuid = request.WebPartGuid!.Value,
            InstallationId = request.InstallationId!.Value,
            SdkVersion = request.SdkVersion,
            InstalledUtc = utcNow,
            LastValidatedUtc = utcNow,
        });

        _outbox.Stage(
            Topics.License,
            DomainEventEnvelope.Create(
                eventType: nameof(LicenseActivated),
                eventVersion: EventMetadata.Version10,
                publisher: EventMetadata.Publisher,
                correlationId: context.CorrelationId,
                organizationId: payload.CustomerId,
                tenantId: payload.TenantId,
                payload: new LicenseActivated
                {
                    LicenseId = payload.LicenseId,
                    InstallationId = request.InstallationId!.Value,
                    CustomerTenantId = request.TenantId!.Value,
                    ActivatedUtc = utcNow,
                },
                timeProvider: _timeProvider));
    }

    /// <summary>
    /// Stages a LicenseValidationFailed event and persists it. Returns null so callers can
    /// fall through to the appropriate error via the null-coalescing pattern.
    /// </summary>
    private async Task<Result<ValidateLicenseResponse>?> FailAsync(
        RequestContext context,
        ValidateLicenseRequest request,
        string reason,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken,
        Guid? licenseId = null)
    {
        await StageValidationFailedAsync(context, request, reason, utcNow, licenseId).ConfigureAwait(false);
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    private Task StageValidationFailedAsync(
        RequestContext context,
        ValidateLicenseRequest request,
        string reason,
        DateTimeOffset utcNow,
        Guid? licenseId)
    {
        _outbox.Stage(
            Topics.License,
            DomainEventEnvelope.Create(
                eventType: nameof(LicenseValidationFailed),
                eventVersion: EventMetadata.Version10,
                publisher: EventMetadata.Publisher,
                correlationId: context.CorrelationId,
                organizationId: Guid.Empty, // Failure may precede trustworthy org resolution.
                tenantId: request.TenantId,
                payload: new LicenseValidationFailed
                {
                    ProjectId = request.ProjectId ?? Guid.Empty,
                    InstallationId = request.InstallationId,
                    LicenseId = licenseId,
                    FailureReason = reason,
                    FailedUtc = utcNow,
                },
                timeProvider: _timeProvider));

        return Task.CompletedTask;
    }
}
