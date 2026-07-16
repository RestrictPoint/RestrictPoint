using Azure.Security.KeyVault.Keys;
using RestrictPoint.Api.Licensing.Application.Abstractions;

namespace RestrictPoint.Api.Licensing.Infrastructure;

/// <summary>
/// Enumerates enabled P-256 versions of the license signing key from Key Vault, cached
/// for 24 hours (docs/10 public key TTL). Serving the JWKS never blocks on Key Vault
/// within the cache window.
/// </summary>
public sealed class KeyVaultLicenseKeySetProvider : ILicenseKeySetProvider, IDisposable
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private readonly KeyClient _keyClient;
    private readonly string _keyName;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private IReadOnlyList<LicenseSigningKey> _cachedKeys = [];
    private DateTimeOffset _cacheExpiresUtc = DateTimeOffset.MinValue;

    public KeyVaultLicenseKeySetProvider(KeyClient keyClient, string keyName, TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyName);
        _keyClient = keyClient;
        _keyName = keyName;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<LicenseSigningKey>> GetActiveKeysAsync(CancellationToken cancellationToken)
    {
        if (_timeProvider.GetUtcNow() < _cacheExpiresUtc)
        {
            return _cachedKeys;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_timeProvider.GetUtcNow() < _cacheExpiresUtc)
            {
                return _cachedKeys;
            }

            var keys = new List<LicenseSigningKey>();

            await foreach (var properties in _keyClient
                .GetPropertiesOfKeyVersionsAsync(_keyName, cancellationToken)
                .ConfigureAwait(false))
            {
                if (properties.Enabled != true || properties.Version is null)
                {
                    continue;
                }

                var key = await _keyClient
                    .GetKeyAsync(_keyName, properties.Version, cancellationToken)
                    .ConfigureAwait(false);

                var jwk = key.Value.Key;
                if (jwk.KeyType != KeyType.Ec || jwk.X is null || jwk.Y is null)
                {
                    continue;
                }

                keys.Add(new LicenseSigningKey
                {
                    KeyId = properties.Version,
                    X = Base64Url(jwk.X),
                    Y = Base64Url(jwk.Y),
                });
            }

            _cachedKeys = keys;
            _cacheExpiresUtc = _timeProvider.GetUtcNow().Add(CacheTtl);
            return _cachedKeys;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public void Dispose() => _refreshLock.Dispose();
}
