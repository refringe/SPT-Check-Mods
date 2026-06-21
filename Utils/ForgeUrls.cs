namespace CheckMods.Utils;

/// <summary>
/// Builds links to the Forge website (mod detail pages and downloads). These are website URLs, distinct from the Forge
/// API base URL configured in <see cref="Configuration.ForgeApiOptions"/>.
/// </summary>
public static class ForgeUrls
{
    private const string BaseUrl = "https://forge.sp-tarkov.com";

    /// <summary>
    /// Builds the URL of a mod's detail page on the Forge.
    /// </summary>
    /// <param name="modId">The Forge mod ID.</param>
    /// <param name="slug">The mod's URL slug.</param>
    public static string ModPage(int modId, string? slug)
    {
        return $"{BaseUrl}/mod/{modId}/{slug}";
    }

    /// <summary>
    /// Builds the URL to download a specific version of a mod from the Forge.
    /// </summary>
    /// <param name="modId">The Forge mod ID.</param>
    /// <param name="slug">The mod's URL slug.</param>
    /// <param name="version">The version to download.</param>
    public static string Download(int modId, string? slug, string? version)
    {
        return $"{BaseUrl}/mod/download/{modId}/{slug}/{version}";
    }
}
