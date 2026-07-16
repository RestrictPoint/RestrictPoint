using System.Text.Json;
using Microsoft.Extensions.Logging;
using RestrictPoint.Api.Licensing.Application.Abstractions;
using StackExchange.Redis;

namespace RestrictPoint.Api.Licensing.Infrastructure;

/// <summary>
/// Redis licensing cache (docs/10 caching strategy). All operations degrade gracefully:
/// license-state failures fall back to the database; nonce-registration failures skip
/// replay hardening rather than failing validation availability.
/// </summary>
public sealed partial class RedisLicenseCache : ILicenseCache
{
    private static readonly TimeSpan LicenseTtl = TimeSpan.FromHours(12);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisLicenseCache> _logger;

    public RedisLicenseCache(IConnectionMultiplexer redis, ILogger<RedisLicenseCache> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<CachedLicenseState?> GetLicenseAsync(Guid licenseId, CancellationToken cancellationToken)
    {
        try
        {
            var value = await _redis.GetDatabase().StringGetAsync(LicenseKey(licenseId)).ConfigureAwait(false);
            return value.IsNullOrEmpty
                ? null
                : JsonSerializer.Deserialize<CachedLicenseState>(value.ToString());
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException or JsonException)
        {
            LogCacheReadFailed(_logger, exception);
            return null;
        }
    }

    public async Task SetLicenseAsync(CachedLicenseState state, CancellationToken cancellationToken)
    {
        try
        {
            await _redis.GetDatabase().StringSetAsync(
                LicenseKey(state.LicenseId),
                JsonSerializer.Serialize(state),
                LicenseTtl).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            LogCacheWriteFailed(_logger, exception);
        }
    }

    public async Task InvalidateLicenseAsync(Guid licenseId, CancellationToken cancellationToken)
    {
        try
        {
            await _redis.GetDatabase().KeyDeleteAsync(LicenseKey(licenseId)).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            // Revocation still lands in SQL; the cache self-heals within the 12h TTL —
            // but log at error level because this extends the revocation window.
            LogInvalidationFailed(_logger, exception, licenseId);
        }
    }

    public async Task<bool> TryRegisterNonceAsync(
        string nonce,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        try
        {
            // SET NX: returns false when the key already exists — a replay.
            return await _redis.GetDatabase().StringSetAsync(
                $"licensing:nonce:{nonce}",
                RedisValue.EmptyString,
                window,
                When.NotExists).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or RedisTimeoutException)
        {
            LogNonceCheckSkipped(_logger, exception);
            return true; // Availability over replay hardening (see ILicenseCache contract).
        }
    }

    private static string LicenseKey(Guid licenseId) => $"license:{licenseId}";

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "License cache read failed; falling back to database.")]
    private static partial void LogCacheReadFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "License cache write failed; continuing without cache.")]
    private static partial void LogCacheWriteFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "License cache invalidation failed for {LicenseId}; revocation propagation delayed up to TTL.")]
    private static partial void LogInvalidationFailed(ILogger logger, Exception exception, Guid licenseId);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Nonce registration unavailable; replay check skipped for this request.")]
    private static partial void LogNonceCheckSkipped(ILogger logger, Exception exception);
}
