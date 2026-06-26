namespace CheckMods.Utils;

/// <summary>
/// Path validation utilities that prevent directory traversal.
/// </summary>
public static class SecurityHelper
{
    /// <summary>
    /// Gets the string comparison to use for local filesystem paths on the current platform.
    /// </summary>
    public static StringComparison PathStringComparison => OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    /// <summary>
    /// Gets the string comparer to use for local filesystem paths on the current platform.
    /// </summary>
    public static StringComparer PathStringComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    /// <summary>
    /// Validates and returns a safe absolute path, preventing directory traversal attacks. Resolves relative path
    /// segments and ensures the result stays within the base path if provided.
    /// </summary>
    /// <param name="inputPath">The input path to validate and sanitize.</param>
    /// <param name="basePath">Optional base path to restrict the result to. If provided, the result must be within this path.</param>
    /// <returns>A safe absolute path or null if the input is invalid or represents a directory traversal attempt.</returns>
    public static string? GetSafePath(string? inputPath, string? basePath = null)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            return null;
        }

        try
        {
            var fullPath = Path.GetFullPath(inputPath);

            if (string.IsNullOrWhiteSpace(basePath))
            {
                return fullPath;
            }

            return IsWithinDirectory(fullPath, basePath) ? fullPath : null;
        }
        catch (ArgumentException)
        {
            return null; // Invalid path characters
        }
        catch (PathTooLongException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null; // Path contains a colon in the middle of the string
        }
    }

    /// <summary>
    /// Determines whether <paramref name="path"/> lives inside <paramref name="directory"/> or is that directory,
    /// using platform-appropriate filesystem path comparison semantics.
    /// </summary>
    public static bool IsWithinDirectory(string path, string directory)
    {
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var fullDirectory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));

        if (string.Equals(fullPath, fullDirectory, PathStringComparison))
        {
            return true;
        }

        var prefix = fullDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? fullDirectory
            : fullDirectory + Path.DirectorySeparatorChar;

        return fullPath.StartsWith(prefix, PathStringComparison);
    }
}
