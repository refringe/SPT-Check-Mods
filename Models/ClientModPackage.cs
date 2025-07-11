namespace CheckMods.Models;

/// <summary>
/// Represents a client-side mod package (BepInEx plugin) with metadata extracted from DLL attributes.
/// </summary>
public class ClientModPackage
{
    /// <summary>
    /// The name of the client mod.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The author of the client mod (parsed from plugin name or GUID).
    /// </summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>
    /// The version of the client mod.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// The file path to the DLL containing this plugin.
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// The BepInEx plugin GUID.
    /// </summary>
    public string PluginGuid { get; set; } = string.Empty;

    /// <summary>
    /// Creates a ClientModPackage from a BepInEx plugin attribute.
    /// </summary>
    /// <param name="plugin">The BepInEx plugin attribute containing metadata.</param>
    /// <param name="filePath">The file path to the DLL containing the plugin.</param>
    /// <returns>A new ClientModPackage instance.</returns>
    public static ClientModPackage FromBepInPlugin(BepInPluginAttribute plugin, string filePath)
    {
        var (author, name) = ParseAuthorAndName(plugin.Name, plugin.Guid);

        return new ClientModPackage
        {
            Name = name,
            Author = author,
            Version = plugin.Version,
            FilePath = filePath,
            PluginGuid = plugin.Guid,
        };
    }

    /// <summary>
    /// Parses author and mod name from various formats commonly used in BepInEx plugins. Tries multiple strategies:
    /// dash separator, dot separator, GUID parsing, and filename parsing.
    /// </summary>
    /// <param name="fullName">The full plugin name from the BepInPlugin attribute.</param>
    /// <param name="guid">The plugin GUID which may contain author information.</param>
    /// <returns>A tuple containing the parsed author and mod name.</returns>
    private static (string Author, string Name) ParseAuthorAndName(string fullName, string guid)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return ("", "Unknown");

        // Try to parse Author-ModName format first (dash separator)
        var dashIndex = fullName.IndexOf('-');
        if (dashIndex > 0 && dashIndex < fullName.Length - 1)
        {
            var author = fullName[..dashIndex].Trim();
            var name = fullName[(dashIndex + 1)..].Trim();
            return (author, name);
        }

        // Try to parse Author.ModName format (dot separator)
        var dotIndex = fullName.IndexOf('.');
        if (dotIndex > 0 && dotIndex < fullName.Length - 1)
        {
            var author = fullName[..dotIndex].Trim();
            var name = fullName[(dotIndex + 1)..].Trim();

            // Make sure the author part isn't a common prefix or too short
            if (
                author.Length > 2
                && !author.Equals("com", StringComparison.OrdinalIgnoreCase)
                && !author.Equals("dev", StringComparison.OrdinalIgnoreCase)
                && !author.Equals("mod", StringComparison.OrdinalIgnoreCase)
            )
            {
                return (author, name);
            }
        }

        // If no separator in name, try to extract author from GUID
        if (!string.IsNullOrWhiteSpace(guid))
        {
            // GUID often contains author info like "DrakiaXYZ.ModName" or "acidphantasm.modname"
            var guidParts = guid.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (guidParts.Length >= 2)
            {
                var potentialAuthor = guidParts[0].Trim();

                // Filter out common system prefixes
                if (
                    !potentialAuthor.Equals("com", StringComparison.OrdinalIgnoreCase)
                    && !potentialAuthor.Equals("dev", StringComparison.OrdinalIgnoreCase)
                    && !potentialAuthor.Equals("mod", StringComparison.OrdinalIgnoreCase)
                    && potentialAuthor.Length > 2
                )
                {
                    return (potentialAuthor, fullName.Trim());
                }
            }

            // Try dash in GUID as a fallback
            var guidDashIndex = guid.IndexOf('-');
            if (guidDashIndex > 0 && guidDashIndex < guid.Length - 1)
            {
                var guidAuthor = guid[..guidDashIndex].Trim();
                if (guidAuthor.Length > 2)
                {
                    return (guidAuthor, fullName.Trim());
                }
            }
        }

        // Last resort: use filename to extract author
        var fileName = Path.GetFileNameWithoutExtension(Path.GetFileName(fullName));

        // Try dash in filename
        var fileNameDashIndex = fileName.IndexOf('-');
        if (fileNameDashIndex > 0 && fileNameDashIndex < fileName.Length - 1)
        {
            var fileAuthor = fileName[..fileNameDashIndex].Trim();
            if (fileAuthor.Length > 2)
            {
                return (fileAuthor, fullName.Trim());
            }
        }

        // Try dot in the filename
        var fileNameDotIndex = fileName.IndexOf('.');
        if (fileNameDotIndex > 0 && fileNameDotIndex < fileName.Length - 1)
        {
            var fileAuthor = fileName[..fileNameDotIndex].Trim();
            if (
                fileAuthor.Length > 2
                && !fileAuthor.Equals("com", StringComparison.OrdinalIgnoreCase)
                && !fileAuthor.Equals("dev", StringComparison.OrdinalIgnoreCase)
                && !fileAuthor.Equals("mod", StringComparison.OrdinalIgnoreCase)
            )
            {
                return (fileAuthor, fullName.Trim());
            }
        }

        // No author found, use empty string and full name as mod name
        return ("", fullName.Trim());
    }
}
