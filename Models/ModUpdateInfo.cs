namespace CheckMods.Models;

/// <summary>
/// Represents information about a mod name/author update mapping. Used to track when mods have been renamed or
/// transferred between authors.
/// </summary>
public class ModUpdateInfo
{
    /// <summary>
    /// The original name of the mod before the update.
    /// </summary>
    public string FromName { get; set; } = string.Empty;

    /// <summary>
    /// The original author of the mod before the update.
    /// </summary>
    public string FromAuthor { get; set; } = string.Empty;

    /// <summary>
    /// The new name of the mod after the update.
    /// </summary>
    public string ToName { get; set; } = string.Empty;

    /// <summary>
    /// The new author of the mod after the update.
    /// </summary>
    public string ToAuthor { get; set; } = string.Empty;
}
