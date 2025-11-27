using CheckMods.Models;
using OneOf;

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
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// One of:
    /// - bool (true): The API key is valid and has read permissions
    /// - InvalidApiKey: The API key is invalid or lacks permissions (includes ShouldDeleteKey flag)
    /// - ApiError: An error occurred during validation (transient errors)
    /// </returns>
    Task<OneOf<bool, InvalidApiKey, ApiError>> ValidateApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validates that an SPT version exists in the Forge API.
    /// </summary>
    /// <param name="sptVersion">The SPT version to validate.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// One of:
    /// - bool (true): The SPT version is valid
    /// - InvalidSptVersion: The SPT version does not exist in the API
    /// - ApiError: An error occurred during validation
    /// </returns>
    Task<OneOf<bool, InvalidSptVersion, ApiError>> ValidateSptVersionAsync(
        string sptVersion,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves all available SPT versions from the Forge API.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// One of:
    /// - List&lt;SptVersionResult&gt;: List of all available SPT versions
    /// - ApiError: An error occurred during the API call
    /// </returns>
    Task<OneOf<List<SptVersionResult>, ApiError>> GetAllSptVersionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for server mods using the Forge API.
    /// </summary>
    /// <param name="modName">The name of the mod to search for.</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// One of:
    /// - List&lt;ModSearchResult&gt;: List of matching mods (may be empty if no matches)
    /// - ApiError: An error occurred during the search
    /// </returns>
    Task<OneOf<List<ModSearchResult>, ApiError>> SearchModsAsync(
        string modName,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Searches for client mods using the Forge API.
    /// </summary>
    /// <param name="modName">The name of the client mod to search for.</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// One of:
    /// - List&lt;ModSearchResult&gt;: List of matching mods (may be empty if no matches)
    /// - ApiError: An error occurred during the search
    /// </returns>
    Task<OneOf<List<ModSearchResult>, ApiError>> SearchClientModsAsync(
        string modName,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves a specific mod by its ID from the Forge API.
    /// </summary>
    /// <param name="modId">The ID of the mod to retrieve.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// One of:
    /// - ModSearchResult: The mod was found
    /// - NotFound: No mod exists with this ID
    /// - InvalidInput: The mod ID was invalid (e.g., less than or equal to 0)
    /// - ApiError: An error occurred during the API call
    /// </returns>
    Task<OneOf<ModSearchResult, NotFound, InvalidInput, ApiError>> GetModByIdAsync(
        int modId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves a mod by its GUID from the Forge API.
    /// </summary>
    /// <param name="modGuid">The GUID of the mod (e.g., "com.author.modname").</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// One of:
    /// - ModSearchResult: The mod was found and has a compatible version
    /// - NotFound: No mod exists with this GUID
    /// - NoCompatibleVersion: Mod exists but no version is compatible with the SPT version
    /// - ApiError: An error occurred during the API call
    /// </returns>
    Task<OneOf<ModSearchResult, NotFound, NoCompatibleVersion, ApiError>> GetModByGuidAsync(
        string modGuid,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves batch update information for multiple mods in a single API call.
    /// </summary>
    /// <param name="modUpdates">Collection of mod ID and current version pairs to check.</param>
    /// <param name="sptVersion">The SPT version to check compatibility against.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// One of:
    /// - ModUpdatesData: Categorized update information for the requested mods
    /// - NotFound: No mods were provided or none were found
    /// - ApiError: An error occurred during the API call
    /// </returns>
    Task<OneOf<ModUpdatesData, NotFound, ApiError>> GetModUpdatesAsync(
        IEnumerable<(int ModId, string CurrentVersion)> modUpdates,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Retrieves dependency information for multiple mods in a single API call.
    /// </summary>
    /// <param name="modVersions">Collection of identifier (mod ID or GUID) and version pairs.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// One of:
    /// - List&lt;ModDependency&gt;: Flattened dependency tree with recursive structure
    /// - NotFound: No mods were provided
    /// - ApiError: An error occurred during the API call
    /// </returns>
    Task<OneOf<List<ModDependency>, NotFound, ApiError>> GetModDependenciesAsync(
        IEnumerable<(string Identifier, string Version)> modVersions,
        CancellationToken cancellationToken = default
    );
}
