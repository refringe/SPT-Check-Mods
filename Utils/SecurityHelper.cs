namespace CheckMods.Utils;

/// <summary>
/// Path validation utilities that prevent directory traversal.
/// </summary>
public static class SecurityHelper
{
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

            var baseFullPath = Path.GetFullPath(basePath);

            // Return null if a path traversal attempt is detected
            return !fullPath.StartsWith(baseFullPath, StringComparison.OrdinalIgnoreCase) ? null : fullPath;
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
}
