using CheckMods.Configuration;
using CheckMods.Logging;
using CheckMods.Services;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CheckMods.Extensions;

/// <summary>
/// Extension methods for configuring dependency injection services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all CheckMods services with the dependency injection container.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The configured service collection.</returns>
    public static IServiceCollection AddCheckModsServices(this IServiceCollection services)
    {
        // Register configuration options with default values
        services.Configure<ForgeApiOptions>(_ => { });
        services.Configure<RateLimitOptions>(_ => { });
        services.Configure<ModScannerOptions>(_ => { });
        services.Configure<LoggingOptions>(_ => { });

        // Register logging infrastructure
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddFileLogger();

            // Suppress verbose HttpClient logging (we log full URLs ourselves)
            builder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        });

        // Register HttpClient with ForgeApiService (transient)
        services.AddHttpClient<IForgeApiService, ForgeApiService>();

        // Register infrastructure services
        services.AddSingleton<IRateLimitService, RateLimitService>();

        // Register mod processing services
        services.AddTransient<IModScannerService, ModScannerService>();
        services.AddTransient<IModReconciliationService, ModReconciliationService>();

        // Register API services
        services.AddTransient<IModMatchingService, ModMatchingService>();
        services.AddTransient<IModEnrichmentService, ModEnrichmentService>();
        services.AddTransient<IModDependencyService, ModDependencyService>();

        // Register application services
        services.AddTransient<IServerModService, ServerModService>();
        services.AddTransient<IApplicationService, ApplicationService>();

        return services;
    }
}
