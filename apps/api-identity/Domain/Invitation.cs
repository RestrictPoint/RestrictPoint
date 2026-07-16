using RestrictPoint.Database;

namespace RestrictPoint.Api.Identity.Domain;

/// <summary>
/// An invitation for an email address to join an organization with a role (docs/09).
/// The raw token is delivered out-of-band (email); only its SHA-256 hash is stored.
/// </summary>
public sealed class Invitation : BaseEntity
{
    public const int EmailMaxLength = 256;
    public const int TokenHashMaxLength = 512;
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    public required Guid OrganizationId { get; set; }

    public required string Email { get; set; }

    public required OrganizationRole Role { get; set; }

    /// <summary>SHA-256 hash of the invitation token. The raw token is never persisted.</summary>
    public required string TokenHash { get; set; }

    public required Guid InvitedByUserId { get; set; }

    public required DateTimeOffset ExpiresUtc { get; set; }

    public DateTimeOffset? AcceptedUtc { get; set; }

    public Organization? Organization { get; set; }

    public bool IsExpired(DateTimeOffset utcNow) => utcNow >= ExpiresUtc;

    public bool IsPending(DateTimeOffset utcNow) => AcceptedUtc is null && !IsExpired(utcNow) && !IsDeleted;
}
