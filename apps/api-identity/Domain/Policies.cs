namespace RestrictPoint.Api.Identity.Domain;

/// <summary>
/// Policy-based authorization (docs/11): roles map to policies, and handlers check policies —
/// never raw roles. Adding a role or adjusting a mapping happens here, in one place.
/// </summary>
public static class Policies
{
    public const string CanManageMembers = nameof(CanManageMembers);
    public const string CanManageOrganization = nameof(CanManageOrganization);
    public const string CanViewOrganization = nameof(CanViewOrganization);

    private static readonly Dictionary<string, OrganizationRole[]> PolicyRoles = new()
    {
        [CanManageMembers] = [OrganizationRole.Owner, OrganizationRole.Admin],
        [CanManageOrganization] = [OrganizationRole.Owner, OrganizationRole.Admin],
        [CanViewOrganization] =
        [
            OrganizationRole.Owner,
            OrganizationRole.Admin,
            OrganizationRole.Developer,
            OrganizationRole.Billing,
            OrganizationRole.Support,
            OrganizationRole.ReadOnly,
        ],
    };

    /// <summary>Returns true when the role satisfies the policy.</summary>
    public static bool Grants(string policy, OrganizationRole role) =>
        PolicyRoles.TryGetValue(policy, out var roles)
            ? roles.Contains(role)
            : throw new ArgumentException($"Unknown policy '{policy}'.", nameof(policy));
}
