namespace RestrictPoint.Api.Identity.Contracts;

/// <summary>An organization membership as seen by the member (docs/16 identity APIs).</summary>
public sealed record OrganizationSummary
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    public required string Role { get; init; }

    public required string Status { get; init; }
}

/// <summary>Response for GET /v1/identity/me.</summary>
public sealed record MeResponse
{
    public required Guid UserId { get; init; }

    public required string Email { get; init; }

    public required string DisplayName { get; init; }

    public required IReadOnlyList<OrganizationSummary> Organizations { get; init; }

    public Guid? ActiveOrganizationId { get; init; }
}

/// <summary>Response for organization creation.</summary>
public sealed record OrganizationCreatedResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    public required string BillingEmail { get; init; }
}

/// <summary>
/// Response for a created invitation. The invitation token is never returned through the API;
/// it is delivered to the invitee out-of-band when the acceptance flow ships.
/// </summary>
public sealed record InvitationCreatedResponse
{
    public required Guid InvitationId { get; init; }

    public required string Email { get; init; }

    public required string Role { get; init; }

    public required DateTimeOffset ExpiresUtc { get; init; }
}
