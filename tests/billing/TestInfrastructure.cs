using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Billing.Infrastructure;
using RestrictPoint.Database;

namespace RestrictPoint.Api.Billing.Tests;

/// <summary>Deterministic time source for tests.</summary>
public sealed class TestTimeProvider : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = new(2026, 7, 16, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => UtcNow;
}

/// <summary>SQLite-backed billing database with the auditing interceptor active.</summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestDatabase(TimeProvider timeProvider)
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditingSaveChangesInterceptor(timeProvider))
            .Options;

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public DbContextOptions<BillingDbContext> Options { get; }

    public BillingDbContext CreateContext() => new(Options);

    public void Dispose() => _connection.Dispose();
}
