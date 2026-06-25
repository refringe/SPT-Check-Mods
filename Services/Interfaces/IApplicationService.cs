using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Interface for the main application orchestration service.
/// </summary>
public interface IApplicationService
{
    /// <summary>
    /// Main entry point for the application workflow.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The reconciled, enriched mod list (with update suppressions applied), or an empty list on any early exit.</returns>
    Task<IReadOnlyList<Mod>> RunAsync(string[] args, CancellationToken cancellationToken = default);
}
