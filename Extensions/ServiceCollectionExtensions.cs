using System.Reflection;
using CheckMods.Configuration;
using CheckMods.Logging;
using CheckMods.Services;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SPTarkov.DI;

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

        // Use SPTarkov.DI to auto-register all services with [Injectable] attribute
        var diHandler = new DependencyInjectionHandler(services);
        diHandler.AddInjectableTypesFromAssembly(Assembly.GetExecutingAssembly());
        diHandler.InjectAll();

        // Register ForgeApiService as HttpClient after SPTarkov.DI registration
        // AddHttpClient provides proper HttpClient lifecycle management
        services.AddHttpClient<IForgeApiService, ForgeApiService>();

        return services;
    }
}
