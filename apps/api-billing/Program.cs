using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Secrets;
using FluentValidation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RestrictPoint.Api.Billing.Application.Abstractions;
using RestrictPoint.Api.Billing.Application.ConnectOnboarding;
using RestrictPoint.Api.Billing.Application.CreateCheckout;
using RestrictPoint.Api.Billing.Application.ManageSubscriptions;
using RestrictPoint.Api.Billing.Application.ProcessWebhook;
using RestrictPoint.Api.Billing.Contracts;
using RestrictPoint.Api.Billing.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Database;
using RestrictPoint.Messaging;
using Stripe;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Authentication: management endpoints require Entra bearer tokens. The Stripe webhook is
// anonymous by design — its credential is the Stripe signature (verified before handling).
builder.UseMiddleware<AuthenticationMiddleware>();

builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new AuthenticationMiddlewareOptions
{
    AnonymousFunctions = ["HealthLive", "HealthReady", "StripeWebhook"],
});

var credential = new DefaultAzureCredential();

// --- Authentication -------------------------------------------------------------------
var entraOptions = new EntraAuthenticationOptions
{
    TenantSubdomain = builder.Configuration.GetRequiredValue("EntraExternalId:TenantSubdomain"),
    TenantId = builder.Configuration.GetRequiredValue("EntraExternalId:TenantId"),
    Audiences = builder.Configuration.GetRequiredValue("EntraExternalId:Audience").Split(';'),
};

builder.Services.AddSingleton(entraOptions);
builder.Services.AddSingleton<IJwtValidator>(_ => EntraJwtValidator.Create(entraOptions));

// --- Database (Managed Identity) --------------------------------------------------------
var sqlConnectionString = builder.Configuration.GetRequiredValue("Sql:ConnectionString");

builder.Services.AddSingleton<AuditingSaveChangesInterceptor>();
builder.Services.AddDbContext<BillingDbContext>((provider, options) =>
    options.UseSqlServer(sqlConnectionString, sql => sql.EnableRetryOnFailure())
        .AddInterceptors(provider.GetRequiredService<AuditingSaveChangesInterceptor>()));

// --- Stripe (secrets fetched from Key Vault at startup via Managed Identity) -----------
var vaultUri = new Uri(builder.Configuration.GetRequiredValue("KeyVault:VaultUri"));
var secretClient = new SecretClient(vaultUri, credential);

var stripeOptions = new StripeOptions
{
    ApiKey = secretClient.GetSecret("stripe-api-key").Value.Value,
    WebhookSecret = secretClient.GetSecret("stripe-webhook-secret").Value.Value,
};

builder.Services.AddSingleton(stripeOptions);
builder.Services.AddSingleton(new StripeClient(stripeOptions.ApiKey));
builder.Services.AddSingleton<IPaymentProvider, StripePaymentProvider>();
builder.Services.AddSingleton<IWebhookVerifier, StripeWebhookVerifier>();

// --- Messaging (Managed Identity) -------------------------------------------------------
var serviceBusNamespace = builder.Configuration.GetRequiredValue("ServiceBus:FullyQualifiedNamespace");

builder.Services.AddSingleton(_ => new ServiceBusClient(serviceBusNamespace, credential));
builder.Services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();

// --- Identity service client (organization role authorization) --------------------------
builder.Services.AddHttpClient<IOrganizationRoleResolver, IdentityOrganizationRoleResolver>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetRequiredValue("Identity:BaseUrl"));
    client.Timeout = TimeSpan.FromSeconds(10);
});

// --- Application ------------------------------------------------------------------------
builder.Services.AddSingleton(new BillingOptions
{
    PlatformFeePercent = decimal.Parse(
        builder.Configuration["Billing:PlatformFeePercent"] ?? "10",
        System.Globalization.CultureInfo.InvariantCulture),
});

builder.Services.AddScoped<IOutboxWriter>(provider =>
    new OutboxWriter(provider.GetRequiredService<BillingDbContext>()));
builder.Services.AddSingleton<OutboxDispatcher>();
builder.Services.AddScoped<CreateCheckoutHandler>();
builder.Services.AddScoped<ProcessWebhookHandler>();
builder.Services.AddScoped<ManageSubscriptionsHandler>();
builder.Services.AddScoped<ConnectOnboardingHandler>();
builder.Services.AddSingleton<IValidator<CreateCheckoutRequest>, CreateCheckoutRequestValidator>();
builder.Services.AddSingleton<IValidator<CancelSubscriptionRequest>, CancelSubscriptionRequestValidator>();
builder.Services.AddSingleton<IValidator<ConnectOnboardingRequest>, ConnectOnboardingRequestValidator>();

builder.Build().Run();

/// <summary>Configuration helpers for fail-fast startup.</summary>
internal static class ConfigurationExtensions
{
    /// <summary>Reads a required configuration value, failing at startup when absent.</summary>
    public static string GetRequiredValue(this IConfiguration configuration, string key) =>
        configuration[key] ?? throw new InvalidOperationException(
            $"Required configuration value '{key}' is missing.");
}
