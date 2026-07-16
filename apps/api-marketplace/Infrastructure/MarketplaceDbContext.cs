using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Marketplace.Domain;
using RestrictPoint.Database;

namespace RestrictPoint.Api.Marketplace.Infrastructure;

/// <summary>
/// EF Core context for the Marketplace bounded context.
/// </summary>
public sealed class MarketplaceDbContext : DbContext
{
    public MarketplaceDbContext(DbContextOptions<MarketplaceDbContext> options)
        : base(options)
    {
    }

    public DbSet<Listing> Listings => Set<Listing>();
    public DbSet<PricingPlan> PricingPlans => Set<PricingPlan>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var isSqlServer = Database.IsSqlServer();

        modelBuilder.HasDefaultSchema("marketplace");

        // Outbox table configuration (replicates pattern from billing/licensing)
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(o => o.Id);

            entity.Property(o => o.Topic).IsRequired().HasMaxLength(128);
            entity.Property(o => o.EventType).IsRequired().HasMaxLength(128);
            entity.Property(o => o.Payload).IsRequired().HasColumnType(isSqlServer ? "nvarchar(max)" : "TEXT");
            entity.Property(o => o.OccurredUtc).IsRequired();
            entity.Property(o => o.DispatchedUtc).IsRequired(false);
            entity.Property(o => o.AttemptCount).IsRequired();
            entity.Property(o => o.LastError).HasMaxLength(2000);

            entity.HasIndex(o => o.DispatchedUtc);
            entity.HasIndex(o => new { o.DispatchedUtc, o.AttemptCount });
        });

        // Listing configuration
        modelBuilder.Entity<Listing>(entity =>
        {
            entity.ToTable("Listings");
            entity.HasKey(l => l.Id);

            entity.Property(l => l.Title)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(l => l.Description)
                .IsRequired();

            entity.Property(l => l.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(l => l.LogoUrl)
                .HasMaxLength(500);

            entity.Property(l => l.Screenshots)
                .HasColumnType(isSqlServer ? "nvarchar(max)" : "TEXT");

            entity.Property(l => l.SupportUrl)
                .HasMaxLength(500);

            entity.Property(l => l.DocumentationUrl)
                .HasMaxLength(500);

            entity.Property(l => l.AverageRating)
                .HasPrecision(3, 2);

            entity.HasIndex(l => l.ProjectId)
                .IsUnique();

            entity.HasIndex(l => l.OrganizationId);
            entity.HasIndex(l => l.CategoryId);
            entity.HasIndex(l => l.Status);
            entity.HasIndex(l => l.IsFeatured);

            // Composite index for common queries
            entity.HasIndex(l => new { l.Status, l.IsFeatured, l.AverageRating });

            if (isSqlServer)
            {
                entity.Property(l => l.RowVersion).IsRowVersion();
            }

            entity.HasQueryFilter(l => l.DeletedUtc == null);
        });

        // PricingPlan configuration
        modelBuilder.Entity<PricingPlan>(entity =>
        {
            entity.ToTable("PricingPlans");
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Name)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(p => p.PricingType)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(p => p.Price)
                .HasPrecision(18, 2);

            entity.Property(p => p.Currency)
                .IsRequired()
                .HasMaxLength(10);

            entity.Property(p => p.BillingInterval)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(p => p.StripePriceId)
                .HasMaxLength(128);

            entity.Property(p => p.LicenseTemplate)
                .HasColumnType(isSqlServer ? "nvarchar(max)" : "TEXT");

            entity.HasIndex(p => p.ListingId);
            entity.HasIndex(p => p.IsActive);
            entity.HasIndex(p => p.StripePriceId);

            if (isSqlServer)
            {
                entity.Property(p => p.RowVersion).IsRowVersion();
            }

            entity.HasQueryFilter(p => p.DeletedUtc == null);
        });

        // Category configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("Categories");
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Name)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(c => c.Slug)
                .IsRequired()
                .HasMaxLength(128);

            entity.HasIndex(c => c.Slug)
                .IsUnique();

            entity.HasIndex(c => c.ParentCategoryId);
            entity.HasIndex(c => c.DisplayOrder);

            if (isSqlServer)
            {
                entity.Property(c => c.RowVersion).IsRowVersion();
            }

            entity.HasQueryFilter(c => c.DeletedUtc == null);
        });

        // Tag configuration
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("Tags");
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(t => t.Slug)
                .IsRequired()
                .HasMaxLength(64);

            entity.HasIndex(t => t.Name)
                .IsUnique();

            entity.HasIndex(t => t.Slug)
                .IsUnique();

            entity.HasIndex(t => t.UsageCount);

            if (isSqlServer)
            {
                entity.Property(t => t.RowVersion).IsRowVersion();
            }

            entity.HasQueryFilter(t => t.DeletedUtc == null);
        });

        // Review configuration
        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("Reviews");
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Comment)
                .HasMaxLength(4000);

            entity.HasIndex(r => new { r.ListingId, r.UserId })
                .IsUnique(); // One review per user per listing

            entity.HasIndex(r => r.ListingId);
            entity.HasIndex(r => r.UserId);
            entity.HasIndex(r => r.Rating);

            if (isSqlServer)
            {
                entity.Property(r => r.RowVersion).IsRowVersion();
            }

            entity.HasQueryFilter(r => r.DeletedUtc == null);
        });

        // ListingTag join table
        modelBuilder.Entity<ListingTag>(entity =>
        {
            entity.ToTable("ListingTags");
            entity.HasKey(lt => new { lt.ListingId, lt.TagId });

            entity.HasOne(lt => lt.Listing)
                .WithMany(l => l.Tags)
                .HasForeignKey(lt => lt.ListingId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(lt => lt.Tag)
                .WithMany()
                .HasForeignKey(lt => lt.TagId)
                .OnDelete(DeleteBehavior.Cascade);

            // Match the principals' soft-delete filters (required FK to filtered entities).
            entity.HasQueryFilter(lt => lt.Listing.DeletedUtc == null && lt.Tag.DeletedUtc == null);
        });

        // SQLite (unit tests) cannot order or compare DateTimeOffset/decimal; store as
        // UTC ticks and double respectively.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var dateTimeOffsetConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion
                .DateTimeOffsetToBinaryConverter();
            var decimalConverter = new Microsoft.EntityFrameworkCore.Storage.ValueConversion
                .CastingConverter<decimal, double>();

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset)
                        || property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(dateTimeOffsetConverter);
                    }
                    else if (property.ClrType == typeof(decimal))
                    {
                        property.SetValueConverter(decimalConverter);
                    }
                }
            }
        }
    }
}
