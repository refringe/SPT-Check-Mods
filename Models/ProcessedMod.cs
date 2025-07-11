namespace CheckMods.Models;

/// <summary>
/// Represents a mod that has been processed for compatibility and API matching. Contains the original mod data,
/// processing results, and API match information.
/// </summary>
public class ProcessedMod
{
    /// <summary>
    /// The original mod package.
    /// </summary>
    public ModPackage Mod { get; set; }
    
    /// <summary>
    /// The processing record containing status and match details.
    /// </summary>
    public ModRecord Record { get; set; }
    
    /// <summary>
    /// The processing status of the mod.
    /// </summary>
    public ModStatus Status => Record.Status;
    
    /// <summary>
    /// The API search result that matched this mod, if any.
    /// </summary>
    public ModSearchResult? ApiMatch { get; private set; }

    /// <summary>
    /// Initializes a new ProcessedMod with the specified mod and processing status.
    /// </summary>
    /// <param name="mod">The mod package being processed.</param>
    /// <param name="status">The processing status.</param>
    /// <param name="apiMatch">The API match result, if any.</param>
    public ProcessedMod(ModPackage mod, ModStatus status, ModSearchResult? apiMatch = null)
    {
        Mod = mod;
        Record = new ModRecord(mod, status);
        ApiMatch = apiMatch;

        if (apiMatch == null || status != ModStatus.Verified) return;
        
        Record.UpdateFromApiMatch(apiMatch, 100);
        Record.IsConfirmed = true;
    }

    /// <summary>
    /// Updates the confirmation status based on user input for low-confidence matches.
    /// </summary>
    /// <param name="confirmed">Whether the user confirmed the match.</param>
    /// <param name="apiMatch">The API match result to apply if confirmed.</param>
    public void UpdateConfirmation(bool confirmed, ModSearchResult? apiMatch = null)
    {
        Record.UpdateConfirmation(confirmed);

        switch (confirmed)
        {
            case true when apiMatch != null:
                ApiMatch = apiMatch;
                Record.UpdateFromApiMatch(apiMatch, Record.ConfidenceScore);
                break;
            case false:
                ApiMatch = null;
                break;
        }
    }
    
    /// <summary>
    /// Sets the confidence score for the API match.
    /// </summary>
    /// <param name="score">The confidence score (0-100).</param>
    public void SetConfidenceScore(int score)
    {
        Record.ConfidenceScore = score;
    }
    
    /// <summary>
    /// Updates the processed mod with API match information.
    /// </summary>
    /// <param name="apiMatch">The API search result that matched this mod.</param>
    /// <param name="confidenceScore">The confidence score for the match.</param>
    public void UpdateFromApiMatch(ModSearchResult apiMatch, int confidenceScore)
    {
        ApiMatch = apiMatch;
        Record.UpdateFromApiMatch(apiMatch, confidenceScore);
    }
}