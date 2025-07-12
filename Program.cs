using System.Reflection;
using CheckMods.Extensions;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace CheckMods;

/// <summary>
/// Main entry point for the CheckMods application. Configures dependency injection and runs the application service.
/// </summary>
public class Program
{
    /// <summary>
    /// Sets up dependency injection, runs the application, and handles any unhandled exceptions.
    /// </summary>
    /// <param name="args">Command line arguments. The first argument can be the SPT installation path.</param>
    public static async Task Main(string[] args)
    {
        try
        {
            // Setup dependency injection container
            var services = new ServiceCollection();
            services.AddCheckModsServices();
            await using var serviceProvider = services.BuildServiceProvider();

            // Run the main application logic
            var applicationService = serviceProvider.GetRequiredService<IApplicationService>();
            await applicationService.RunAsync(args);
        }
        catch (Exception ex)
        {
            // Display any unhandled exceptions with the formatted output
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
        }
        finally
        {
            // Wait for user input before closing
            AnsiConsole.WriteLine();

            // Display git hash
            var gitHash = GetGitHash();
            AnsiConsole.MarkupLine($"[grey]Build: {gitHash}[/]");

            AnsiConsole.MarkupLine("[grey]Press any key to exit...[/]");
            Console.ReadKey();
        }
    }

    /// <summary>
    /// Gets the git hash from the assembly metadata.
    /// </summary>
    /// <returns>The git hash or "unknown" if not found.</returns>
    private static string GetGitHash()
    {
        var assembly = Assembly.GetExecutingAssembly();

        var gitHashAttribute = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attr => attr.Key == "GitHash");

        return gitHashAttribute?.Value ?? "unknown";
    }
}
