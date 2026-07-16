using FluentValidation;
using RestrictPoint.Api.Billing.Application.Abstractions;
using RestrictPoint.Api.Billing.Contracts;
using RestrictPoint.Api.Billing.Domain;
using RestrictPoint.Auth;
using RestrictPoint.Common;

namespace RestrictPoint.Api.Billing.Application.ConnectOnboarding;

/// <summary>Input validation for POST /v1/billing/stripe/connect.</summary>
public sealed class ConnectOnboardingRequestValidator : AbstractValidator<ConnectOnboardingRequest>
{
    public ConnectOnboardingRequestValidator()
    {
        RuleFor(r => r.DeveloperOrganizationId).NotEmpty();
        RuleFor(r => r.ReturnUrl).NotEmpty().Must(BeHttps)
            .WithMessage("ReturnUrl must be an absolute https URL.");
        RuleFor(r => r.RefreshUrl).NotEmpty().Must(BeHttps)
            .WithMessage("RefreshUrl must be an absolute https URL.");

        static bool BeHttps(string? url) =>
            Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }
}

/// <summary>
/// POST /v1/billing/stripe/connect — starts Stripe Connect Express onboarding for a
/// developer organization. Requires Owner or Admin in that organization.
/// </summary>
public sealed class ConnectOnboardingHandler
{
    private static readonly string[] OnboardingRoles = ["Owner", "Admin"];

    private readonly IPaymentProvider _paymentProvider;
    private readonly IOrganizationRoleResolver _roleResolver;

    public ConnectOnboardingHandler(IPaymentProvider paymentProvider, IOrganizationRoleResolver roleResolver)
    {
        _paymentProvider = paymentProvider;
        _roleResolver = roleResolver;
    }

    public async Task<Result<ConnectOnboardingResponse>> HandleAsync(
        RequestContext context,
        string bearerToken,
        ConnectOnboardingRequest request,
        CancellationToken cancellationToken)
    {
        var role = await _roleResolver
            .GetCallerRoleAsync(bearerToken, request.DeveloperOrganizationId!.Value, cancellationToken)
            .ConfigureAwait(false);

        if (role.IsFailure)
        {
            return role.Error!;
        }

        if (role.Value is null || !OnboardingRoles.Contains(role.Value, StringComparer.OrdinalIgnoreCase))
        {
            return BillingErrors.NotAuthorizedForOrganization;
        }

        var link = await _paymentProvider.CreateConnectOnboardingLinkAsync(
            request.DeveloperOrganizationId!.Value,
            context.Email ?? string.Empty,
            request.ReturnUrl!,
            request.RefreshUrl!,
            cancellationToken).ConfigureAwait(false);

        if (link.IsFailure)
        {
            return link.Error!;
        }

        return new ConnectOnboardingResponse { OnboardingUrl = link.Value };
    }
}
