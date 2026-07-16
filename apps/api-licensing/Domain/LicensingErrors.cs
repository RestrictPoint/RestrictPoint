using RestrictPoint.Common;

namespace RestrictPoint.Api.Licensing.Domain;

/// <summary>Canonical licensing-domain errors. Codes are part of the public API contract.</summary>
public static class LicensingErrors
{
    public static readonly Error LicenseNotFound = Error.NotFound(
        "licensing.license_not_found",
        "The license does not exist or the caller is not authorized to access it.");

    public static readonly Error InvalidSignature = Error.Validation(
        "licensing.invalid_signature",
        "The license token signature is invalid.");

    public static readonly Error MalformedToken = Error.Validation(
        "licensing.malformed_token",
        "The license token is malformed.");

    public static readonly Error UnknownSigningKey = Error.Validation(
        "licensing.unknown_signing_key",
        "The license token references an unknown signing key.");

    public static readonly Error TenantMismatch = Error.Forbidden(
        "licensing.tenant_mismatch",
        "The license is not valid for this tenant.");

    public static readonly Error WebPartMismatch = Error.Forbidden(
        "licensing.webpart_mismatch",
        "The license is not valid for this web part.");

    public static readonly Error ProjectMismatch = Error.Forbidden(
        "licensing.project_mismatch",
        "The license is not valid for this project.");

    public static readonly Error ReplayDetected = Error.Forbidden(
        "licensing.replay_detected",
        "The validation request was rejected by replay protection.");

    public static readonly Error StaleTimestamp = Error.Validation(
        "licensing.stale_timestamp",
        "The validation request timestamp is outside the allowed window.");

    public static readonly Error NotAuthorizedForOrganization = Error.Forbidden(
        "licensing.not_authorized",
        "The caller does not have permission to manage licenses for this organization.");

    public static readonly Error InvalidLicenseType = Error.Validation(
        "licensing.invalid_license_type",
        "The specified license type is not valid.");

    public static readonly Error ExpiryRequired = Error.Validation(
        "licensing.expiry_required",
        "Non-lifetime licenses require an expiration date in the future.");

    public static readonly Error AlreadyRevoked = Error.Conflict(
        "licensing.already_revoked",
        "The license is already revoked.");

    public static readonly Error IdentityUnavailable = Error.Unexpected(
        "licensing.identity_unavailable",
        "The identity service could not be reached to authorize the request.");
}
