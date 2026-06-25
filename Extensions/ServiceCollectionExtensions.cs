using System.Net.Http.Headers;
using System.Reflection;
using CheckMods.Configuration;
using CheckMods.Logging;
using CheckMods.Services;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    public static IServiceCollection AddCheckModsServices(this IServiceCollection services)
    {
        services.Configure<ForgeApiOptions>(_ => { });
        services.Configure<RateLimitOptions>(_ => { });
        services.Configure<ModScannerOptions>(_ => { });
        services.Configure<LoggingOptions>(_ => { });
        services.Configure<UpdateCheckOptions>(_ => { });
        services.Configure<IgnoredUpdateOptions>(_ => { });

        services.AddMemoryCache();

        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddFileLogger();

            // Suppress verbose HttpClient logging.
            builder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        });

        var diHandler = new DependencyInjectionHandler(services);
        diHandler.AddInjectableTypesFromAssembly(Assembly.GetExecutingAssembly());
        diHandler.InjectAll();

        // Register ForgeApiService as a typed HttpClient.
        services.AddHttpClient<IForgeApiService, ForgeApiService>(
            (serviceProvider, client) =>
            {
                var rateLimitOptions = serviceProvider.GetRequiredService<IOptions<RateLimitOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(rateLimitOptions.RequestTimeoutSeconds);

                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SPT-Check-Mods", version));
                client.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("(+https://github.com/refringe/SPT-Check-Mods)")
                );
            }
        );

        // Register the remote ignore-list client as a typed HttpClient.
        services.AddHttpClient<IRemoteIgnoreFileClient, RemoteIgnoreFileClient>(
            (serviceProvider, client) =>
            {
                var ignoredOptions = serviceProvider.GetRequiredService<IOptions<IgnoredUpdateOptions>>().Value;
                client.Timeout = TimeSpan.FromSeconds(ignoredOptions.RemoteTimeoutSeconds);

                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SPT-Check-Mods", version));
                client.DefaultRequestHeaders.UserAgent.Add(
                    new ProductInfoHeaderValue("(+https://github.com/refringe/SPT-Check-Mods)")
                );
            }
        );

        return services;
    }
}
