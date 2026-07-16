using RestrictPoint.Common;

namespace RestrictPoint.Api.Billing.Domain;

/// <summary>Canonical billing-domain errors. Codes are part of the public API contract.</summary>
public static class BillingErrors
{
    public static readonly Error SubscriptionNotFound = Error.NotFound(
        "billing.subscription_not_found",
        "The subscription does not exist or the caller is not authorized to access it.");

    public static readonly Error NotAuthorizedForOrganization = Error.Forbidden(
        "billing.not_authorized",
        "The caller does not have permission to manage billing for this organization.");

    public static readonly Error InvalidWebhookSignature = Error.Unauthorized(
        "billing.invalid_webhook_signature",
        "The webhook signature is invalid.");

    public static readonly Error IllegalStateTransition = Error.Conflict(
        "billing.illegal_state_transition",
        "The requested subscription state transition is not permitted.");

    public static readonly Error AlreadyCanceled = Error.Conflict(
        "billing.already_canceled",
        "The subscription is already canceled.");

    public static readonly Error InvalidLicenseTemplate = Error.Validation(
        "billing.invalid_license_template",
        "The license template is invalid.");

    public static readonly Error PaymentProviderUnavailable = Error.Unexpected(
        "billing.payment_provider_unavailable",
        "The payment provider could not be reached.");

    public static readonly Error IdentityUnavailable = Error.Unexpected(
        "billing.identity_unavailable",
        "The identity service could not be reached to authorize the request.");
}
