using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RestrictPoint.Common;

namespace RestrictPoint.Auth;

/// <summary>
/// Resolves the caller's role in an organization. Services use this for cross-context
/// authorization decisions; roles always come from the Identity service, never from
/// token claims or local tables.
/// </summary>
public interface IOrganizationRoleResolver
{
    /// <summary>
    /// Returns the caller's role name in the organization, or null when the caller is not
    /// an active member. Failures reaching Identity surface as an error result.
    /// </summary>
    Task<Result<string?>> GetCallerRoleAsync(
        string bearerToken,
        Guid organizationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Resolves roles via the Identity service's GET /v1/identity/me, forwarding the caller's
/// own bearer token. Bounded contexts communicate over REST — no shared tables.
/// </summary>
public sealed partial class IdentityOrganizationRoleResolver : IOrganizationRoleResolver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Error IdentityUnavailable = Error.Unexpected(
        "auth.identity_unavailable",
        "The identity service could not be reached to authorize the request.");

    private readonly HttpClient _httpClient;
    private readonly ILogger<IdentityOrganizationRoleResolver> _logger;

    public IdentityOrganizationRoleResolver(
        HttpClient httpClient,
        ILogger<IdentityOrganizationRoleResolver> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<string?>> GetCallerRoleAsync(
        string bearerToken,
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bearerToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, "v1/identity/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException exception)
        {
            LogIdentityUnreachable(_logger, exception);
            return IdentityUnavailable;
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return Error.Unauthorized("auth.invalid_token", "The access token is invalid or expired.");
            }

            if (!response.IsSuccessStatusCode)
            {
                LogIdentityError(_logger, (int)response.StatusCode);
                return IdentityUnavailable;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            MeEnvelope? envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<MeEnvelope>(body, JsonOptions);
            }
            catch (JsonException exception)
            {
                LogIdentityUnreachable(_logger, exception);
                return IdentityUnavailable;
            }

            var role = envelope?.Data?.Organizations?
                .FirstOrDefault(o => o.Id == organizationId)?.Role;

            return Result.Success(role);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Identity service unreachable during authorization.")]
    private static partial void LogIdentityUnreachable(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error,
        Message = "Identity service returned unexpected status {StatusCode} during authorization.")]
    private static partial void LogIdentityError(ILogger logger, int statusCode);

    private sealed record MeEnvelope(MeData? Data);

    private sealed record MeData(IReadOnlyList<MeOrganization>? Organizations);

    private sealed record MeOrganization(Guid Id, string? Role);
}
