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

    /// <summary>Writes a section heading.</summary>
    void Heading(string text);

    /// <summary>Writes a muted status line.</summary>
    void Status(string text);

    /// <summary>Writes a success line.</summary>
    void Success(string text);

    /// <summary>Writes a warning line.</summary>
    void Warning(string text);

    /// <summary>Writes an error line.</summary>
    void Error(string text);

    /// <summary>Warns that a mod DLL could not be read during scanning.</summary>
    void CouldNotReadModDll(string fileName, string reason);

    /// <summary>Warns that the SPT version could not be read.</summary>
    void CouldNotReadSptVersion(string reason);

    /// <summary>Warns that the BepInEx plugins directory could not be found during scanning.</summary>
    void PluginsDirectoryNotFound(string path);

    /// <summary>Warns that a client mod's metadata could not be extracted during scanning.</summary>
    void CouldNotExtractClientMod(string fileName, string reason);

    /// <summary>Runs work under a Forge-query progress bar, passing a callback to report completed-item counts.</summary>
    Task RunForgeQueryProgressAsync(int total, Func<Action<int>, Task> work);

    /// <summary>Runs work under a Forge-query progress bar and returns its result.</summary>
    Task<T> RunForgeQueryProgressAsync<T>(int total, Func<Action<int>, Task<T>> work);

    /// <summary>Reports the resolved SPT installation path.</summary>
    void UsingPath(string path);

    /// <summary>Reports that the provided directory does not exist.</summary>
    void DirectoryDoesNotExist(string path);

    /// <summary>Reports the local SPT version and that validation is in progress (no trailing newline).</summary>
    void ValidatingSptVersion(string version);

    /// <summary>Reports that the SPT version was validated.</summary>
    void SptVersionValidated(string version);

    /// <summary>Reports the latest available SPT update.</summary>
    void SptUpdateAvailable(SptVersionResult latest);

    /// <summary>Displays the outcome of the Check Mods self-update check.</summary>
    void CheckModsUpdate(CheckModsUpdateResult result, SemanticVersioning.Version sptVersion);

    /// <summary>Reports that no mods were found, with the expected install locations.</summary>
    void NoModsFound();

    /// <summary>Displays the SPT version-compatibility results for the checked mods.</summary>
    void VersionCompatibilityResults(List<Mod> mods, SemanticVersioning.Version sptVersion);

    /// <summary>Displays an unhandled exception.</summary>
    void Exception(Exception ex);

    /// <summary>Displays warnings for the given mods that have loading issues.</summary>
    void LoadingWarnings(List<Mod> modsWithWarnings);

    /// <summary>Displays the results of mod reconciliation.</summary>
    void ReconciliationResults(ModReconciliationResult result);

    /// <summary>Displays mods installed in the wrong location, shown before they're excluded from the remaining checks.</summary>
    void MisplacedMods(MisplacedModReport report);

    /// <summary>Lists mods with no Forge match (informational).</summary>
    void UnverifiedMods(List<Mod> mods);

    /// <summary>Displays the dependency tree and any conflicts or missing dependencies.</summary>
    void DependencyResults(DependencyAnalysisResult result);

    /// <summary>Displays the final version summary table and update/blocked lists.</summary>
    void VersionTable(List<Mod> mods);

    /// <summary>Asks whether to fetch the author-maintained remote ignore list. Defaults to no.</summary>
    bool PromptFetchRemoteIgnores();

    /// <summary>Reports the outcome of merging the remote ignore list (number of new entries added).</summary>
    void RemoteIgnoresMerged(int added);

    /// <summary>Reports that the remote ignore list couldn't be fetched and the local list is unchanged.</summary>
    void RemoteIgnoresUnavailable();

    /// <summary>
    /// Shows the end-of-run menu and returns the chosen action. The "open update pages" entry is offered only when
    /// <paramref name="openableUpdateCount"/> is greater than zero; the "manage ignored updates" entry only when
    /// <paramref name="canManageIgnoredUpdates"/> is true. "Close" is always available.
    /// </summary>
    EndOfRunChoice PromptEndOfRun(int openableUpdateCount, bool canManageIgnoredUpdates);

    /// <summary>
    /// Shows a checklist of update candidates (those in <paramref name="preIgnoredApiModIds"/> pre-checked) and returns
    /// the mods the user chose to ignore.
    /// </summary>
    IReadOnlyList<Mod> SelectUpdatesToIgnore(IReadOnlyList<Mod> candidates, ISet<int> preIgnoredApiModIds);

    /// <summary>Reports the outcome of opening update pages in the browser (how many of the total opened).</summary>
    void UpdatePagesOpened(int opened, int total);

    /// <summary>Asks whether to report the just-confirmed ignores as a GitHub issue. Defaults to no.</summary>
    bool PromptReportIgnores();

    /// <summary>
    /// Reports the outcome of opening the ignore-suggestion issue: whether the browser opened, the link (for manual
    /// use), and whether the entries were pre-filled.
    /// </summary>
    void IgnoreReportOpened(string url, bool browserOpened, bool prefilled);
}
