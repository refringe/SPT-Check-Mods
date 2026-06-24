using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Service responsible for matching local mods with their Forge API counterparts.
/// </summary>
public interface IModMatchingService
{
    /// <summary>
    /// Matches a single mod with the Forge API using GUID lookup and fuzzy name fallback, updating its metadata in-place.
    /// </summary>
    /// <param name="mod">The mod to match.</param>
    /// <param name="sptVersion">The SPT version for compatibility filtering.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The same mod instance, updated with API match data if found.</returns>
    Task<Mod> MatchModAsync(
        Mod mod,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Matches multiple mods with the Forge API in parallel, updating each mod's metadata in-place.
    /// </summary>
    /// <param name="mods">The mods to match.</param>
    /// <param name="sptVersion">The SPT version for compatibility filtering.</param>
    /// <param name="progressCallback">Optional callback for progress reporting.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The same mod instances, updated with API match data where found.</returns>
    Task<IReadOnlyList<Mod>> MatchModsAsync(
        IEnumerable<Mod> mods,
        SemanticVersioning.Version sptVersion,
        Action<Mod, int, int>? progressCallback = null,
        CancellationToken cancellationToken = default
    );
}
