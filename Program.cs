using CheckMods.Configuration;
using CheckMods.Extensions;
using CheckMods.Services.Interfaces;
using CheckMods.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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

        // Path reported to the user at the end of the run. Resolved from the configured LoggingOptions once DI is up
        // so it tracks where the logger actually writes; falls back to the default if setup fails before then.
        var logFilePath = LoggingOptions.CurrentLogFilePath;

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

            // Capture the active log path from the same options the logger uses, so the closing banner points at the
            // real file rather than the static default.
            logFilePath = serviceProvider.GetRequiredService<IOptions<LoggingOptions>>().Value.LogFilePath;

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

            // Display version, build hash, and log file location
            AnsiConsole.MarkupLine($"[grey]Check Mods v{VersionInfo.SemVer} (build {VersionInfo.GitHash})[/]");
            AnsiConsole.MarkupLine($"[grey]Log file: {logFilePath}[/]");

            // Wait for user input (if not manually cancelled and the console is interactive). When input is
            // redirected (CI, piping) there is no interactive console, and Console.KeyAvailable/ReadKey would throw.
            if (!_wasCancelled && !Console.IsInputRedirected)
            {
                // Drain any keystrokes buffered during the run so ReadKey actually waits.
                while (Console.KeyAvailable)
                {
                    Console.ReadKey(intercept: true);
                }

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
}
