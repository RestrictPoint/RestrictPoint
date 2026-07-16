using RestrictPoint.Database;

namespace RestrictPoint.Api.Identity.Domain;

/// <summary>
/// A platform user. Users are provisioned just-in-time on first authenticated request,
/// keyed by their Entra External ID object id. A user may belong to many organizations.
/// </summary>
public sealed class User : BaseEntity
{
    public const int EmailMaxLength = 256;
    public const int DisplayNameMaxLength = 256;
    public const int ExternalIdMaxLength = 256;
    public const int ExternalProviderMaxLength = 50;

    public required string Email { get; set; }

    public required string DisplayName { get; set; }

    /// <summary>Identity provider, e.g. <c>EntraExternalId</c>.</summary>
    public required string ExternalProvider { get; set; }

    /// <summary>Stable external identity key (Entra object id).</summary>
    public required string ExternalId { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Membership> Memberships { get; } = [];
}
