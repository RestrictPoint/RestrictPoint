using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using RestrictPoint.Api.Marketplace.Application.BrowseListings;
using RestrictPoint.Api.Marketplace.Application.CreateListing;
using RestrictPoint.Api.Marketplace.Application.ManagePricing;
using RestrictPoint.Api.Marketplace.Application.PublishListing;
using RestrictPoint.Api.Marketplace.Application.SubmitReview;
using RestrictPoint.Api.Marketplace.Contracts;
using RestrictPoint.Auth;
using RestrictPoint.Auth.Http;

namespace RestrictPoint.Api.Marketplace.Functions;

/// <summary>
/// HTTP triggers for the Marketplace APIs (docs/13, docs/16). Catalog reads (list, get,
/// search) are anonymous by design — the marketplace is the public commercial surface.
/// All writes require an authenticated caller.
/// </summary>
public sealed class MarketplaceFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string[]> EmptyBodyDetails = new()
    {
        ["body"] = ["A JSON request body is required."],
    };

    private static readonly Dictionary<string, string[]> ListingIdRequired = new()
    {
        ["listingId"] = ["A valid listing id is required in the route."],
    };

    private readonly CreateListingHandler _createListing;
    private readonly PublishListingHandler _publishListing;
    private readonly AddPricingPlanHandler _addPricing;
    private readonly SubmitReviewHandler _submitReview;
    private readonly BrowseListingsHandler _browse;
    private readonly IValidator<CreateListingRequest> _createValidator;
    private readonly IValidator<AddPricingPlanRequest> _pricingValidator;
    private readonly IValidator<SubmitReviewRequest> _reviewValidator;

    public MarketplaceFunctions(
        CreateListingHandler createListing,
        PublishListingHandler publishListing,
        AddPricingPlanHandler addPricing,
        SubmitReviewHandler submitReview,
        BrowseListingsHandler browse,
        IValidator<CreateListingRequest> createValidator,
        IValidator<AddPricingPlanRequest> pricingValidator,
        IValidator<SubmitReviewRequest> reviewValidator)
    {
        _createListing = createListing;
        _publishListing = publishListing;
        _addPricing = addPricing;
        _submitReview = submitReview;
        _browse = browse;
        _createValidator = createValidator;
        _pricingValidator = pricingValidator;
        _reviewValidator = reviewValidator;
    }

    [Function("CreateListing")]
    public async Task<IActionResult> CreateListingAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/marketplace/listings")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();
        var cancellationToken = request.HttpContext.RequestAborted;

        var body = await ReadBodyAsync<CreateListingRequest>(request, cancellationToken);
        if (body is null)
        {
            return ApiResults.ValidationFailure(EmptyBodyDetails, context.CorrelationId);
        }

        var validation = await _createValidator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiResults.ValidationFailure(ToDetails(validation), context.CorrelationId);
        }

        var result = await _createListing.HandleAsync(context, BearerToken(request), body, cancellationToken);

        return ApiResults.From(result, context.CorrelationId, successStatus: 201);
    }

    [Function("PublishListing")]
    public async Task<IActionResult> PublishListingAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/marketplace/listings/{listingId}/publish")]
        HttpRequest request,
        string listingId,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();

        if (!Guid.TryParse(listingId, out var id))
        {
            return ApiResults.ValidationFailure(ListingIdRequired, context.CorrelationId);
        }

        var result = await _publishListing.HandleAsync(
            context, BearerToken(request), id, request.HttpContext.RequestAborted);

        return ApiResults.From(result, context.CorrelationId);
    }

    [Function("AddPricingPlan")]
    public async Task<IActionResult> AddPricingPlanAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/marketplace/listings/{listingId}/pricing")]
        HttpRequest request,
        string listingId,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();
        var cancellationToken = request.HttpContext.RequestAborted;

        if (!Guid.TryParse(listingId, out var id))
        {
            return ApiResults.ValidationFailure(ListingIdRequired, context.CorrelationId);
        }

        var body = await ReadBodyAsync<AddPricingPlanRequest>(request, cancellationToken);
        if (body is null)
        {
            return ApiResults.ValidationFailure(EmptyBodyDetails, context.CorrelationId);
        }

        var validation = await _pricingValidator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiResults.ValidationFailure(ToDetails(validation), context.CorrelationId);
        }

        var result = await _addPricing.HandleAsync(context, BearerToken(request), id, body, cancellationToken);

        return ApiResults.From(result, context.CorrelationId, successStatus: 201);
    }

    [Function("SubmitReview")]
    public async Task<IActionResult> SubmitReviewAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/marketplace/listings/{listingId}/review")]
        HttpRequest request,
        string listingId,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();
        var cancellationToken = request.HttpContext.RequestAborted;

        if (!Guid.TryParse(listingId, out var id))
        {
            return ApiResults.ValidationFailure(ListingIdRequired, context.CorrelationId);
        }

        var body = await ReadBodyAsync<SubmitReviewRequest>(request, cancellationToken);
        if (body is null)
        {
            return ApiResults.ValidationFailure(EmptyBodyDetails, context.CorrelationId);
        }

        var validation = await _reviewValidator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiResults.ValidationFailure(ToDetails(validation), context.CorrelationId);
        }

        var result = await _submitReview.HandleAsync(context, BearerToken(request), id, body, cancellationToken);

        return ApiResults.From(result, context.CorrelationId, successStatus: 201);
    }

    [Function("ListListings")]
    public async Task<IActionResult> ListListingsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/marketplace/listings")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();

        var query = new BrowseListingsQuery
        {
            CategoryId = ParseGuid(request.Query["categoryId"]),
            OrganizationId = ParseGuid(request.Query["organizationId"]),
            Tag = NullIfEmpty(request.Query["tag"]),
            FeaturedOnly = string.Equals(request.Query["featured"], "true", StringComparison.OrdinalIgnoreCase),
            Page = ParseInt(request.Query["page"], 1),
            PageSize = ParseInt(request.Query["pageSize"], 20),
        };

        var result = await _browse.ListAsync(query, request.HttpContext.RequestAborted);

        return ApiResults.From(result, context.CorrelationId);
    }

    [Function("GetListing")]
    public async Task<IActionResult> GetListingAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/marketplace/listings/{listingId}")]
        HttpRequest request,
        string listingId,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();

        if (!Guid.TryParse(listingId, out var id))
        {
            return ApiResults.ValidationFailure(ListingIdRequired, context.CorrelationId);
        }

        var result = await _browse.GetAsync(id, request.HttpContext.RequestAborted);

        return ApiResults.From(result, context.CorrelationId);
    }

    [Function("SearchListings")]
    public async Task<IActionResult> SearchListingsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/marketplace/search")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();

        var result = await _browse.SearchAsync(
            NullIfEmpty(request.Query["q"]),
            ParseGuid(request.Query["categoryId"]),
            NullIfEmpty(request.Query["tag"]),
            request.HttpContext.RequestAborted);

        return ApiResults.From(result, context.CorrelationId);
    }

    private static string BearerToken(HttpRequest request) =>
        request.Headers.Authorization.ToString()["Bearer ".Length..].Trim();

    private static Guid? ParseGuid(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) ? parsed : fallback;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static async Task<T?> ReadBodyAsync<T>(HttpRequest request, CancellationToken cancellationToken)
        where T : class
    {
        try
        {
            return await JsonSerializer.DeserializeAsync<T>(request.Body, JsonOptions, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, string[]> ToDetails(FluentValidation.Results.ValidationResult validation) =>
        validation.Errors
            .GroupBy(e => e.PropertyName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => JsonNamingPolicy.CamelCase.ConvertName(g.Key),
                g => g.Select(e => e.ErrorMessage).ToArray());
}
