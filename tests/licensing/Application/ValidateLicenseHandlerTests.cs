using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Licensing.Application.Common;
using RestrictPoint.Api.Licensing.Application.ValidateLicense;
using RestrictPoint.Api.Licensing.Contracts;
using RestrictPoint.Api.Licensing.Domain;
using RestrictPoint.Api.Licensing.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using Xunit;

namespace RestrictPoint.Api.Licensing.Tests.Application;

public sealed class ValidateLicenseHandlerTests : IDisposable
{
    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;
    private readonly InMemoryLicenseSigning _signing = new();
    private readonly InMemoryLicenseCache _cache = new();
    private readonly LicenseTokenService _tokenService;

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _projectId = Guid.NewGuid();
    private readonly Guid _webPartGuid = Guid.NewGuid();
    private License _license = null!;
    private string _token = null!;

    public ValidateLicenseHandlerTests()
    {
        _database = new TestDatabase(_time);
        _tokenService = new LicenseTokenService(_signing, _signing);
        SeedLicenseAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _database.Dispose();
        _signing.Dispose();
    }

    private async Task SeedLicenseAsync()
    {
        using var context = _database.CreateContext();

        _license = new License
        {
            ProjectId = _projectId,
            DeveloperOrganizationId = Guid.NewGuid(),
            CustomerOrganizationId = Guid.NewGuid(),
            CustomerTenantId = _tenantId,
            LicenseType = LicenseType.Annual,
            IssuedUtc = _time.UtcNow,
            ExpiresUtc = _time.UtcNow.AddYears(1),
        };
        _license.Features.Add(new LicenseFeature
        {
            LicenseId = _license.Id, FeatureKey = "Export", Enabled = true,
        });
        _license.Limits.Add(new LicenseLimit
        {
            LicenseId = _license.Id, LimitKey = "maxUsers", Value = 50,
        });
        _license.WebParts.Add(new LicenseWebPart
        {
            LicenseId = _license.Id, WebPartGuid = _webPartGuid,
        });

        context.Licenses.Add(_license);
        await context.SaveChangesAsync();

        _token = await _tokenService.CreateTokenAsync(
            LicensePayloadFactory.Create(_license, tokenId: Guid.NewGuid().ToString("N")),
            CancellationToken.None);
    }

    private static RequestContext Context() => new() { CorrelationId = "corr-validate" };

    private ValidateLicenseHandler CreateHandler(LicensingDbContext context) =>
        new(context, _tokenService, _cache, new OutboxWriter(context), _time);

    private ValidateLicenseRequest Request(
        string? token = null,
        Guid? tenantId = null,
        Guid? projectId = null,
        Guid? webPartGuid = null,
        string? nonce = null,
        DateTimeOffset? timestamp = null) => new()
    {
        LicenseToken = token ?? _token,
        TenantId = tenantId ?? _tenantId,
        ProjectId = projectId ?? _projectId,
        WebPartGuid = webPartGuid ?? _webPartGuid,
        InstallationId = Guid.NewGuid(),
        Nonce = nonce ?? Guid.NewGuid().ToString("N"),
        TimestampUtc = timestamp ?? _time.UtcNow,
        SdkVersion = "1.0.0",
    };

    [Fact]
    public async Task Valid_license_passes_and_activates_installation()
    {
        using var context = _database.CreateContext();
        var request = Request();

        var result = await CreateHandler(context).HandleAsync(Context(), request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsValid);
        Assert.Equal("active", result.Value.Status);
        Assert.True(result.Value.Features["Export"]);
        Assert.Equal(50, result.Value.Limits["maxUsers"]);

        using var verification = _database.CreateContext();
        var installation = await verification.Installations.SingleAsync();
        Assert.Equal(request.InstallationId, installation.InstallationId);
        Assert.NotNull(installation.LastValidatedUtc);

        var eventTypes = await verification.OutboxMessages.Select(m => m.EventType).ToListAsync();
        Assert.Contains("LicenseActivated", eventTypes);
        Assert.Contains("LicenseValidationSucceeded", eventTypes);
    }

    [Fact]
    public async Task Second_validation_from_same_installation_does_not_reactivate()
    {
        using (var first = _database.CreateContext())
        {
            var request = Request();
            await CreateHandler(first).HandleAsync(Context(), request, CancellationToken.None);

            using var second = _database.CreateContext();
            var repeat = Request() with { InstallationId = request.InstallationId };
            var result = await CreateHandler(second).HandleAsync(Context(), repeat, CancellationToken.None);
            Assert.True(result.Value.IsValid);
        }

        using var verification = _database.CreateContext();
        Assert.Equal(1, await verification.Installations.CountAsync());
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "LicenseActivated"));
    }

    [Fact]
    public async Task Tenant_mismatch_is_rejected_with_failure_event()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), Request(tenantId: Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LicensingErrors.TenantMismatch.Code, result.Error!.Code);

        using var verification = _database.CreateContext();
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "LicenseValidationFailed"));
    }

    [Fact]
    public async Task WebPart_mismatch_is_rejected()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), Request(webPartGuid: Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(LicensingErrors.WebPartMismatch.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Project_mismatch_is_rejected()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), Request(projectId: Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(LicensingErrors.ProjectMismatch.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Stale_timestamp_is_rejected()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), Request(timestamp: _time.UtcNow.AddMinutes(-6)), CancellationToken.None);

        Assert.Equal(LicensingErrors.StaleTimestamp.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Nonce_reuse_is_rejected_as_replay()
    {
        using var context = _database.CreateContext();
        var handler = CreateHandler(context);

        var first = await handler.HandleAsync(Context(), Request(nonce: "nonce-1"), CancellationToken.None);
        Assert.True(first.IsSuccess);

        var replay = await handler.HandleAsync(Context(), Request(nonce: "nonce-1"), CancellationToken.None);
        Assert.Equal(LicensingErrors.ReplayDetected.Code, replay.Error!.Code);
    }

    [Fact]
    public async Task Revoked_license_returns_invalid_with_revoked_status()
    {
        using (var seed = _database.CreateContext())
        {
            var license = await seed.Licenses.SingleAsync();
            license.Status = LicenseStatus.Revoked;
            license.RevokedUtc = _time.UtcNow;
            await seed.SaveChangesAsync();
        }

        using var context = _database.CreateContext();
        var result = await CreateHandler(context).HandleAsync(Context(), Request(), CancellationToken.None);

        Assert.True(result.IsSuccess); // Invalid license is a 200 with isValid=false, not an error.
        Assert.False(result.Value.IsValid);
        Assert.Equal("revoked", result.Value.Status);
    }

    [Fact]
    public async Task Expired_license_returns_invalid_with_expired_status()
    {
        _time.UtcNow = _time.UtcNow.AddYears(2); // Past ExpiresUtc.

        using var context = _database.CreateContext();
        var result = await CreateHandler(context).HandleAsync(
            Context(), Request(timestamp: _time.UtcNow), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsValid);
        Assert.Equal("expired", result.Value.Status);
    }

    [Fact]
    public async Task Forged_token_is_rejected()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), Request(token: _token[..^10] + "AAAAAAAAAA"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LicensingErrors.InvalidSignature.Code, result.Error!.Code);
    }
}
