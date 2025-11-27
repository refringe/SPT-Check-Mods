using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneOf;

namespace CheckMods.Services;

/// <summary>
/// Service for interacting with the Forge API with rate limiting. Handles authentication, mod searching, version
/// validation, and data retrieval.
/// </summary>
public partial class ForgeApiService(
    HttpClient httpClient,
    IRateLimitService rateLimitService,
    IOptions<ForgeApiOptions> options,
    ILogger<ForgeApiService> logger
) : IForgeApiService
{
    private readonly ForgeApiOptions _options = options.Value;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// A regular expression to convert camelCase strings to space-separated words.
    /// </summary>
    [GeneratedRegex(@"(?<!^)(?<![\p{Lu}])(?=[\p{Lu}])|(?<=[\p{Ll}])(?=[\p{Lu}])")]
    private static partial Regex ConvertCamelCaseRegex();

    /// <summary>
    /// Converts camelCase strings to space-separated words for better API search results. Handles special cases like
    /// all-uppercase strings (MOAR, SPT, API).
    /// </summary>
    /// <param name="input">The camelCase string to convert.</param>
    /// <returns>Space-separated string.</returns>
    private static string ConvertCamelCaseToSpaces(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return input;
        }

        // Handle special cases where the entire string is uppercase (like "MOAR")
        if (input.All(c => !char.IsLetter(c) || char.IsUpper(c)))
        {
            return input;
        }

        return ConvertCamelCaseRegex().Replace(input, " ").Trim();
    }

    /// <summary>
    /// Sets the API key for authentication with the Forge API.
    /// </summary>
    /// <param name="apiKey">The Bearer token for API authentication.</param>
    public void SetApiKey(string apiKey)
    {
        logger.LogDebug("Setting API key for Forge API authentication");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    /// <summary>
    /// Validates an API key by checking if it has the required 'read' permissions.
    /// </summary>
    /// <param name="apiKey">The API key to validate.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// One of:
    /// - bool (true): The API key is valid and has read permissions
    /// - InvalidApiKey: The API key is invalid or lacks permissions
    /// - ApiError: An error occurred during validation
    /// </returns>
    public async Task<OneOf<bool, InvalidApiKey, ApiError>> ValidateApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Validating API key");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogWarning("API key validation failed: key is empty or whitespace");
            return new InvalidApiKey(ShouldDeleteKey: true);
        }

        try
        {
            var url = _options.BaseUrl + "auth/abilities";
            logger.LogDebug("API Request: GET {Url}", url);

            var response = await rateLimitService.ExecuteWithRetryAsync(
                async () =>
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    return await httpClient.SendAsync(request, cancellationToken);
                },
                cancellationToken
            );

            // Definitive auth failures - key is bad
            if (
                response.StatusCode == System.Net.HttpStatusCode.Unauthorized
                || response.StatusCode == System.Net.HttpStatusCode.Forbidden
            )
            {
                logger.LogWarning("API key validation failed: {StatusCode}", response.StatusCode);
                return new InvalidApiKey(ShouldDeleteKey: true);
            }

            // Server errors - don't delete the key, might be transient
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("API key validation failed with server error: {StatusCode}", response.StatusCode);
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var authResponse = JsonSerializer.Deserialize<AuthAbilitiesResponse>(jsonContent, _jsonOptions);
            var hasReadPermission =
                authResponse is { Success: true, Data: not null } && authResponse.Data.Contains("read");

            if (!hasReadPermission)
            {
                logger.LogWarning("API key validation failed: key lacks read permission");
                return new InvalidApiKey(ShouldDeleteKey: true);
            }

            logger.LogDebug("API key validated successfully with read permission");
            return true;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error during API key validation");
            // Network error - don't delete the key
            return new ApiError("Network error occurred", Exception: ex);
        }
    }

    /// <summary>
    /// Validates that an SPT version exists in the Forge API.
    /// </summary>
    /// <param name="sptVersion">The SPT version to validate.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>
    /// One of:
    /// - bool (true): The SPT version is valid
    /// - InvalidSptVersion: The SPT version does not exist
    /// - ApiError: An error occurred during validation
    /// </returns>
    public async Task<OneOf<bool, InvalidSptVersion, ApiError>> ValidateSptVersionAsync(
        string sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Validating SPT version: {SptVersion}", sptVersion);

        try
        {
            var escapedVersion = Uri.EscapeDataString(sptVersion);
            var url = $"{_options.BaseUrl}spt/versions?filter[spt_version]={escapedVersion}";
            logger.LogDebug("API Request: GET {Url}", url);

            var response = await rateLimitService.ExecuteWithRetryAsync(
                () => httpClient.GetAsync(url, cancellationToken),
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("SPT version validation failed: {StatusCode}", response.StatusCode);
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<SptVersionApiResponse>(jsonContent, _jsonOptions);

            var isValid =
                apiResponse is { Success: true, Data: not null } && apiResponse.Data.Any(v => v.Version == sptVersion);

            if (!isValid)
            {
                logger.LogWarning("SPT version {SptVersion} not found in Forge API", sptVersion);
                return new InvalidSptVersion();
            }

            logger.LogDebug("SPT version {SptVersion} validated successfully", sptVersion);
            return true;
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error during SPT version validation");
            return new ApiError("Network error occurred", Exception: ex);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse SPT version validation response");
            return new ApiError("Failed to parse API response", Exception: ex);
        }
    }

    /// <summary>
    /// Retrieves all available SPT versions from the Forge API.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>List of all available SPT versions or an error.</returns>
    public async Task<OneOf<List<SptVersionResult>, ApiError>> GetAllSptVersionsAsync(
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Fetching all SPT versions from Forge API");

        try
        {
            var url = $"{_options.BaseUrl}spt/versions?sort=-version&per_page=15";
            logger.LogDebug("API Request: GET {Url}", url);

            var response = await rateLimitService.ExecuteWithRetryAsync(
                () => httpClient.GetAsync(url, cancellationToken),
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to fetch SPT versions: {StatusCode}", response.StatusCode);
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<SptVersionApiResponse>(jsonContent, _jsonOptions);

            return apiResponse is { Success: true, Data: not null } ? apiResponse.Data : [];
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Network error during SPT versions fetch");
            return new ApiError("Network error occurred", Exception: ex);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to parse SPT versions response");
            return new ApiError("Failed to parse API response", Exception: ex);
        }
    }

    /// <summary>
    /// Searches for server mods using the Forge API search results.
    /// </summary>
    /// <param name="modName">The name of the mod to search for.</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>List of matching mod search results or an error.</returns>
    public async Task<OneOf<List<ModSearchResult>, ApiError>> SearchModsAsync(
        string modName,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Searching for server mod: {ModName}", modName);

        var searchQuery = ConvertCamelCaseToSpaces(modName);
        return await SearchModsInternalAsync(searchQuery, sptVersion, cancellationToken);
    }

    /// <summary>
    /// Searches for client mods using the Forge API.
    /// </summary>
    /// <param name="modName">The name of the client mod to search for.</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>List of matching client mod search results or an error.</returns>
    public async Task<OneOf<List<ModSearchResult>, ApiError>> SearchClientModsAsync(
        string modName,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Searching for client mod: {ModName}", modName);

        var searchQuery = ConvertCamelCaseToSpaces(modName);
        return await SearchModsInternalAsync(searchQuery, sptVersion, cancellationToken);
    }

    /// <summary>
    /// Retrieves a specific mod by its ID from the Forge API.
    /// </summary>
    /// <param name="modId">The ID of the mod to retrieve.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The mod search result or an error type.</returns>
    public async Task<OneOf<ModSearchResult, NotFound, InvalidInput, ApiError>> GetModByIdAsync(
        int modId,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Getting mod by ID: {ModId}", modId);

        if (modId <= 0)
        {
            logger.LogWarning("Invalid mod ID: {ModId}", modId);
            return new InvalidInput("modId", "Mod ID must be greater than 0");
        }

        try
        {
            var url = $"{_options.BaseUrl}mod/{modId}?include=versions,source_code_links";
            logger.LogDebug("API Request: GET {Url}", url);

            var response = await rateLimitService.ExecuteWithRetryAsync(
                () => httpClient.GetAsync(url, cancellationToken),
                cancellationToken
            );

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return new NotFound();
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(jsonContent);

            if (
                !jsonDoc.RootElement.TryGetProperty("success", out var successElement)
                || !successElement.GetBoolean()
                || !jsonDoc.RootElement.TryGetProperty("data", out var dataElement)
            )
            {
                return new NotFound();
            }

            var result = JsonSerializer.Deserialize<ModSearchResult>(dataElement.GetRawText(), _jsonOptions);
            if (result is null)
            {
                return new NotFound();
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            return new ApiError("Network error occurred", Exception: ex);
        }
        catch (JsonException ex)
        {
            return new ApiError("Failed to parse API response", Exception: ex);
        }
    }

    /// <summary>
    /// Retrieves a mod by its GUID from the Forge API.
    /// </summary>
    /// <param name="modGuid">The GUID of the mod (e.g., "com.author.modname").</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The mod search result or an error type.</returns>
    public async Task<OneOf<ModSearchResult, NotFound, NoCompatibleVersion, ApiError>> GetModByGuidAsync(
        string modGuid,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Getting mod by GUID: {ModGuid}", modGuid);

        if (string.IsNullOrWhiteSpace(modGuid))
        {
            logger.LogDebug("Empty GUID provided");
            return new NotFound();
        }

        try
        {
            var url =
                $"{_options.BaseUrl}mods?filter[guid]={Uri.EscapeDataString(modGuid)}&include=versions,source_code_links";
            logger.LogDebug("API Request: GET {Url}", url);

            var response = await rateLimitService.ExecuteWithRetryAsync(
                () => httpClient.GetAsync(url, cancellationToken),
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<ModSearchApiResponse>(jsonContent, _jsonOptions);

            if (apiResponse is not { Success: true, Data.Count: > 0 })
            {
                return new NotFound();
            }

            var result = apiResponse.Data[0];

            // Check if any version is compatible with the requested SPT version
            if (result.Versions is not { Count: > 0 })
            {
                return result;
            }

            var hasCompatibleVersion = result.Versions.Any(v =>
            {
                if (string.IsNullOrWhiteSpace(v.SptVersionConstraint))
                {
                    return false;
                }

                try
                {
                    var range = new SemanticVersioning.Range(v.SptVersionConstraint);
                    return range.IsSatisfied(sptVersion.ToString());
                }
                catch
                {
                    return false;
                }
            });

            if (!hasCompatibleVersion)
            {
                return new NoCompatibleVersion();
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            return new ApiError("Network error occurred", Exception: ex);
        }
        catch (JsonException ex)
        {
            return new ApiError("Failed to parse API response", Exception: ex);
        }
    }

    /// <summary>
    /// Internal method for searching mods with shared logic between server and client mod searches.
    /// </summary>
    /// <param name="searchQuery">The processed search query.</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>List of matching mod search results or an error.</returns>
    private async Task<OneOf<List<ModSearchResult>, ApiError>> SearchModsInternalAsync(
        string searchQuery,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var url =
                $"{_options.BaseUrl}mods?query={Uri.EscapeDataString(searchQuery)}&filter[spt_version]={sptVersion}&include=versions,source_code_links";
            logger.LogDebug("API Request: GET {Url}", url);

            var response = await rateLimitService.ExecuteWithRetryAsync(
                () => httpClient.GetAsync(url, cancellationToken),
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<ModSearchApiResponse>(jsonContent, _jsonOptions);

            return apiResponse is { Success: true, Data: not null } ? apiResponse.Data : [];
        }
        catch (HttpRequestException ex)
        {
            return new ApiError("Network error occurred", Exception: ex);
        }
        catch (JsonException ex)
        {
            return new ApiError("Failed to parse API response", Exception: ex);
        }
    }

    /// <summary>
    /// Retrieves batch update information for multiple mods in a single API call.
    /// </summary>
    /// <param name="modUpdates">Collection of mod ID and current version pairs to check.</param>
    /// <param name="sptVersion">The SPT version to check compatibility against.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Categorized update information for the requested mods or an error.</returns>
    public async Task<OneOf<ModUpdatesData, NotFound, ApiError>> GetModUpdatesAsync(
        IEnumerable<(int ModId, string CurrentVersion)> modUpdates,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        var modList = modUpdates.ToList();
        if (modList.Count == 0)
        {
            return new NotFound();
        }

        try
        {
            // Build mods query parameter as comma-separated "id:version" pairs
            var modsParam = string.Join(
                ",",
                modList.Select(m => $"{m.ModId}:{Uri.EscapeDataString(m.CurrentVersion)}")
            );

            var url = $"{_options.BaseUrl}mods/updates?mods={modsParam}&spt_version={sptVersion}";
            logger.LogDebug("API Request: GET {Url}", url);

            var response = await rateLimitService.ExecuteWithRetryAsync(
                () => httpClient.GetAsync(url, cancellationToken),
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<ModUpdatesApiResponse>(jsonContent, _jsonOptions);

            if (apiResponse?.Success != true || apiResponse.Data is null)
            {
                return new NotFound();
            }

            return apiResponse.Data;
        }
        catch (HttpRequestException ex)
        {
            return new ApiError("Network error occurred", Exception: ex);
        }
        catch (JsonException ex)
        {
            return new ApiError("Failed to parse API response", Exception: ex);
        }
    }

    /// <summary>
    /// Retrieves dependency information for multiple mods in a single API call.
    /// </summary>
    /// <param name="modVersions">Collection of identifier (mod ID or GUID) and version pairs.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Flattened dependency tree with recursive structure or an error.</returns>
    public async Task<OneOf<List<ModDependency>, NotFound, ApiError>> GetModDependenciesAsync(
        IEnumerable<(string Identifier, string Version)> modVersions,
        CancellationToken cancellationToken = default
    )
    {
        var modList = modVersions.ToList();
        if (modList.Count == 0)
        {
            return new NotFound();
        }

        try
        {
            // Build mods query parameter as comma-separated "identifier:version" pairs
            var modsParam = string.Join(
                ",",
                modList.Select(m => $"{Uri.EscapeDataString(m.Identifier)}:{Uri.EscapeDataString(m.Version)}")
            );

            var url = $"{_options.BaseUrl}mods/dependencies?mods={modsParam}";
            logger.LogDebug("API Request: GET {Url}", url);

            var response = await rateLimitService.ExecuteWithRetryAsync(
                () => httpClient.GetAsync(url, cancellationToken),
                cancellationToken
            );

            if (!response.IsSuccessStatusCode)
            {
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var apiResponse = JsonSerializer.Deserialize<ModDependenciesApiResponse>(jsonContent, _jsonOptions);

            if (apiResponse?.Success != true || apiResponse.Data is null)
            {
                return new NotFound();
            }

            return apiResponse.Data;
        }
        catch (HttpRequestException ex)
        {
            return new ApiError("Network error occurred", Exception: ex);
        }
        catch (JsonException ex)
        {
            return new ApiError("Failed to parse API response", Exception: ex);
        }
    }
}
