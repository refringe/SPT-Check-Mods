using System.Reflection;
using System.Runtime.Loader;
using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

namespace CheckMods.Services;

/// <summary>
/// Unified service for scanning both server and client mods from disk.
/// Returns Mod objects directly with validation warnings populated.
/// </summary>
public sealed class ModScannerService(IOptions<ModScannerOptions> options, ILogger<ModScannerService> logger)
    : IModScannerService
{
    private readonly ModScannerOptions _options = options.Value;

    /// <inheritdoc />
    public List<Mod> ScanServerMods(string sptPath, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Scanning server mods at: {SptPath}", sptPath);

        var modsDir = Path.Combine(sptPath, "SPT", "user", "mods");
        List<Mod> mods = [];

        if (!Directory.Exists(modsDir))
        {
            logger.LogDebug("Server mods directory not found: {ModsDir}", modsDir);
            AnsiConsole.MarkupLine("[grey]Scanning server mods... none found.[/]");
            return mods;
        }

        var modDirs = Directory.GetDirectories(modsDir);
        logger.LogDebug("Found {DirCount} mod directories", modDirs.Length);
        AnsiConsole.MarkupLine($"[grey]Scanning {modDirs.Length} mod directories for server mods...[/]");

        foreach (var modDir in modDirs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dllFiles = Directory.GetFiles(modDir, "*.dll", SearchOption.TopDirectoryOnly);

            foreach (var dllPath in dllFiles)
            {
                try
                {
                    var mod = ExtractServerModMetadata(dllPath, sptPath);
                    if (mod is null)
                    {
                        continue;
                    }

                    mods.Add(mod);
                    break; // Only one mod per directory
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine(
                        $"[orange1]Warning:[/] Could not read mod DLL [grey]{Path.GetFileName(dllPath)}[/]. Reason: {ex.Message.EscapeMarkup()}"
                    );
                }
            }
        }

        logger.LogInformation("Found {ModCount} server mods", mods.Count);
        AnsiConsole.MarkupLine($"[grey]Found {mods.Count} server mods.[/]");
        return mods;
    }

    /// <inheritdoc />
    public async Task<List<Mod>> ScanClientModsAsync(string sptPath, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Scanning client mods at: {SptPath}", sptPath);

        var pluginsDir = Path.Combine(sptPath, "BepInEx", "plugins");
        List<Mod> mods = [];

        if (!Directory.Exists(pluginsDir))
        {
            logger.LogWarning("BepInEx plugins directory not found: {PluginsDir}", pluginsDir);
            AnsiConsole.MarkupLine(
                $"[yellow]Warning: BepInEx plugins directory not found: {pluginsDir.EscapeMarkup()}[/]"
            );
            return mods;
        }

        var dllFiles = GetValidClientDllFiles(pluginsDir);
        if (dllFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No DLL files found in plugins directory.[/]");
            return mods;
        }

        AnsiConsole.MarkupLine($"[grey]Scanning {dllFiles.Count} DLL files for BepInEx plugins...[/]");

        // Group DLLs by their parent directory
        var dllsByDirectory = GroupDllsByDirectory(dllFiles, pluginsDir);

        // Process loose DLLs (directly in plugins folder) as individual mods
        if (dllsByDirectory.TryGetValue(pluginsDir, out var looseDlls))
        {
            var looseResults = await ProcessClientDllsInParallelAsync(looseDlls, cancellationToken);
            mods.AddRange(FilterDuplicateClientMods(looseResults));
            dllsByDirectory.Remove(pluginsDir);
        }

        // Process DLLs in subdirectories - consolidate each directory into a single mod
        var consolidatedMods = dllsByDirectory
            .Select(kvp =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ConsolidateDirectoryMods(kvp.Key, kvp.Value);
            })
            .Where(mod => mod is not null)
            .Cast<Mod>();

        mods.AddRange(consolidatedMods);

        logger.LogInformation("Found {ModCount} client mods", mods.Count);
        AnsiConsole.MarkupLine($"[grey]Found {mods.Count} client mods.[/]");
        return mods;
    }

    /// <summary>
    /// Groups DLL files by their immediate parent directory.
    /// </summary>
    private static Dictionary<string, List<string>> GroupDllsByDirectory(List<string> dllFiles, string pluginsDir) =>
        dllFiles
            .GroupBy(dllPath => GetModDirectory(dllPath, pluginsDir), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the mod's root directory (immediate child of plugins, or plugins itself for loose DLLs).
    /// </summary>
    private static string GetModDirectory(string dllPath, string pluginsDir)
    {
        var directory = Path.GetDirectoryName(dllPath);

        if (directory is null || directory.Equals(pluginsDir, StringComparison.OrdinalIgnoreCase))
        {
            return pluginsDir;
        }

        // Walk up to find the immediate child of plugins
        while (true)
        {
            var parent = Path.GetDirectoryName(directory);
            if (parent is null || parent.Equals(pluginsDir, StringComparison.OrdinalIgnoreCase))
            {
                return directory;
            }

            directory = parent;
        }
    }

    /// <summary>
    /// Consolidates multiple DLLs from a single directory into one mod entry.
    /// </summary>
    private Mod? ConsolidateDirectoryMods(string directory, List<string> dllPaths)
    {
        // Extract metadata from all DLLs
        List<(string DllPath, BepInPluginAttribute Plugin)> allPlugins = [];

        foreach (var dllPath in dllPaths)
        {
            try
            {
                using var loadContext = CreateMetadataLoadContext(dllPath);
                var assembly = loadContext.LoadFromAssemblyPath(dllPath);
                var plugin = ScanAssemblyForBepInPluginAttribute(assembly);

                if (plugin is null)
                {
                    continue;
                }

                allPlugins.Add((dllPath, plugin));
            }
            catch
            {
                // Skip DLLs that can't be read
            }
        }

        if (allPlugins.Count == 0)
        {
            return null;
        }

        // Select the primary plugin
        var directoryName = Path.GetFileName(directory);
        var (primaryDll, primaryPlugin) = SelectPrimaryPlugin(allPlugins, directoryName);

        // Create the mod from the primary plugin
        var mod = CreateModFromBepInPlugin(primaryPlugin, primaryDll);

        // Add alternate GUIDs from other plugins
        var alternateGuids = allPlugins
            .Select(p => p.Plugin.Guid)
            .Where(guid => !guid.Equals(primaryPlugin.Guid, StringComparison.OrdinalIgnoreCase))
            .Except(mod.AlternateGuids, StringComparer.OrdinalIgnoreCase);

        foreach (var guid in alternateGuids)
        {
            mod.AlternateGuids.Add(guid);
        }

        return mod;
    }

    /// <summary>
    /// Selects the primary plugin from a list of plugins in the same directory.
    /// Priority: filename matches directory name > "Core" in name > shortest GUID > alphabetically first.
    /// </summary>
    private static (string DllPath, BepInPluginAttribute Plugin) SelectPrimaryPlugin(
        List<(string DllPath, BepInPluginAttribute Plugin)> plugins,
        string directoryName
    )
    {
        // Normalize directory name for comparison (remove common prefixes like author names)
        var normalizedDirName = NormalizeModName(directoryName);

        // Try to find a plugin whose filename matches the directory name
        var directoryMatch = plugins
            .Where(p =>
            {
                var fileName = Path.GetFileNameWithoutExtension(p.DllPath);
                return fileName.Equals(directoryName, StringComparison.OrdinalIgnoreCase)
                    || fileName.Contains(normalizedDirName, StringComparison.OrdinalIgnoreCase)
                    || NormalizeModName(fileName).Equals(normalizedDirName, StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(p => Path.GetFileNameWithoutExtension(p.DllPath).Length)
            .FirstOrDefault();

        if (directoryMatch.Plugin is not null)
        {
            return directoryMatch;
        }

        // Try to find a "Core" plugin
        var corePlugin = plugins
            .Where(p =>
            {
                var fileName = Path.GetFileNameWithoutExtension(p.DllPath);
                return fileName.Contains("Core", StringComparison.OrdinalIgnoreCase)
                    || p.Plugin.Name.Contains("Core", StringComparison.OrdinalIgnoreCase);
            })
            .FirstOrDefault();

        if (corePlugin.Plugin is not null)
        {
            return corePlugin;
        }

        // Fall back to the plugin with the shortest/simplest GUID, then alphabetically
        return plugins
            .OrderBy(p => p.Plugin.Guid.Split('.').Length)
            .ThenBy(p => p.Plugin.Guid.Length)
            .ThenBy(p => p.Plugin.Name, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    /// <summary>
    /// Normalizes a mod name by removing common prefixes and suffixes.
    /// </summary>
    private static string NormalizeModName(string name)
    {
        // Remove common author-name prefixes (e.g., "kmyuhkyuk-GamePanelHUD" -> "GamePanelHUD")
        var dashIndex = name.IndexOf('-');
        if (dashIndex > 0 && dashIndex < name.Length - 1)
        {
            name = name[(dashIndex + 1)..];
        }

        // Remove common suffixes
        string[] suffixes = ["Client", "Plugin", "Mod", "BepInEx"];
        var matchingSuffix = suffixes.FirstOrDefault(s => name.EndsWith(s, StringComparison.OrdinalIgnoreCase));
        if (matchingSuffix is not null)
        {
            name = name[..^matchingSuffix.Length];
        }

        return name.Trim();
    }

    /// <summary>
    /// Scans an assembly for a BepInPlugin attribute and returns it (without creating a Mod).
    /// </summary>
    private static BepInPluginAttribute? ScanAssemblyForBepInPluginAttribute(Assembly assembly)
    {
        foreach (var type in assembly.GetTypes())
        {
            try
            {
                var bepInPlugin = ExtractBepInPluginAttribute(type);
                if (bepInPlugin is null)
                {
                    continue;
                }

                return bepInPlugin;
            }
            catch
            {
                // Skip types that can't be inspected
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<(List<Mod> ServerMods, List<Mod> ClientMods)> ScanAllModsAsync(
        string sptPath,
        CancellationToken cancellationToken = default
    )
    {
        var serverMods = ScanServerMods(sptPath, cancellationToken);
        var clientMods = await ScanClientModsAsync(sptPath, cancellationToken);
        return (serverMods, clientMods);
    }

    /// <inheritdoc />
    public string? GetSptVersion(string sptPath)
    {
        var coreDllPath = Path.Combine(sptPath, "SPT", "SPTarkov.Server.Core.dll");

        if (!File.Exists(coreDllPath))
        {
            return null;
        }

        try
        {
            var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(coreDllPath);
            return versionInfo.FileVersion;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[orange1]Warning:[/] Could not read SPT version. Reason: {ex.Message.EscapeMarkup()}"
            );
        }

        return null;
    }

    #region Server Mod Extraction

    private Mod? ExtractServerModMetadata(string dllPath, string sptDirectory)
    {
        var loadContext = new SptAssemblyLoadContext(sptDirectory);

        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(dllPath);
            var metadata = LoadSptMetadataFromAssembly(assembly);

            if (metadata is null)
            {
                return null;
            }

            // Use reflection to access properties since the type is from a different load context
            var metadataType = metadata.GetType();
            var modGuid = metadataType.GetProperty("ModGuid")?.GetValue(metadata)?.ToString();
            var name = metadataType.GetProperty("Name")?.GetValue(metadata)?.ToString();
            var author = metadataType.GetProperty("Author")?.GetValue(metadata)?.ToString();
            var modVersion = metadataType.GetProperty("Version")?.GetValue(metadata)?.ToString();
            var sptVersion = metadataType.GetProperty("SptVersion")?.GetValue(metadata)?.ToString();

            if (string.IsNullOrEmpty(modGuid))
            {
                return null;
            }

            var version = modVersion ?? GetAssemblyVersion(assembly);

            var warnings = ValidateModMetadata(name ?? string.Empty, author ?? string.Empty, version, modGuid);

            return new Mod
            {
                Guid = modGuid,
                FilePath = dllPath,
                IsServerMod = true,
                LocalName = name ?? string.Empty,
                LocalAuthor = author ?? string.Empty,
                LocalVersion = version,
                LocalSptVersion = sptVersion,
                LoadWarnings = warnings,
            };
        }
        finally
        {
            loadContext.Unload();
        }
    }

    private static object? LoadSptMetadataFromAssembly(Assembly assembly)
    {
        var types = assembly.Modules.SelectMany(m => m.GetTypes());
        var metadataType = types.FirstOrDefault(t => t.BaseType?.Name == "AbstractModMetadata" && !t.IsAbstract);

        if (metadataType is null)
        {
            return null;
        }

        return Activator.CreateInstance(metadataType);
    }

    private static string GetAssemblyVersion(Assembly assembly)
    {
        var infoVersionAttr = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();

        if (infoVersionAttr is null)
        {
            var version = assembly.GetName().Version;
            if (version is null)
            {
                return string.Empty;
            }

            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        var fullVersion = infoVersionAttr.InformationalVersion;
        if (string.IsNullOrEmpty(fullVersion))
        {
            var version = assembly.GetName().Version;
            if (version is null)
            {
                return string.Empty;
            }

            return $"{version.Major}.{version.Minor}.{version.Build}";
        }

        var plusIndex = fullVersion.IndexOf('+');
        if (plusIndex > 0)
        {
            fullVersion = fullVersion[..plusIndex];
        }

        return fullVersion;
    }

    /// <summary>
    /// Custom AssemblyLoadContext that resolves SPT assemblies from the SPT directory.
    /// </summary>
    private sealed class SptAssemblyLoadContext(string sptDirectory) : AssemblyLoadContext(true)
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Try to find the assembly in the SPT directory
            var sptPath = Path.Combine(sptDirectory, "SPT", $"{assemblyName.Name}.dll");
            if (File.Exists(sptPath))
            {
                return LoadFromAssemblyPath(sptPath);
            }

            return null;
        }
    }

    #endregion

    #region Client Mod Extraction

    private List<string> GetValidClientDllFiles(string pluginsPath)
    {
        var sptDir = Path.Combine(pluginsPath, "spt");

        return Directory
            .GetFiles(pluginsPath, "*.dll", SearchOption.AllDirectories)
            .Where(file => !file.StartsWith(sptDir, StringComparison.OrdinalIgnoreCase))
            .Where(file => new FileInfo(file).Length <= _options.MaxDllSizeBytes)
            .ToList();
    }

    private async Task<List<Mod>> ProcessClientDllsInParallelAsync(
        List<string> dllFiles,
        CancellationToken cancellationToken = default
    )
    {
        var tasks = dllFiles.Select(async dllPath =>
        {
            try
            {
                return await Task.Run(() => ExtractClientModMetadata(dllPath), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]Warning: Failed to scan {Path.GetFileName(dllPath)}: {ex.Message.EscapeMarkup()}[/]"
                );
                return null;
            }
        });

        var results = await Task.WhenAll(tasks);
        return results.Where(r => r is not null).Cast<Mod>().ToList();
    }

    private static List<Mod> FilterDuplicateClientMods(List<Mod> mods) =>
        mods
            .DistinctBy(m => (m.LocalName.ToLowerInvariant(), m.LocalAuthor.ToLowerInvariant()))
            .ToList();

    private Mod? ExtractClientModMetadata(string dllPath)
    {
        try
        {
            using var loadContext = CreateMetadataLoadContext(dllPath);
            var assembly = loadContext.LoadFromAssemblyPath(dllPath);

            return ScanAssemblyForBepInPlugin(assembly, dllPath);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Warning: Failed to extract BepInPlugin from {Path.GetFileName(dllPath)}: {ex.Message.EscapeMarkup()}[/]"
            );
            return null;
        }
    }

    private static MetadataLoadContext CreateMetadataLoadContext(string dllPath)
    {
        var resolver = new AssemblyResolver(dllPath);
        return new MetadataLoadContext(resolver);
    }

    private Mod? ScanAssemblyForBepInPlugin(Assembly assembly, string dllPath)
    {
        foreach (var type in assembly.GetTypes())
        {
            try
            {
                var bepInPlugin = ExtractBepInPluginAttribute(type);
                if (bepInPlugin is null)
                {
                    continue;
                }

                return CreateModFromBepInPlugin(bepInPlugin, dllPath);
            }
            catch
            {
                // Skip types that can't be inspected
            }
        }

        return null;
    }

    private static BepInPluginAttribute? ExtractBepInPluginAttribute(Type type)
    {
        var customAttributes = type.GetCustomAttributesData();

        var bepInPluginAttribute = customAttributes.FirstOrDefault(attr =>
            attr.AttributeType.Name is "BepInPlugin" or "BepInPluginAttribute"
            || (attr.AttributeType.FullName?.Contains("BepInPlugin") ?? false)
        );

        if (bepInPluginAttribute is null || bepInPluginAttribute.ConstructorArguments.Count < 3)
        {
            return null;
        }

        var guid = bepInPluginAttribute.ConstructorArguments[0].Value?.ToString() ?? "";
        var name = bepInPluginAttribute.ConstructorArguments[1].Value?.ToString() ?? "";
        var version = bepInPluginAttribute.ConstructorArguments[2].Value?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        return new BepInPluginAttribute(guid, name, version);
    }

    private Mod CreateModFromBepInPlugin(BepInPluginAttribute plugin, string dllPath)
    {
        var (author, name) = ParseAuthorAndName(plugin.Name, plugin.Guid);

        var warnings = ValidateModMetadata(name, author, plugin.Version, plugin.Guid);

        return new Mod
        {
            Guid = plugin.Guid,
            FilePath = dllPath,
            IsServerMod = false,
            LocalName = name,
            LocalAuthor = author,
            LocalVersion = plugin.Version,
            LocalSptVersion = null,
            LoadWarnings = warnings,
        };
    }

    /// <summary>
    /// Parses author and name from BepInEx plugin metadata.
    /// </summary>
    private static (string Author, string Name) ParseAuthorAndName(string pluginName, string guid)
    {
        // Try to extract author from formats like "AuthorName - ModName" or "ModName by AuthorName"
        if (pluginName.Contains(" - "))
        {
            var parts = pluginName.Split(" - ", 2);
            if (parts.Length == 2)
            {
                return (parts[0].Trim(), parts[1].Trim());
            }
        }

        if (pluginName.Contains(" by ", StringComparison.OrdinalIgnoreCase))
        {
            var byIndex = pluginName.IndexOf(" by ", StringComparison.OrdinalIgnoreCase);
            var name = pluginName[..byIndex].Trim();
            var author = pluginName[(byIndex + 4)..].Trim();
            return (author, name);
        }

        // Try to extract the author from GUID (e.g., "com.author.modname")
        var guidParts = guid.Split('.');
        if (guidParts.Length < 2)
        {
            return ("Unknown", pluginName);
        }

        // Assume the second-to-last part is author for GUIDs like "com.author.modname"
        var potentialAuthor = guidParts.Length >= 3 ? guidParts[^2] : guidParts[0];

        // Don't use common prefixes as author names
        if (
            string.Equals(potentialAuthor, "com", StringComparison.OrdinalIgnoreCase)
            || string.Equals(potentialAuthor, "org", StringComparison.OrdinalIgnoreCase)
            || string.Equals(potentialAuthor, "spt", StringComparison.OrdinalIgnoreCase)
            || string.Equals(potentialAuthor, "aki", StringComparison.OrdinalIgnoreCase)
        )
        {
            return ("Unknown", pluginName);
        }

        return (potentialAuthor, pluginName);
    }

    #endregion

    #region Validation

    private static List<string> ValidateModMetadata(string name, string author, string version, string guid)
    {
        List<string> warnings = [];

        if (string.IsNullOrWhiteSpace(name))
        {
            warnings.Add("Missing mod name");
        }

        if (string.IsNullOrWhiteSpace(author))
        {
            warnings.Add("Missing author");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            warnings.Add("Missing version");
        }
        else if (!IsValidVersion(version))
        {
            warnings.Add($"Invalid version format: {version}");
        }

        if (string.IsNullOrWhiteSpace(guid))
        {
            warnings.Add("Missing GUID");
        }

        return warnings;
    }

    private static bool IsValidVersion(string version)
    {
        try
        {
            _ = new SemanticVersioning.Version(version);
            return true;
        }
        catch
        {
            return false;
        }
    }

    #endregion
}
