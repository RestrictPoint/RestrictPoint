using RestrictPoint.Database;

namespace RestrictPoint.Api.Licensing.Domain;

/// <summary>
/// Tracks every signed license token issued (docs/09 LicenseTokens). Enables revocation of
/// individual tokens by jti without revoking the underlying license.
/// </summary>
public sealed class LicenseToken : BaseEntity
{
    public const int TokenIdMaxLength = 256;
    public const int KeyIdMaxLength = 128;

    public required Guid LicenseId { get; set; }

    /// <summary>The JWS <c>jti</c> claim.</summary>
    public required string TokenId { get; set; }

    /// <summary>Key Vault key version used to sign this token.</summary>
    public required string KeyId { get; set; }

    public required DateTimeOffset IssuedUtc { get; set; }

    public DateTimeOffset? ExpiresUtc { get; set; }

    public bool Revoked { get; set; }
}

/// <summary>
/// Tracks SPFx deployments of a license (docs/09 Installations). Created on first
/// successful validation from a new installation id (activation).
/// </summary>
public sealed class Installation : BaseEntity
{
    public const int SdkVersionMaxLength = 50;

    public required Guid LicenseId { get; set; }

    public required Guid TenantId { get; set; }

    public Guid? SiteCollectionId { get; set; }

    public required Guid WebPartGuid { get; set; }

    /// <summary>Client-generated stable installation identifier.</summary>
    public required Guid InstallationId { get; set; }

    public string? SdkVersion { get; set; }

    public required DateTimeOffset InstalledUtc { get; set; }

    public DateTimeOffset? LastValidatedUtc { get; set; }
}
