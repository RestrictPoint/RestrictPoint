using System.Security.Cryptography;

namespace RestrictPoint.Api.Licensing.Application.Abstractions;

/// <summary>
/// Signs SHA-256 digests with ES256 (ECDSA P-256). The production implementation delegates
/// to Azure Key Vault's <c>sign</c> operation — the private key never leaves the vault.
/// Signatures are IEEE P-1363 (r||s, 64 bytes), which is exactly the JWS ES256 format.
/// </summary>
public interface ILicenseSigner
{
    /// <summary>The key identifier embedded in the JWS header (<c>kid</c>) — the Key Vault key version.</summary>
    string KeyId { get; }

    Task<byte[]> SignDigestAsync(byte[] sha256Digest, CancellationToken cancellationToken);
}

/// <summary>
/// Resolves ES256 public keys by key id for server-side signature verification.
/// Keys are cached; validation never round-trips to Key Vault on the hot path.
/// </summary>
public interface ILicensePublicKeyProvider
{
    /// <summary>Returns the public key for the key id, or null when unknown.</summary>
    Task<ECDsa?> GetPublicKeyAsync(string keyId, CancellationToken cancellationToken);
}
