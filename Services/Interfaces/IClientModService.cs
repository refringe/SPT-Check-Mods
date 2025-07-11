using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Interface for client-side mod discovery, validation, and compatibility checking. Handles scanning BepInEx plugin
/// DLLs and coordinating with the Forge API.
/// </summary>
public interface IClientModService
{
    /// <summary>
    /// Scans the BepInEx plugins directory for client mods and extracts their metadata.
    /// </summary>
    /// <param name="pluginsPath">Path to the BepInEx plugins directory.</param>
    /// <returns>List of discovered client mod packages.</returns>
    List<ClientModPackage> GetClientMods(string pluginsPath);
    
    /// <summary>
    /// Processes a list of client mods for compatibility with the specified SPT version.
    /// </summary>
    /// <param name="clientMods">List of client mods to process.</param>
    /// <param name="sptVersion">SPT version to check compatibility against.</param>
    /// <returns>List of processed mods with their compatibility status.</returns>
    Task<List<ProcessedMod>> ProcessClientModCompatibility(List<ClientModPackage> clientMods, SemanticVersioning.Version sptVersion);
}