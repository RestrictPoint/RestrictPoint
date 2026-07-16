using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using RestrictPoint.Api.Licensing.Application.Abstractions;

namespace RestrictPoint.Api.Licensing.Functions;

/// <summary>
/// GET /v1/licenses/keys — RFC 7517 JWKS document exposing the active ES256 public keys.
/// Anonymous by design: public keys are public. The SPFx SDK fetches this set to verify
/// license tokens offline via WebCrypto (docs/14 offline-first validation).
/// </summary>
public sealed class KeySetFunctions
{
    private readonly ILicenseKeySetProvider _keySet;

    public KeySetFunctions(ILicenseKeySetProvider keySet)
    {
        _keySet = keySet;
    }

    [Function("GetLicenseKeys")]
    public async Task<IActionResult> GetKeysAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/licenses/keys")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var keys = await _keySet.GetActiveKeysAsync(request.HttpContext.RequestAborted);

        // Standard JWKS shape so any JOSE library can consume it — no response envelope.
        var document = new
        {
            keys = keys.Select(k => new
            {
                kty = "EC",
                crv = "P-256",
                use = "sig",
                alg = "ES256",
                kid = k.KeyId,
                x = k.X,
                y = k.Y,
            }),
        };

        request.HttpContext.Response.Headers.CacheControl = "public, max-age=3600";

        return new OkObjectResult(document);
    }
}
