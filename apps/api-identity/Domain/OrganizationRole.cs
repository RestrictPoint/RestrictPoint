namespace RestrictPoint.Api.Identity.Domain;

/// <summary>
/// Organization roles per docs/11. Stored as strings in the database for readability;
/// ordering here is not a privilege hierarchy — use <see cref="Policies"/> for authorization.
/// </summary>
public enum OrganizationRole
{
    Owner,
    Admin,
    Developer,
    Billing,
    Support,
    ReadOnly,
}

public static class OrganizationRoleExtensions
{
    /// <summary>Parses a role name case-insensitively. Returns false for unknown roles.</summary>
    public static bool TryParse(string? value, out OrganizationRole role) =>
        Enum.TryParse(value, ignoreCase: true, out role) && Enum.IsDefined(role);
}
