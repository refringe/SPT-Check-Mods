namespace CheckMods.Models;

/// <summary>
/// Represents the result of a fuzzy matching operation between a local mod and API search result.
/// </summary>
public class MatchResult
{
    /// <summary>
    /// The API search result being matched.
    /// </summary>
    public required ModSearchResult ApiResult { get; init; }
    
    /// <summary>
    /// The confidence score (0-100) based on fuzzy string matching.
    /// </summary>
    public required int Score { get; init; }
    
    /// <summary>
    /// Whether this is a high-confidence match that can be auto-verified.
    /// </summary>
    public required bool IsHighConfidence { get; init; }
    
    /// <summary>
    /// Whether this is a medium confidence match that requires user confirmation.
    /// </summary>
    public required bool IsMediumConfidence { get; init; }
}