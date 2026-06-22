using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Drives the end-of-run interaction: offers to manage ignored updates, runs the multi-select prompt, persists the
/// result, and waits for the exit keypress.
/// </summary>
public interface IIgnoredUpdateWorkflow
{
    /// <summary>
    /// Runs the end-of-run flow for the given (already enriched, suppression-applied) mod list. Safe to call with a
    /// null/empty list — it falls back to a plain "press any key to exit".
    /// </summary>
    Task RunAsync(IReadOnlyList<Mod>? mods, CancellationToken cancellationToken = default);
}
