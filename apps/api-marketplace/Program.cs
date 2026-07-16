using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RestrictPoint.Api.Marketplace.Infrastructure;
using RestrictPoint.Database;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        var config = context.Configuration;

        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Database with Managed Identity (following billing/licensing pattern)
        var connectionString = config["SqlConnection:ConnectionString"] ?? throw new InvalidOperationException("SqlConnection:ConnectionString is required");

        services.AddSingleton<AuditingSaveChangesInterceptor>();
        services.AddDbContext<MarketplaceDbContext>((provider, options) =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure())
                .AddInterceptors(provider.GetRequiredService<AuditingSaveChangesInterceptor>()));

        services.AddLogging(logging =>
        {
            logging.AddConsole();
            logging.SetMinimumLevel(LogLevel.Information);
        });
    })
    .Build();

await host.RunAsync();

