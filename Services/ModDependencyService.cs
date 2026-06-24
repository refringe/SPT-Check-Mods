using CheckMods.Models;
using CheckMods.Services.Interfaces;
using CheckMods.Utils;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace CheckMods.Services;

/// <summary>
/// Service responsible for analyzing mod dependencies and building a dependency tree.
/// </summary>
[Injectable(InjectionType.Transient)]
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

        // Mods with an available update get a second dependency fetch at the proposed version so the update's
        // dependency changes can be diffed against the installed version. Dedupe by API mod ID (paired
        // server/client components share an ID).
        var updatableGroups = modList
            .Where(m =>
                m.IsMatched
                && m.ApiModId.HasValue
                && m.UpdateStatus == UpdateStatus.UpdateAvailable
                && !string.IsNullOrWhiteSpace(m.LatestVersion)
            )
            .GroupBy(m => m.ApiModId!.Value)
            .ToList();

        var totalToFetch = uniqueModIds.Count + updatableGroups.Count;
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

        // Second pass: for each updatable mod, fetch dependencies at the proposed version and diff them against the
        // installed version's dependencies (already cached above) to surface what the update adds or removes.
        foreach (var group in updatableGroups)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var modId = group.Key;
            var targetVersion = group.First().LatestVersion!;

            var targetResult = await forgeApiService.GetModDependenciesAsync(
                [(modId.ToString(), targetVersion)],
                cancellationToken
            );

            fetchedCount++;
            progressCallback?.Invoke(fetchedCount, totalToFetch);

            // Skip the diff on a not-found/error response; an empty success list is a valid "no dependencies".
            var targetDeps = targetResult.Match(
                dependencies => (List<ModDependency>?)dependencies,
                _ => null,
                _ => null
            );
            if (targetDeps is null)
            {
                continue;
            }

            var installedDeps = modDependencyCache.GetValueOrDefault(modId, []);
            var delta = BuildUpdateDependencyDelta(installedDeps, targetDeps, modByGuid, modById, installedModGuids);
            if (!delta.HasChanges)
            {
                continue;
            }

            foreach (var mod in group)
            {
                mod.SetUpdateDependencyChanges(delta);
            }
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
                downloadLink = ForgeUrls.Download(
                    dependency.Id,
                    dependency.Slug,
                    dependency.LatestCompatibleVersion.Version
                );
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

    /// <summary>
    /// Diffs the installed and proposed dependency trees (recursively flattened by GUID) and builds the resulting set
    /// of added and removed dependencies, each annotated with its install state relative to what's installed.
    /// </summary>
    private static UpdateDependencyDelta BuildUpdateDependencyDelta(
        List<ModDependency> installedDeps,
        List<ModDependency> targetDeps,
        Dictionary<string, Mod> modByGuid,
        Dictionary<int, Mod> modById,
        HashSet<string> installedGuids
    )
    {
        var installedFlat = FlattenDependencies(installedDeps);
        var targetFlat = FlattenDependencies(targetDeps);

        var added = targetFlat
            .Where(kvp => !installedFlat.ContainsKey(kvp.Key))
            .Select(kvp => BuildDependencyChange(kvp.Value, modByGuid, modById, installedGuids))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var removed = installedFlat
            .Where(kvp => !targetFlat.ContainsKey(kvp.Key))
            .Select(kvp => BuildDependencyChange(kvp.Value, modByGuid, modById, installedGuids))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new UpdateDependencyDelta { Added = added, Removed = removed };
    }

    /// <summary>
    /// Recursively flattens a dependency tree into a GUID-keyed map. Blank GUIDs are skipped and each GUID is visited
    /// once, which also guards against cycles and duplicate paths.
    /// </summary>
    private static Dictionary<string, ModDependency> FlattenDependencies(List<ModDependency> deps)
    {
        var map = new Dictionary<string, ModDependency>(StringComparer.OrdinalIgnoreCase);
        CollectDependencies(deps, map);
        return map;
    }

    private static void CollectDependencies(List<ModDependency> deps, Dictionary<string, ModDependency> map)
    {
        foreach (var dep in deps)
        {
            if (string.IsNullOrWhiteSpace(dep.Guid) || !map.TryAdd(dep.Guid, dep))
            {
                continue;
            }

            if (dep.Dependencies is { Count: > 0 })
            {
                CollectDependencies(dep.Dependencies, map);
            }
        }
    }

    /// <summary>
    /// Builds a <see cref="DependencyChange"/> for a dependency, resolving whether it is installed and, if so, whether
    /// its installed version looks older than the latest Forge-compatible version.
    /// </summary>
    private static DependencyChange BuildDependencyChange(
        ModDependency dependency,
        Dictionary<string, Mod> modByGuid,
        Dictionary<int, Mod> modById,
        HashSet<string> installedGuids
    )
    {
        Mod? installedMod = null;
        if (!string.IsNullOrWhiteSpace(dependency.Guid) && modByGuid.TryGetValue(dependency.Guid, out var foundByGuid))
        {
            installedMod = foundByGuid;
        }
        else if (modById.TryGetValue(dependency.Id, out var foundById))
        {
            installedMod = foundById;
        }

        var isInstalled =
            installedMod != null
            || (!string.IsNullOrWhiteSpace(dependency.Guid) && installedGuids.Contains(dependency.Guid));

        var recommendedVersion = dependency.LatestCompatibleVersion?.Version;

        // Construct the Forge download URL when there's enough information, mirroring the missing-dependency path.
        string? downloadLink = null;
        if (
            dependency.Id > 0
            && !string.IsNullOrWhiteSpace(dependency.Slug)
            && !string.IsNullOrWhiteSpace(recommendedVersion)
        )
        {
            downloadLink = ForgeUrls.Download(dependency.Id, dependency.Slug, recommendedVersion);
        }

        DependencyInstallState state;
        if (!isInstalled)
        {
            state = DependencyInstallState.NotInstalled;
        }
        else if (
            installedMod != null
            && !string.IsNullOrWhiteSpace(recommendedVersion)
            && SemVer.ParseOrZero(installedMod.LocalVersion) < SemVer.ParseOrZero(recommendedVersion)
        )
        {
            state = DependencyInstallState.InstalledOutdated;
        }
        else
        {
            state = DependencyInstallState.InstalledOk;
        }

        return new DependencyChange
        {
            Name = dependency.Name,
            Guid = dependency.Guid,
            ModId = dependency.Id,
            Slug = dependency.Slug,
            RecommendedVersion = recommendedVersion ?? "unknown",
            DownloadLink = downloadLink,
            InstallState = state,
            InstalledVersion = installedMod?.LocalVersion,
            Conflict = dependency.Conflict,
        };
    }
}
