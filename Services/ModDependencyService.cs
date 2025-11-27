using CheckMods.Models;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace CheckMods.Services;

/// <summary>
/// Service responsible for analyzing mod dependencies and building a dependency tree.
/// </summary>
public sealed class ModDependencyService(IForgeApiService forgeApiService, ILogger<ModDependencyService> logger)
    : IModDependencyService
{
    /// <summary>
    /// Analyzes dependencies for a collection of mods.
    /// </summary>
    /// <param name="mods">The mods to analyze dependencies for.</param>
    /// <param name="installedModGuids">Set of GUIDs for mods that are currently installed.</param>
    /// <param name="progressCallback">Optional callback for progress updates (current, total).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Dependency analysis result containing tree structure and any issues.</returns>
    public async Task<DependencyAnalysisResult> AnalyzeDependenciesAsync(
        IEnumerable<Mod> mods,
        HashSet<string> installedModGuids,
        Action<int, int>? progressCallback = null,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Analyzing mod dependencies");

        var modList = mods.ToList();
        var result = new DependencyAnalysisResult();

        // Only analyze mods that are matched with the API
        var matchedMods = modList.Where(m => m.IsMatched && m.ApiModId.HasValue).ToList();
        if (matchedMods.Count == 0)
        {
            logger.LogDebug("No matched mods to analyze for dependencies");
            // No matched mods, return all as roots with no children
            result.RootMods.AddRange(modList.Select(m => new DependencyNode { Mod = m }));
            return result;
        }
        logger.LogDebug("Analyzing dependencies for {ModCount} matched mods", matchedMods.Count);

        // Build lookup maps for finding installed mods
        var modByGuid = modList
            .Where(m => !string.IsNullOrWhiteSpace(m.Guid))
            .GroupBy(m => m.Guid, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var modById = matchedMods
            .Where(m => m.ApiModId.HasValue)
            .GroupBy(m => m.ApiModId!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        // Track all missing dependencies and conflicts across all mods
        var missingDeps = new Dictionary<string, MissingDependency>(StringComparer.OrdinalIgnoreCase);
        List<DependencyConflict> conflicts = [];

        // Fetch dependencies for each matched mod individually
        // This allows us to build a proper tree showing which mod depends on which
        var modDependencyCache = new Dictionary<int, List<ModDependency>>();

        // Get unique mod IDs to fetch
        var uniqueModIds = matchedMods
            .Where(m => m.ApiModId.HasValue)
            .Select(m => m.ApiModId!.Value)
            .Distinct()
            .ToList();

        var totalToFetch = uniqueModIds.Count;
        var fetchedCount = 0;

        foreach (var mod in matchedMods)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var modId = mod.ApiModId!.Value;
            if (modDependencyCache.ContainsKey(modId))
            {
                continue;
            }

            var depsResult = await forgeApiService.GetModDependenciesAsync(
                [(modId.ToString(), mod.LocalVersion)],
                cancellationToken
            );

            // Extract dependencies or use empty list on error/not found
            var deps = depsResult.Match(
                dependencies => dependencies,
                _ => [], // NotFound
                _ => [] // ApiError
            );

            modDependencyCache[modId] = deps;
            fetchedCount++;
            progressCallback?.Invoke(fetchedCount, totalToFetch);
        }

        // Build the tree for each mod
        foreach (var mod in modList)
        {
            // Get dependencies for this mod (if it's matched)
            List<ModDependency> modDeps = [];
            if (mod.ApiModId.HasValue && modDependencyCache.TryGetValue(mod.ApiModId.Value, out var cachedDeps))
            {
                modDeps = cachedDeps;
            }

            // Build the dependency subtree for this mod
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { mod.Guid };
            var children = modDeps
                .Select(dep =>
                    BuildDependencySubtree(dep, modByGuid, modById, installedModGuids, missingDeps, conflicts, visited)
                )
                .Where(node => node is not null)
                .Cast<DependencyNode>()
                .ToList();

            result.RootMods.Add(
                new DependencyNode
                {
                    Mod = mod,
                    DependencyInfo = null,
                    IsInstalled = true,
                    Children = children,
                }
            );
        }

        result.Conflicts.AddRange(conflicts);
        result.MissingDependencies.AddRange(missingDeps.Values);

        logger.LogDebug(
            "Dependency analysis complete. Conflicts: {ConflictCount}, Missing: {MissingCount}",
            conflicts.Count,
            missingDeps.Count
        );

        return result;
    }

    /// <summary>
    /// Builds a dependency subtree from the API dependency structure.
    /// </summary>
    private static DependencyNode? BuildDependencySubtree(
        ModDependency dependency,
        Dictionary<string, Mod> modByGuid,
        Dictionary<int, Mod> modById,
        HashSet<string> installedGuids,
        Dictionary<string, MissingDependency> missingDeps,
        List<DependencyConflict> conflicts,
        HashSet<string> visited
    )
    {
        // Prevent circular recursion
        if (!visited.Add(dependency.Guid))
        {
            return null;
        }

        // Check for conflicts
        if (
            dependency.Conflict
            && !conflicts.Any(c => c.ModGuid.Equals(dependency.Guid, StringComparison.OrdinalIgnoreCase))
        )
        {
            conflicts.Add(
                new DependencyConflict
                {
                    ModName = dependency.Name,
                    ModGuid = dependency.Guid,
                    Description = "Version constraint conflict detected",
                    DependencyInfo = dependency,
                }
            );
        }

        // Try to find the installed mod for this dependency
        Mod? installedMod = null;
        if (modByGuid.TryGetValue(dependency.Guid, out var foundByGuid))
        {
            installedMod = foundByGuid;
        }
        else if (modById.TryGetValue(dependency.Id, out var foundById))
        {
            installedMod = foundById;
        }

        var isInstalled = installedMod != null || installedGuids.Contains(dependency.Guid);

        // Track missing dependencies
        if (!isInstalled && !missingDeps.ContainsKey(dependency.Guid))
        {
            // Always construct the Forge download URL for consistency
            string? downloadLink = null;
            if (
                dependency.Id > 0
                && !string.IsNullOrWhiteSpace(dependency.Slug)
                && !string.IsNullOrWhiteSpace(dependency.LatestCompatibleVersion?.Version)
            )
            {
                downloadLink =
                    $"https://forge.sp-tarkov.com/mod/download/{dependency.Id}/{dependency.Slug}/{dependency.LatestCompatibleVersion.Version}";
            }

            missingDeps[dependency.Guid] = new MissingDependency
            {
                Name = dependency.Name,
                Guid = dependency.Guid,
                ModId = dependency.Id,
                Slug = dependency.Slug,
                RecommendedVersion = dependency.LatestCompatibleVersion?.Version ?? "unknown",
                DownloadLink = downloadLink,
            };
        }

        // Build children from nested dependencies
        if (dependency.Dependencies is not { Count: > 0 })
        {
            return CreateDependencyNode(dependency, installedMod, isInstalled, []);
        }

        var children = dependency
            .Dependencies.Select(nestedDep =>
                BuildDependencySubtree(nestedDep, modByGuid, modById, installedGuids, missingDeps, conflicts, visited)
            )
            .Where(node => node is not null)
            .Cast<DependencyNode>()
            .ToList();

        return CreateDependencyNode(dependency, installedMod, isInstalled, children);
    }

    /// <summary>
    /// Creates a DependencyNode from dependency info and optional installed mod.
    /// </summary>
    private static DependencyNode CreateDependencyNode(
        ModDependency dependency,
        Mod? installedMod,
        bool isInstalled,
        List<DependencyNode> children
    )
    {
        var mod =
            installedMod
            ?? new Mod
            {
                Guid = dependency.Guid,
                FilePath = string.Empty,
                IsServerMod = true,
                LocalName = dependency.Name,
                LocalAuthor = string.Empty,
                LocalVersion = dependency.LatestCompatibleVersion?.Version ?? "unknown",
            };

        return new DependencyNode
        {
            Mod = mod,
            DependencyInfo = dependency,
            IsInstalled = isInstalled,
            Children = children,
        };
    }
}
