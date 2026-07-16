using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RestrictPoint.Api.Licensing.Infrastructure;

/// <summary>
/// Design-time factory for <c>dotnet ef</c> commands. Uses the SQL_CONNECTION_STRING
/// environment variable when present (migration apply); otherwise a placeholder connection
/// string that supports offline migration generation.
/// </summary>
public sealed class LicensingDbContextFactory : IDesignTimeDbContextFactory<LicensingDbContext>
{
    public LicensingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("SQL_CONNECTION_STRING")
            ?? "Server=localhost;Database=RestrictPoint;Integrated Security=true;TrustServerCertificate=true";

        var options = new DbContextOptionsBuilder<LicensingDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new LicensingDbContext(options);
    }
}
