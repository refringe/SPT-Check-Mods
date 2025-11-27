using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Service responsible for SPT installation validation.
/// </summary>
public interface IServerModService
{
    /// <summary>
    /// Reads and validates the SPT version from the installation.
    /// </summary>
    /// <param name="sptPath">Path to the SPT installation directory.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Validated SPT version or null if validation fails.</returns>
    Task<SemanticVersioning.Version?> GetAndValidateSptVersionAsync(string sptPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks for available SPT updates and displays them to the user.
    /// </summary>
    /// <param name="currentVersion">The currently installed SPT version.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>List of available SPT version updates, or empty list if none available or an error occurred.</returns>
    Task<List<SptVersionResult>> CheckForSptUpdatesAsync(SemanticVersioning.Version currentVersion, CancellationToken cancellationToken = default);
}
