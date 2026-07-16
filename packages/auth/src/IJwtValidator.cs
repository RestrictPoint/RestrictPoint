using RestrictPoint.Common;

namespace RestrictPoint.Auth;

/// <summary>
/// The identity extracted from a validated Entra External ID access token.
/// Organization membership and roles are resolved from the database, never from token claims,
/// so that revocation and role changes take effect without waiting for token expiry.
/// </summary>
public sealed record AuthenticatedPrincipal
{
    /// <summary>Entra object id (<c>oid</c> claim) — the stable external identity key.</summary>
    public required string ObjectId { get; init; }

    /// <summary>Email address from the <c>email</c> or <c>preferred_username</c> claim.</summary>
    public required string Email { get; init; }

    /// <summary>Display name from the <c>name</c> claim; falls back to email when absent.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Identity provider hint (<c>idp</c> claim), e.g. local, github.com.</summary>
    public string? IdentityProvider { get; init; }
}

/// <summary>Validates bearer tokens and extracts the caller principal.</summary>
public interface IJwtValidator
{
    Task<Result<AuthenticatedPrincipal>> ValidateAsync(string bearerToken, CancellationToken cancellationToken);
}
