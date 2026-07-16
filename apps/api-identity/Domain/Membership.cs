using RestrictPoint.Database;

namespace RestrictPoint.Api.Identity.Domain;

public enum MembershipStatus
{
    Active,
    Suspended,
}

/// <summary>
/// Links a user to an organization with a role (docs/09 UserOrganizations table).
/// A user has at most one membership per organization.
/// </summary>
public sealed class Membership : BaseEntity
{
    public required Guid UserId { get; set; }

    public required Guid OrganizationId { get; set; }

    public required OrganizationRole Role { get; set; }

    public MembershipStatus Status { get; set; } = MembershipStatus.Active;

    public User? User { get; set; }

    public Organization? Organization { get; set; }
}
