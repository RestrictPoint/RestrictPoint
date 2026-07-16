using System.Text.Json.Serialization;

namespace RestrictPoint.Api.Licensing.Domain;

/// <summary>
/// The signed license payload (docs/10 License Model). Serialized camelCase into the JWS
/// payload segment; the SPFx SDK verifies and evaluates it offline.
/// </summary>
public sealed record LicensePayload
{
    [JsonPropertyName("jti")]
    public required string TokenId { get; init; }

    public required Guid LicenseId { get; init; }

    public required Guid ProjectId { get; init; }

    /// <summary>The customer SharePoint tenant the license is bound to.</summary>
    public required Guid TenantId { get; init; }

    /// <summary>The customer organization id.</summary>
    public required Guid CustomerId { get; init; }

    public required string LicenseType { get; init; }

    /// <summary>Unix epoch seconds.</summary>
    public required long IssuedAt { get; init; }

    /// <summary>Unix epoch seconds. Null for lifetime licenses.</summary>
    public long? ExpiresAt { get; init; }

    public required IReadOnlyDictionary<string, bool> Features { get; init; }

    public required IReadOnlyDictionary<string, int> Limits { get; init; }

    public Guid? InstallationId { get; init; }

    public required IReadOnlyList<Guid> WebPartGuids { get; init; }

    public required int Version { get; init; }
}
