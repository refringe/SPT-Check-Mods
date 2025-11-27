using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Service responsible for analyzing mod dependencies and building a dependency tree.
/// </summary>
public interface IModDependencyService
{
    /// <summary>
    /// Analyzes dependencies for a collection of mods.
    /// </summary>
    /// <param name="mods">The mods to analyze dependencies for.</param>
    /// <param name="installedModGuids">Set of GUIDs for mods that are currently installed.</param>
    /// <param name="progressCallback">Optional callback for progress updates (current, total).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Dependency analysis result containing tree structure and any issues.</returns>
    Task<DependencyAnalysisResult> AnalyzeDependenciesAsync(
        IEnumerable<Mod> mods,
        HashSet<string> installedModGuids,
        Action<int, int>? progressCallback = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of dependency analysis containing the tree structure and any detected issues.
/// </summary>
public class DependencyAnalysisResult
{
    /// <summary>
    /// Root mods that are not dependencies of any other installed mod.
    /// </summary>
    public List<DependencyNode> RootMods { get; init; } = [];

    /// <summary>
    /// Dependencies that have version conflicts.
    /// </summary>
    public List<DependencyConflict> Conflicts { get; init; } = [];

    /// <summary>
    /// Dependencies that are required but not installed.
    /// </summary>
    public List<MissingDependency> MissingDependencies { get; init; } = [];

    /// <summary>
    /// Whether the analysis has any issues (conflicts or missing dependencies).
    /// </summary>
    public bool HasIssues
    {
        get { return Conflicts.Count > 0 || MissingDependencies.Count > 0; }
    }
}

/// <summary>
/// Represents a node in the dependency tree.
/// </summary>
public class DependencyNode
{
    /// <summary>
    /// The mod at this node.
    /// </summary>
    public required Mod Mod { get; init; }

    /// <summary>
    /// The dependency information from the API (null for root mods).
    /// </summary>
    public ModDependency? DependencyInfo { get; init; }

    /// <summary>
    /// Child dependencies of this mod.
    /// </summary>
    public List<DependencyNode> Children { get; init; } = [];

    /// <summary>
    /// Whether this dependency is installed locally.
    /// </summary>
    public bool IsInstalled { get; init; } = true;

    /// <summary>
    /// Whether this mod is a dependency of other mods (vs a standalone/root mod).
    /// </summary>
    public bool IsDependency { get; init; }
}

/// <summary>
/// Represents a version conflict between dependencies.
/// </summary>
public class DependencyConflict
{
    /// <summary>
    /// The mod that has the conflict.
    /// </summary>
    public required string ModName { get; init; }

    /// <summary>
    /// The GUID of the conflicting mod.
    /// </summary>
    public required string ModGuid { get; init; }

    /// <summary>
    /// Description of the conflict.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// The dependency information with conflict details.
    /// </summary>
    public required ModDependency DependencyInfo { get; init; }
}

/// <summary>
/// Represents a missing dependency.
/// </summary>
public class MissingDependency
{
    /// <summary>
    /// The name of the missing dependency.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The GUID of the missing dependency.
    /// </summary>
    public required string Guid { get; init; }

    /// <summary>
    /// The mod ID on Forge (for download link).
    /// </summary>
    public required int ModId { get; init; }

    /// <summary>
    /// The URL slug on Forge.
    /// </summary>
    public required string Slug { get; init; }

    /// <summary>
    /// The recommended version to install.
    /// </summary>
    public required string RecommendedVersion { get; init; }

    /// <summary>
    /// Download link for the dependency.
    /// </summary>
    public string? DownloadLink { get; init; }

    /// <summary>
    /// Names of mods that require this dependency.
    /// </summary>
    public List<string> RequiredBy { get; init; } = [];
}
