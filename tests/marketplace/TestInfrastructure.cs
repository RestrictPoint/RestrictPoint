using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Common;
using RestrictPoint.Database;

namespace RestrictPoint.Api.Marketplace.Tests;

/// <summary>Deterministic time source for tests.</summary>
public sealed class TestTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

/// <summary>SQLite-backed marketplace database with the auditing interceptor active.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase(TimeProvider timeProvider)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<MarketplaceDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditingSaveChangesInterceptor(timeProvider))
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public DbContextOptions<MarketplaceDbContext> Options { get; }

    public MarketplaceDbContext CreateContext() => new(Options);

    public void Dispose() => _connection.Dispose();
}

/// <summary>
/// Deterministic role resolver: returns the configured role for members, null for
/// non-members, or a failure when Identity is "unreachable".
/// </summary>
public sealed class StubRoleResolver : IOrganizationRoleResolver
{
    private readonly Dictionary<Guid, string> _roles = [];

    public bool FailNextCall { get; set; }

    public void SetRole(Guid organizationId, string role) => _roles[organizationId] = role;

    public Task<Result<string?>> GetCallerRoleAsync(
        string bearerToken, Guid organizationId, CancellationToken cancellationToken)
    {
        if (FailNextCall)
        {
            return Task.FromResult(Result.Failure<string?>(
                Error.Unexpected("auth.identity_unavailable", "Identity unreachable.")));
        }

        return Task.FromResult(Result.Success(
            _roles.TryGetValue(organizationId, out var role) ? role : null));
    }
}
