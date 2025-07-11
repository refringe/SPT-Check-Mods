using System.Text.Json.Serialization;

namespace CheckMods.Models;

/// <summary>
/// Response from the Forge API authentication abilities endpoint.
/// </summary>
public class AuthAbilitiesResponse
{
    /// <summary>
    /// Whether the API call was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    /// <summary>
    /// List of abilities/permissions for the authenticated user.
    /// </summary>
    [JsonPropertyName("data")]
    public List<string>? Data { get; set; }
}

/// <summary>
/// Response from the Forge API mod search endpoint.
/// </summary>
public class ModSearchApiResponse
{
    /// <summary>
    /// Whether the API call was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    /// <summary>
    /// List of mod search results.
    /// </summary>
    [JsonPropertyName("data")]
    public List<ModSearchResult>? Data { get; set; }
}

/// <summary>
/// Represents a mod search result from the Forge API.
/// </summary>
public class ModSearchResult
{
    /// <summary>
    /// The unique identifier of the mod.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    /// <summary>
    /// The hub identifier of the mod.
    /// </summary>
    [JsonPropertyName("hub_id")]
    public int HubId { get; set; }
    
    /// <summary>
    /// The name of the mod.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The URL slug of the mod.
    /// </summary>
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;
    
    /// <summary>
    /// A short description or teaser for the mod.
    /// </summary>
    [JsonPropertyName("teaser")]
    public string? Teaser { get; set; }
    
    /// <summary>
    /// URL to the mod's thumbnail image.
    /// </summary>
    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }
    
    /// <summary>
    /// The number of downloads for this mod.
    /// </summary>
    [JsonPropertyName("downloads")]
    public int Downloads { get; set; }
    
    /// <summary>
    /// URL to the mod's source code repository.
    /// </summary>
    [JsonPropertyName("source_code_url")]
    public string? SourceCodeUrl { get; set; }
    
    /// <summary>
    /// URL to the mod's detail page.
    /// </summary>
    [JsonPropertyName("detail_url")]
    public string? DetailUrl { get; set; }
    
    /// <summary>
    /// The author/owner of the mod.
    /// </summary>
    [JsonPropertyName("owner")]
    public ModAuthor? Owner { get; set; }
    
    /// <summary>
    /// List of available versions for this mod.
    /// </summary>
    [JsonPropertyName("versions")]
    public List<ModVersion>? Versions { get; set; }
}

/// <summary>
/// Represents the author/owner of a mod.
/// </summary>
public class ModAuthor
{
    /// <summary>
    /// The unique identifier of the author.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    /// <summary>
    /// The display name of the author.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// URL to the author's profile photo.
    /// </summary>
    [JsonPropertyName("profile_photo_url")]
    public string? ProfilePhotoUrl { get; set; }
}

/// <summary>
/// Represents a specific version of a mod.
/// </summary>
public class ModVersion
{
    /// <summary>
    /// The unique identifier of this version.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    /// <summary>
    /// The hub identifier this version belongs to.
    /// </summary>
    [JsonPropertyName("hub_id")]
    public int? HubId { get; set; }
    
    /// <summary>
    /// The version string (e.g., "1.0.0").
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// Description of changes or features in this version.
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    /// <summary>
    /// Direct download link for this version.
    /// </summary>
    [JsonPropertyName("link")]
    public string? Link { get; set; }
    
    /// <summary>
    /// SPT version compatibility constraint (e.g., ">=3.9.0").
    /// </summary>
    [JsonPropertyName("spt_version_constraint")]
    public string SptVersionConstraint { get; set; } = string.Empty;
    
    /// <summary>
    /// Link to VirusTotal scan results for this version.
    /// </summary>
    [JsonPropertyName("virus_total_link")]
    public string? VirusTotalLink { get; set; }
    
    /// <summary>
    /// Number of downloads for this specific version.
    /// </summary>
    [JsonPropertyName("downloads")]
    public int Downloads { get; set; }
    
    /// <summary>
    /// ISO 8601 timestamp when this version was published.
    /// </summary>
    [JsonPropertyName("published_at")]
    public string? PublishedAt { get; set; }
    
    /// <summary>
    /// ISO 8601 timestamp when this version was created.
    /// </summary>
    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; set; }
    
    /// <summary>
    /// ISO 8601 timestamp when this version was last updated.
    /// </summary>
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; set; }
}

/// <summary>
/// Response from the Forge API mod versions' endpoint.
/// </summary>
public class ModVersionsApiResponse
{
    /// <summary>
    /// Whether the API call was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }
    
    /// <summary>
    /// List of mod versions returned by the API.
    /// </summary>
    [JsonPropertyName("data")]
    public List<ModVersion>? Data { get; set; }
}