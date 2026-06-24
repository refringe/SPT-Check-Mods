namespace CheckMods.Services.Interfaces;

/// <summary>
/// Opens URLs in the user's default browser.
/// </summary>
public interface IBrowserLauncher
{
    /// <summary>
    /// Attempts to open <paramref name="url"/> in the default browser, returning false on any failure or a non-http(s) URL.
    /// </summary>
    bool TryOpenUrl(string url);
}
