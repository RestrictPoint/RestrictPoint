using Microsoft.EntityFrameworkCore;
using RestrictPoint.Api.Billing.Domain;
using RestrictPoint.Database;

namespace RestrictPoint.Api.Billing.Infrastructure;

/// <summary>
/// EF Core context for the Billing bounded context. Owns the Billing schema (docs/09);
/// no other service may access these tables.
/// </summary>
public sealed class BillingDbContext : DbContext
{
    public BillingDbContext(DbContextOptions<BillingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<Payment> Payments => Set<Payment>();

    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var isSqlServer = Database.IsSqlServer();

        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions", "Billing");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(s => s.Plan).HasMaxLength(Subscription.PlanMaxLength).IsRequired();
            entity.Property(s => s.StripeSubscriptionId).HasMaxLength(Subscription.StripeIdMaxLength);
            entity.Property(s => s.StripeCustomerId).HasMaxLength(Subscription.StripeIdMaxLength);
            entity.Property(s => s.LicenseTemplate).IsRequired();
            if (isSqlServer)
            {
                entity.Property(s => s.RowVersion).IsRowVersion();
            }

            var stripeIndex = entity.HasIndex(s => s.StripeSubscriptionId).IsUnique();
            if (isSqlServer)
            {
                stripeIndex.HasFilter("[StripeSubscriptionId] IS NOT NULL");
            }

            entity.HasIndex(s => s.CustomerOrganizationId);
            entity.HasIndex(s => s.DeveloperOrganizationId);
            entity.HasQueryFilter(s => s.DeletedUtc == null);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoices", "Billing");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(i => i.Currency).HasMaxLength(10).IsRequired();
            entity.Property(i => i.Amount).HasPrecision(18, 2);
            entity.Property(i => i.StripeInvoiceId).HasMaxLength(Subscription.StripeIdMaxLength).IsRequired();
            if (isSqlServer)
            {
                entity.Property(i => i.RowVersion).IsRowVersion();
            }

            entity.HasIndex(i => i.StripeInvoiceId).IsUnique();
            entity.HasIndex(i => i.SubscriptionId);
            entity.HasQueryFilter(i => i.DeletedUtc == null);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments", "Billing");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Status).HasConversion<string>().HasMaxLength(50);
            entity.Property(p => p.Currency).HasMaxLength(10).IsRequired();
            entity.Property(p => p.Amount).HasPrecision(18, 2);
            entity.Property(p => p.StripePaymentIntentId).HasMaxLength(Subscription.StripeIdMaxLength).IsRequired();
            entity.Property(p => p.FailureReason).HasMaxLength(1024);
            if (isSqlServer)
            {
                entity.Property(p => p.RowVersion).IsRowVersion();
            }

            entity.HasIndex(p => p.SubscriptionId);
            entity.HasQueryFilter(p => p.DeletedUtc == null);
        });

        modelBuilder.Entity<ProcessedWebhookEvent>(entity =>
        {
            entity.ToTable("ProcessedWebhookEvents", "Billing");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StripeEventId)
                .HasMaxLength(ProcessedWebhookEvent.StripeEventIdMaxLength).IsRequired();
            entity.Property(e => e.EventType)
                .HasMaxLength(ProcessedWebhookEvent.EventTypeMaxLength).IsRequired();

            // The idempotency guarantee (docs/12): duplicate deliveries violate this index.
            entity.HasIndex(e => e.StripeEventId).IsUnique();
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages", "Billing");
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
