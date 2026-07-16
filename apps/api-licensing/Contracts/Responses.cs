namespace RestrictPoint.Api.Licensing.Contracts;

/// <summary>Response for POST /v1/licenses/validate (docs/16).</summary>
public sealed record ValidateLicenseResponse
{
    public required bool IsValid { get; init; }

    /// <summary>active, expired, grace, revoked, or suspended.</summary>
    public required string Status { get; init; }

    public required IReadOnlyDictionary<string, bool> Features { get; init; }

    public required IReadOnlyDictionary<string, int> Limits { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>Machine-readable failure reason when IsValid is false.</summary>
    public string? FailureReason { get; init; }
}

/// <summary>Response for POST /v1/licenses/issue — includes the signed token exactly once.</summary>
public sealed record LicenseIssuedResponse
{
    public required Guid LicenseId { get; init; }

    public required string LicenseToken { get; init; }

    public required string LicenseType { get; init; }

    public required string Status { get; init; }

    public DateTimeOffset? ExpiresUtc { get; init; }

    public required DateTimeOffset IssuedUtc { get; init; }
}

/// <summary>License summary for list/get endpoints.</summary>
public sealed record LicenseSummary
{
    public required Guid Id { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid CustomerOrganizationId { get; init; }

    public required Guid CustomerTenantId { get; init; }

    public required string LicenseType { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset IssuedUtc { get; init; }

    public DateTimeOffset? ExpiresUtc { get; init; }

    public DateTimeOffset? RevokedUtc { get; init; }

    public required int Version { get; init; }

    public required IReadOnlyDictionary<string, bool> Features { get; init; }

    public required IReadOnlyDictionary<string, int> Limits { get; init; }

    public required IReadOnlyList<Guid> WebPartGuids { get; init; }
}
