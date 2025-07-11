using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Interface for server-side mod discovery, validation, and compatibility checking.
/// </summary>
public interface IModService
{
    /// <summary>
    /// Reads and validates the SPT version from the core.json configuration file.
    /// </summary>
    /// <param name="sptPath">Path to the SPT installation directory.</param>
    /// <returns>Validated SPT version or null if validation fails.</returns>
    Task<SemanticVersioning.Version?> GetAndValidateSptVersionAsync(string sptPath);
    
    /// <summary>
    /// Scans the mods directory for server mods and reads their package.json files.
    /// </summary>
    /// <param name="modsDirPath">Path to the user/mods directory.</param>
    /// <returns>List of discovered mod packages.</returns>
    List<ModPackage> GetLocalMods(string modsDirPath);
    
    /// <summary>
    /// Processes a list of local mods for compatibility with the specified SPT version.
    /// </summary>
    /// <param name="localMods">List of local mods to process.</param>
    /// <param name="sptVersion">SPT version to check compatibility against.</param>
    /// <returns>List of processed mods with their compatibility status.</returns>
    Task<List<ProcessedMod>> ProcessModCompatibility(List<ModPackage> localMods, SemanticVersioning.Version sptVersion);
}