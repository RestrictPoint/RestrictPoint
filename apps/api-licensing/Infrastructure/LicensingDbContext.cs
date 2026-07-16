using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Licensing.Domain;
using RestrictPoint.Database;

namespace RestrictPoint.Api.Licensing.Infrastructure;

/// <summary>
/// EF Core context for the Licensing bounded context. Owns the Licensing schema (docs/09);
/// no other service may access these tables. Global query filters enforce soft deletes.
/// </summary>
public sealed class LicensingDbContext : DbContext
{
    public LicensingDbContext(DbContextOptions<LicensingDbContext> options)
        : base(options)
    {
    }

    public DbSet<License> Licenses => Set<License>();

    public DbSet<LicenseFeature> LicenseFeatures => Set<LicenseFeature>();

    public DbSet<LicenseLimit> LicenseLimits => Set<LicenseLimit>();

    public DbSet<LicenseWebPart> LicenseWebParts => Set<LicenseWebPart>();

    public DbSet<LicenseToken> LicenseTokens => Set<LicenseToken>();

    public DbSet<Installation> Installations => Set<Installation>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlServer = Database.IsSqlServer();

        modelBuilder.Entity<License>(entity =>
        {
            entity.ToTable("Licenses", "Licensing");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.LicenseType).HasConversion<string>().HasMaxLength(50);
            entity.Property(l => l.Status).HasConversion<string>().HasMaxLength(50);
            if (isSqlServer)
            {
                entity.Property(l => l.RowVersion).IsRowVersion();
            }

            entity.HasIndex(l => l.ProjectId);
            entity.HasIndex(l => l.DeveloperOrganizationId);
            entity.HasIndex(l => new { l.CustomerTenantId, l.ProjectId });

            entity.HasMany(l => l.Features).WithOne().HasForeignKey(f => f.LicenseId);
            entity.HasMany(l => l.Limits).WithOne().HasForeignKey(f => f.LicenseId);
            entity.HasMany(l => l.WebParts).WithOne().HasForeignKey(f => f.LicenseId);

            entity.HasQueryFilter(l => l.DeletedUtc == null);
        });

        modelBuilder.Entity<LicenseFeature>(entity =>
        {
            entity.ToTable("LicenseFeatures", "Licensing");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.FeatureKey).HasMaxLength(LicenseFeature.FeatureKeyMaxLength).IsRequired();
            if (isSqlServer)
            {
                entity.Property(f => f.RowVersion).IsRowVersion();
            }

            entity.HasIndex(f => new { f.LicenseId, f.FeatureKey }).IsUnique();
            entity.HasQueryFilter(f => f.DeletedUtc == null);
        });

        modelBuilder.Entity<LicenseLimit>(entity =>
        {
            entity.ToTable("LicenseLimits", "Licensing");
            entity.HasKey(l => l.Id);
            entity.Property(l => l.LimitKey).HasMaxLength(LicenseLimit.LimitKeyMaxLength).IsRequired();
            if (isSqlServer)
            {
                entity.Property(l => l.RowVersion).IsRowVersion();
            }

            entity.HasIndex(l => new { l.LicenseId, l.LimitKey }).IsUnique();
            entity.HasQueryFilter(l => l.DeletedUtc == null);
        });

        modelBuilder.Entity<LicenseWebPart>(entity =>
        {
            entity.ToTable("LicenseWebParts", "Licensing");
            entity.HasKey(w => w.Id);
            if (isSqlServer)
            {
                entity.Property(w => w.RowVersion).IsRowVersion();
            }

            entity.HasIndex(w => new { w.LicenseId, w.WebPartGuid }).IsUnique();
            entity.HasQueryFilter(w => w.DeletedUtc == null);
        });

        modelBuilder.Entity<LicenseToken>(entity =>
        {
            entity.ToTable("LicenseTokens", "Licensing");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TokenId).HasMaxLength(LicenseToken.TokenIdMaxLength).IsRequired();
            entity.Property(t => t.KeyId).HasMaxLength(LicenseToken.KeyIdMaxLength).IsRequired();
            if (isSqlServer)
            {
                entity.Property(t => t.RowVersion).IsRowVersion();
            }

            entity.HasIndex(t => t.TokenId).IsUnique();
            entity.HasIndex(t => t.LicenseId);
            entity.HasQueryFilter(t => t.DeletedUtc == null);
        });

        modelBuilder.Entity<Installation>(entity =>
        {
            entity.ToTable("Installations", "Licensing");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.SdkVersion).HasMaxLength(Installation.SdkVersionMaxLength);
            if (isSqlServer)
            {
                entity.Property(i => i.RowVersion).IsRowVersion();
            }

            entity.HasIndex(i => new { i.LicenseId, i.InstallationId }).IsUnique();
            entity.HasQueryFilter(i => i.DeletedUtc == null);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages", "Licensing");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Topic).HasMaxLength(128).IsRequired();
            entity.Property(m => m.EventType).HasMaxLength(128).IsRequired();
            entity.Property(m => m.Payload).IsRequired();

            var outboxIndex = entity.HasIndex(m => m.DispatchedUtc);
            if (isSqlServer)
            {
                outboxIndex.HasFilter("[DispatchedUtc] IS NULL");
            }
        });

        // SQLite (unit tests) cannot order or compare DateTimeOffset; store as UTC ticks.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var converter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion
                .DateTimeOffsetToBinaryConverter();

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset)
                        || property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(converter);
                    }
                }
            }
        }
    }
}
