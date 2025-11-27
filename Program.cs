using System.Reflection;
using CheckMods.Configuration;
using CheckMods.Extensions;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace CheckMods;

/// <summary>
/// Main entry point for the CheckMods application. Configures dependency injection and runs the application service.
/// </summary>
public class Program
{
    private static CancellationTokenSource? _cts;
    private static bool _wasCancelled;

    /// <summary>
    /// Sets up dependency injection, runs the application, and handles any unhandled exceptions.
    /// </summary>
    /// <param name="args">Command line arguments. The only argument is the SPT installation path.</param>
    public static async Task Main(string[] args)
    {
        ILogger<Program>? logger = null;

        _wasCancelled = false;
        _cts = new CancellationTokenSource();
        Console.CancelKeyPress += OnCancelKeyPress;

        try
        {
            // Set up dependency injection container
            var services = new ServiceCollection();
            services.AddCheckModsServices();
            await using var serviceProvider = services.BuildServiceProvider();

            // Get a logger for Program
            logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("CheckMods application starting. Args: {Args}", string.Join(", ", args));

            // Run the main application logic
            var applicationService = serviceProvider.GetRequiredService<IApplicationService>();
            await applicationService.RunAsync(args, _cts.Token);

            logger.LogInformation("CheckMods application completed successfully");
        }
        catch (OperationCanceledException)
        {
            logger?.LogInformation("Application was cancelled by user");
        }
        catch (Exception ex)
        {
            // Log the exception
            logger?.LogCritical(ex, "Unhandled exception occurred");

            // Display any unhandled exceptions with the formatted output
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
            _cts.Dispose();
            _cts = null;

            AnsiConsole.WriteLine();

            // Display git hash and log file location
            var gitHash = GetGitHash();
            AnsiConsole.MarkupLine($"[grey]Build: {gitHash}[/]");
            AnsiConsole.MarkupLine($"[grey]Log file: {LoggingOptions.CurrentLogFilePath}[/]");

            // Wait for user input (if not manually canceled)
            if (!_wasCancelled)
            {
                AnsiConsole.MarkupLine("[grey]Press any key to exit...[/]");
                Console.ReadKey();
            }
        }
    }

    /// <summary>
    /// Handles the Ctrl+C key press event to gracefully cancel the application.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The console cancel event arguments.</param>
    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true; // Prevent immediate termination
        _wasCancelled = true;
        _cts?.Cancel();
        AnsiConsole.MarkupLine("[yellow]Cancellation requested. Shutting down gracefully...[/]");
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
