using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using CheckMods.Utils;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneOf;

namespace CheckMods.Services;

/// <summary>
/// Service for interacting with the Forge API with rate limiting. Handles mod searching, version validation, and data
/// retrieval. The Forge API is open and read-only, so no authentication is required.
/// </summary>
/// <remarks>
/// This service is NOT decorated with [Injectable] because it requires special registration via AddHttpClient
/// for proper HttpClient lifecycle management. It is registered manually in ServiceCollectionExtensions.
/// </remarks>
public sealed partial class ForgeApiService(
    HttpClient httpClient,
    IRateLimitService rateLimitService,
    IMemoryCache cache,
    IOptions<ForgeApiOptions> options,
    ILogger<ForgeApiService> logger
) : IForgeApiService
{
    private readonly ForgeApiOptions _options = options.Value;

    /// <summary>
    /// Maximum number of mods sent in a single batch updates request. Mods are chunked so the request URL stays
    /// comfortably within server and proxy length limits, even for installs with many mods.
    /// </summary>
    private const int MaxModsPerUpdateRequest = 50;

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Cache lifetime for API responses. Comfortably longer than a single run so identical requests within a run are
    /// served from cache, while still bounding memory if the process is unusually long-lived.
    /// </summary>
    private static readonly MemoryCacheEntryOptions _cacheEntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
    };

    /// <summary>
    /// A cached HTTP response: the status code and body text from a completed request.
    /// </summary>
    private sealed record CachedResponse(HttpStatusCode StatusCode, string Body)
    {
        public bool IsSuccessStatusCode
        {
            get { return (int)StatusCode is >= 200 and < 300; }
        }
    }

    /// <summary>
    /// Issues a rate-limited GET request and returns its status code and body. Successful (2xx) and NotFound (404)
    /// responses are cached by URL so identical requests within a run are served from cache rather than repeated.
    /// Server errors are not cached, so a later call can retry them.
    /// </summary>
    /// <param name="url">The request URL, which doubles as the cache key.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    private async Task<CachedResponse> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(url, out CachedResponse? cached) && cached is not null)
        {
            logger.LogDebug("Cache hit: GET {Url}", url);
            return cached;
        }

        logger.LogDebug("API Request: GET {Url}", url);
        var response = await rateLimitService.ExecuteWithRetryAsync(
            () => httpClient.GetAsync(url, cancellationToken),
            cancellationToken
        );

        var statusCode = response.StatusCode;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        response.Dispose();

        var result = new CachedResponse(statusCode, body);
        if (result.IsSuccessStatusCode || statusCode == HttpStatusCode.NotFound)
        {
            cache.Set(url, result, _cacheEntryOptions);
        }

        return result;
    }

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

            var response = await GetJsonAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("SPT version validation failed: {StatusCode}", response.StatusCode);
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var apiResponse = JsonSerializer.Deserialize<SptVersionApiResponse>(response.Body, _jsonOptions);

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

            var response = await GetJsonAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Failed to fetch SPT versions: {StatusCode}", response.StatusCode);
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var apiResponse = JsonSerializer.Deserialize<SptVersionApiResponse>(response.Body, _jsonOptions);

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

            var response = await GetJsonAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new NotFound();
            }

            if (!response.IsSuccessStatusCode)
            {
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            using var jsonDoc = JsonDocument.Parse(response.Body);

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

            var response = await GetJsonAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var apiResponse = JsonSerializer.Deserialize<ModSearchApiResponse>(response.Body, _jsonOptions);

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
                SemVer.SatisfiesRange(v.SptVersionConstraint, sptVersion)
            );

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
                $"{_options.BaseUrl}mods?query={Uri.EscapeDataString(searchQuery)}&filter[spt_version]={sptVersion}&include=versions,source_code_links&per_page=50";

            var response = await GetJsonAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var apiResponse = JsonSerializer.Deserialize<ModSearchApiResponse>(response.Body, _jsonOptions);

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

        // Common case: the whole batch fits in one request, so call it directly and return its result unchanged,
        // skipping the cross-chunk merge (which would otherwise allocate four lists and copy every entry across).
        if (modList.Count <= MaxModsPerUpdateRequest)
        {
            return await GetModUpdatesChunkAsync(modList, sptVersion, cancellationToken);
        }

        // Larger batches are split so the request URL stays within length limits. Dispatch the chunks concurrently
        // and let the shared rate limiter throttle them, rather than waiting for each round-trip before the next.
        var chunkResults = await Task.WhenAll(
            modList
                .Chunk(MaxModsPerUpdateRequest)
                .Select(chunk => GetModUpdatesChunkAsync(chunk, sptVersion, cancellationToken))
        );

        // Combine results across chunks.
        var safeToUpdate = new List<SafeToUpdateMod>();
        var blocked = new List<BlockedUpdateMod>();
        var upToDate = new List<UpToDateMod>();
        var incompatible = new List<IncompatibleMod>();
        var anyData = false;

        foreach (var chunkResult in chunkResults)
        {
            // The request is atomic: surfacing only the merged successes would silently hide updates for every mod in
            // a failed chunk, so a single chunk error fails the whole call.
            if (chunkResult.TryPickT2(out var error, out _))
            {
                logger.LogDebug(
                    "A mod-updates chunk failed ({Error}); failing the batch update request",
                    error.Message
                );
                return error;
            }

            if (!chunkResult.TryPickT0(out var data, out _))
            {
                continue;
            }

            anyData = true;
            if (data.SafeToUpdate is not null)
            {
                safeToUpdate.AddRange(data.SafeToUpdate);
            }

            if (data.Blocked is not null)
            {
                blocked.AddRange(data.Blocked);
            }

            if (data.UpToDate is not null)
            {
                upToDate.AddRange(data.UpToDate);
            }

            if (data.Incompatible is not null)
            {
                incompatible.AddRange(data.Incompatible);
            }
        }

        if (!anyData)
        {
            // Every chunk succeeded but none carried data: report a clean miss.
            return new NotFound();
        }

        return new ModUpdatesData(safeToUpdate, blocked, upToDate, incompatible);
    }

    /// <summary>
    /// Retrieves batch update information for a single chunk of mods.
    /// </summary>
    private async Task<OneOf<ModUpdatesData, NotFound, ApiError>> GetModUpdatesChunkAsync(
        IReadOnlyList<(int ModId, string CurrentVersion)> chunk,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Build mods query parameter as comma-separated "id:version" pairs
            var modsParam = string.Join(",", chunk.Select(m => $"{m.ModId}:{Uri.EscapeDataString(m.CurrentVersion)}"));

            var url = $"{_options.BaseUrl}mods/updates?mods={modsParam}&spt_version={sptVersion}";

            var response = await GetJsonAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var apiResponse = JsonSerializer.Deserialize<ModUpdatesApiResponse>(response.Body, _jsonOptions);

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

            var response = await GetJsonAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new ApiError($"API returned status {response.StatusCode}", (int)response.StatusCode);
            }

            var apiResponse = JsonSerializer.Deserialize<ModDependenciesApiResponse>(response.Body, _jsonOptions);

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
