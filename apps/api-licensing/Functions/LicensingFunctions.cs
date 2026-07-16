using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using RestrictPoint.Api.Licensing.Application.IssueLicense;
using RestrictPoint.Api.Licensing.Application.ListLicenses;
using RestrictPoint.Api.Licensing.Application.RevokeLicense;
using RestrictPoint.Api.Licensing.Application.ValidateLicense;
using RestrictPoint.Api.Licensing.Contracts;
using RestrictPoint.Auth;
using RestrictPoint.Auth.Http;
using RestrictPoint.Common;

namespace RestrictPoint.Api.Licensing.Functions;

/// <summary>
/// HTTP triggers for the Licensing APIs (docs/16). ValidateLicense is anonymous by design:
/// the signed license token is the credential. All management endpoints require a bearer
/// token, enforced by <see cref="AuthenticationMiddleware"/>.
/// </summary>
public sealed class LicensingFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string[]> EmptyBodyDetails = new()
    {
        ["body"] = ["A JSON request body is required."],
    };

    private readonly ValidateLicenseHandler _validate;
    private readonly IssueLicenseHandler _issue;
    private readonly RevokeLicenseHandler _revoke;
    private readonly ListLicensesHandler _list;
    private readonly IValidator<ValidateLicenseRequest> _validateValidator;
    private readonly IValidator<IssueLicenseRequest> _issueValidator;
    private readonly IValidator<RevokeLicenseRequest> _revokeValidator;

    public LicensingFunctions(
        ValidateLicenseHandler validate,
        IssueLicenseHandler issue,
        RevokeLicenseHandler revoke,
        ListLicensesHandler list,
        IValidator<ValidateLicenseRequest> validateValidator,
        IValidator<IssueLicenseRequest> issueValidator,
        IValidator<RevokeLicenseRequest> revokeValidator)
    {
        _validate = validate;
        _issue = issue;
        _revoke = revoke;
        _list = list;
        _validateValidator = validateValidator;
        _issueValidator = issueValidator;
        _revokeValidator = revokeValidator;
    }

    [Function("ValidateLicense")]
    public async Task<IActionResult> ValidateAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/licenses/validate")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();
        var cancellationToken = request.HttpContext.RequestAborted;

        var body = await ReadBodyAsync<ValidateLicenseRequest>(request, cancellationToken);
        if (body is null)
        {
            return ApiResults.ValidationFailure(EmptyBodyDetails, context.CorrelationId);
        }

        var validation = await _validateValidator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiResults.ValidationFailure(ToDetails(validation), context.CorrelationId);
        }

        var result = await _validate.HandleAsync(context, body, cancellationToken);

        return ApiResults.From(result, context.CorrelationId);
    }

    [Function("IssueLicense")]
    public async Task<IActionResult> IssueAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/licenses/issue")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();
        var cancellationToken = request.HttpContext.RequestAborted;

        var body = await ReadBodyAsync<IssueLicenseRequest>(request, cancellationToken);
        if (body is null)
        {
            return ApiResults.ValidationFailure(EmptyBodyDetails, context.CorrelationId);
        }

        var validation = await _issueValidator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiResults.ValidationFailure(ToDetails(validation), context.CorrelationId);
        }

        var result = await _issue.HandleAsync(context, BearerToken(request), body, cancellationToken);

        return ApiResults.From(result, context.CorrelationId, successStatus: 201);
    }

    [Function("RevokeLicense")]
    public async Task<IActionResult> RevokeAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/licenses/revoke")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();
        var cancellationToken = request.HttpContext.RequestAborted;

        var body = await ReadBodyAsync<RevokeLicenseRequest>(request, cancellationToken);
        if (body is null)
        {
            return ApiResults.ValidationFailure(EmptyBodyDetails, context.CorrelationId);
        }

        var validation = await _revokeValidator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiResults.ValidationFailure(ToDetails(validation), context.CorrelationId);
        }

        var result = await _revoke.HandleAsync(context, BearerToken(request), body, cancellationToken);

        return ApiResults.From(result, context.CorrelationId);
    }

    [Function("ListLicenses")]
    public async Task<IActionResult> ListAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/licenses")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();

        if (!Guid.TryParse(request.Query["organizationId"], out var organizationId))
        {
            return ApiResults.ValidationFailure(
                new Dictionary<string, string[]>
                {
                    ["organizationId"] = ["A valid organizationId query parameter is required."],
                },
                context.CorrelationId);
        }

        Guid? projectId = Guid.TryParse(request.Query["projectId"], out var parsed) ? parsed : null;

        var result = await _list.ListAsync(
            BearerToken(request), organizationId, projectId, request.HttpContext.RequestAborted);

        return ApiResults.From(result, context.CorrelationId);
    }

    [Function("GetLicense")]
    public async Task<IActionResult> GetAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/licenses/{licenseId:guid}")]
        HttpRequest request,
        Guid licenseId,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();

        var result = await _list.GetAsync(BearerToken(request), licenseId, request.HttpContext.RequestAborted);

        return ApiResults.From(result, context.CorrelationId);
    }

    private static string BearerToken(HttpRequest request) =>
        request.Headers.Authorization.ToString()["Bearer ".Length..].Trim();

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
