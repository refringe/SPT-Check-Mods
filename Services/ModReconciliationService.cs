using CheckMods.Models;
using CheckMods.Services.Interfaces;
using CheckMods.Utils;
using Microsoft.Extensions.Logging;

namespace CheckMods.Services;

/// <summary>
/// Service responsible for reconciling server and client mod components.
/// Matches components of the same mod and selects the best version when duplicates exist.
/// </summary>
public sealed class ModReconciliationService(ILogger<ModReconciliationService> logger) : IModReconciliationService
{
    /// <inheritdoc />
    public ModReconciliationResult ReconcileMods(List<Mod> serverMods, List<Mod> clientMods)
    {
        logger.LogDebug(
            "Reconciling {ServerCount} server mods with {ClientCount} client mods",
            serverMods.Count,
            clientMods.Count
        );

        List<ModPair> reconciledPairs = [];
        var matchedServerIndices = new HashSet<int>();
        var matchedClientIndices = new HashSet<int>();

        // Try to match each client mod with a server mod
        for (var clientIdx = 0; clientIdx < clientMods.Count; clientIdx++)
        {
            var clientMod = clientMods[clientIdx];

            for (var serverIdx = 0; serverIdx < serverMods.Count; serverIdx++)
            {
                if (matchedServerIndices.Contains(serverIdx))
                {
                    continue;
                }

                var serverMod = serverMods[serverIdx];

                if (!ModsMatch(serverMod, clientMod))
                {
                    continue;
                }

                var (selectedMod, notes) = SelectBestMod(serverMod, clientMod);

                // Update the selected mod with the paired component path
                selectedMod.PairedComponentPath = selectedMod == serverMod ? clientMod.FilePath : serverMod.FilePath;

                reconciledPairs.Add(
                    new ModPair
                    {
                        ServerMod = serverMod,
                        ClientMod = clientMod,
                        SelectedMod = selectedMod,
                        Notes = notes,
                    }
                );

                matchedServerIndices.Add(serverIdx);
                matchedClientIndices.Add(clientIdx);
                break;
            }
        }

        // Collect unmatched mods
        var unmatchedServerMods = serverMods.Where((_, idx) => !matchedServerIndices.Contains(idx)).ToList();

        var unmatchedClientMods = clientMods.Where((_, idx) => !matchedClientIndices.Contains(idx)).ToList();

        // Build final mod list: reconciled pairs (selected mod) + unmatched mods
        var allMods = reconciledPairs
            .Select(p => p.SelectedMod)
            .Concat(unmatchedServerMods)
            .Concat(unmatchedClientMods)
            .ToList();

        logger.LogDebug(
            "Reconciliation complete. Pairs: {PairCount}, Unmatched server: {UnmatchedServer}, Unmatched client: {UnmatchedClient}",
            reconciledPairs.Count,
            unmatchedServerMods.Count,
            unmatchedClientMods.Count
        );

        return new ModReconciliationResult
        {
            Mods = allMods,
            ReconciledPairs = reconciledPairs,
            UnmatchedServerMods = unmatchedServerMods,
            UnmatchedClientMods = unmatchedClientMods,
        };
    }

    /// <summary>
    /// Determines if two mods are the same mod (server and client components).
    /// </summary>
    private static bool ModsMatch(Mod serverMod, Mod clientMod)
    {
        // Direct GUID match (case-insensitive)
        if (
            !string.IsNullOrWhiteSpace(serverMod.Guid)
            && !string.IsNullOrWhiteSpace(clientMod.Guid)
            && string.Equals(serverMod.Guid, clientMod.Guid, StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        // Normalized name matching (with server/client suffix removal)
        if (ModNameNormalizer.IsExactMatch(serverMod.LocalName, clientMod.LocalName, removeComponentSuffixes: true))
        {
            return true;
        }

        // Try matching GUID name component against mod name
        var serverGuidName = ModNameNormalizer.ExtractNameFromGuid(serverMod.Guid);
        var clientGuidName = ModNameNormalizer.ExtractNameFromGuid(clientMod.Guid);

        if (!string.IsNullOrEmpty(serverGuidName) && !string.IsNullOrEmpty(clientGuidName))
        {
            // Compare GUID names
            if (ModNameNormalizer.IsExactMatch(serverGuidName, clientGuidName, removeComponentSuffixes: true))
            {
                return true;
            }

            // Compare GUID name to local name
            if (
                ModNameNormalizer.IsExactMatch(serverGuidName, clientMod.LocalName, removeComponentSuffixes: true)
                || ModNameNormalizer.IsExactMatch(serverMod.LocalName, clientGuidName, removeComponentSuffixes: true)
            )
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Selects the best mod from a server/client pair based on version and completeness.
    /// </summary>
    private static (Mod SelectedMod, List<string> Notes) SelectBestMod(Mod serverMod, Mod clientMod)
    {
        List<string> notes = [];

        // Check for GUID mismatch
        if (!string.Equals(serverMod.Guid, clientMod.Guid, StringComparison.OrdinalIgnoreCase))
        {
            notes.Add($"GUID mismatch: server '{serverMod.Guid}' vs client '{clientMod.Guid}'");
        }

        // Compare versions
        var serverVersion = ParseVersion(serverMod.LocalVersion);
        var clientVersion = ParseVersion(clientMod.LocalVersion);

        if (serverVersion is not null && clientVersion is not null)
        {
            if (serverVersion != clientVersion)
            {
                notes.Add($"Version mismatch: server '{serverMod.LocalVersion}' vs client '{clientMod.LocalVersion}'");
            }

            // Select the mod with the higher version
            if (clientVersion > serverVersion)
            {
                return (clientMod, notes);
            }

            if (serverVersion > clientVersion)
            {
                return (serverMod, notes);
            }
        }
        else if (clientVersion is not null && serverVersion is null)
        {
            notes.Add($"Server mod has invalid version: '{serverMod.LocalVersion}'");
            return (clientMod, notes);
        }
        else if (serverVersion is not null && clientVersion is null)
        {
            notes.Add($"Client mod has invalid version: '{clientMod.LocalVersion}'");
            return (serverMod, notes);
        }

        // Versions are equal or both invalid - prefer server mod (has SPT version info)
        return (serverMod, notes);
    }

    /// <summary>
    /// Parses a version string, returning null if invalid.
    /// </summary>
    private static SemanticVersioning.Version? ParseVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        try
        {
            return new SemanticVersioning.Version(version);
        }
        catch
        {
            return null;
        }
    }
}
