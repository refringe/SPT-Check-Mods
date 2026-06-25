using CheckMods.Configuration;
using CheckMods.Extensions;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using CheckMods.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace CheckMods;

/// <summary>
/// Main entry point for the CheckMods application.
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

        // Path reported at the end of the run; falls back to the static default if DI setup fails.
        var logFilePath = LoggingOptions.CurrentLogFilePath;

        _wasCancelled = false;
        _cts = new CancellationTokenSource();
        Console.CancelKeyPress += OnCancelKeyPress;

        ServiceProvider? serviceProvider = null;
        IIgnoredUpdateWorkflow? ignoredUpdateWorkflow = null;
        IReadOnlyList<Mod>? mods = null;

        try
        {
            var services = new ServiceCollection();
            services.AddCheckModsServices();
            serviceProvider = services.BuildServiceProvider();

            logger = serviceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogInformation("CheckMods application starting. Args: {Args}", string.Join(", ", args));

            logFilePath = serviceProvider.GetRequiredService<IOptions<LoggingOptions>>().Value.LogFilePath;

            var applicationService = serviceProvider.GetRequiredService<IApplicationService>();
            ignoredUpdateWorkflow = serviceProvider.GetRequiredService<IIgnoredUpdateWorkflow>();

            mods = await applicationService.RunAsync(args, _cts.Token);

            logger.LogInformation("CheckMods application completed successfully");
        }
        catch (OperationCanceledException)
        {
            logger?.LogInformation("Application was cancelled by user");
        }
        catch (Exception ex)
        {
            logger?.LogCritical(ex, "Unhandled exception occurred");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
        }
        finally
        {
            Console.CancelKeyPress -= OnCancelKeyPress;
            _cts.Dispose();
            _cts = null;

            AnsiConsole.WriteLine();

            AnsiConsole.MarkupLine($"[grey]Check Mods v{VersionInfo.SemVer} (build {VersionInfo.GitHash})[/]");
            AnsiConsole.MarkupLine($"[grey]Log file: {logFilePath}[/]");

            if (!_wasCancelled && !Console.IsInputRedirected)
            {
                if (ignoredUpdateWorkflow is not null)
                {
                    await ignoredUpdateWorkflow.RunAsync(mods);
                }
                else
                {
                    // Fallback when DI setup failed before the workflow was resolved.
                    while (Console.KeyAvailable)
                    {
                        Console.ReadKey(intercept: true);
                    }

                    AnsiConsole.MarkupLine("[grey]Press any key to exit...[/]");
                    Console.ReadKey();
                }
            }

            if (serviceProvider is not null)
            {
                await serviceProvider.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// Handles the Ctrl+C event to cancel the application.
    /// </summary>
    /// <param name="sender">The event sender.</param>
    /// <param name="e">The console cancel event arguments.</param>
    private static void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        e.Cancel = true;
        _wasCancelled = true;
        _cts?.Cancel();
        AnsiConsole.MarkupLine("[yellow]Cancellation requested. Shutting down gracefully...[/]");
    }
}
