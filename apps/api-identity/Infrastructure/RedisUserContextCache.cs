using System.Text.Json;
using Microsoft.Extensions.Logging;
using RestrictPoint.Api.Identity.Application.Abstractions;
using StackExchange.Redis;

namespace RestrictPoint.Api.Identity.Infrastructure;

/// <summary>
/// Redis-backed user context cache. All operations degrade gracefully: a Redis outage is
/// logged and treated as a cache miss (docs/11 failure mode: "Redis failure → fallback to DB").
/// </summary>
public sealed partial class RedisUserContextCache : IUserContextCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisUserContextCache> _logger;

    public RedisUserContextCache(IConnectionMultiplexer redis, ILogger<RedisUserContextCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<CachedUserContext?> GetAsync(
        string externalObjectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = await _redis.GetDatabase().StringGetAsync(Key(externalObjectId))
                .ConfigureAwait(false);

            return value.IsNullOrEmpty
                ? null
                : JsonSerializer.Deserialize<CachedUserContext>(value.ToString());
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException or JsonException)
        {
            LogCacheReadFailed(_logger, exception);
            return null;
        }
    }

    public async Task SetAsync(
        string externalObjectId,
        CachedUserContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            await _redis.GetDatabase().StringSetAsync(
                    Key(externalObjectId),
                    JsonSerializer.Serialize(context),
                    Ttl)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            LogCacheWriteFailed(_logger, exception);
        }
    }

    public async Task InvalidateAsync(string externalObjectId, CancellationToken cancellationToken)
    {
        try
        {
            await _redis.GetDatabase().KeyDeleteAsync(Key(externalObjectId)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            // A failed invalidation self-heals within the TTL window (10 minutes).
            LogCacheInvalidationFailed(_logger, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "User context cache read failed; falling back to database.")]
    private static partial void LogCacheReadFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "User context cache write failed; continuing without cache.")]
    private static partial void LogCacheWriteFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "User context cache invalidation failed; entry expires via TTL.")]
    private static partial void LogCacheInvalidationFailed(ILogger logger, Exception exception);

    private static string Key(string externalObjectId) => $"identity:user-context:{externalObjectId}";
}

/// <summary>
/// No-op cache used when Redis is not configured (local development without a cache).
/// </summary>
public sealed class NullUserContextCache : IUserContextCache
{
    public Task<CachedUserContext?> GetAsync(string externalObjectId, CancellationToken cancellationToken) =>
        Task.FromResult<CachedUserContext?>(null);

    public Task SetAsync(string externalObjectId, CachedUserContext context, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task InvalidateAsync(string externalObjectId, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
