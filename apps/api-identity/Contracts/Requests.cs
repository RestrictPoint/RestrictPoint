namespace RestrictPoint.Api.Identity.Contracts;

/// <summary>Request to create an organization (POST /v1/identity/organizations).</summary>
public sealed record CreateOrganizationRequest
{
    public string? Name { get; init; }

    /// <summary>Billing contact email. Defaults to the creator's email when omitted.</summary>
    public string? BillingEmail { get; init; }
}

/// <summary>Request to invite a member (POST /v1/identity/organizations/{id}/invite).</summary>
public sealed record InviteMemberRequest
{
    public string? Email { get; init; }

    /// <summary>Role name: Admin, Developer, Billing, Support, or ReadOnly.</summary>
    public string? Role { get; init; }
}
