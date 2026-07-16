using RestrictPoint.Api.Identity.Contracts;

namespace RestrictPoint.Api.Identity.Application.Abstractions;

/// <summary>Cached identity context for a user, keyed by external object id.</summary>
public sealed record CachedUserContext
{
    public required Guid UserId { get; init; }

    public required string Email { get; init; }

    public required string DisplayName { get; init; }

    public required bool IsActive { get; init; }

    public required IReadOnlyList<OrganizationSummary> Organizations { get; init; }
}

/// <summary>
/// Redis-backed user context cache (docs/11: user→organization mapping, TTL 5–15 minutes).
/// Implementations must degrade gracefully: a cache failure falls back to the database
/// and never fails the request.
/// </summary>
public interface IUserContextCache
{
    Task<CachedUserContext?> GetAsync(string externalObjectId, CancellationToken cancellationToken);

    Task SetAsync(string externalObjectId, CachedUserContext context, CancellationToken cancellationToken);

    Task InvalidateAsync(string externalObjectId, CancellationToken cancellationToken);
}
