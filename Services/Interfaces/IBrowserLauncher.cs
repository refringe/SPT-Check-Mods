namespace CheckMods.Services.Interfaces;

/// <summary>
/// Opens URLs in the user's default browser.
/// </summary>
public interface IBrowserLauncher
{
    /// <summary>
    /// Attempts to open <paramref name="url"/> in the default browser. Returns false on any failure (or a non-http(s)
    /// URL), so callers can fall back to printing the link.
    /// </summary>
    bool TryOpenUrl(string url);
}
