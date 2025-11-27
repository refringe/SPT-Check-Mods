using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using CheckMods.Utils;
using Microsoft.Extensions.Logging;

namespace CheckMods.Services;

/// <summary>
/// Service responsible for matching local mods with their Forge API counterparts.
/// Uses GUID lookup as the primary method with multiple fallback strategies.
/// </summary>
public sealed class ModMatchingService(IForgeApiService forgeApiService, ILogger<ModMatchingService> logger)
    : IModMatchingService
{
    /// <inheritdoc />
    public async Task<Mod> MatchModAsync(
        Mod mod,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Matching mod: {ModName} (GUID: {Guid})", mod.LocalName, mod.Guid);

        // Try GUID lookup first (most reliable)
        if (!string.IsNullOrWhiteSpace(mod.Guid))
        {
            var guidResult = await forgeApiService.GetModByGuidAsync(mod.Guid, sptVersion, cancellationToken);

            if (guidResult.TryPickT0(out var guidMatch, out _))
            {
                logger.LogDebug("Mod matched by GUID: {ModName} -> {ApiName}", mod.LocalName, guidMatch.Name);
                mod.UpdateFromApiMatch(guidMatch, MatchingConstants.ExactGuidConfidence, MatchMethod.ExactGuid);
                return mod;
            }
        }

        // Try alternate GUIDs (for multi-DLL mods where primary GUID might not be on Forge)
        foreach (var alternateGuid in mod.AlternateGuids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var altGuidResult = await forgeApiService.GetModByGuidAsync(alternateGuid, sptVersion, cancellationToken);

            if (altGuidResult.TryPickT0(out var altGuidMatch, out _))
            {
                mod.UpdateFromApiMatch(
                    altGuidMatch,
                    MatchingConstants.ExactGuidConfidence - MatchingConstants.AlternateGuidConfidenceReduction,
                    MatchMethod.ExactGuid
                );
                return mod;
            }
        }

        // Build list of search terms to try in order of preference
        var searchTerms = BuildSearchTerms(mod);

        foreach (var searchTerm in searchTerms)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var searchResult = mod.IsServerMod
                ? await forgeApiService.SearchModsAsync(searchTerm, sptVersion, cancellationToken)
                : await forgeApiService.SearchClientModsAsync(searchTerm, sptVersion, cancellationToken);

            // Extract the list from the result (empty list if error)
            var searchResults = searchResult.Match(
                results => results,
                _ => [] // ApiError - return empty list
            );

            if (searchResults.Count == 0)
            {
                continue;
            }

            // Try to find a matching result using multiple comparison strategies
            var bestMatch = FindBestMatch(mod, searchResults);
            if (bestMatch is null)
            {
                continue;
            }

            mod.UpdateFromApiMatch(bestMatch.Value.Result, bestMatch.Value.Confidence, bestMatch.Value.Method);
            return mod;
        }

        logger.LogDebug("No match found for mod: {ModName}", mod.LocalName);
        mod.Status = ModStatus.NoMatch;
        return mod;
    }

    /// <summary>
    /// Builds a list of search terms to try, in order of preference.
    /// </summary>
    private static List<string> BuildSearchTerms(Mod mod)
    {
        List<string> terms = [];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1. Original local name
        AddIfNew(terms, seen, mod.LocalName);

        // 2. Name without server/client suffix (e.g., "ModNameServer" -> "ModName")
        var nameWithoutSuffix = RemoveComponentSuffix(mod.LocalName);
        if (!string.Equals(nameWithoutSuffix, mod.LocalName, StringComparison.OrdinalIgnoreCase))
        {
            AddIfNew(terms, seen, nameWithoutSuffix);
        }

        // 3. Name extracted from GUID (e.g., "com.author.modname" -> "modname")
        if (!string.IsNullOrWhiteSpace(mod.Guid))
        {
            var guidName = ModNameNormalizer.ExtractNameFromGuid(mod.Guid);
            AddIfNew(terms, seen, guidName);

            // Also try without suffix
            var guidNameWithoutSuffix = RemoveComponentSuffix(guidName);
            if (!string.Equals(guidNameWithoutSuffix, guidName, StringComparison.OrdinalIgnoreCase))
            {
                AddIfNew(terms, seen, guidNameWithoutSuffix);
            }
        }

        // 4. Author + name combination (if author is known and not generic)
        if (
            !string.IsNullOrWhiteSpace(mod.LocalAuthor)
            && !string.Equals(mod.LocalAuthor, "Unknown", StringComparison.OrdinalIgnoreCase)
        )
        {
            AddIfNew(terms, seen, $"{mod.LocalAuthor} {mod.LocalName}");
        }

        return terms;
    }

    /// <summary>
    /// Removes common component suffixes from a mod name.
    /// </summary>
    private static string RemoveComponentSuffix(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        string[] suffixes = ["Server", "Client"];
        var matchingSuffix = suffixes.FirstOrDefault(s =>
            name.EndsWith(s, StringComparison.OrdinalIgnoreCase) && name.Length > s.Length
        );

        return matchingSuffix is not null ? name[..^matchingSuffix.Length] : name;
    }

    /// <summary>
    /// Adds a term to the list if it's not already present and not empty.
    /// </summary>
    private static void AddIfNew(List<string> terms, HashSet<string> seen, string term)
    {
        if (!string.IsNullOrWhiteSpace(term) && seen.Add(term))
        {
            terms.Add(term);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Mod>> MatchModsAsync(
        IEnumerable<Mod> mods,
        SemanticVersioning.Version sptVersion,
        Action<Mod, int, int>? progressCallback = null,
        CancellationToken cancellationToken = default
    )
    {
        var modList = mods.ToList();
        var totalCount = modList.Count;
        var completedCount = 0;

        var tasks = modList.Select(async mod =>
        {
            await MatchModAsync(mod, sptVersion, cancellationToken);

            var current = Interlocked.Increment(ref completedCount);
            progressCallback?.Invoke(mod, current, totalCount);

            return mod;
        });

        var results = await Task.WhenAll(tasks);
        return results;
    }

    /// <summary>
    /// Finds the best matching API result for a given mod using multiple comparison strategies.
    /// </summary>
    private static (ModSearchResult Result, int Confidence, MatchMethod Method)? FindBestMatch(
        Mod mod,
        List<ModSearchResult> searchResults
    )
    {
        // 1. Try exact normalized name match
        foreach (var result in searchResults)
        {
            if (ModNameNormalizer.IsExactMatch(mod.LocalName, result.Name))
            {
                return (result, MatchingConstants.ExactNameConfidence, MatchMethod.ExactName);
            }
        }

        // 2. Try exact match with component suffix removed
        var nameWithoutSuffix = RemoveComponentSuffix(mod.LocalName);
        if (!string.Equals(nameWithoutSuffix, mod.LocalName, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var result in searchResults)
            {
                if (ModNameNormalizer.IsExactMatch(nameWithoutSuffix, result.Name, removeComponentSuffixes: true))
                {
                    return (result, MatchingConstants.ExactNameConfidence - 2, MatchMethod.ExactName);
                }
            }
        }

        // 3. Try matching by slug (normalized)
        foreach (var result in searchResults)
        {
            if (!string.IsNullOrWhiteSpace(result.Slug))
            {
                // Compare normalized slug to normalized local name
                if (ModNameNormalizer.IsExactMatch(mod.LocalName, result.Slug, removeComponentSuffixes: true))
                {
                    return (result, MatchingConstants.ExactNameConfidence - 3, MatchMethod.ExactName);
                }

                // Also compare GUID name part to slug
                if (!string.IsNullOrWhiteSpace(mod.Guid))
                {
                    var guidName = ModNameNormalizer.ExtractNameFromGuid(mod.Guid);
                    if (ModNameNormalizer.IsExactMatch(guidName, result.Slug, removeComponentSuffixes: true))
                    {
                        return (result, MatchingConstants.ExactNameConfidence - 5, MatchMethod.ExactName);
                    }
                }
            }
        }

        // 4. Try matching by author + name combination
        if (
            !string.IsNullOrWhiteSpace(mod.LocalAuthor)
            && !string.Equals(mod.LocalAuthor, "Unknown", StringComparison.OrdinalIgnoreCase)
        )
        {
            foreach (var result in searchResults)
            {
                if (
                    result.Owner is not null
                    && string.Equals(mod.LocalAuthor, result.Owner.Name, StringComparison.OrdinalIgnoreCase)
                    && ModNameNormalizer.IsExactMatch(mod.LocalName, result.Name, removeComponentSuffixes: true)
                )
                {
                    return (result, MatchingConstants.ExactNameConfidence, MatchMethod.ExactName);
                }
            }
        }

        // 5. Try fuzzy matching with minimum threshold
        var bestFuzzyMatch = searchResults
            .Select(r => new
            {
                Result = r,
                NameScore = ModNameNormalizer.GetFuzzyMatchScore(mod.LocalName, r.Name),
                SlugScore = !string.IsNullOrWhiteSpace(r.Slug)
                    ? ModNameNormalizer.GetFuzzyMatchScore(mod.LocalName, r.Slug)
                    : 0,
            })
            .Select(x => new { x.Result, Score = Math.Max(x.NameScore, x.SlugScore) })
            .Where(x => x.Score >= MatchingConstants.MinimumFuzzyMatchScore)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestFuzzyMatch is null)
        {
            return null;
        }

        // Scale confidence based on fuzzy score
        var confidence = (int)(bestFuzzyMatch.Score * MatchingConstants.FuzzyNameConfidence / 100.0);

        return (bestFuzzyMatch.Result, confidence, MatchMethod.FuzzyName);
    }
}
