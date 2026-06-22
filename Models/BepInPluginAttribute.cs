namespace CheckMods.Models;

/// <summary>
/// Represents a BepInEx plugin attribute containing metadata about a client mod. This mirrors the BepInPlugin attribute
/// structure found in BepInEx plugins.
/// </summary>
/// <param name="guid">The unique identifier for the plugin.</param>
/// <param name="name">The display name of the plugin.</param>
/// <param name="version">The version of the plugin.</param>
public sealed class BepInPluginAttribute(string guid, string name, string version)
{
    /// <summary>
    /// The unique identifier for the plugin.
    /// </summary>
    public string Guid { get; init; } = guid;

    /// <summary>
    /// The display name of the plugin.
    /// </summary>
    public string Name { get; init; } = name;

    /// <summary>
    /// The version of the plugin.
    /// </summary>
    public string Version { get; init; } = version;
}
