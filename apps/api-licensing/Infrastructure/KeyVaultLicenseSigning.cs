using System.Collections.Concurrent;
using System.Security.Cryptography;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using RestrictPoint.Api.Licensing.Application.Abstractions;

namespace RestrictPoint.Api.Licensing.Infrastructure;

/// <summary>
/// ES256 signer backed by Azure Key Vault's <c>sign</c> operation via Managed Identity.
/// The private key never leaves the vault (docs/10 key storage). Key Vault returns ES256
/// signatures in IEEE P-1363 format (r||s), which is the JWS wire format directly.
/// </summary>
public sealed class KeyVaultLicenseSigner : ILicenseSigner
{
    private readonly CryptographyClient _cryptographyClient;

    public KeyVaultLicenseSigner(CryptographyClient cryptographyClient, string keyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        _cryptographyClient = cryptographyClient;
        KeyId = keyId;
    }

    /// <summary>The Key Vault key version, embedded as the JWS <c>kid</c>.</summary>
    public string KeyId { get; }

    public async Task<byte[]> SignDigestAsync(byte[] sha256Digest, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sha256Digest);

        var result = await _cryptographyClient
            .SignAsync(SignatureAlgorithm.ES256, sha256Digest, cancellationToken)
            .ConfigureAwait(false);

        return result.Signature;
    }
}

/// <summary>
/// Resolves ES256 public keys from Key Vault by key version, cached indefinitely per
/// process (public keys for a given version are immutable). Validation therefore never
/// blocks on Key Vault after first resolution of each key version.
/// </summary>
public sealed class KeyVaultLicensePublicKeyProvider : ILicensePublicKeyProvider
{
    private readonly KeyClient _keyClient;
    private readonly string _keyName;
    private readonly ConcurrentDictionary<string, ECDsa?> _cache = new();

    public KeyVaultLicensePublicKeyProvider(KeyClient keyClient, string keyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyName);
        _keyClient = keyClient;
        _keyName = keyName;
    }

    public async Task<ECDsa?> GetPublicKeyAsync(string keyId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);

        if (_cache.TryGetValue(keyId, out var cached))
        {
            return cached;
        }

        ECDsa? publicKey;
        try
        {
            var key = await _keyClient.GetKeyAsync(_keyName, keyId, cancellationToken).ConfigureAwait(false);
            publicKey = key.Value.Key.ToECDsa(includePrivateParameters: false);
        }
        catch (Azure.RequestFailedException exception) when (exception.Status == 404)
        {
            publicKey = null; // Unknown key version: cache the miss; tokens with it are rejected.
        }

        _cache[keyId] = publicKey;
        return publicKey;
    }
}
