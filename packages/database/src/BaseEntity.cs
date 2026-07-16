namespace RestrictPoint.Database;

/// <summary>
/// Common columns required on every business table (docs/09): GUID key, audit timestamps,
/// soft-delete marker, and optimistic-concurrency row version.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Set automatically by <see cref="AuditingSaveChangesInterceptor"/>.</summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>Set automatically by <see cref="AuditingSaveChangesInterceptor"/>.</summary>
    public DateTimeOffset UpdatedUtc { get; set; }

    /// <summary>Soft-delete marker. Hard deletes of business entities are forbidden.</summary>
    public DateTimeOffset? DeletedUtc { get; set; }

    /// <summary>SQL Server rowversion for optimistic concurrency.</summary>
    public byte[] RowVersion { get; set; } = [];

    public bool IsDeleted => DeletedUtc is not null;
}
