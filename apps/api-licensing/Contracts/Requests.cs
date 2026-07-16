namespace RestrictPoint.Api.Licensing.Contracts;

/// <summary>
/// Request for POST /v1/licenses/validate (docs/16, extended with the replay-protection
/// fields mandated by docs/10: nonce + timestamp are required on every validation).
/// </summary>
public sealed record ValidateLicenseRequest
{
    public string? LicenseToken { get; init; }

    public Guid? TenantId { get; init; }

    public Guid? ProjectId { get; init; }

    public Guid? WebPartGuid { get; init; }

    public Guid? InstallationId { get; init; }

    /// <summary>Client-generated unique nonce (replay protection, docs/10).</summary>
    public string? Nonce { get; init; }

    /// <summary>Client clock at request time; must be within ±5 minutes (docs/10).</summary>
    public DateTimeOffset? TimestampUtc { get; init; }

    public string? SdkVersion { get; init; }
}

/// <summary>Request for POST /v1/licenses/issue.</summary>
public sealed record IssueLicenseRequest
{
    public Guid? ProjectId { get; init; }

    public Guid? DeveloperOrganizationId { get; init; }

    public Guid? CustomerOrganizationId { get; init; }

    /// <summary>The customer SharePoint tenant the license binds to.</summary>
    public Guid? CustomerTenantId { get; init; }

    /// <summary>Trial, Monthly, Annual, Enterprise, or Lifetime.</summary>
    public string? LicenseType { get; init; }

    /// <summary>Required unless LicenseType is Lifetime.</summary>
    public DateTimeOffset? ExpiresUtc { get; init; }

    public Guid? SubscriptionId { get; init; }

    public IReadOnlyDictionary<string, bool>? Features { get; init; }

    public IReadOnlyDictionary<string, int>? Limits { get; init; }

    public IReadOnlyList<Guid>? WebPartGuids { get; init; }
}

/// <summary>Request for POST /v1/licenses/revoke.</summary>
public sealed record RevokeLicenseRequest
{
    public Guid? LicenseId { get; init; }

    public string? Reason { get; init; }
}
