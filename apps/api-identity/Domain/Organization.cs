using RestrictPoint.Database;

namespace RestrictPoint.Api.Identity.Domain;

public enum OrganizationStatus
{
    Active,
    Suspended,
}

/// <summary>
/// A software vendor organization — the root of the multi-tenant hierarchy.
/// Organizations own projects, licenses, and billing relationships.
/// </summary>
public sealed class Organization : BaseEntity
{
    public const int NameMaxLength = 256;
    public const int SlugMaxLength = 128;
    public const int BillingEmailMaxLength = 256;

    public required string Name { get; set; }

    /// <summary>URL-safe unique identifier derived from the name.</summary>
    public required string Slug { get; set; }

    public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;

    public required string BillingEmail { get; set; }

    public ICollection<Membership> Memberships { get; } = [];
}
