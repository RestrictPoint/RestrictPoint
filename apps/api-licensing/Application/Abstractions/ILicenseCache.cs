namespace RestrictPoint.Api.Licensing.Application.Abstractions;

/// <summary>Cached license state used on the validation hot path (docs/10 caching strategy).</summary>
public sealed record CachedLicenseState
{
    public required Guid LicenseId { get; init; }

    /// <summary>Persisted status name (Active/Suspended/Revoked/Expired).</summary>
    public required string Status { get; init; }

    public DateTimeOffset? ExpiresUtc { get; init; }
}

/// <summary>
/// Redis-backed licensing cache. Implementations degrade gracefully: cache failures fall
/// back to the database and never fail a validation request. Replay protection is the
/// exception — when Redis is unavailable, nonce checks are skipped rather than failing
/// closed, because availability of license validation outranks replay hardening (docs/10
/// offline mode tolerates far weaker guarantees than this path).
/// </summary>
public interface ILicenseCache
{
    Task<CachedLicenseState?> GetLicenseAsync(Guid licenseId, CancellationToken cancellationToken);

    Task SetLicenseAsync(CachedLicenseState state, CancellationToken cancellationToken);

    Task InvalidateLicenseAsync(Guid licenseId, CancellationToken cancellationToken);

    /// <summary>
    /// Registers a nonce for replay protection. Returns false when the nonce was already
    /// seen inside the deduplication window (a replay).
    /// </summary>
    Task<bool> TryRegisterNonceAsync(string nonce, TimeSpan window, CancellationToken cancellationToken);
}
