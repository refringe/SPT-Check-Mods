namespace CheckMods.Models;

/// <summary>
/// Represents the result of processing a mod during the verification workflow. Contains both the processed mod
/// information and any pending confirmation required.
/// </summary>
/// <param name="processedMod">The processed mod with verification results.</param>
/// <param name="pendingConfirmation">The pending confirmation if user interaction is required.</param>
public class ProcessingResult(ProcessedMod processedMod, PendingConfirmation? pendingConfirmation = null)
{
    /// <summary>
    /// The processed mod containing verification results and status information.
    /// </summary>
    public ProcessedMod ProcessedMod { get; set; } = processedMod;

    /// <summary>
    /// The pending confirmation if the match requires user interaction due to low confidence.
    /// Null if no confirmation is required.
    /// </summary>
    public PendingConfirmation? PendingConfirmation { get; set; } = pendingConfirmation;
}
