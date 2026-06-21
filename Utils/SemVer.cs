namespace CheckMods.Utils;

/// <summary>
/// Helpers for parsing semantic version strings without exception-driven control flow. Mod and SPT version strings
/// come from untrusted metadata and the Forge API, so they are frequently missing or malformed.
/// </summary>
public static class SemVer
{
    /// <summary>
    /// Parses a semantic version, returning null when the string is missing or invalid.
    /// </summary>
    /// <param name="version">The version string to parse.</param>
    /// <returns>The parsed version, or null if it could not be parsed.</returns>
    public static SemanticVersioning.Version? TryParse(string? version)
    {
        return !string.IsNullOrWhiteSpace(version) && SemanticVersioning.Version.TryParse(version, out var parsed)
            ? parsed
            : null;
    }

    /// <summary>
    /// Parses a semantic version, falling back to 0.0.0 when the string is missing or invalid. Useful for ordering,
    /// where an unparseable version should sort lowest rather than throw.
    /// </summary>
    /// <param name="version">The version string to parse.</param>
    /// <returns>The parsed version, or 0.0.0 if it could not be parsed.</returns>
    public static SemanticVersioning.Version ParseOrZero(string? version)
    {
        return TryParse(version) ?? new SemanticVersioning.Version(0, 0, 0);
    }
}
