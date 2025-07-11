using System.Text.Json.Serialization;

namespace CheckMods.Models;

/// <summary>
/// Represents a server-side mod package with metadata parsed from package.json.
/// </summary>
public class ModPackage
{
    /// <summary>
    /// The name of the mod.
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// The author of the mod.
    /// </summary>
    public string Author { get; set; } = string.Empty;
    
    /// <summary>
    /// The version of the mod.
    /// </summary>
    public string Version { get; set; } = string.Empty;
    
    /// <summary>
    /// The SPT version constraint for this mod.
    /// </summary>
    [JsonPropertyName("sptVersion")]
    public string? SptVersion { get; set; }
}