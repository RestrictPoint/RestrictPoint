using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Security.KeyVault.Keys;
using FluentValidation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RestrictPoint.Api.Licensing.Application.Abstractions;
using RestrictPoint.Api.Licensing.Application.Common;
using RestrictPoint.Api.Licensing.Application.IssueLicense;
using RestrictPoint.Api.Licensing.Application.ListLicenses;
using RestrictPoint.Api.Licensing.Application.RevokeLicense;
using RestrictPoint.Api.Licensing.Application.ValidateLicense;
using RestrictPoint.Api.Licensing.Contracts;
using RestrictPoint.Api.Licensing.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Database;
using RestrictPoint.Messaging;
using StackExchange.Redis;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Authentication: management endpoints require Entra bearer tokens. ValidateLicense is
// anonymous by design — the signed license token is the credential (docs/10).
builder.UseMiddleware<AuthenticationMiddleware>();

builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(new AuthenticationMiddlewareOptions
{
    AnonymousFunctions = ["HealthLive", "HealthReady", "ValidateLicense", "GetLicenseKeys"],
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
builder.Services.AddDbContext<LicensingDbContext>((provider, options) =>
    options.UseSqlServer(sqlConnectionString, sql => sql.EnableRetryOnFailure())
        .AddInterceptors(provider.GetRequiredService<AuditingSaveChangesInterceptor>()));

// --- Key Vault license signing (docs/10: private key never leaves the vault) -----------
var vaultUri = new Uri(builder.Configuration.GetRequiredValue("KeyVault:VaultUri"));
var signingKeyName = builder.Configuration.GetRequiredValue("KeyVault:SigningKeyName");

builder.Services.AddSingleton(_ => new KeyClient(vaultUri, credential));
builder.Services.AddSingleton<ILicenseSigner>(provider =>
{
    var keyClient = provider.GetRequiredService<KeyClient>();
    var key = keyClient.GetKey(signingKeyName);
    var keyVersion = key.Value.Properties.Version;

    return new KeyVaultLicenseSigner(
        keyClient.GetCryptographyClient(signingKeyName, keyVersion),
        keyVersion);
});
builder.Services.AddSingleton<ILicensePublicKeyProvider>(provider =>
    new KeyVaultLicensePublicKeyProvider(provider.GetRequiredService<KeyClient>(), signingKeyName));
builder.Services.AddSingleton<ILicenseKeySetProvider>(provider =>
    new KeyVaultLicenseKeySetProvider(
        provider.GetRequiredService<KeyClient>(), signingKeyName, provider.GetRequiredService<TimeProvider>()));
builder.Services.AddSingleton<LicenseTokenService>();

// --- Messaging (Managed Identity) -------------------------------------------------------
var serviceBusNamespace = builder.Configuration.GetRequiredValue("ServiceBus:FullyQualifiedNamespace");

builder.Services.AddSingleton(_ => new ServiceBusClient(serviceBusNamespace, credential));
builder.Services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();

// --- Redis cache (Managed Identity) -----------------------------------------------------
var redisHostName = builder.Configuration.GetRequiredValue("Redis:HostName");

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var configurationOptions = ConfigurationOptions.Parse($"{redisHostName}:6380");
    configurationOptions.Ssl = true;
    configurationOptions.AbortOnConnectFail = false;
    configurationOptions.ConfigureForAzureWithTokenCredentialAsync(credential)
        .GetAwaiter()
        .GetResult();

    return ConnectionMultiplexer.Connect(configurationOptions);
});
builder.Services.AddSingleton<ILicenseCache, RedisLicenseCache>();

// --- Identity service client (organization role authorization) --------------------------
builder.Services.AddHttpClient<IOrganizationRoleResolver, IdentityOrganizationRoleResolver>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration.GetRequiredValue("Identity:BaseUrl"));
    client.Timeout = TimeSpan.FromSeconds(10);
});

// --- Application ------------------------------------------------------------------------
builder.Services.AddScoped<IOutboxWriter>(provider =>
    new OutboxWriter(provider.GetRequiredService<LicensingDbContext>()));
builder.Services.AddSingleton<OutboxDispatcher>();
builder.Services.AddScoped<LicenseIssuanceService>();
builder.Services.AddScoped<RestrictPoint.Api.Licensing.Application.ConsumeBillingEvents.SubscriptionActivatedConsumer>();
builder.Services.AddScoped<ValidateLicenseHandler>();
builder.Services.AddScoped<IssueLicenseHandler>();
builder.Services.AddScoped<RevokeLicenseHandler>();
builder.Services.AddScoped<ListLicensesHandler>();
builder.Services.AddSingleton<IValidator<ValidateLicenseRequest>, ValidateLicenseRequestValidator>();
builder.Services.AddSingleton<IValidator<IssueLicenseRequest>, IssueLicenseRequestValidator>();
builder.Services.AddSingleton<IValidator<RevokeLicenseRequest>, RevokeLicenseRequestValidator>();

builder.Build().Run();

/// <summary>Configuration helpers for fail-fast startup.</summary>
internal static class ConfigurationExtensions
{
    /// <summary>Reads a required configuration value, failing at startup when absent.</summary>
    public static string GetRequiredValue(this IConfiguration configuration, string key) =>
        configuration[key] ?? throw new InvalidOperationException(
            $"Required configuration value '{key}' is missing.");
}
