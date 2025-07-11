using System.Reflection;
using System.Runtime.InteropServices;

namespace CheckMods.Services;

/// <summary>
/// Custom assembly resolver that handles missing assemblies gracefully for BepInEx plugin scanning.
/// </summary>
public class AssemblyResolver : MetadataAssemblyResolver
{
    private readonly PathAssemblyResolver _pathResolver;

    /// <summary>
    /// Initializes a new instance of the AssemblyResolver.
    /// </summary>
    /// <param name="dllPath">Path to the DLL being analyzed.</param>
    public AssemblyResolver(string dllPath)
    {
        _pathResolver = new PathAssemblyResolver(BuildMinimalAssemblySearchPaths(dllPath));
    }

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
        var assemblyPaths = new List<string> { dllPath };

        // Add .NET runtime assemblies, required for MetadataLoadContext core assembly
        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        if (Directory.Exists(runtimeDir))
        {
            assemblyPaths.AddRange(Directory.GetFiles(runtimeDir, "*.dll"));
        }

        // Add BepInEx core assemblies, required for BepInPlugin attribute resolution
        var pluginDir = Path.GetDirectoryName(dllPath) ?? "";
        var sptRoot = Path.GetDirectoryName(Path.GetDirectoryName(pluginDir)) ?? "";
        var bepInExCoreDir = Path.Combine(sptRoot, "BepInEx", "core");

        if (Directory.Exists(bepInExCoreDir))
        {
            assemblyPaths.AddRange(Directory.GetFiles(bepInExCoreDir, "*.dll"));
        }

        return assemblyPaths.Distinct();
    }
}
