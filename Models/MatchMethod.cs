namespace CheckMods.Models;

/// <summary>
/// Specifies how a mod was matched with its API counterpart.
/// </summary>
public enum MatchMethod
{
    /// <summary>
    /// No match was found or attempted.
    /// </summary>
    None = 0,

    /// <summary>
    /// Matched via exact GUID lookup (highest confidence).
    /// </summary>
    ExactGuid = 1,

    /// <summary>
    /// Matched via exact name comparison after normalization.
    /// </summary>
    ExactName = 2,

    /// <summary>
    /// Matched via fuzzy name comparison.
    /// </summary>
    FuzzyName = 3,

    /// <summary>
    /// Manually confirmed by the user.
    /// </summary>
    Manual = 4
}
