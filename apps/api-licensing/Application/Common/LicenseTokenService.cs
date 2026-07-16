using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RestrictPoint.Api.Licensing.Application.Abstractions;
using RestrictPoint.Api.Licensing.Domain;
using RestrictPoint.Common;

namespace RestrictPoint.Api.Licensing.Application.Common;

/// <summary>
/// Creates and verifies JWS compact license tokens (docs/10). Header:
/// <c>{ "alg": "ES256", "typ": "JWT", "kid": "&lt;key version&gt;" }</c>.
/// Signing is delegated to Key Vault; verification uses cached public keys and never
/// depends on the vault at validation time.
/// </summary>
public sealed class LicenseTokenService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly ILicenseSigner _signer;
    private readonly ILicensePublicKeyProvider _publicKeys;

    public LicenseTokenService(ILicenseSigner signer, ILicensePublicKeyProvider publicKeys)
    {
        _signer = signer;
        _publicKeys = publicKeys;
    }

    /// <summary>The key id used for newly signed tokens.</summary>
    public string SignerKeyId => _signer.KeyId;

    /// <summary>Creates a signed JWS compact token for the payload.</summary>
    public async Task<string> CreateTokenAsync(LicensePayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var header = new { alg = "ES256", typ = "JWT", kid = _signer.KeyId };

        var signingInput =
            Base64Url(JsonSerializer.SerializeToUtf8Bytes(header, JsonOptions))
            + "."
            + Base64Url(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions));

        var digest = SHA256.HashData(Encoding.ASCII.GetBytes(signingInput));
        var signature = await _signer.SignDigestAsync(digest, cancellationToken).ConfigureAwait(false);

        return signingInput + "." + Base64Url(signature);
    }

    /// <summary>
    /// Verifies a token's signature and structure, returning the payload. Expiry and
    /// binding checks are the caller's responsibility (grace-period logic lives above).
    /// </summary>
    public async Task<Result<LicensePayload>> VerifyTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return LicensingErrors.MalformedToken;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return LicensingErrors.MalformedToken;
        }

        string? keyId;
        LicensePayload? payload;
        byte[] signature;
        try
        {
            var header = JsonSerializer.Deserialize<JsonElement>(FromBase64Url(parts[0]));
            if (!header.TryGetProperty("alg", out var alg) || alg.GetString() != "ES256")
            {
                // Reject anything but ES256 — prevents algorithm-substitution attacks.
                return LicensingErrors.MalformedToken;
            }

            keyId = header.TryGetProperty("kid", out var kid) ? kid.GetString() : null;
            payload = JsonSerializer.Deserialize<LicensePayload>(FromBase64Url(parts[1]), JsonOptions);
            signature = FromBase64Url(parts[2]);
        }
        catch (Exception exception) when (exception is JsonException or FormatException)
        {
            return LicensingErrors.MalformedToken;
        }

        if (payload is null || string.IsNullOrWhiteSpace(keyId) || signature.Length != 64)
        {
            return LicensingErrors.MalformedToken;
        }

        var publicKey = await _publicKeys.GetPublicKeyAsync(keyId, cancellationToken).ConfigureAwait(false);
        if (publicKey is null)
        {
            return LicensingErrors.UnknownSigningKey;
        }

        var signingInput = Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]);
        var isValid = publicKey.VerifyData(
            signingInput, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return isValid ? payload : LicensingErrors.InvalidSignature;
    }

    internal static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    internal static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '='));
    }
}
