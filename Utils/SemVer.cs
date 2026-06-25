namespace CheckMods.Utils;

/// <summary>
/// Helpers for parsing semantic version strings.
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
    /// Parses a semantic version, falling back to 0.0.0 when the string is missing or invalid.
    /// </summary>
    /// <param name="version">The version string to parse.</param>
    /// <returns>The parsed version, or 0.0.0 if it could not be parsed.</returns>
    public static SemanticVersioning.Version ParseOrZero(string? version)
    {
        return TryParse(version) ?? new SemanticVersioning.Version(0, 0, 0);
    }

    /// <summary>
    /// Determines whether <paramref name="version"/> satisfies the given SPT version constraint (a semver range).
    /// Returns false when the constraint is missing or cannot be parsed.
    /// </summary>
    /// <param name="constraint">The semver range constraint (e.g. "~4.0.0").</param>
    /// <param name="version">The version to test against the constraint.</param>
    /// <returns>True if the version satisfies the constraint; false if it does not or the constraint is invalid.</returns>
    public static bool SatisfiesRange(string? constraint, SemanticVersioning.Version version)
    {
        return !string.IsNullOrWhiteSpace(constraint)
            && SemanticVersioning.Range.TryParse(constraint, out var range)
            && range.IsSatisfied(version);
    }
}
