using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Drives the end-of-run interaction: offers to manage ignored updates, runs the multi-select prompt, persists the
/// result, and waits for the exit keypress.
/// </summary>
public interface IIgnoredUpdateWorkflow
{
    /// <summary>
    /// Runs the end-of-run flow for the given mod list, falling back to a plain exit prompt when the list is null or empty.
    /// </summary>
    Task RunAsync(IReadOnlyList<Mod>? mods, CancellationToken cancellationToken = default);
}
