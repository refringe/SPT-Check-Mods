using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Interface for interacting with the Forge API, providing caching and rate limiting. Handles authentication, mod
/// searching, version validation, and data retrieval.
/// </summary>
public interface IForgeApiService
{
    /// <summary>
    /// Sets the API key for authentication with the Forge API.
    /// </summary>
    /// <param name="apiKey">The Bearer token for API authentication.</param>
    void SetApiKey(string apiKey);

    /// <summary>
    /// Validates an API key by checking if it has the required 'read' permissions.
    /// </summary>
    /// <param name="apiKey">The API key to validate.</param>
    /// <returns>True if the API key is valid and has read permissions.</returns>
    Task<bool> ValidateApiKeyAsync(string apiKey);

    /// <summary>
    /// Validates that an SPT version exists in the Forge API.
    /// </summary>
    /// <param name="sptVersion">The SPT version to validate.</param>
    /// <returns>True if the SPT version is valid.</returns>
    Task<bool> ValidateSptVersionAsync(string sptVersion);

    /// <summary>
    /// Searches for server mods using the Forge API.
    /// </summary>
    /// <param name="modName">The name of the mod to search for.</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <returns>List of matching mod search results.</returns>
    Task<List<ModSearchResult>> SearchModsAsync(string modName, SemanticVersioning.Version sptVersion);

    /// <summary>
    /// Searches for client mods using the Forge API.
    /// </summary>
    /// <param name="modName">The name of the client mod to search for.</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <returns>List of matching client mod search results.</returns>
    Task<List<ModSearchResult>> SearchClientModsAsync(string modName, SemanticVersioning.Version sptVersion);

    /// <summary>
    /// Retrieves a specific mod by its ID from the Forge API.
    /// </summary>
    /// <param name="modId">The ID of the mod to retrieve.</param>
    /// <returns>The mod search result or null if not found.</returns>
    Task<ModSearchResult?> GetModByIdAsync(int modId);

    /// <summary>
    /// Retrieves all versions of a specific mod that are compatible with the given SPT version.
    /// </summary>
    /// <param name="modId">The ID of the mod to get versions for.</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <returns>List of mod versions compatible with the SPT version.</returns>
    Task<List<ModVersion>> GetModVersionsAsync(int modId, SemanticVersioning.Version sptVersion);
}
