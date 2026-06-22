namespace CheckMods.Configuration;

/// <summary>
/// Configuration for the ignored-updates feature: where the local list is stored and where the optional
/// author-maintained remote base list is fetched from.
/// </summary>
public class IgnoredUpdateOptions
{
    /// <summary>The full path to the local ignored-updates file. Overridable (e.g. by tests).</summary>
    public string FilePath { get; set; } = DefaultFilePath;

    /// <summary>
    /// URL of the author-maintained remote base list, or null/empty to disable the remote-fetch prompt entirely.
    /// Hosted on the Forge's static domain to keep it within the same trust boundary as the rest of the data.
    /// </summary>
    public string? RemoteUrl { get; set; } = "https://forge-static.sp-tarkov.com/check-mods/ignored-updates.json";

    /// <summary>Timeout for the remote fetch, in seconds.</summary>
    public int RemoteTimeoutSeconds { get; set; } = 10;

    /// <summary>The directory holding Check Mods' app data (shared with logs and other local state).</summary>
    public static string DefaultDirectory
    {
        get
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SptCheckMods");
        }
    }

    /// <summary>The default local file path: <c>%AppData%/SptCheckMods/ignored-updates.json</c>.</summary>
    public static string DefaultFilePath
    {
        get { return Path.Combine(DefaultDirectory, "ignored-updates.json"); }
    }
}
