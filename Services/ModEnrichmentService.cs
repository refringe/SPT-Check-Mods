using CheckMods.Models;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace CheckMods.Services;

/// <summary>
/// Service responsible for enriching matched mods with additional API data such as version information.
/// </summary>
[Injectable(InjectionType.Transient)]
public sealed class ModEnrichmentService(IForgeApiService forgeApiService, ILogger<ModEnrichmentService> logger)
    : IModEnrichmentService
{
    /// <inheritdoc />
    public async Task EnrichAllWithVersionDataAsync(
        IEnumerable<Mod> mods,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Enriching mods with version data");

        // Filter to only matched mods and deduplicate by API mod ID
        var matchedMods = mods.Where(m => m.IsMatched && m.ApiModId.HasValue).ToList();

        // Group by API mod ID to deduplicate (paired server/client mods share the same API ID)
        var uniqueModsById = matchedMods.GroupBy(m => m.ApiModId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        if (uniqueModsById.Count == 0)
        {
            logger.LogDebug("No matched mods to enrich");
            return;
        }

        logger.LogDebug("Enriching {ModCount} unique mods", uniqueModsById.Count);

        // Create request items for the batch API call
        var modUpdates = uniqueModsById
            .Select(kvp => (ModId: kvp.Key, CurrentVersion: kvp.Value[0].LocalVersion))
            .ToList();

        // Make single batch API call
        var updatesResult = await forgeApiService.GetModUpdatesAsync(modUpdates, sptVersion, cancellationToken);

        // Extract the data or return early on error/not found
        if (!updatesResult.TryPickT0(out var updatesData, out _))
        {
            return;
        }

        // Helper to process update categories
        void ProcessUpdates<T>(IEnumerable<T>? updates, Func<T, int> getModId, Action<Mod, T> updateAction)
        {
            if (updates is null)
            {
                return;
            }

            var modsToUpdate = updates
                .Where(u => uniqueModsById.ContainsKey(getModId(u)))
                .SelectMany(u => uniqueModsById[getModId(u)].Select(m => (Mod: m, Update: u)));

            foreach (var (mod, update) in modsToUpdate)
            {
                updateAction(mod, update);
            }
        }

        // Process all update categories
        ProcessUpdates(updatesData.SafeToUpdate, u => u.ModId, (m, u) => m.UpdateFromSafeToUpdate(u));
        ProcessUpdates(updatesData.Blocked, b => b.ModId, (m, b) => m.UpdateFromBlocked(b));
        ProcessUpdates(updatesData.UpToDate, u => u.ModId, (m, u) => m.UpdateFromUpToDate(u));
        ProcessUpdates(updatesData.Incompatible, i => i.ModId, (m, i) => m.UpdateFromIncompatible(i));
    }
}
