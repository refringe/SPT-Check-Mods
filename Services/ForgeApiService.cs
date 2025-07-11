using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace CheckMods.Services;

/// <summary>
/// Service for interacting with the Forge API, providing caching and rate limiting. Handles authentication, mod
/// searching, version validation, and data retrieval.
/// </summary>
public partial class ForgeApiService(HttpClient httpClient, IMemoryCache cache, IRateLimitService rateLimitService)
    : IForgeApiService
{
    private const string ForgeApiBaseUrl = "https://forge.sp-tarkov.com/api/v0/";
    private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(10);

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
    /// Generates a cache key for API requests.
    /// </summary>
    /// <param name="operation">The operation type (e.g., "search_mods", "validate_api_key").</param>
    /// <param name="parameters">Additional parameters to include in the cache key.</param>
    /// <returns>Generated cache key.</returns>
    private static string GenerateCacheKey(string operation, params object[] parameters)
    {
        var keyParts = new List<string> { operation };
        keyParts.AddRange(parameters.Select(p => p.ToString() ?? "null"));
        return string.Join("_", keyParts);
    }

    /// <summary>
    /// Sets the API key for authentication with the Forge API.
    /// </summary>
    /// <param name="apiKey">The Bearer token for API authentication.</param>
    public void SetApiKey(string apiKey)
    {
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
    }

    /// <summary>
    /// Validates an API key by checking if it has the required 'read' permissions. Results are cached to avoid repeated
    /// validation calls.
    /// </summary>
    /// <param name="apiKey">The API key to validate.</param>
    /// <returns>True if the API key is valid and has read permissions.</returns>
    public async Task<bool> ValidateApiKeyAsync(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        var cacheKey = GenerateCacheKey("validate_api_key", apiKey.GetHashCode());
        if (cache.TryGetValue(cacheKey, out bool cachedResult))
        {
            return cachedResult;
        }

        try
        {
            await rateLimitService.WaitForApiCallAsync();

            var request = new HttpRequestMessage(HttpMethod.Get, ForgeApiBaseUrl + "auth/abilities");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var response = await httpClient.SendAsync(request);
            var jsonContent = await response.Content.ReadAsStringAsync();
            var authResponse = JsonSerializer.Deserialize<AuthAbilitiesResponse>(jsonContent, _jsonOptions);
            var result = authResponse is { Success: true, Data: not null } && authResponse.Data.Contains("read");

            cache.Set(cacheKey, result, _cacheExpiration);
            return result;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates that an SPT version exists in the Forge API. Results are cached to avoid repeated validation calls.
    /// </summary>
    /// <param name="sptVersion">The SPT version to validate.</param>
    /// <returns>True if the SPT version is valid.</returns>
    public async Task<bool> ValidateSptVersionAsync(string sptVersion)
    {
        var escapedVersion = Uri.EscapeDataString(sptVersion);
        var cacheKey = GenerateCacheKey("validate_spt_version", escapedVersion);

        if (cache.TryGetValue(cacheKey, out bool cachedResult))
        {
            return cachedResult;
        }

        try
        {
            var url = $"{ForgeApiBaseUrl}spt/versions?filter[spt_version]={escapedVersion}";
            var apiResponse = await MakeApiCallAsync<SptVersionApiResponse>(url, cacheKey);

            var result =
                apiResponse is { Success: true, Data: not null } && apiResponse.Data.Any(v => v.Version == sptVersion);

            cache.Set(cacheKey, result, _cacheExpiration);
            return result;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Searches for server mods using the Forge API search results.
    /// </summary>
    /// <param name="modName">The name of the mod to search for.</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <returns>List of matching mod search results.</returns>
    public async Task<List<ModSearchResult>> SearchModsAsync(string modName, SemanticVersioning.Version sptVersion)
    {
        var searchQuery = ConvertCamelCaseToSpaces(modName);
        var cacheKey = GenerateCacheKey("search_mods", searchQuery, sptVersion);

        return await SearchModsInternalAsync(searchQuery, sptVersion, cacheKey, "mod");
    }

    /// <summary>
    /// Searches for client mods using the Forge API.
    /// </summary>
    /// <param name="modName">The name of the client mod to search for.</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <returns>List of matching client mod search results.</returns>
    public async Task<List<ModSearchResult>> SearchClientModsAsync(
        string modName,
        SemanticVersioning.Version sptVersion
    )
    {
        var searchQuery = ConvertCamelCaseToSpaces(modName);
        var cacheKey = GenerateCacheKey("search_client_mods", searchQuery, sptVersion);

        return await SearchModsInternalAsync(searchQuery, sptVersion, cacheKey, "client mod");
    }

    /// <summary>
    /// Retrieves a specific mod by its ID from the Forge API.
    /// </summary>
    /// <param name="modId">The ID of the mod to retrieve.</param>
    /// <returns>The mod search result or null if not found.</returns>
    public async Task<ModSearchResult?> GetModByIdAsync(int modId)
    {
        if (modId <= 0)
        {
            return null;
        }

        var cacheKey = GenerateCacheKey("get_mod", modId);
        if (cache.TryGetValue(cacheKey, out ModSearchResult? cachedResult))
        {
            return cachedResult;
        }

        try
        {
            var url = $"{ForgeApiBaseUrl}mods/{modId}?include=owner,versions";
            var result = await MakeApiCallAsync<ModSearchResult>(url, cacheKey, isDataWrapped: true);

            cache.Set(cacheKey, result, _cacheExpiration);
            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Retrieves all versions of a specific mod that are compatible with the given SPT version. Results are sorted by
    /// version and creation date (newest first).
    /// </summary>
    /// <param name="modId">The ID of the mod to get versions for.</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <returns>List of mod versions compatible with the SPT version.</returns>
    public async Task<List<ModVersion>> GetModVersionsAsync(int modId, SemanticVersioning.Version sptVersion)
    {
        var cacheKey = GenerateCacheKey("mod_versions", modId, sptVersion);
        if (cache.TryGetValue(cacheKey, out List<ModVersion>? cachedResult))
        {
            return cachedResult ?? [];
        }

        try
        {
            var url =
                $"{ForgeApiBaseUrl}mod/{modId}/versions?filter[spt_version]={sptVersion}&sort=-version,-created_at";
            var apiResponse = await MakeApiCallAsync<ModVersionsApiResponse>(
                url,
                cacheKey,
                logContext: $"mod versions (ID: {modId})"
            );

            var result = apiResponse is { Success: true, Data: not null } ? apiResponse.Data : [];
            cache.Set(cacheKey, result, _cacheExpiration);
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching versions for mod ID {modId}: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Internal method for searching mods with shared logic between server and client mod searches.
    /// </summary>
    /// <param name="searchQuery">The processed search query.</param>
    /// <param name="sptVersion">The SPT version to filter by.</param>
    /// <param name="cacheKey">The cache key for storing results.</param>
    /// <param name="modType">The type of mod being searched (for logging).</param>
    /// <returns>List of matching mod search results.</returns>
    private async Task<List<ModSearchResult>> SearchModsInternalAsync(
        string searchQuery,
        SemanticVersioning.Version sptVersion,
        string cacheKey,
        string modType
    )
    {
        if (cache.TryGetValue(cacheKey, out List<ModSearchResult>? cachedResult))
        {
            return cachedResult ?? [];
        }

        try
        {
            var url =
                $"{ForgeApiBaseUrl}mods?query={Uri.EscapeDataString(searchQuery)}&filter[spt_version]={sptVersion}&include=owner,versions";
            var apiResponse = await MakeApiCallAsync<ModSearchApiResponse>(
                url,
                cacheKey,
                logContext: $"{modType} '{searchQuery}'"
            );

            var result = apiResponse is { Success: true, Data: not null } ? apiResponse.Data : [];
            cache.Set(cacheKey, result, _cacheExpiration);
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error searching for {modType} '{searchQuery}': {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Generic method for making API calls with rate limiting, caching, and error handling.
    /// </summary>
    /// <typeparam name="T">The type of response expected from the API.</typeparam>
    /// <param name="url">The API endpoint URL.</param>
    /// <param name="cacheKey">The cache key for storing results.</param>
    /// <param name="isDataWrapped">Whether the response data is wrapped in a 'data' property.</param>
    /// <param name="logContext">Context information for logging errors.</param>
    /// <returns>The deserialized API response.</returns>
    private async Task<T?> MakeApiCallAsync<T>(
        string url,
        string cacheKey,
        bool isDataWrapped = false,
        string? logContext = null
    )
    {
        try
        {
            await rateLimitService.WaitForApiCallAsync();

            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var context = logContext ?? "API request";
                Console.WriteLine($"API request failed for {context}: {response.StatusCode}");
                return default;
            }

            var jsonContent = await response.Content.ReadAsStringAsync();

            if (!isDataWrapped)
            {
                return JsonSerializer.Deserialize<T>(jsonContent, _jsonOptions);
            }

            var jsonDoc = JsonDocument.Parse(jsonContent);
            if (
                !jsonDoc.RootElement.TryGetProperty("success", out var successElement)
                || !successElement.GetBoolean()
                || !jsonDoc.RootElement.TryGetProperty("data", out var dataElement)
            )
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(dataElement.GetRawText(), _jsonOptions);
        }
        catch (Exception ex)
        {
            var context = logContext ?? "API request";
            Console.WriteLine($"Error in {context}: {ex.Message}");
            return default;
        }
    }
}
