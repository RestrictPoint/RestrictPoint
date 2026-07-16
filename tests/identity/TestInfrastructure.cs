using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Identity.Infrastructure;
using RestrictPoint.Database;

namespace RestrictPoint.Api.Identity.Tests;

/// <summary>Deterministic time source for tests.</summary>
public sealed class TestTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

/// <summary>
/// Creates an <see cref="IdentityDbContext"/> backed by SQLite in-memory, with the auditing
/// interceptor active so soft-delete and timestamp behavior matches production.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase(TimeProvider timeProvider)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditingSaveChangesInterceptor(timeProvider))
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public DbContextOptions<IdentityDbContext> Options { get; }

    public IdentityDbContext CreateContext() => new(Options);

    public void Dispose() => _connection.Dispose();
}
