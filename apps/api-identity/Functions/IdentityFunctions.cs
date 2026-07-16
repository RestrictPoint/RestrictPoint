using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using RestrictPoint.Api.Identity.Application.CreateOrganization;
using RestrictPoint.Api.Identity.Application.GetMe;
using RestrictPoint.Api.Identity.Application.InviteMember;
using RestrictPoint.Api.Identity.Application.ListOrganizations;
using RestrictPoint.Api.Identity.Contracts;
using RestrictPoint.Api.Identity.Functions.Http;
using RestrictPoint.Auth;

namespace RestrictPoint.Api.Identity.Functions;

/// <summary>
/// HTTP triggers for the Identity APIs (docs/16). Authentication is enforced by
/// <see cref="AuthenticationMiddleware"/> before any of these functions execute.
/// Authorization is enforced inside the application handlers.
/// </summary>
public sealed class IdentityFunctions
{
    private const string ActiveOrganizationHeader = "x-organization-id";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GetMeHandler _getMe;
    private readonly ListOrganizationsHandler _listOrganizations;
    private readonly CreateOrganizationHandler _createOrganization;
    private readonly InviteMemberHandler _inviteMember;
    private readonly IValidator<CreateOrganizationRequest> _createOrganizationValidator;
    private readonly IValidator<InviteMemberRequest> _inviteMemberValidator;

    public IdentityFunctions(
        GetMeHandler getMe,
        ListOrganizationsHandler listOrganizations,
        CreateOrganizationHandler createOrganization,
        InviteMemberHandler inviteMember,
        IValidator<CreateOrganizationRequest> createOrganizationValidator,
        IValidator<InviteMemberRequest> inviteMemberValidator)
    {
        _getMe = getMe;
        _listOrganizations = listOrganizations;
        _createOrganization = createOrganization;
        _inviteMember = inviteMember;
        _createOrganizationValidator = createOrganizationValidator;
        _inviteMemberValidator = inviteMemberValidator;
    }

    [Function("GetMe")]
    public async Task<IActionResult> GetMeAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/identity/me")] HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();
        var hint = ResolveOrganizationHint(request);

        var result = await _getMe.HandleAsync(context, hint, request.HttpContext.RequestAborted);

        return ApiResults.From(result, context.CorrelationId);
    }

    [Function("ListOrganizations")]
    public async Task<IActionResult> ListOrganizationsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/identity/organizations")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();

        var result = await _listOrganizations.HandleAsync(context, request.HttpContext.RequestAborted);

        return ApiResults.From(result, context.CorrelationId);
    }

    [Function("CreateOrganization")]
    public async Task<IActionResult> CreateOrganizationAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/identity/organizations")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();
        var cancellationToken = request.HttpContext.RequestAborted;

        var body = await ReadBodyAsync<CreateOrganizationRequest>(request, cancellationToken);
        if (body is null)
        {
            return ApiResults.ValidationFailure(EmptyBodyDetails, context.CorrelationId);
        }

        var validation = await _createOrganizationValidator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiResults.ValidationFailure(ToDetails(validation), context.CorrelationId);
        }

        var result = await _createOrganization.HandleAsync(context, body, cancellationToken);

        return ApiResults.From(result, context.CorrelationId, successStatus: 201);
    }

    [Function("InviteMember")]
    public async Task<IActionResult> InviteMemberAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/identity/organizations/{organizationId:guid}/invite")]
        HttpRequest request,
        Guid organizationId,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();
        var cancellationToken = request.HttpContext.RequestAborted;

        var body = await ReadBodyAsync<InviteMemberRequest>(request, cancellationToken);
        if (body is null)
        {
            return ApiResults.ValidationFailure(EmptyBodyDetails, context.CorrelationId);
        }

        var validation = await _inviteMemberValidator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiResults.ValidationFailure(ToDetails(validation), context.CorrelationId);
        }

        var result = await _inviteMember.HandleAsync(context, organizationId, body, cancellationToken);

        return ApiResults.From(result, context.CorrelationId, successStatus: 201);
    }

    private static readonly Dictionary<string, string[]> EmptyBodyDetails = new()
    {
        ["body"] = ["A JSON request body is required."],
    };

    private static Guid? ResolveOrganizationHint(HttpRequest request) =>
        Guid.TryParse(request.Headers[ActiveOrganizationHeader].ToString(), out var id) ? id : null;

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
