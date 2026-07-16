using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Identity.Domain;
using RestrictPoint.Database;

namespace RestrictPoint.Api.Identity.Infrastructure;

/// <summary>
/// EF Core context for the Identity bounded context. Owns the Identity and Organizations
/// schemas (docs/09); no other service may access these tables.
/// Global query filters enforce soft-delete invisibility.
/// </summary>
public sealed class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options)
        : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<Membership> Memberships => Set<Membership>();

    public DbSet<Invitation> Invitations => Set<Invitation>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // rowversion and filtered indexes are SQL Server features; unit tests run on SQLite.
        var isSqlServer = Database.IsSqlServer();
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users", "Identity");
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
            entity.Property(u => u.DisplayName).HasMaxLength(User.DisplayNameMaxLength).IsRequired();
            entity.Property(u => u.ExternalProvider).HasMaxLength(User.ExternalProviderMaxLength).IsRequired();
            entity.Property(u => u.ExternalId).HasMaxLength(User.ExternalIdMaxLength).IsRequired();
            if (isSqlServer)
            {
                entity.Property(u => u.RowVersion).IsRowVersion();
            }

            entity.HasIndex(u => new { u.ExternalProvider, u.ExternalId }).IsUnique();
            entity.HasIndex(u => u.Email);
            entity.HasQueryFilter(u => u.DeletedUtc == null);
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.ToTable("Organizations", "Organizations");
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Name).HasMaxLength(Organization.NameMaxLength).IsRequired();
            entity.Property(o => o.Slug).HasMaxLength(Organization.SlugMaxLength).IsRequired();
            entity.Property(o => o.BillingEmail).HasMaxLength(Organization.BillingEmailMaxLength).IsRequired();
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(50);
            if (isSqlServer)
            {
                entity.Property(o => o.RowVersion).IsRowVersion();
            }

            entity.HasIndex(o => o.Slug).IsUnique();
            entity.HasQueryFilter(o => o.DeletedUtc == null);
        });

        modelBuilder.Entity<Membership>(entity =>
        {
            entity.ToTable("UserOrganizations", "Identity");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Role).HasConversion<string>().HasMaxLength(50);
            entity.Property(m => m.Status).HasConversion<string>().HasMaxLength(50);
            if (isSqlServer)
            {
                entity.Property(m => m.RowVersion).IsRowVersion();
            }

            entity.HasIndex(m => new { m.UserId, m.OrganizationId }).IsUnique();
            entity.HasIndex(m => m.OrganizationId);

            entity.HasOne(m => m.User)
                .WithMany(u => u.Memberships)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(m => m.Organization)
                .WithMany(o => o.Memberships)
                .HasForeignKey(m => m.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(m => m.DeletedUtc == null);
        });

        modelBuilder.Entity<Invitation>(entity =>
        {
            entity.ToTable("Invitations", "Organizations");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Email).HasMaxLength(Invitation.EmailMaxLength).IsRequired();
            entity.Property(i => i.TokenHash).HasMaxLength(Invitation.TokenHashMaxLength).IsRequired();
            entity.Property(i => i.Role).HasConversion<string>().HasMaxLength(50);
            if (isSqlServer)
            {
                entity.Property(i => i.RowVersion).IsRowVersion();
            }

            entity.HasIndex(i => new { i.OrganizationId, i.Email });

            entity.HasOne(i => i.Organization)
                .WithMany()
                .HasForeignKey(i => i.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasQueryFilter(i => i.DeletedUtc == null);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages", "Identity");
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
