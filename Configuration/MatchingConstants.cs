namespace CheckMods.Configuration;

/// <summary>
/// Constants used for mod matching operations.
/// </summary>
public static class MatchingConstants
{
    /// <summary>
    /// Minimum fuzzy match score (0-100) required to consider a match valid.
    /// </summary>
    public const int MinimumFuzzyMatchScore = 70;

    public const int NameSearchFuzzyThreshold = 80;

    /// <summary>
    /// Maximum length for mod names in display tables before truncation.
    /// </summary>
    public const int MaxDisplayNameLength = 40;

    /// <summary>
    /// Maximum length for author names in display tables before truncation.
    /// </summary>
    public const int MaxDisplayAuthorLength = 20;
}
