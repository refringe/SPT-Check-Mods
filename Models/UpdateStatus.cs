namespace CheckMods.Models;

/// <summary>
/// Specifies the update status of a mod compared to the latest available version.
/// </summary>
public enum UpdateStatus
{
    /// <summary>
    /// Update status has not been determined yet.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// The installed version matches the latest available version.
    /// </summary>
    UpToDate = 1,

    /// <summary>
    /// A newer version is available on Forge.
    /// </summary>
    UpdateAvailable = 2,

    /// <summary>
    /// The installed version is newer than the latest on Forge.
    /// </summary>
    NewerInstalled = 3,

    /// <summary>
    /// No SPT-compatible versions were found on Forge.
    /// </summary>
    NoVersionsFound = 4,

    /// <summary>
    /// An update is available but blocked by dependency constraints from other installed mods.
    /// </summary>
    UpdateBlocked = 5,

    /// <summary>
    /// The mod has no compatible version for the current SPT version.
    /// </summary>
    Incompatible = 6,
}
