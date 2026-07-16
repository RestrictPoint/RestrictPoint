using RestrictPoint.Common;

namespace RestrictPoint.Api.Identity.Domain;

/// <summary>Canonical identity-domain errors. Codes are part of the public API contract.</summary>
public static class IdentityErrors
{
    public static readonly Error UserNotProvisioned = Error.Unauthorized(
        "identity.user_not_provisioned",
        "The authenticated user has not been provisioned. Call GET /v1/identity/me first.");

    public static readonly Error UserInactive = Error.Forbidden(
        "identity.user_inactive",
        "The user account is deactivated.");

    public static readonly Error OrganizationNotFound = Error.NotFound(
        "identity.organization_not_found",
        "The organization does not exist or the caller is not a member.");

    public static readonly Error OrganizationSuspended = Error.Forbidden(
        "identity.organization_suspended",
        "The organization is suspended.");

    public static readonly Error NotAuthorizedForOrganization = Error.Forbidden(
        "identity.not_authorized",
        "The caller does not have permission to perform this action in the organization.");

    public static readonly Error InvalidOrganizationName = Error.Validation(
        "identity.invalid_organization_name",
        "The organization name must contain at least one letter or digit.");

    public static readonly Error InvalidRole = Error.Validation(
        "identity.invalid_role",
        "The specified role is not a valid organization role.");

    public static readonly Error CannotInviteOwner = Error.Validation(
        "identity.cannot_invite_owner",
        "The Owner role cannot be granted by invitation. Ownership is transferred explicitly.");

    public static readonly Error AlreadyMember = Error.Conflict(
        "identity.already_member",
        "A user with this email address is already a member of the organization.");

    public static readonly Error InvitationAlreadyPending = Error.Conflict(
        "identity.invitation_pending",
        "An invitation for this email address is already pending.");
}
