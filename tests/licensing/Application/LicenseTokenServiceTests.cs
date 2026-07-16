using RestrictPoint.Api.Licensing.Application.Common;
using RestrictPoint.Api.Licensing.Domain;
using Xunit;

namespace RestrictPoint.Api.Licensing.Tests.Application;

public sealed class LicenseTokenServiceTests : IDisposable
{
    private readonly InMemoryLicenseSigning _signing = new();
    private readonly LicenseTokenService _service;

    public LicenseTokenServiceTests()
    {
        _service = new LicenseTokenService(_signing, _signing);
    }

    public void Dispose() => _signing.Dispose();

    private static LicensePayload SamplePayload() => new()
    {
        TokenId = "abc123",
        LicenseId = Guid.NewGuid(),
        ProjectId = Guid.NewGuid(),
        TenantId = Guid.NewGuid(),
        CustomerId = Guid.NewGuid(),
        LicenseType = "Annual",
        IssuedAt = 1784200000,
        ExpiresAt = 1815736000,
        Features = new Dictionary<string, bool> { ["Export"] = true, ["AI"] = false },
        Limits = new Dictionary<string, int> { ["maxUsers"] = 50 },
        WebPartGuids = [Guid.NewGuid()],
        Version = 1,
    };

    [Fact]
    public async Task Sign_verify_roundtrip_preserves_payload()
    {
        var payload = SamplePayload();

        var token = await _service.CreateTokenAsync(payload, CancellationToken.None);
        var result = await _service.VerifyTokenAsync(token, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(payload.LicenseId, result.Value.LicenseId);
        Assert.Equal(payload.TenantId, result.Value.TenantId);
        Assert.Equal(payload.Features, result.Value.Features);
        Assert.Equal(payload.Limits, result.Value.Limits);
        Assert.Equal(payload.WebPartGuids, result.Value.WebPartGuids);
        Assert.Equal("abc123", result.Value.TokenId);
    }

    [Fact]
    public async Task Tampered_payload_fails_signature_verification()
    {
        var token = await _service.CreateTokenAsync(SamplePayload(), CancellationToken.None);
        var parts = token.Split('.');

        // Re-encode a modified payload while keeping the original signature.
        var payloadJson = System.Text.Encoding.UTF8.GetString(LicenseTokenService.FromBase64Url(parts[1]));
        var tamperedJson = payloadJson.Replace("\"ai\":false", "\"ai\":true", StringComparison.OrdinalIgnoreCase)
            .Replace("\"AI\":false", "\"AI\":true", StringComparison.Ordinal);
        var tampered = parts[0] + "."
            + LicenseTokenService.Base64Url(System.Text.Encoding.UTF8.GetBytes(tamperedJson))
            + "." + parts[2];

        var result = await _service.VerifyTokenAsync(tampered, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LicensingErrors.InvalidSignature.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Tampered_signature_is_rejected()
    {
        var token = await _service.CreateTokenAsync(SamplePayload(), CancellationToken.None);
        var parts = token.Split('.');

        var signature = LicenseTokenService.FromBase64Url(parts[2]);
        signature[10] ^= 0xFF;
        var tampered = parts[0] + "." + parts[1] + "." + LicenseTokenService.Base64Url(signature);

        var result = await _service.VerifyTokenAsync(tampered, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LicensingErrors.InvalidSignature.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Algorithm_substitution_is_rejected()
    {
        var token = await _service.CreateTokenAsync(SamplePayload(), CancellationToken.None);
        var parts = token.Split('.');

        // Forge a header claiming alg=none while keeping structure intact.
        var forgedHeader = LicenseTokenService.Base64Url(
            System.Text.Encoding.UTF8.GetBytes("""{"alg":"none","typ":"JWT","kid":"test-key-1"}"""));
        var forged = forgedHeader + "." + parts[1] + "." + parts[2];

        var result = await _service.VerifyTokenAsync(forged, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LicensingErrors.MalformedToken.Code, result.Error!.Code);
    }

    [Fact]
    public async Task Unknown_key_id_is_rejected()
    {
        var token = await _service.CreateTokenAsync(SamplePayload(), CancellationToken.None);
        var parts = token.Split('.');

        var forgedHeader = LicenseTokenService.Base64Url(
            System.Text.Encoding.UTF8.GetBytes("""{"alg":"ES256","typ":"JWT","kid":"rogue-key"}"""));
        var forged = forgedHeader + "." + parts[1] + "." + parts[2];

        var result = await _service.VerifyTokenAsync(forged, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LicensingErrors.UnknownSigningKey.Code, result.Error!.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("a.b")]
    [InlineData("a.b.c.d")]
    [InlineData("!!!.###.$$$")]
    public async Task Malformed_tokens_are_rejected(string token)
    {
        var result = await _service.VerifyTokenAsync(token, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(LicensingErrors.MalformedToken.Code, result.Error!.Code);
    }
}
