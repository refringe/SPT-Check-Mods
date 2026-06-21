using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Renders the user-facing output of the mod-check workflow. This is the single boundary between workflow logic and
/// the console, so the orchestrator and services stay free of direct console dependencies (and remain testable).
/// </summary>
public interface IModCheckReporter
{
    /// <summary>Writes the application banner and introductory information.</summary>
    void Banner();

    /// <summary>Writes a horizontal rule separator.</summary>
    void Rule();

    /// <summary>Writes a blank line.</summary>
    void Blank();

    /// <summary>Displays warnings for mods with loading issues.</summary>
    void LoadingWarnings(List<Mod> serverMods, List<Mod> clientMods);

    /// <summary>Displays the results of mod reconciliation.</summary>
    void ReconciliationResults(ModReconciliationResult result);

    /// <summary>Displays mods installed in the wrong location, shown right before the workflow halts.</summary>
    void MisplacedMods(MisplacedModReport report);

    /// <summary>Lists mods with no Forge match (informational).</summary>
    void UnverifiedMods(List<Mod> mods);

    /// <summary>Displays the dependency tree and any conflicts or missing dependencies.</summary>
    void DependencyResults(DependencyAnalysisResult result);

    /// <summary>Displays the final version summary table and update/blocked lists.</summary>
    void VersionTable(List<Mod> mods);
}
