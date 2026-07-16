using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using RestrictPoint.Api.Billing.Application.Abstractions;
using RestrictPoint.Api.Billing.Application.ConnectOnboarding;
using RestrictPoint.Api.Billing.Application.CreateCheckout;
using RestrictPoint.Api.Billing.Application.ManageSubscriptions;
using RestrictPoint.Api.Billing.Application.ProcessWebhook;
using RestrictPoint.Api.Billing.Contracts;
using RestrictPoint.Auth;
using RestrictPoint.Auth.Http;

namespace RestrictPoint.Api.Billing.Functions;

/// <summary>
/// HTTP triggers for the Billing APIs (docs/16). The Stripe webhook is anonymous by design:
/// its credential is the Stripe signature, verified before any processing.
/// </summary>
public sealed class BillingFunctions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Dictionary<string, string[]> EmptyBodyDetails = new()
    {
        ["body"] = ["A JSON request body is required."],
    };

    private readonly CreateCheckoutHandler _checkout;
    private readonly ProcessWebhookHandler _webhook;
    private readonly ManageSubscriptionsHandler _subscriptions;
    private readonly ConnectOnboardingHandler _connect;
    private readonly IWebhookVerifier _webhookVerifier;
    private readonly IValidator<CreateCheckoutRequest> _checkoutValidator;
    private readonly IValidator<CancelSubscriptionRequest> _cancelValidator;
    private readonly IValidator<ConnectOnboardingRequest> _connectValidator;

    public BillingFunctions(
        CreateCheckoutHandler checkout,
        ProcessWebhookHandler webhook,
        ManageSubscriptionsHandler subscriptions,
        ConnectOnboardingHandler connect,
        IWebhookVerifier webhookVerifier,
        IValidator<CreateCheckoutRequest> checkoutValidator,
        IValidator<CancelSubscriptionRequest> cancelValidator,
        IValidator<ConnectOnboardingRequest> connectValidator)
    {
        _checkout = checkout;
        _webhook = webhook;
        _subscriptions = subscriptions;
        _connect = connect;
        _webhookVerifier = webhookVerifier;
        _checkoutValidator = checkoutValidator;
        _cancelValidator = cancelValidator;
        _connectValidator = connectValidator;
    }

    [Function("CreateCheckout")]
    public async Task<IActionResult> CreateCheckoutAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/billing/checkout")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();
        var cancellationToken = request.HttpContext.RequestAborted;

        var body = await ReadBodyAsync<CreateCheckoutRequest>(request, cancellationToken);
        if (body is null)
        {
            return ApiResults.ValidationFailure(EmptyBodyDetails, context.CorrelationId);
        }

        var validation = await _checkoutValidator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiResults.ValidationFailure(ToDetails(validation), context.CorrelationId);
        }

        var result = await _checkout.HandleAsync(context, body, cancellationToken);

        return ApiResults.From(result, context.CorrelationId, successStatus: 201);
    }

    [Function("StripeWebhook")]
    public async Task<IActionResult> StripeWebhookAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/billing/webhook")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();
        var cancellationToken = request.HttpContext.RequestAborted;

        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        var signature = request.Headers["Stripe-Signature"].ToString();

        var verification = _webhookVerifier.VerifyAndParse(payload, signature);
        if (verification.IsFailure)
        {
            return ApiResults.Failure(verification.Error!, context.CorrelationId);
        }

        var result = await _webhook.HandleAsync(context, verification.Value, cancellationToken);

        // Stripe expects a 2xx acknowledgment; failures cause redelivery.
        return ApiResults.From(result, context.CorrelationId);
    }

    [Function("CancelSubscription")]
    public async Task<IActionResult> CancelSubscriptionAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/billing/subscriptions/cancel")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();
        var cancellationToken = request.HttpContext.RequestAborted;

        var body = await ReadBodyAsync<CancelSubscriptionRequest>(request, cancellationToken);
        if (body is null)
        {
            return ApiResults.ValidationFailure(EmptyBodyDetails, context.CorrelationId);
        }

        var validation = await _cancelValidator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiResults.ValidationFailure(ToDetails(validation), context.CorrelationId);
        }

        var result = await _subscriptions.CancelAsync(context, BearerToken(request), body, cancellationToken);

        return ApiResults.From(result, context.CorrelationId);
    }

    [Function("ListSubscriptions")]
    public async Task<IActionResult> ListSubscriptionsAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/billing/subscriptions")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();

        if (!Guid.TryParse(request.Query["organizationId"], out var organizationId))
        {
            return ApiResults.ValidationFailure(OrganizationIdRequired, context.CorrelationId);
        }

        var result = await _subscriptions.ListAsync(
            BearerToken(request), organizationId, request.HttpContext.RequestAborted);

        return ApiResults.From(result, context.CorrelationId);
    }

    [Function("ListInvoices")]
    public async Task<IActionResult> ListInvoicesAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/billing/invoices")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();

        if (!Guid.TryParse(request.Query["organizationId"], out var organizationId))
        {
            return ApiResults.ValidationFailure(OrganizationIdRequired, context.CorrelationId);
        }

        var result = await _subscriptions.ListInvoicesAsync(
            BearerToken(request), organizationId, request.HttpContext.RequestAborted);

        return ApiResults.From(result, context.CorrelationId);
    }

    [Function("ConnectOnboarding")]
    public async Task<IActionResult> ConnectOnboardingAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/billing/stripe/connect")]
        HttpRequest request,
        FunctionContext functionContext)
    {
        var context = functionContext.GetRequestContext();
        var cancellationToken = request.HttpContext.RequestAborted;

        var body = await ReadBodyAsync<ConnectOnboardingRequest>(request, cancellationToken);
        if (body is null)
        {
            return ApiResults.ValidationFailure(EmptyBodyDetails, context.CorrelationId);
        }

        var validation = await _connectValidator.ValidateAsync(body, cancellationToken);
        if (!validation.IsValid)
        {
            return ApiResults.ValidationFailure(ToDetails(validation), context.CorrelationId);
        }

        var result = await _connect.HandleAsync(context, BearerToken(request), body, cancellationToken);

        return ApiResults.From(result, context.CorrelationId, successStatus: 201);
    }

    private static readonly Dictionary<string, string[]> OrganizationIdRequired = new()
    {
        ["organizationId"] = ["A valid organizationId query parameter is required."],
    };

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
