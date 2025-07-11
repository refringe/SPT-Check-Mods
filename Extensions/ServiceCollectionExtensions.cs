using CheckMods.Services;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

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
        // Register caching
        services.AddMemoryCache();
        
        // Register HttpClient
        services.AddHttpClient<IForgeApiService, ForgeApiService>();
        
        // Register services
        services.AddSingleton<IRateLimitService, RateLimitService>();
        services.AddSingleton<ModMatchingService>();
        services.AddSingleton<BepInExScannerService>();
        services.AddScoped<IForgeApiService, ForgeApiService>();
        services.AddScoped<IModService, ModService>();
        services.AddScoped<IClientModService, ClientModService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        
        return services;
    }
}