namespace RestrictPoint.Api.Licensing.Application.Events;

/// <summary>Service Bus topics this service publishes to.</summary>
public static class Topics
{
    public const string License = "LicenseEvents";
}

/// <summary>Event metadata constants.</summary>
public static class EventMetadata
{
    public const string Publisher = "licensing";
    public const string Version10 = "1.0";
}

/// <summary>LicenseIssued v1.0 (docs/20).</summary>
public sealed record LicenseIssued
{
    public required Guid LicenseId { get; init; }

    public required Guid ProjectId { get; init; }

    public required Guid CustomerOrganizationId { get; init; }

    public required Guid DeveloperOrganizationId { get; init; }

    public Guid? SubscriptionId { get; init; }

    public required string LicenseType { get; init; }

    public DateTimeOffset? ExpiresUtc { get; init; }

    public required DateTimeOffset IssuedUtc { get; init; }
}

/// <summary>LicenseActivated v1.0 (docs/20) — first validation from a new installation.</summary>
public sealed record LicenseActivated
{
    public required Guid LicenseId { get; init; }

    public required Guid InstallationId { get; init; }

    public required Guid CustomerTenantId { get; init; }

    public required DateTimeOffset ActivatedUtc { get; init; }
}

/// <summary>LicenseValidationSucceeded v1.0 (docs/20) — asynchronous, informational.</summary>
public sealed record LicenseValidationSucceeded
{
    public required Guid LicenseId { get; init; }

    public required Guid InstallationId { get; init; }

    public required Guid ProjectId { get; init; }

    public required string ValidationMethod { get; init; }

    public required DateTimeOffset ValidatedUtc { get; init; }
}

/// <summary>LicenseValidationFailed v1.0 (docs/20).</summary>
public sealed record LicenseValidationFailed
{
    public required Guid ProjectId { get; init; }

    public Guid? InstallationId { get; init; }

    public Guid? LicenseId { get; init; }

    public required string FailureReason { get; init; }

    public required DateTimeOffset FailedUtc { get; init; }
}

/// <summary>LicenseRevoked v1.0 (docs/20).</summary>
public sealed record LicenseRevoked
{
    public required Guid LicenseId { get; init; }

    public required Guid ProjectId { get; init; }

    public required string Reason { get; init; }

    public Guid? RevokedByUserId { get; init; }

    public required DateTimeOffset RevokedUtc { get; init; }
}
