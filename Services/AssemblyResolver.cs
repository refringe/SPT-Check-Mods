using System.Reflection;
using System.Runtime.InteropServices;

namespace CheckMods.Services;

/// <summary>
/// Custom assembly resolver that handles missing assemblies gracefully for BepInEx plugin scanning.
/// </summary>
/// <param name="dllPath">Path to the DLL being analyzed.</param>
public class AssemblyResolver(string dllPath) : MetadataAssemblyResolver
{
    private readonly PathAssemblyResolver _pathResolver = new(BuildMinimalAssemblySearchPaths(dllPath));

    /// <summary>
    /// Resolves assembly references, returning null for missing assemblies to allow inspection to continue.
    /// </summary>
    /// <param name="context">The metadata load context.</param>
    /// <param name="assemblyName">The assembly name to resolve.</param>
    /// <returns>Resolved assembly or null if not found.</returns>
    public override Assembly? Resolve(MetadataLoadContext context, AssemblyName assemblyName)
    {
        try
        {
            return _pathResolver.Resolve(context, assemblyName);
        }
        catch
        {
            // Return null for missing assemblies to allow inspection to continue. Key to avoiding dependency issues.
            return null;
        }
    }

    /// <summary>
    /// Builds a minimal list of assembly search paths required for MetadataLoadContext. Includes the target DLL, .NET
    /// runtime assemblies, and BepInEx core assemblies.
    /// </summary>
    /// <param name="dllPath">Path to the DLL being analyzed.</param>
    /// <returns>List of assembly paths.</returns>
    private static IEnumerable<string> BuildMinimalAssemblySearchPaths(string dllPath)
    {
        List<string> assemblyPaths = [dllPath];

        // Add .NET runtime assemblies, required for MetadataLoadContext core assembly
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        if (Directory.Exists(runtimeDir))
        {
            assemblyPaths.AddRange(Directory.GetFiles(runtimeDir, "*.dll"));
        }

        // Add BepInEx core assemblies, required for BepInPlugin attribute resolution
        // Walk up from the DLL path to find the plugins directory, then locate BepInEx/core
        var bepInExCoreDir = FindBepInExCoreDirectory(dllPath);
        if (bepInExCoreDir != null && Directory.Exists(bepInExCoreDir))
        {
            assemblyPaths.AddRange(Directory.GetFiles(bepInExCoreDir, "*.dll"));
        }

        return assemblyPaths.Distinct();
    }

    /// <summary>
    /// Finds the BepInEx core directory by walking up from the DLL path, looking for a sibling BepInEx/core folder at
    /// each ancestor. This resolves the BepInEx assemblies regardless of where the DLL lives within the SPT install,
    /// whether correctly placed under BepInEx/plugins or misplaced under SPT/user/mods, since both sit beside the
    /// BepInEx directory at the SPT root.
    /// </summary>
    /// <param name="dllPath">Path to the DLL being analyzed.</param>
    /// <returns>Path to BepInEx/core directory, or null if not found.</returns>
    private static string? FindBepInExCoreDirectory(string dllPath)
    {
        var currentDir = Path.GetDirectoryName(dllPath);

        while (!string.IsNullOrEmpty(currentDir))
        {
            var coreDir = Path.Combine(currentDir, "BepInEx", "core");
            if (Directory.Exists(coreDir))
            {
                return coreDir;
            }

            currentDir = Path.GetDirectoryName(currentDir);
        }

        return null;
    }
}
