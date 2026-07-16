using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Licensing.Application.Abstractions;
using RestrictPoint.Api.Licensing.Application.Common;
using RestrictPoint.Api.Licensing.Application.IssueLicense;
using RestrictPoint.Api.Licensing.Application.RevokeLicense;
using RestrictPoint.Api.Licensing.Contracts;
using RestrictPoint.Api.Licensing.Domain;
using RestrictPoint.Api.Licensing.Infrastructure;
using RestrictPoint.Common;
using RestrictPoint.Database;
using Xunit;

namespace RestrictPoint.Api.Licensing.Tests.Application;

/// <summary>Stub authorizer returning a fixed role (or null for non-members).</summary>
public sealed class StubAuthorizer : IOrganizationAuthorizer
{
    public string? Role { get; set; }

    public Task<Result<string?>> GetCallerRoleAsync(
        string bearerToken, Guid organizationId, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(Role));
}

public sealed class IssueLicenseHandlerTests : IDisposable
{
    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;
    private readonly InMemoryLicenseSigning _signing = new();
    private readonly StubAuthorizer _authorizer = new() { Role = "Admin" };
    private readonly LicenseTokenService _tokenService;

    public IssueLicenseHandlerTests()
    {
        _database = new TestDatabase(_time);
        _tokenService = new LicenseTokenService(_signing, _signing);
    }

    public void Dispose()
    {
        _database.Dispose();
        _signing.Dispose();
    }

    private static RequestContext Context() => new() { CorrelationId = "corr-issue", UserId = Guid.NewGuid() };

    private IssueLicenseHandler CreateHandler(LicensingDbContext context) =>
        new(context, _tokenService, _authorizer, new OutboxWriter(context), _time);

    private IssueLicenseRequest Request(string licenseType = "Annual", DateTimeOffset? expires = null) => new()
    {
        ProjectId = Guid.NewGuid(),
        DeveloperOrganizationId = Guid.NewGuid(),
        CustomerOrganizationId = Guid.NewGuid(),
        CustomerTenantId = Guid.NewGuid(),
        LicenseType = licenseType,
        ExpiresUtc = expires ?? _time.UtcNow.AddYears(1),
        Features = new Dictionary<string, bool> { ["Export"] = true },
        Limits = new Dictionary<string, int> { ["maxUsers"] = 25 },
        WebPartGuids = [Guid.NewGuid()],
    };

    [Fact]
    public async Task Issues_license_with_verifiable_token_and_event()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), "token", Request(), CancellationToken.None);

        Assert.True(result.IsSuccess);

        // The returned token must verify and carry the license payload.
        var verified = await _tokenService.VerifyTokenAsync(result.Value.LicenseToken, CancellationToken.None);
        Assert.True(verified.IsSuccess);
        Assert.Equal(result.Value.LicenseId, verified.Value.LicenseId);
        Assert.True(verified.Value.Features["Export"]);

        using var verification = _database.CreateContext();
        Assert.Equal(1, await verification.Licenses.CountAsync());
        Assert.Equal(1, await verification.LicenseTokens.CountAsync());
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "LicenseIssued"));
    }

    [Theory]
    [InlineData("Owner", true)]
    [InlineData("Admin", true)]
    [InlineData("Developer", true)]
    [InlineData("Billing", false)]
    [InlineData("Support", false)]
    [InlineData("ReadOnly", false)]
    [InlineData(null, false)]
    public async Task Issuance_requires_an_issuing_role(string? role, bool expectedAllowed)
    {
        _authorizer.Role = role;
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), "token", Request(), CancellationToken.None);

        Assert.Equal(expectedAllowed, result.IsSuccess);
        if (!expectedAllowed)
        {
            Assert.Equal(LicensingErrors.NotAuthorizedForOrganization.Code, result.Error!.Code);
        }
    }

    [Fact]
    public async Task Lifetime_license_needs_no_expiry()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), "token", Request("Lifetime") with { ExpiresUtc = null }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ExpiresUtc);
    }

    [Fact]
    public async Task Non_lifetime_license_requires_future_expiry()
    {
        using var context = _database.CreateContext();
        var handler = CreateHandler(context);

        var missing = await handler.HandleAsync(
            Context(), "token", Request() with { ExpiresUtc = null }, CancellationToken.None);
        Assert.Equal(LicensingErrors.ExpiryRequired.Code, missing.Error!.Code);

        var past = await handler.HandleAsync(
            Context(), "token", Request(expires: _time.UtcNow.AddDays(-1)), CancellationToken.None);
        Assert.Equal(LicensingErrors.ExpiryRequired.Code, past.Error!.Code);
    }

    [Fact]
    public async Task Unknown_license_type_is_rejected()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), "token", Request("Forever"), CancellationToken.None);

        Assert.Equal(LicensingErrors.InvalidLicenseType.Code, result.Error!.Code);
    }
}

public sealed class RevokeLicenseHandlerTests : IDisposable
{
    private readonly TestTimeProvider _time = new();
    private readonly TestDatabase _database;
    private readonly InMemoryLicenseCache _cache = new();
    private readonly StubAuthorizer _authorizer = new() { Role = "Owner" };
    private Guid _licenseId;

    public RevokeLicenseHandlerTests()
    {
        _database = new TestDatabase(_time);

        using var seed = _database.CreateContext();
        var license = new License
        {
            ProjectId = Guid.NewGuid(),
            DeveloperOrganizationId = Guid.NewGuid(),
            CustomerOrganizationId = Guid.NewGuid(),
            CustomerTenantId = Guid.NewGuid(),
            LicenseType = LicenseType.Annual,
            IssuedUtc = _time.UtcNow,
            ExpiresUtc = _time.UtcNow.AddYears(1),
        };
        seed.Licenses.Add(license);
        seed.LicenseTokens.Add(new LicenseToken
        {
            LicenseId = license.Id,
            TokenId = "tok-1",
            KeyId = "test-key-1",
            IssuedUtc = _time.UtcNow,
        });
        seed.SaveChanges();
        _licenseId = license.Id;
    }

    public void Dispose() => _database.Dispose();

    private static RequestContext Context() => new() { CorrelationId = "corr-revoke", UserId = Guid.NewGuid() };

    private RevokeLicenseHandler CreateHandler(LicensingDbContext context) =>
        new(context, _authorizer, _cache, new OutboxWriter(context), _time);

    [Fact]
    public async Task Revocation_updates_state_tokens_cache_and_event()
    {
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), "token", new RevokeLicenseRequest { LicenseId = _licenseId, Reason = "fraud" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Revoked", result.Value.Status);

        using var verification = _database.CreateContext();
        var license = await verification.Licenses.SingleAsync();
        Assert.Equal(LicenseStatus.Revoked, license.Status);
        Assert.NotNull(license.RevokedUtc);

        Assert.True(await verification.LicenseTokens.AllAsync(t => t.Revoked));
        Assert.Contains(_licenseId, _cache.InvalidatedLicenses);
        Assert.Equal(1, await verification.OutboxMessages.CountAsync(m => m.EventType == "LicenseRevoked"));
    }

    [Fact]
    public async Task Double_revocation_conflicts()
    {
        using (var first = _database.CreateContext())
        {
            await CreateHandler(first).HandleAsync(
                Context(), "token", new RevokeLicenseRequest { LicenseId = _licenseId, Reason = "fraud" },
                CancellationToken.None);
        }

        using var second = _database.CreateContext();
        var result = await CreateHandler(second).HandleAsync(
            Context(), "token", new RevokeLicenseRequest { LicenseId = _licenseId, Reason = "again" },
            CancellationToken.None);

        Assert.Equal(LicensingErrors.AlreadyRevoked.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Non_member_receives_not_found()
    {
        _authorizer.Role = null;
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), "token", new RevokeLicenseRequest { LicenseId = _licenseId, Reason = "x" },
            CancellationToken.None);

        Assert.Equal(LicensingErrors.LicenseNotFound.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Developer_role_cannot_revoke()
    {
        _authorizer.Role = "Developer";
        using var context = _database.CreateContext();

        var result = await CreateHandler(context).HandleAsync(
            Context(), "token", new RevokeLicenseRequest { LicenseId = _licenseId, Reason = "x" },
            CancellationToken.None);

        Assert.Equal(LicensingErrors.NotAuthorizedForOrganization.Code, result.Error!.Code);
    }
}
