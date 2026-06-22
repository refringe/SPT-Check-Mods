namespace CheckMods.Configuration;

/// <summary>
/// Configuration options for the Check Mods self-update check.
/// </summary>
public class UpdateCheckOptions
{
    /// <summary>
    /// The Forge mod ID for Check Mods itself, used to look up the latest available version.
    /// </summary>
    public int ForgeModId { get; set; } = 2471;
}
