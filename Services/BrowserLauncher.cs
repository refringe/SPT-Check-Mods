using System.Diagnostics;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace CheckMods.Services;

/// <summary>
/// Default <see cref="IBrowserLauncher"/>. Uses the OS shell to open URLs, which resolves the user's default browser
/// across Windows, macOS, and Linux.
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed class BrowserLauncher(ILogger<BrowserLauncher> logger) : IBrowserLauncher
{
    /// <inheritdoc />
    public bool TryOpenUrl(string url)
    {
        // Only ever hand http(s) URLs to the shell; refuse anything else as a defensive measure.
        if (
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            && !url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
        )
        {
            logger.LogWarning("Refusing to open non-http(s) URL");
            return false;
        }

        try
        {
            // UseShellExecute lets the OS pick the default browser. The returned process may be null when the URL is
            // handed to an already-running browser, so treat "no exception" as success.
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not open the browser");
            return false;
        }
    }
}
