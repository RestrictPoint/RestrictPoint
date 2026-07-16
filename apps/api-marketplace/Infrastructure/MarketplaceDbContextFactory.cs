using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RestrictPoint.Api.Marketplace.Infrastructure;

/// <summary>
/// Design-time factory for MarketplaceDbContext to support EF Core migrations.
/// </summary>
public sealed class MarketplaceDbContextFactory : IDesignTimeDbContextFactory<MarketplaceDbContext>
{
    public MarketplaceDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MarketplaceDbContext>();
        
        // Use a dummy connection string for design-time only (migrations generation)
        optionsBuilder.UseSqlServer("Server=localhost;Database=RestrictPoint;");
        
        return new MarketplaceDbContext(optionsBuilder.Options);
    }
}
