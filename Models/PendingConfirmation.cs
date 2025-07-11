namespace CheckMods.Models;

/// <summary>
/// Represents a mod match that requires user confirmation due to a low confidence score. Used to queue up matches for
/// interactive confirmation during the verification process.
/// </summary>
/// <param name="localMod">The local mod package information.</param>
/// <param name="originalMod">The original mod package before any name transformations.</param>
/// <param name="apiMatch">The matched mod from the Forge API search results.</param>
/// <param name="confidenceScore">The confidence score of the match (0-100).</param>
/// <param name="resultIndex">The index of the result in the processing queue.</param>
public class PendingConfirmation(
    ModPackage localMod,
    ModPackage originalMod,
    ModSearchResult apiMatch,
    int confidenceScore,
    int resultIndex)
{
    /// <summary>
    /// The local mod package information as processed by the application.
    /// </summary>
    public ModPackage LocalMod { get; set; } = localMod;
    
    /// <summary>
    /// The original mod package information before any name transformations or updates.
    /// </summary>
    public ModPackage OriginalMod { get; set; } = originalMod;
    
    /// <summary>
    /// The matched mod from the Forge API search results.
    /// </summary>
    public ModSearchResult ApiMatch { get; set; } = apiMatch;
    
    /// <summary>
    /// The confidence score of the match based on fuzzy string matching (0-100).
    /// </summary>
    public int ConfidenceScore { get; set; } = confidenceScore;
    
    /// <summary>
    /// The index of this result in the processing queue for display purposes.
    /// </summary>
    public int ResultIndex { get; set; } = resultIndex;
}