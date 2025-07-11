using System.Reflection;
using CheckMods.Models;
using Spectre.Console;

namespace CheckMods.Services;

/// <summary>
/// Service responsible for scanning BepInEx plugin DLLs and extracting plugin metadata. Uses MetadataLoadContext for
/// safe reflection without loading assemblies into the main domain.
/// </summary>
public class BepInExScannerService
{
    private const int MaxDllSizeBytes = 100 * 1024 * 1024; // 100MB limit

    /// <summary>
    /// Scans a directory for BepInEx plugin DLLs and extracts their metadata.
    /// </summary>
    /// <param name="pluginsPath">Path to the BepInEx plugins directory.</param>
    /// <returns>List of discovered client mod packages.</returns>
    public List<ClientModPackage> ScanPluginsDirectory(string pluginsPath)
    {
        var clientMods = new List<ClientModPackage>();

        if (!Directory.Exists(pluginsPath))
        {
            AnsiConsole.MarkupLine(
                $"[yellow]Warning: BepInEx plugins directory not found: {pluginsPath.EscapeMarkup()}[/]"
            );
            return clientMods;
        }

        var dllFiles = GetValidDllFiles(pluginsPath);
        if (dllFiles.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No DLL files found in plugins directory.[/]");
            return clientMods;
        }

        AnsiConsole.MarkupLine($"[blue]Scanning {dllFiles.Count} DLL files for BepInEx plugins...[/]");

        var results = ProcessDllsInParallel(dllFiles);
        clientMods.AddRange(FilterDuplicateMods(results));

        AnsiConsole.MarkupLine($"[green]Found {clientMods.Count} client mods.[/]");
        return clientMods;
    }

    /// <summary>
    /// Gets valid DLL files from the plugins directory, filtering by size.
    /// </summary>
    /// <param name="pluginsPath">Path to the plugins directory.</param>
    /// <returns>List of valid DLL file paths.</returns>
    private static List<string> GetValidDllFiles(string pluginsPath)
    {
        return Directory
            .GetFiles(pluginsPath, "*.dll", SearchOption.AllDirectories)
            .Where(file => new FileInfo(file).Length <= MaxDllSizeBytes)
            .ToList();
    }

    /// <summary>
    /// Processes DLL files in parallel to extract BepInEx plugin information.
    /// </summary>
    /// <param name="dllFiles">List of DLL file paths to process.</param>
    /// <returns>List of extracted client mod packages.</returns>
    private List<ClientModPackage> ProcessDllsInParallel(List<string> dllFiles)
    {
        var tasks = dllFiles.Select(async dllPath =>
        {
            try
            {
                return await Task.Run(() => ExtractBepInPluginInfo(dllPath));
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]Warning: Failed to scan {Path.GetFileName(dllPath)}: {ex.Message.EscapeMarkup()}[/]"
                );
                return null;
            }
        });

        var results = Task.WhenAll(tasks).Result;
        return results.Where(r => r != null).Cast<ClientModPackage>().ToList();
    }

    /// <summary>
    /// Filters out duplicate mods based on a name and author combination.
    /// </summary>
    /// <param name="mods">List of client mod packages to filter.</param>
    /// <returns>List of unique client mod packages.</returns>
    private static List<ClientModPackage> FilterDuplicateMods(List<ClientModPackage> mods)
    {
        var uniqueMods = new List<ClientModPackage>();

        foreach (var mod in mods)
        {
            if (
                !uniqueMods.Any(existing =>
                    existing.Name.Equals(mod.Name, StringComparison.OrdinalIgnoreCase)
                    && existing.Author.Equals(mod.Author, StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                uniqueMods.Add(mod);
            }
        }

        return uniqueMods;
    }

    /// <summary>
    /// Extracts BepInEx plugin information from a DLL file using safe reflection.
    /// </summary>
    /// <param name="dllPath">Path to the DLL file.</param>
    /// <returns>Client mod package or null if no plugin found.</returns>
    private ClientModPackage? ExtractBepInPluginInfo(string dllPath)
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

    /// <summary>
    /// Creates a MetadataLoadContext with the appropriate assembly resolver.
    /// </summary>
    /// <param name="dllPath">Path to the DLL being analyzed.</param>
    /// <returns>Configured MetadataLoadContext.</returns>
    private static MetadataLoadContext CreateMetadataLoadContext(string dllPath)
    {
        var resolver = new AssemblyResolver(dllPath);
        return new MetadataLoadContext(resolver);
    }

    /// <summary>
    /// Scans an assembly for BepInEx plugin attributes.
    /// </summary>
    /// <param name="assembly">Assembly to scan.</param>
    /// <param name="dllPath">Path to the DLL file.</param>
    /// <returns>Client mod package or null if no plugin found.</returns>
    private ClientModPackage? ScanAssemblyForBepInPlugin(Assembly assembly, string dllPath)
    {
        foreach (var type in assembly.GetTypes())
        {
            try
            {
                var bepInPlugin = ExtractBepInPluginAttribute(type);
                if (bepInPlugin != null)
                {
                    return ClientModPackage.FromBepInPlugin(bepInPlugin, dllPath);
                }
            }
            catch
            {
                // Skip types that can't be inspected
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts BepInPlugin attribute from a type.
    /// </summary>
    /// <param name="type">Type to examine.</param>
    /// <returns>BepInPlugin attribute or null if not found.</returns>
    private static BepInPluginAttribute? ExtractBepInPluginAttribute(Type type)
    {
        var customAttributes = type.GetCustomAttributesData();

        // Look for the BepInPlugin attribute by checking multiple possible names
        var bepInPluginAttribute = customAttributes.FirstOrDefault(attr =>
            attr.AttributeType.Name == "BepInPlugin"
            || attr.AttributeType.Name == "BepInPluginAttribute"
            || (attr.AttributeType.FullName?.Contains("BepInPlugin") ?? false)
        );

        if (bepInPluginAttribute == null || bepInPluginAttribute.ConstructorArguments.Count < 3)
            return null;

        // Extract constructor arguments: GUID, Name, Version
        var guid = bepInPluginAttribute.ConstructorArguments[0].Value?.ToString() ?? "";
        var name = bepInPluginAttribute.ConstructorArguments[1].Value?.ToString() ?? "";
        var version = bepInPluginAttribute.ConstructorArguments[2].Value?.ToString() ?? "";

        if (string.IsNullOrWhiteSpace(guid) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(version))
            return null;

        return new BepInPluginAttribute(guid, name, version);
    }
}
