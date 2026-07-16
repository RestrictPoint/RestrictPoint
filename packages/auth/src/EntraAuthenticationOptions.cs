namespace RestrictPoint.Auth;

/// <summary>
/// Configuration for validating JWTs issued by Microsoft Entra External ID.
/// Values are supplied via App Configuration / environment settings — never hardcoded.
/// </summary>
public sealed record EntraAuthenticationOptions
{
    public const string SectionName = "EntraExternalId";

    /// <summary>
    /// The Entra External ID tenant subdomain, e.g. <c>restrictpointext</c>.
    /// Used to build the CIAM authority: https://{subdomain}.ciamlogin.com/{tenantId}/v2.0
    /// </summary>
    public required string TenantSubdomain { get; init; }

    /// <summary>The external tenant ID (GUID).</summary>
    public required string TenantId { get; init; }

    /// <summary>
    /// Accepted audiences: the API app registration's Application ID URI and/or client ID.
    /// </summary>
    public required IReadOnlyList<string> Audiences { get; init; }

    /// <summary>Clock skew tolerance for token lifetime validation.</summary>
    public TimeSpan ClockSkew { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>The OIDC authority URL for the external tenant.</summary>
    public string Authority => $"https://{TenantSubdomain}.ciamlogin.com/{TenantId}/v2.0";

    /// <summary>The OIDC discovery document URL.</summary>
    public string MetadataAddress => $"{Authority}/.well-known/openid-configuration";
}
