using System.Text.Json.Serialization;

namespace CheckMods.Models;

/// <summary>
/// Represents the required data in the SPT core configuration file (core.json).
/// </summary>
public class CoreConfig
{
    /// <summary>
    /// The SPT version string (e.g., "3.11.3").
    /// </summary>
    [JsonPropertyName("sptVersion")]
    public string? SptVersion { get; set; }
}