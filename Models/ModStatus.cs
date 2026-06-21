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
}
