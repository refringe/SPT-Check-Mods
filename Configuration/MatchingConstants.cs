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

    /// <summary>
    /// Confidence score assigned to exact GUID matches.
    /// </summary>
    public const int ExactGuidConfidence = 100;

    /// <summary>
    /// Confidence score assigned to exact name matches (after normalization).
    /// </summary>
    public const int ExactNameConfidence = 95;

    /// <summary>
    /// Confidence score assigned to fuzzy name matches.
    /// </summary>
    public const int FuzzyNameConfidence = 85;

    /// <summary>
    /// Confidence reduction applied when matching via alternate GUID instead of primary GUID.
    /// </summary>
    public const int AlternateGuidConfidenceReduction = 5;

    /// <summary>
    /// Minimum fuzzy match score for name search results to be considered.
    /// </summary>
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
