namespace CheckMods.Models;

/// <summary>
/// Represents a BepInEx plugin attribute containing metadata about a client mod.
/// </summary>
/// <param name="guid">The unique identifier for the plugin.</param>
/// <param name="name">The display name of the plugin.</param>
/// <param name="version">The version of the plugin.</param>
public sealed class BepInPluginAttribute(string guid, string name, string version)
{
    public string Guid { get; init; } = guid;

    public string Name { get; init; } = name;

    public string Version { get; init; } = version;
}
