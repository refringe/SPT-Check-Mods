using System.Text.Json.Serialization;

namespace CheckMods.Models;

/// <summary>
/// Response from the Forge API SPT versions endpoint.
/// </summary>
public class SptVersionApiResponse
{
    /// <summary>
    /// Whether the API call was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// List of available SPT versions returned by the API.
    /// </summary>
    [JsonPropertyName("data")]
    public List<SptVersionResult>? Data { get; set; }
}

/// <summary>
/// Represents an SPT version from the Forge API.
/// </summary>
public class SptVersionResult
{
    /// <summary>
    /// The unique identifier of the SPT version.
    /// </summary>
    [JsonPropertyName("id")]
    public int Id { get; set; }

    /// <summary>
    /// The full version string (e.g., "3.9.0").
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// The major version number.
    /// </summary>
    [JsonPropertyName("version_major")]
    public int VersionMajor { get; set; }

    /// <summary>
    /// The minor version number.
    /// </summary>
    [JsonPropertyName("version_minor")]
    public int VersionMinor { get; set; }

    /// <summary>
    /// The patch version number.
    /// </summary>
    [JsonPropertyName("version_patch")]
    public int VersionPatch { get; set; }

    /// <summary>
    /// Additional version labels (e.g., "beta", "rc").
    /// </summary>
    [JsonPropertyName("version_labels")]
    public string VersionLabels { get; set; } = string.Empty;

    /// <summary>
    /// The number of mods available for this SPT version.
    /// </summary>
    [JsonPropertyName("mod_count")]
    public int ModCount { get; set; }

    /// <summary>
    /// Download or detail link for this SPT version.
    /// </summary>
    [JsonPropertyName("link")]
    public string? Link { get; set; }

    /// <summary>
    /// CSS color class for UI display purposes.
    /// </summary>
    [JsonPropertyName("color_class")]
    public string? ColorClass { get; set; }

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
