using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Licensing.Application.Abstractions;
using RestrictPoint.Api.Licensing.Infrastructure;
using RestrictPoint.Database;

namespace RestrictPoint.Api.Licensing.Tests;

/// <summary>Deterministic time source for tests.</summary>
public sealed class TestTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

/// <summary>SQLite-backed licensing database with the auditing interceptor active.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase(TimeProvider timeProvider)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<LicensingDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditingSaveChangesInterceptor(timeProvider))
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public DbContextOptions<LicensingDbContext> Options { get; }

    public LicensingDbContext CreateContext() => new(Options);

    public void Dispose() => _connection.Dispose();
}

/// <summary>
/// In-memory ES256 signer/key-provider pair sharing one P-256 key. Signature format matches
/// production: .NET's SignHash emits IEEE P-1363 (r||s), identical to Key Vault ES256 output.
/// </summary>
public sealed class InMemoryLicenseSigning : ILicenseSigner, ILicensePublicKeyProvider, IDisposable
{
    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public string KeyId { get; } = "test-key-1";

    public Task<byte[]> SignDigestAsync(byte[] sha256Digest, CancellationToken cancellationToken) =>
        Task.FromResult(_key.SignHash(sha256Digest));

    public Task<ECDsa?> GetPublicKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        if (keyId != KeyId)
        {
            return Task.FromResult<ECDsa?>(null);
        }

        var publicKey = ECDsa.Create(_key.ExportParameters(includePrivateParameters: false));
        return Task.FromResult<ECDsa?>(publicKey);
    }

    public void Dispose() => _key.Dispose();
}

/// <summary>In-memory license cache with real nonce semantics for handler tests.</summary>
public sealed class InMemoryLicenseCache : ILicenseCache
{
    private readonly Dictionary<Guid, CachedLicenseState> _licenses = [];
    private readonly HashSet<string> _nonces = [];

    public IReadOnlyCollection<Guid> InvalidatedLicenses => _invalidated;
    private readonly List<Guid> _invalidated = [];

    public Task<CachedLicenseState?> GetLicenseAsync(Guid licenseId, CancellationToken cancellationToken) =>
        Task.FromResult(_licenses.TryGetValue(licenseId, out var state) ? state : null);

    public Task SetLicenseAsync(CachedLicenseState state, CancellationToken cancellationToken)
    {
        _licenses[state.LicenseId] = state;
        return Task.CompletedTask;
    }

    public Task InvalidateLicenseAsync(Guid licenseId, CancellationToken cancellationToken)
    {
        _licenses.Remove(licenseId);
        _invalidated.Add(licenseId);
        return Task.CompletedTask;
    }

    public Task<bool> TryRegisterNonceAsync(string nonce, TimeSpan window, CancellationToken cancellationToken) =>
        Task.FromResult(_nonces.Add(nonce));
}
