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
    Task RunAsync(string[] args, CancellationToken cancellationToken = default);
}
