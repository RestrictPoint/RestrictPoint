using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RestrictPoint.Database;

/// <summary>
/// Maintains audit timestamps on every save and converts hard deletes of business entities
/// into soft deletes, enforcing the docs/09 rule that hard deletes are forbidden.
/// </summary>
public sealed class AuditingSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;

    public AuditingSaveChangesInterceptor(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditRules(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditRules(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditRules(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var utcNow = _timeProvider.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedUtc = utcNow;
                    entry.Entity.UpdatedUtc = utcNow;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedUtc = utcNow;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.DeletedUtc = utcNow;
                    entry.Entity.UpdatedUtc = utcNow;
                    break;

                default:
                    break;
            }
        }
    }
}
