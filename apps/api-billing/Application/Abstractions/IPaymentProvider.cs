using RestrictPoint.Common;

namespace RestrictPoint.Api.Billing.Application.Abstractions;

/// <summary>Inputs for creating a hosted checkout session.</summary>
public sealed record CheckoutSessionRequest
{
    public required Guid SubscriptionId { get; init; }

    public required string PlanPriceId { get; init; }

    public required string CustomerEmail { get; init; }

    public required string SuccessUrl { get; init; }

    public required string CancelUrl { get; init; }

    /// <summary>Platform fee percentage applied to the developer's connected account (docs/12).</summary>
    public required decimal PlatformFeePercent { get; init; }

    /// <summary>The developer's Stripe Connect account receiving the split.</summary>
    public string? ConnectedAccountId { get; init; }
}

/// <summary>
/// Payment provider abstraction (docs/12: billing is abstracted via IPaymentProvider).
/// The production implementation is Stripe Connect; handlers never reference Stripe types.
/// </summary>
public interface IPaymentProvider
{
    /// <summary>Creates a hosted checkout session and returns its URL.</summary>
    Task<Result<string>> CreateCheckoutSessionAsync(
        CheckoutSessionRequest request,
        CancellationToken cancellationToken);

    /// <summary>Requests cancellation of the provider-side subscription.</summary>
    Task<Result<Result.Unit>> CancelSubscriptionAsync(
        string providerSubscriptionId,
        bool cancelAtPeriodEnd,
        CancellationToken cancellationToken);

    /// <summary>Creates a Stripe Connect Express onboarding link for a developer organization.</summary>
    Task<Result<string>> CreateConnectOnboardingLinkAsync(
        Guid developerOrganizationId,
        string email,
        string returnUrl,
        string refreshUrl,
        CancellationToken cancellationToken);
}
