namespace RestrictPoint.Api.Licensing.Application.Abstractions;

/// <summary>
/// An ES256 (P-256) public signing key in JWK form. Coordinates are base64url-encoded,
/// ready for RFC 7517 serialization and WebCrypto import in the SDK.
/// </summary>
public sealed record LicenseSigningKey
{
    /// <summary>Key Vault key version — matches the JWS <c>kid</c> header.</summary>
    public required string KeyId { get; init; }

    /// <summary>Base64url X coordinate.</summary>
    public required string X { get; init; }

    /// <summary>Base64url Y coordinate.</summary>
    public required string Y { get; init; }
}

/// <summary>
/// Enumerates the active public signing keys for the JWKS endpoint. The SPFx SDK fetches
/// this set (24h TTL, docs/10) to verify license tokens offline via WebCrypto.
/// </summary>
public interface ILicenseKeySetProvider
{
    Task<IReadOnlyList<LicenseSigningKey>> GetActiveKeysAsync(CancellationToken cancellationToken);
}
