namespace CheckMods.Models;

/// <summary>
/// Represents the result of a mod verification operation against the Forge API. Contains information about whether a
/// mod was successfully matched and verified.
/// </summary>
/// <param name="isVerified">Whether the mod was successfully verified.</param>
/// <param name="matchedMod">The matched mod from the Forge API, if any.</param>
/// <param name="requiresConfirmation">Whether the match requires user confirmation due to low confidence.</param>
/// <param name="confidenceScore">The confidence score of the match (0-100).</param>
public class ModVerificationResult(
    bool isVerified,
    ModSearchResult? matchedMod,
    bool requiresConfirmation = false,
    int confidenceScore = 0)
{
    /// <summary>
    /// Whether the mod was successfully verified against the Forge API.
    /// </summary>
    public bool IsVerified { get; set; } = isVerified;
    
    /// <summary>
    /// The matched mod from the Forge API search results if a match was found.
    /// </summary>
    public ModSearchResult? MatchedMod { get; set; } = matchedMod;
    
    /// <summary>
    /// Whether the match requires user confirmation due to a low confidence score.
    /// </summary>
    public bool RequiresConfirmation { get; set; } = requiresConfirmation;
    
    /// <summary>
    /// The confidence score of the match based on fuzzy string matching (0-100).
    /// Higher scores indicate better matches.
    /// </summary>
    public int ConfidenceScore { get; set; } = confidenceScore;
}