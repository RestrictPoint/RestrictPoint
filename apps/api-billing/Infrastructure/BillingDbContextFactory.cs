using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RestrictPoint.Api.Billing.Infrastructure;

/// <summary>
/// Design-time factory for <c>dotnet ef</c> commands. Uses the SQL_CONNECTION_STRING
/// environment variable when present; otherwise a placeholder for offline generation.
/// </summary>
public sealed class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
            ?? "Server=localhost;Database=RestrictPoint;Integrated Security=true;TrustServerCertificate=true";

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new BillingDbContext(options);
    }
}
