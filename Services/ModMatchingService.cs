using CheckMods.Models;
using FuzzySharp;

namespace CheckMods.Services;

/// <summary>
/// Service responsible for fuzzy matching logic between local mods and Forge API results.
/// </summary>
public class ModMatchingService
{
    private const int HighConfidenceThreshold = 75;
    private const int MediumConfidenceThreshold = 20;

    /// <summary>
    /// Calculates match confidence between a local mod and API search result.
    /// </summary>
    /// <param name="localMod">The local mod package.</param>
    /// <param name="apiResult">The API search result.</param>
    /// <returns>Match result with a confidence score and threshold status.</returns>
    public static MatchResult CalculateMatchConfidence(ModPackage localMod, ModSearchResult apiResult)
    {
        var nameScore = Fuzz.Ratio(localMod.Name, apiResult.Name);
        var authorScore = Fuzz.Ratio(localMod.Author, apiResult.Owner?.Name ?? "");
        var overallScore = (nameScore * 2 + authorScore) / 3;

        return new MatchResult
        {
            ApiResult = apiResult,
            Score = overallScore,
            IsHighConfidence = overallScore >= HighConfidenceThreshold,
            IsMediumConfidence = overallScore is >= MediumConfidenceThreshold and < HighConfidenceThreshold,
        };
    }

    /// <summary>
    /// Finds the best matching mod from API search results.
    /// </summary>
    /// <param name="localMod">The local mod to match.</param>
    /// <param name="searchResults">API search results.</param>
    /// <returns>Best match result or null if no suitable match found.</returns>
    public static MatchResult? FindBestMatch(ModPackage localMod, List<ModSearchResult> searchResults)
    {
        if (searchResults.Count == 0)
        {
            return null;
        }

        var scoredResults = searchResults
            .Select(result => CalculateMatchConfidence(localMod, result))
            .OrderByDescending(x => x.Score)
            .ToList();

        return scoredResults.FirstOrDefault();
    }
}
