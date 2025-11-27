namespace CheckMods.Models;

/// <summary>
/// Represents the various states a mod can be in after processing.
/// </summary>
public enum ModStatus
{
    /// <summary>
    /// The mod was successfully matched with the Forge API and is compatible.
    /// </summary>
    Verified,

    /// <summary>
    /// No matching mod was found in the Forge API.
    /// </summary>
    NoMatch,

    /// <summary>
    /// The mod is incompatible with the current SPT version or is blacklisted.
    /// </summary>
    Incompatible,

    /// <summary>
    /// The mod has an invalid version format that cannot be parsed.
    /// </summary>
    InvalidVersion,

    /// <summary>
    /// The mod matched with low confidence and needs user confirmation.
    /// </summary>
    NeedsConfirmation,
}
