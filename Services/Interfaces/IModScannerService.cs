using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Service responsible for scanning and loading mods from disk.
/// Returns unified Mod objects with validation warnings populated.
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
    /// <returns>Tuple of server mods and client mods.</returns>
    Task<(List<Mod> ServerMods, List<Mod> ClientMods)> ScanAllModsAsync(string sptPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the SPT version from the installation.
    /// </summary>
    /// <param name="sptPath">Path to the SPT installation directory.</param>
    /// <returns>The SPT version string, or null if not found.</returns>
    string? GetSptVersion(string sptPath);
}
