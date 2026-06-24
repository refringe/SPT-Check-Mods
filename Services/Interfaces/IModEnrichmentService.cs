using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Service responsible for enriching matched mods with additional API data such as version information.
/// </summary>
public interface IModEnrichmentService
{
    /// <summary>
    /// Enriches matched mods with version information using the batch updates API endpoint.
    /// </summary>
    /// <param name="mods">The mods to enrich.</param>
    /// <param name="sptVersion">The SPT version for compatibility filtering.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task EnrichAllWithVersionDataAsync(
        IEnumerable<Mod> mods,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    );
}
