namespace CheckMods.Tests;

/// <summary>
/// Helpers for creating throwaway directories for the file-touching tests. The constructed path is resolved and
/// verified to stay under the OS temp root before any directory is created, so a tampered TEMP/TMP value can't
/// redirect test file I/O outside the temp tree. Centralizing creation here keeps that guard in one place instead of
/// duplicated across each test class.
/// </summary>
internal static class TempWorkspace
{
    /// <summary>
    /// Creates a uniquely named directory under the OS temp folder and returns its full, validated path.
    /// </summary>
    public static string CreateDirectory(string prefix)
    {
        var root = Path.GetFullPath(Path.GetTempPath());
        var candidate = Path.GetFullPath(Path.Combine(root, $"{prefix}-{Guid.NewGuid():N}"));

        // Reject anything that escaped the temp root before it is used for any file operation.
        if (!candidate.StartsWith(root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Resolved temp path '{candidate}' escaped the temp root '{root}'.");
        }

        Directory.CreateDirectory(candidate);
        return candidate;
    }

    /// <summary>
    /// Best-effort recursive delete of a temp directory created by <see cref="CreateDirectory"/>. A lingering handle
    /// shouldn't fail a test, so the common cleanup races are swallowed.
    /// </summary>
    public static void SafeDelete(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort - a lingering handle shouldn't fail the test.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort.
        }
    }
}
