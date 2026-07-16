using RestrictPoint.Common;

namespace RestrictPoint.Api.Licensing.Application.Abstractions;

/// <summary>
/// Resolves the caller's role in an organization by querying the Identity service with the
/// caller's own bearer token (bounded contexts communicate via REST, never shared tables).
/// </summary>
public interface IOrganizationAuthorizer
{
    /// <summary>
    /// Returns the caller's role name in the organization, or null when the caller is not
    /// an active member. Failures reaching Identity surface as an error result.
    /// </summary>
    Task<Result<string?>> GetCallerRoleAsync(
        string bearerToken,
        Guid organizationId,
        CancellationToken cancellationToken);
}
