using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using RestrictPoint.Common;

namespace RestrictPoint.Auth;

/// <summary>
/// Validates Entra External ID JWTs using the tenant's OIDC discovery document.
/// Signing keys are cached and automatically refreshed by <see cref="ConfigurationManager{T}"/>,
/// which handles Entra key rollover without redeployment.
/// </summary>
public sealed class EntraJwtValidator : IJwtValidator
{
    private static readonly Error InvalidToken =
        Error.Unauthorized("auth.invalid_token", "The access token is invalid or expired.");

    private static readonly Error MissingClaims =
        Error.Unauthorized("auth.missing_claims", "The access token is missing required claims.");

    private readonly EntraAuthenticationOptions _options;
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
    private readonly JsonWebTokenHandler _handler = new();

    public EntraJwtValidator(
        EntraAuthenticationOptions options,
        IConfigurationManager<OpenIdConnectConfiguration> configurationManager)
    {
        _options = options;
        _configurationManager = configurationManager;
    }

    /// <summary>Creates a validator with the standard OIDC metadata retriever.</summary>
    public static EntraJwtValidator Create(EntraAuthenticationOptions options)
    {
        var configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            options.MetadataAddress,
            new OpenIdConnectConfigurationRetriever());

        return new EntraJwtValidator(options, configurationManager);
    }

    public async Task<Result<AuthenticatedPrincipal>> ValidateAsync(
        string bearerToken,
        CancellationToken cancellationToken)
    {
        OpenIdConnectConfiguration configuration;
        try
        {
            configuration = await _configurationManager.GetConfigurationAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Discovery endpoint unreachable: fail closed. Callers observe 401 and retry.
            return Error.Unauthorized(
                "auth.metadata_unavailable",
                "Token validation is temporarily unavailable.");
        }

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = configuration.Issuer,
            ValidAudiences = _options.Audiences,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = _options.ClockSkew,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        };

        var validation = await _handler.ValidateTokenAsync(bearerToken, parameters).ConfigureAwait(false);
        if (!validation.IsValid)
        {
            return InvalidToken;
        }

        return ExtractPrincipal(validation.Claims);
    }

    private static Result<AuthenticatedPrincipal> ExtractPrincipal(IDictionary<string, object> claims)
    {
        var objectId = GetClaim(claims, "oid");
        var email = GetClaim(claims, "email") ?? GetClaim(claims, "preferred_username");

        if (string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(email))
        {
            return MissingClaims;
        }

        var displayName = GetClaim(claims, "name");

        return new AuthenticatedPrincipal
        {
            ObjectId = objectId,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName,
            IdentityProvider = GetClaim(claims, "idp"),
        };
    }

    private static string? GetClaim(IDictionary<string, object> claims, string name) =>
        claims.TryGetValue(name, out var value) ? value.ToString() : null;
}
