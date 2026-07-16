using RestrictPoint.Common;

namespace RestrictPoint.Api.Marketplace.Domain;

public static class MarketplaceErrors
{
    // Listing errors
    public static readonly Error ListingNotFound = Error.NotFound(
        "Marketplace.ListingNotFound",
        "The specified listing does not exist.");

    public static readonly Error ListingAlreadyExists = Error.Conflict(
        "Marketplace.ListingAlreadyExists",
        "A listing for this project already exists.");

    public static readonly Error ListingNotPublished = Error.Validation(
        "Marketplace.ListingNotPublished",
        "This listing is not published.");

    public static readonly Error CannotPublishWithoutPricing = Error.Validation(
        "Marketplace.CannotPublishWithoutPricing",
        "Cannot publish listing without at least one active pricing plan.");

    public static readonly Error InvalidStateTransition = Error.Validation(
        "Marketplace.InvalidStateTransition",
        "The requested state transition is not allowed.");

    // Pricing errors
    public static readonly Error PricingPlanNotFound = Error.NotFound(
        "Marketplace.PricingPlanNotFound",
        "The specified pricing plan does not exist.");

    public static readonly Error PricingPlanInactive = Error.Validation(
        "Marketplace.PricingPlanInactive",
        "This pricing plan is not active.");

    // Category errors
    public static readonly Error CategoryNotFound = Error.NotFound(
        "Marketplace.CategoryNotFound",
        "The specified category does not exist.");

    public static readonly Error CategoryHasListings = Error.Conflict(
        "Marketplace.CategoryHasListings",
        "Cannot delete category with active listings.");

    // Tag errors
    public static readonly Error TagNotFound = Error.NotFound(
        "Marketplace.TagNotFound",
        "The specified tag does not exist.");

    public static readonly Error TagAlreadyExists = Error.Conflict(
        "Marketplace.TagAlreadyExists",
        "A tag with this name already exists.");

    // Review errors
    public static readonly Error ReviewNotFound = Error.NotFound(
        "Marketplace.ReviewNotFound",
        "The specified review does not exist.");

    public static readonly Error ReviewAlreadyExists = Error.Conflict(
        "Marketplace.ReviewAlreadyExists",
        "You have already reviewed this listing.");

    public static readonly Error ReviewEditWindowExpired = Error.Validation(
        "Marketplace.ReviewEditWindowExpired",
        "Reviews can only be edited within 24 hours of creation.");

    public static readonly Error CannotReviewOwnListing = Error.Validation(
        "Marketplace.CannotReviewOwnListing",
        "You cannot review your own listing.");

    // Authorization errors
    public static readonly Error NotAuthorizedForOrganization = Error.Forbidden(
        "Marketplace.NotAuthorizedForOrganization",
        "You do not have the required role in this organization.");

    public static readonly Error MissingUserIdentity = Error.Unauthorized(
        "Marketplace.MissingUserIdentity",
        "The access token does not carry a resolvable user identity.");

    public static readonly Error NotListingOwner = Error.Forbidden(
        "Marketplace.NotListingOwner",
        "You do not have permission to modify this listing.");

    public static readonly Error NotReviewAuthor = Error.Forbidden(
        "Marketplace.NotReviewAuthor",
        "You do not have permission to modify this review.");

    // Project validation errors
    public static readonly Error ProjectNotFound = Error.NotFound(
        "Marketplace.ProjectNotFound",
        "The specified project does not exist.");

    public static readonly Error ProjectNotOwnedByOrganization = Error.Forbidden(
        "Marketplace.ProjectNotOwnedByOrganization",
        "The project is not owned by your organization.");
}
