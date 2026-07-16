using RestrictPoint.Database;

namespace RestrictPoint.Api.Licensing.Domain;

public enum LicenseType
{
    Trial,
    Monthly,
    Annual,
    Enterprise,
    Lifetime,
}

public enum LicenseStatus
{
    Active,
    Suspended,
    Revoked,
    Expired,
}

/// <summary>
/// Core entitlement record (docs/09 Licensing schema). A license authorizes a customer
/// organization to run a developer's project, with feature flags and limits embedded in
/// signed license tokens.
/// </summary>
public sealed class License : BaseEntity
{
    public required Guid ProjectId { get; set; }

    /// <summary>The purchasing customer organization.</summary>
    public required Guid CustomerOrganizationId { get; set; }

    /// <summary>The developer organization that owns the project.</summary>
    public required Guid DeveloperOrganizationId { get; set; }

    /// <summary>The SharePoint tenant the license is bound to.</summary>
    public required Guid CustomerTenantId { get; set; }

    public required LicenseType LicenseType { get; set; }

    public LicenseStatus Status { get; set; } = LicenseStatus.Active;

    public required DateTimeOffset IssuedUtc { get; set; }

    /// <summary>Null for Lifetime licenses.</summary>
    public DateTimeOffset? ExpiresUtc { get; set; }

    public DateTimeOffset? RevokedUtc { get; set; }

    /// <summary>Optional link to the billing subscription that produced this license.</summary>
    public Guid? SubscriptionId { get; set; }

    /// <summary>Monotonic payload version; incremented on refresh/re-issue.</summary>
    public int Version { get; set; } = 1;

    public ICollection<LicenseFeature> Features { get; } = [];

    public ICollection<LicenseLimit> Limits { get; } = [];

    public ICollection<LicenseWebPart> WebParts { get; } = [];

    /// <summary>
    /// The effective status considering expiry: an Active license past its expiry is Expired.
    /// </summary>
    public LicenseStatus EffectiveStatus(DateTimeOffset utcNow) =>
        Status == LicenseStatus.Active && ExpiresUtc is not null && utcNow >= ExpiresUtc
            ? LicenseStatus.Expired
            : Status;
}

/// <summary>Feature flag embedded in the license payload (immutable per license version).</summary>
public sealed class LicenseFeature : BaseEntity
{
    public const int FeatureKeyMaxLength = 128;

    public required Guid LicenseId { get; set; }

    public required string FeatureKey { get; set; }

    public required bool Enabled { get; set; }
}

/// <summary>Numeric limit embedded in the license payload, e.g. maxUsers.</summary>
public sealed class LicenseLimit : BaseEntity
{
    public const int LimitKeyMaxLength = 128;

    public required Guid LicenseId { get; set; }

    public required string LimitKey { get; set; }

    public required int Value { get; set; }
}

/// <summary>A web part GUID the license is bound to (docs/10 installation binding).</summary>
public sealed class LicenseWebPart : BaseEntity
{
    public required Guid LicenseId { get; set; }

    public required Guid WebPartGuid { get; set; }
}
