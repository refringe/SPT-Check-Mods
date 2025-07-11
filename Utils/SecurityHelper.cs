namespace CheckMods.Utils;

/// <summary>
/// Utility class providing security-related helper methods for input validation and path sanitization. Prevents
/// directory traversal attacks and removes potentially dangerous control characters.
/// </summary>
public static partial class SecurityHelper
{
    /// <summary>
    /// Regex pattern for matching control characters (0x00-0x1F and 0x7F) that should be removed from input.
    /// </summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"[\x00-\x1F\x7F]")]
    private static partial System.Text.RegularExpressions.Regex ControlCharactersRegex();
    
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
            return null;

        try
        {
            // Get the full path, resolving any relative segments
            var fullPath = Path.GetFullPath(inputPath);
            
            // If a base path is provided, ensure the resolved path is within it
            if (string.IsNullOrWhiteSpace(basePath)) return fullPath;
            
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
    
    /// <summary>
    /// Sanitizes input string by removing control characters that could be used for injection attacks. Removes
    /// characters in the range 0x00-0x1F and 0x7F, which include null bytes, line feeds, and DEL.
    /// </summary>
    /// <param name="input">The input string to sanitize.</param>
    /// <returns>Sanitized string with control characters removed and whitespace trimmed.</returns>
    public static string SanitizeInput(string input)
    {
        return string.IsNullOrEmpty(input) ? string.Empty : ControlCharactersRegex().Replace(input.Trim(), "");
    }
}