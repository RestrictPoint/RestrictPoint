using Azure.Identity;
using Azure.Messaging.ServiceBus;
using FluentValidation;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RestrictPoint.Api.Identity.Application.Abstractions;
using RestrictPoint.Api.Identity.Application.Common;
using RestrictPoint.Api.Identity.Application.CreateOrganization;
using RestrictPoint.Api.Identity.Application.GetMe;
using RestrictPoint.Api.Identity.Application.InviteMember;
using RestrictPoint.Api.Identity.Application.ListOrganizations;
using RestrictPoint.Api.Identity.Contracts;
using RestrictPoint.Api.Identity.Infrastructure;
using RestrictPoint.Auth;
using RestrictPoint.Database;
using RestrictPoint.Messaging;
using StackExchange.Redis;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Authentication: every HTTP request is validated against Entra External ID.
builder.UseMiddleware<AuthenticationMiddleware>();

builder.Services.AddApplicationInsightsTelemetryWorkerService();
builder.Services.ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton(TimeProvider.System);

// --- Authentication -------------------------------------------------------------------
var entraOptions = new EntraAuthenticationOptions
{
    TenantSubdomain = builder.Configuration.GetRequiredValue("EntraExternalId:TenantSubdomain"),
    TenantId = builder.Configuration.GetRequiredValue("EntraExternalId:TenantId"),
    Audiences = builder.Configuration.GetRequiredValue("EntraExternalId:Audience").Split(';'),
};

builder.Services.AddSingleton(entraOptions);
builder.Services.AddSingleton<IJwtValidator>(_ => EntraJwtValidator.Create(entraOptions));

// --- Database (Managed Identity via Authentication=Active Directory Default) -----------
var sqlConnectionString = builder.Configuration.GetRequiredValue("Sql:ConnectionString");

builder.Services.AddSingleton<AuditingSaveChangesInterceptor>();
builder.Services.AddDbContext<IdentityDbContext>((provider, options) =>
    options.UseSqlServer(sqlConnectionString, sql => sql.EnableRetryOnFailure())
        .AddInterceptors(provider.GetRequiredService<AuditingSaveChangesInterceptor>()));

// --- Messaging (Managed Identity) -------------------------------------------------------
var serviceBusNamespace = builder.Configuration.GetRequiredValue("ServiceBus:FullyQualifiedNamespace");

builder.Services.AddSingleton(_ =>
    new ServiceBusClient(serviceBusNamespace, new DefaultAzureCredential()));
builder.Services.AddSingleton<IEventPublisher, ServiceBusEventPublisher>();

// --- Redis cache (Managed Identity; optional in local development) ---------------------
var redisHostName = builder.Configuration["Redis:HostName"];
if (!string.IsNullOrWhiteSpace(redisHostName))
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var configurationOptions = ConfigurationOptions.Parse($"{redisHostName}:6380");
        configurationOptions.Ssl = true;
        configurationOptions.AbortOnConnectFail = false; // Reconnect in the background.
        configurationOptions.ConfigureForAzureWithTokenCredentialAsync(
                new DefaultAzureCredential())
            .GetAwaiter()
            .GetResult();

        return ConnectionMultiplexer.Connect(configurationOptions);
    });
    builder.Services.AddSingleton<IUserContextCache, RedisUserContextCache>();
}
else
{
    builder.Services.AddSingleton<IUserContextCache, NullUserContextCache>();
}

// --- Application ------------------------------------------------------------------------
builder.Services.AddSingleton(new AuthenticationMiddlewareOptions());
builder.Services.AddScoped<IOutboxWriter>(provider =>
    new OutboxWriter(provider.GetRequiredService<IdentityDbContext>()));
builder.Services.AddSingleton<OutboxDispatcher>();
builder.Services.AddScoped<UserResolver>();
builder.Services.AddScoped<GetMeHandler>();
builder.Services.AddScoped<ListOrganizationsHandler>();
builder.Services.AddScoped<CreateOrganizationHandler>();
builder.Services.AddScoped<InviteMemberHandler>();
builder.Services.AddSingleton<IValidator<CreateOrganizationRequest>, CreateOrganizationRequestValidator>();
builder.Services.AddSingleton<IValidator<InviteMemberRequest>, InviteMemberRequestValidator>();

builder.Build().Run();

/// <summary>Configuration helpers for fail-fast startup.</summary>
internal static class ConfigurationExtensions
{
    /// <summary>Reads a required configuration value, failing at startup when absent.</summary>
    public static string GetRequiredValue(this IConfiguration configuration, string key) =>
        configuration[key] ?? throw new InvalidOperationException(
            $"Required configuration value '{key}' is missing.");
}
