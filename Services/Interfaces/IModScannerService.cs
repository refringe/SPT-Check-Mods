using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Scans and loads mods from disk, returning unified Mod objects with validation warnings.
/// </summary>
public interface IModScannerService
{
    /// <summary>
    /// Scans the SPT user/mods directory for server mods.
    /// </summary>
    /// <param name="sptPath">Path to the SPT installation directory.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>List of server mods with any validation warnings populated.</returns>
    List<Mod> ScanServerMods(string sptPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans the BepInEx plugins directory for client mods.
    /// </summary>
    /// <param name="sptPath">Path to the SPT installation directory.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>List of client mods with any validation warnings populated.</returns>
    Task<List<Mod>> ScanClientModsAsync(string sptPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scans both server and client mod directories.
    /// </summary>
    /// <param name="sptPath">Path to the SPT installation directory.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task<(List<Mod> ServerMods, List<Mod> ClientMods)> ScanAllModsAsync(
        string sptPath,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Extracts the SPT version from the installation.
    /// </summary>
    /// <param name="sptPath">Path to the SPT installation directory.</param>
    /// <returns>The SPT version string, or null if not found.</returns>
    string? GetSptVersion(string sptPath);

    /// <summary>
    /// Detects mods installed in the wrong location.
    /// </summary>
    /// <param name="sptPath">Path to the SPT installation directory.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// A report of misplaced and cross-installed mods; <see cref="MisplacedModReport.Any"/> is false when every mod is
    /// correctly placed.
    /// </returns>
    MisplacedModReport DetectMisplacedMods(string sptPath, CancellationToken cancellationToken = default);
}
