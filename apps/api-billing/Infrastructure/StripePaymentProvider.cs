using Microsoft.Extensions.Logging;
using RestrictPoint.Api.Billing.Application.Abstractions;
using RestrictPoint.Api.Billing.Domain;
using RestrictPoint.Common;
using Stripe;
using Stripe.Checkout;

namespace RestrictPoint.Api.Billing.Infrastructure;

/// <summary>Stripe configuration. The API key and webhook secret come from Key Vault.</summary>
public sealed record StripeOptions
{
    public required string ApiKey { get; init; }

    public required string WebhookSecret { get; init; }
}

/// <summary>
/// Stripe Connect payment provider (docs/12). Stripe is the merchant of record; revenue
/// split is delegated to Stripe via application fees on the connected account.
/// </summary>
public sealed partial class StripePaymentProvider : IPaymentProvider
{
    private readonly StripeClient _client;
    private readonly ILogger<StripePaymentProvider> _logger;

    public StripePaymentProvider(StripeClient client, ILogger<StripePaymentProvider> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<Result<string>> CreateCheckoutSessionAsync(
        CheckoutSessionRequest request,
        CancellationToken cancellationToken)
    {
        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = request.SuccessUrl,
            CancelUrl = request.CancelUrl,
            CustomerEmail = string.IsNullOrWhiteSpace(request.CustomerEmail) ? null : request.CustomerEmail,
            LineItems =
            [
                new SessionLineItemOptions { Price = request.PlanPriceId, Quantity = 1 },
            ],
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                // Correlates webhook events back to our subscription before Stripe ids bind.
                Metadata = new Dictionary<string, string>
                {
                    ["subscriptionId"] = request.SubscriptionId.ToString(),
                },
                ApplicationFeePercent = request.ConnectedAccountId is null
                    ? null
                    : request.PlatformFeePercent,
            },
        };

        var requestOptions = request.ConnectedAccountId is null
            ? null
            : new RequestOptions { StripeAccount = request.ConnectedAccountId };

        try
        {
            var session = await new SessionService(_client)
                .CreateAsync(options, requestOptions, cancellationToken)
                .ConfigureAwait(false);

            return Result.Success(session.Url);
        }
        catch (StripeException exception)
        {
            LogStripeError(_logger, exception, "checkout session creation");
            return BillingErrors.PaymentProviderUnavailable;
        }
    }

    public async Task<Result<Result.Unit>> CancelSubscriptionAsync(
        string providerSubscriptionId,
        bool cancelAtPeriodEnd,
        CancellationToken cancellationToken)
    {
        try
        {
            var service = new SubscriptionService(_client);

            if (cancelAtPeriodEnd)
            {
                await service.UpdateAsync(
                    providerSubscriptionId,
                    new SubscriptionUpdateOptions { CancelAtPeriodEnd = true },
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await service.CancelAsync(
                    providerSubscriptionId,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            return Result.Success();
        }
        catch (StripeException exception)
        {
            LogStripeError(_logger, exception, "subscription cancellation");
            return BillingErrors.PaymentProviderUnavailable;
        }
    }

    public async Task<Result<string>> CreateConnectOnboardingLinkAsync(
        Guid developerOrganizationId,
        string email,
        string returnUrl,
        string refreshUrl,
        CancellationToken cancellationToken)
    {
        try
        {
            var account = await new AccountService(_client).CreateAsync(
                new AccountCreateOptions
                {
                    Type = "express",
                    Email = string.IsNullOrWhiteSpace(email) ? null : email,
                    Metadata = new Dictionary<string, string>
                    {
                        ["developerOrganizationId"] = developerOrganizationId.ToString(),
                    },
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var link = await new AccountLinkService(_client).CreateAsync(
                new AccountLinkCreateOptions
                {
                    Account = account.Id,
                    ReturnUrl = returnUrl,
                    RefreshUrl = refreshUrl,
                    Type = "account_onboarding",
                },
                cancellationToken: cancellationToken).ConfigureAwait(false);

            return Result.Success(link.Url);
        }
        catch (StripeException exception)
        {
            LogStripeError(_logger, exception, "Connect onboarding");
            return BillingErrors.PaymentProviderUnavailable;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Stripe API error during {Operation}.")]
    private static partial void LogStripeError(ILogger logger, Exception exception, string operation);
}
