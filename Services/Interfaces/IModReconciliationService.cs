using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Service responsible for reconciling server and client mod components.
/// Matches components of the same mod and selects the best version when duplicates exist.
/// </summary>
public interface IModReconciliationService
{
    /// <summary>
    /// Reconciles server and client mods, matching components of the same mod and selecting the best version.
    /// Updates paired mods with their component paths.
    /// </summary>
    /// <param name="serverMods">Scanned server mods.</param>
    /// <param name="clientMods">Scanned client mods.</param>
    /// <returns>Reconciliation result containing the unified mod list and pairing details.</returns>
    ModReconciliationResult ReconcileMods(List<Mod> serverMods, List<Mod> clientMods);
}

/// <summary>
/// Result of mod reconciliation containing the unified mod list and pairing information.
/// </summary>
public sealed class ModReconciliationResult
{
    /// <summary>
    /// All unique mods after reconciliation, with the best version selected when duplicates exist.
    /// </summary>
    public required IReadOnlyList<Mod> Mods { get; init; }

    /// <summary>
    /// Mods that were matched between server and client components.
    /// </summary>
    public required IReadOnlyList<ModPair> ReconciledPairs { get; init; }

    /// <summary>
    /// Server mods that had no matching client component.
    /// </summary>
    public required IReadOnlyList<Mod> UnmatchedServerMods { get; init; }

    /// <summary>
    /// Client mods that had no matching server component.
    /// </summary>
    public required IReadOnlyList<Mod> UnmatchedClientMods { get; init; }
}

/// <summary>
/// Represents a pair of server and client mods that were matched as the same mod.
/// </summary>
public sealed class ModPair
{
    /// <summary>
    /// The server mod component.
    /// </summary>
    public required Mod ServerMod { get; init; }

    /// <summary>
    /// The client mod component.
    /// </summary>
    public required Mod ClientMod { get; init; }

    /// <summary>
    /// The mod that was selected (with the higher version or more complete metadata).
    /// </summary>
    public required Mod SelectedMod { get; init; }

    /// <summary>
    /// Notes about the reconciliation (e.g., version mismatch, GUID mismatch).
    /// </summary>
    public required IReadOnlyList<string> Notes { get; init; }
}
