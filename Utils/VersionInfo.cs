using System.Reflection;

namespace CheckMods.Utils;

/// <summary>
/// Provides the running application's version information, read from assembly metadata embedded at build time.
/// </summary>
public static class VersionInfo
{
    /// <summary>
    /// The clean semantic version of the running build (e.g. "1.0.1" or "1.2.0-beta.1"), without build metadata.
    /// </summary>
    public static string SemVer { get; } = ReadSemVer();

    /// <summary>
    /// The short git commit hash embedded at build time.
    /// </summary>
    public static string GitHash { get; } = ReadGitHash();

    /// <summary>
    /// Reads the semantic version from the assembly's informational version, stripping any build metadata.
    /// </summary>
    private static string ReadSemVer()
    {
        var informational = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "0.0.0";
        }

        // Informational version format: "<semver>+<gitHash>".
        var plusIndex = informational.IndexOf('+');
        return plusIndex >= 0 ? informational[..plusIndex] : informational;
    }

    /// <summary>
    /// Reads the git commit hash from the assembly metadata.
    /// </summary>
    private static string ReadGitHash()
    {
        return Assembly
                .GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(attr => attr.Key == "GitHash")
                ?.Value
            ?? "unknown";
    }
}
