using System.Diagnostics;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace CheckMods.Services;

/// <summary>
/// Default <see cref="IBrowserLauncher"/>. Opens URLs via the OS shell.
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed class BrowserLauncher(ILogger<BrowserLauncher> logger) : IBrowserLauncher
{
    /// <inheritdoc />
    public bool TryOpenUrl(string url)
    {
        // Only passes http(s) URLs to the shell.
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
            // UseShellExecute lets the OS pick the default browser; the returned process may be null.
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
