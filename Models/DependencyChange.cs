namespace CheckMods.Models;

/// <summary>
/// How a dependency required by a proposed update relates to what's currently installed.
/// </summary>
public enum DependencyInstallState
{
    /// <summary>The dependency is not installed.</summary>
    NotInstalled = 0,

    /// <summary>
    /// The dependency is installed, but its installed version is older than the latest version Forge reports as
    /// compatible.
    /// </summary>
    InstalledOutdated = 1,

    /// <summary>The dependency is installed at a version that appears to already satisfy the proposed update.</summary>
    InstalledOk = 2,
}

/// <summary>
/// A single dependency that differs between a mod's installed version and its proposed update version.
/// </summary>
public sealed record DependencyChange
{
    public required string Name { get; init; }

    public required string Guid { get; init; }

    /// <summary>The Forge mod ID of the dependency (0 when unknown).</summary>
    public required int ModId { get; init; }

    public required string Slug { get; init; }

    /// <summary>The latest Forge-compatible version of the dependency.</summary>
    public required string RecommendedVersion { get; init; }

    /// <summary>A direct download link for the recommended version, when one could be constructed.</summary>
    public string? DownloadLink { get; init; }

    /// <summary>How this dependency relates to what's currently installed.</summary>
    public required DependencyInstallState InstallState { get; init; }

    /// <summary>The currently installed version of the dependency, when it is installed.</summary>
    public string? InstalledVersion { get; init; }

    /// <summary>Whether Forge flagged this dependency as having a version constraint conflict.</summary>
    public bool Conflict { get; init; }
}

/// <summary>
/// The difference in dependencies between a mod's installed version and its proposed update version.
/// </summary>
public sealed class UpdateDependencyDelta
{
    /// <summary>Dependencies required by the update that the installed version did not require.</summary>
    public List<DependencyChange> Added { get; init; } = [];

    /// <summary>Dependencies the installed version required that the update no longer requires.</summary>
    public List<DependencyChange> Removed { get; init; } = [];

    public bool HasChanges
    {
        get { return Added.Count > 0 || Removed.Count > 0; }
    }
}
