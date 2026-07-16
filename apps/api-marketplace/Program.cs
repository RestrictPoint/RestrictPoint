using Azure.Identity;
using Azure.Messaging.ServiceBus;
using FluentValidation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RestrictPoint.Api.Marketplace.Application.BrowseListings;
using RestrictPoint.Api.Marketplace.Application.CreateListing;
using RestrictPoint.Api.Marketplace.Application.ManagePricing;
using RestrictPoint.Api.Marketplace.Application.PublishListing;
using RestrictPoint.Api.Marketplace.Application.SubmitReview;
using RestrictPoint.Api.Marketplace.Contracts;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Database;
using RestrictPoint.Messaging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Authentication: writes require Entra bearer tokens. Catalog reads (list, get, search)
// are anonymous by design — the marketplace is the public commercial surface (docs/13).
builder.UseMiddleware<AuthenticationMiddleware>();

builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new AuthenticationMiddlewareOptions
{
    AnonymousFunctions = ["HealthLive", "HealthReady", "ListListings", "GetListing", "SearchListings"],
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
builder.Services.AddDbContext<MarketplaceDbContext>((provider, options) =>
    options.UseSqlServer(sqlConnectionString, sql => sql.EnableRetryOnFailure())
        .AddInterceptors(provider.GetRequiredService<AuditingSaveChangesInterceptor>()));

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
builder.Services.AddScoped<IOutboxWriter>(provider =>
    new OutboxWriter(provider.GetRequiredService<MarketplaceDbContext>()));
builder.Services.AddSingleton<OutboxDispatcher>();
builder.Services.AddScoped<CreateListingHandler>();
builder.Services.AddScoped<PublishListingHandler>();
builder.Services.AddScoped<AddPricingPlanHandler>();
builder.Services.AddScoped<SubmitReviewHandler>();
builder.Services.AddScoped<BrowseListingsHandler>();
builder.Services.AddSingleton<IValidator<CreateListingRequest>, CreateListingRequestValidator>();
builder.Services.AddSingleton<IValidator<AddPricingPlanRequest>, AddPricingPlanRequestValidator>();
builder.Services.AddSingleton<IValidator<SubmitReviewRequest>, SubmitReviewRequestValidator>();

builder.Build().Run();

/// <summary>Configuration helpers for fail-fast startup.</summary>
internal static class ConfigurationExtensions
{
    /// <summary>Reads a required configuration value, failing at startup when absent.</summary>
    public static string GetRequiredValue(this IConfiguration configuration, string key) =>
        configuration[key] ?? throw new InvalidOperationException(
            $"Required configuration value '{key}' is missing.");
}
