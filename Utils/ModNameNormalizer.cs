namespace CheckMods.Utils;

/// <summary>
/// Provides centralized name normalization for mod matching operations.
/// </summary>
public static class ModNameNormalizer
{
    private static readonly char[] _charsToRemove = ['-', '_', ' ', '.'];
    private static readonly string[] _suffixesToRemove = ["server", "client"];

    /// <summary>
    /// Normalizes a mod name for comparison by removing special characters,
    /// converting to lowercase, and optionally removing server/client suffixes.
    /// </summary>
    /// <param name="name">The name to normalize.</param>
    /// <param name="removeComponentSuffixes">Whether to remove "server" and "client" suffixes.</param>
    public static string Normalize(string? name, bool removeComponentSuffixes = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var result = _charsToRemove.Aggregate(
            name.ToLowerInvariant(),
            (current, c) => current.Replace(c.ToString(), string.Empty)
        );

        if (removeComponentSuffixes)
        {
            var matchingSuffix = _suffixesToRemove.FirstOrDefault(s => result.EndsWith(s, StringComparison.Ordinal));
            if (matchingSuffix is not null)
            {
                result = result[..^matchingSuffix.Length];
            }
        }

        return result;
    }

    /// <summary>
    /// Extracts a readable name from a mod GUID (e.g., "com.author.modname" -> "modname").
    /// </summary>
    /// <param name="guid">The GUID to extract from.</param>
    /// <returns>The extracted name, or the original GUID if extraction fails.</returns>
    public static string ExtractNameFromGuid(string? guid)
    {
        if (string.IsNullOrWhiteSpace(guid))
        {
            return string.Empty;
        }

        var parts = guid.Split(['.', '-', '_'], StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return guid;
        }

        return parts[^1];
    }

    /// <summary>
    /// Calculates the fuzzy match score between two names.
    /// </summary>
    /// <param name="name1">The first name.</param>
    /// <param name="name2">The second name.</param>
    /// <returns>A score from 0-100 indicating similarity.</returns>
    public static int GetFuzzyMatchScore(string? name1, string? name2)
    {
        var normalized1 = Normalize(name1);
        var normalized2 = Normalize(name2);

        if (string.IsNullOrEmpty(normalized1) || string.IsNullOrEmpty(normalized2))
        {
            return 0;
        }

        return FuzzySharp.Fuzz.Ratio(normalized1, normalized2);
    }

    /// <summary>
    /// Determines if two names match exactly after normalization.
    /// </summary>
    /// <param name="name1">The first name.</param>
    /// <param name="name2">The second name.</param>
    /// <param name="removeComponentSuffixes">Whether to remove server/client suffixes.</param>
    public static bool IsExactMatch(string? name1, string? name2, bool removeComponentSuffixes = false)
    {
        var normalized1 = Normalize(name1, removeComponentSuffixes);
        var normalized2 = Normalize(name2, removeComponentSuffixes);

        return !string.IsNullOrEmpty(normalized1) && string.Equals(normalized1, normalized2, StringComparison.Ordinal);
    }
}
