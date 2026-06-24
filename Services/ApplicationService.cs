using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using CheckMods.Utils;
using Microsoft.Extensions.Logging;
using SPTarkov.DI.Annotations;

namespace CheckMods.Services;

/// <summary>
/// Main application service that orchestrates the SPT mod checking workflow.
/// </summary>
[Injectable(InjectionType.Transient)]
public sealed class ApplicationService(
    IForgeApiService forgeApiService,
    ISptInstallationService sptInstallationService,
    IModScannerService modScannerService,
    IModReconciliationService modReconciliationService,
    IModMatchingService modMatchingService,
    IModEnrichmentService modEnrichmentService,
    IModDependencyService modDependencyService,
    IUpdateCheckService updateCheckService,
    IIgnoredUpdateStore ignoredUpdateStore,
    IRemoteIgnoreFileClient remoteIgnoreFileClient,
    IModCheckReporter reporter,
    ILogger<ApplicationService> logger
) : IApplicationService
{
    /// <summary>
    /// Main entry point for the application. Runs the mod checking workflow.
    /// </summary>
    /// <param name="args">Command line arguments. The first argument can be the SPT installation path.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task<IReadOnlyList<Mod>> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting mod check workflow");
        reporter.Banner();

        try
        {
            // Remove any legacy API key file from previous versions.
            RemoveLegacyApiKeyFile();

            logger.LogDebug("Validating SPT path");
            var sptPath = GetValidatedSptPath(args);
            if (sptPath is null)
            {
                logger.LogWarning("SPT path validation failed, exiting");
                return [];
            }

            logger.LogInformation("Using SPT path: {SptPath}", sptPath);

            logger.LogDebug("Validating SPT installation");
            var sptVersion = await ValidateSptInstallationAsync(sptPath, cancellationToken);
            if (sptVersion is null)
            {
                logger.LogWarning("SPT version validation failed, exiting");
                return [];
            }

            logger.LogInformation("SPT version validated: {SptVersion}", sptVersion);

            // Must run after the SPT update check.
            logger.LogDebug("Checking for Check Mods updates");
            await CheckForCheckModsUpdateAsync(sptVersion, cancellationToken);

            // Offer to refresh the local ignore list from the author-maintained remote list (opt-in, default no).
            logger.LogDebug("Offering remote ignore list refresh");
            await MaybeFetchRemoteIgnoresAsync(cancellationToken);

            reporter.Blank();
            reporter.Heading("Loading mods...");

            logger.LogDebug("Checking for improperly installed mods");
            reporter.Status("Checking mod installation locations...");
            var misplacedReport = modScannerService.DetectMisplacedMods(sptPath, cancellationToken);
            if (misplacedReport.Any)
            {
                logger.LogWarning(
                    "Found {WrongFolder} misplaced mods and {CrossInstalled} cross-installed directories; excluding them from the remaining checks and continuing",
                    misplacedReport.WrongFolder.Count,
                    misplacedReport.CrossInstalled.Count
                );

                // Surface the problem but keep running.
                reporter.MisplacedMods(misplacedReport);
            }

            logger.LogDebug("Scanning and reconciling mods");
            var mods = await ScanAndReconcileModsAsync(sptPath, sptVersion, misplacedReport, cancellationToken);
            if (mods.Count == 0)
            {
                logger.LogInformation("No mods remaining after reconciliation");
                reporter.Warning("No mods remaining after reconciliation.");
                return [];
            }

            logger.LogInformation("Found {ModCount} mods after reconciliation", mods.Count);

            logger.LogDebug("Matching mods with Forge API");
            await MatchModsWithApiAsync(mods, sptVersion, cancellationToken);

            // Enrich matched mods with version data, then apply locally-stored update suppressions.
            logger.LogDebug("Enriching mods with version data");
            await EnrichModsWithVersionDataAsync(mods, sptVersion, cancellationToken);

            logger.LogDebug("Applying ignored updates");
            ApplyIgnoredUpdates(mods);

            // Suppressed false positives are skipped.
            logger.LogDebug("Checking mod version compatibility");
            CheckModVersionCompatibility(mods, sptVersion);

            logger.LogDebug("Checking mod dependencies");
            await CheckModDependenciesAsync(mods, cancellationToken);

            logger.LogDebug("Displaying results");
            reporter.VersionTable(mods);

            logger.LogInformation("Mod check workflow completed successfully");
            return mods;
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Operation was cancelled");
            reporter.Warning("Operation cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during mod check workflow");
            reporter.Exception(ex);
        }

        return [];
    }

    /// <summary>
    /// Offers to refresh the local ignore list from the author-maintained remote list. Opt-in with a default of "no",
    /// and skipped entirely when input is non-interactive or no remote URL is configured. Merges without overwriting
    /// existing local entries; any failure leaves the local list untouched.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    private async Task MaybeFetchRemoteIgnoresAsync(CancellationToken cancellationToken = default)
    {
        // Can't prompt without an interactive console (treated as "no"), and nothing to do without a configured URL.
        if (Console.IsInputRedirected || !remoteIgnoreFileClient.IsConfigured)
        {
            return;
        }

        reporter.Blank();
        reporter.Heading("Community ignore list...");

        if (reporter.PromptFetchRemoteIgnores())
        {
            var remote = await remoteIgnoreFileClient.FetchAsync(cancellationToken);
            if (remote is null)
            {
                reporter.RemoteIgnoresUnavailable();
            }
            else
            {
                reporter.RemoteIgnoresMerged(ignoredUpdateStore.MergeWithoutOverwrite(remote));
            }
        }

        reporter.Blank();
        reporter.Rule();
    }

    /// <summary>
    /// Flags any mod whose available update matches a stored suppression so it renders as ignored (treated as up to
    /// date).
    /// </summary>
    /// <param name="mods">The reconciled, enriched mods to evaluate.</param>
    private void ApplyIgnoredUpdates(List<Mod> mods)
    {
        foreach (var mod in mods)
        {
            if (mod.UpdateStatus == UpdateStatus.UpdateAvailable && ignoredUpdateStore.IsIgnored(mod))
            {
                mod.SetUpdateSuppressed(true);
            }
        }
    }

    /// <summary>
    /// Validates and returns the SPT installation path from arguments or current directory.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Validated SPT path or null if validation failed.</returns>
    private string? GetValidatedSptPath(string[] args)
    {
        reporter.Heading("Validating SPT installation...");

        if (args.Length == 0)
        {
            var currentPath = Directory.GetCurrentDirectory();
            reporter.UsingPath(currentPath);
            return currentPath;
        }

        var safePath = SecurityHelper.GetSafePath(args[0]);
        if (safePath is null)
        {
            reporter.Error("Error: Invalid path provided.");
            return null;
        }

        if (!Directory.Exists(safePath))
        {
            reporter.DirectoryDoesNotExist(safePath);
            return null;
        }

        reporter.UsingPath(safePath);
        return safePath;
    }

    /// <summary>
    /// Validates the SPT installation and returns the version.
    /// </summary>
    /// <param name="sptPath">Path to SPT installation.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>SPT version or null if validation failed.</returns>
    private async Task<SemanticVersioning.Version?> ValidateSptInstallationAsync(
        string sptPath,
        CancellationToken cancellationToken = default
    )
    {
        var sptVersion = await sptInstallationService.GetAndValidateSptVersionAsync(sptPath, cancellationToken);
        if (sptVersion is null)
        {
            return null;
        }

        reporter.SptVersionValidated(sptVersion.ToString());

        await CheckForSptUpdatesAsync(sptVersion, cancellationToken);

        reporter.Blank();
        reporter.Rule();
        reporter.Blank();
        return sptVersion;
    }

    /// <summary>
    /// Checks for available SPT updates and displays them to the user.
    /// </summary>
    /// <param name="currentVersion">The currently installed SPT version.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    private async Task CheckForSptUpdatesAsync(
        SemanticVersioning.Version currentVersion,
        CancellationToken cancellationToken = default
    )
    {
        reporter.Blank();
        reporter.Status("Checking for SPT updates...");

        var availableUpdates = await sptInstallationService.CheckForSptUpdatesAsync(currentVersion, cancellationToken);

        if (availableUpdates.Count == 0)
        {
            reporter.Success("You are running the latest version of SPT!");
            return;
        }

        // Show only the latest available update
        reporter.SptUpdateAvailable(availableUpdates[0]);
    }

    /// <summary>
    /// Checks whether a newer version of Check Mods is available on the Forge and displays the result.
    /// </summary>
    /// <param name="sptVersion">The installed SPT version, used for compatibility filtering.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    private async Task CheckForCheckModsUpdateAsync(
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        reporter.Heading("Checking for Check Mods updates...");

        CheckModsUpdateResult result;
        try
        {
            result = await updateCheckService.CheckAsync(sptVersion, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Check Mods update check failed unexpectedly");
            reporter.Status("Could not check for Check Mods updates.");
            reporter.Blank();
            reporter.Rule();
            return;
        }

        reporter.CheckModsUpdate(result, sptVersion);
    }

    /// <summary>
    /// Scans mods from disk and reconciles server/client components.
    /// </summary>
    /// <param name="sptPath">Path to SPT installation.</param>
    /// <param name="sptVersion">SPT version for API lookups.</param>
    /// <param name="misplacedReport">Incorrectly installed mods which should be excluded from further operations.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    private async Task<List<Mod>> ScanAndReconcileModsAsync(
        string sptPath,
        SemanticVersioning.Version sptVersion,
        MisplacedModReport misplacedReport,
        CancellationToken cancellationToken = default
    )
    {
        var (serverMods, clientMods) = await modScannerService.ScanAllModsAsync(sptPath, cancellationToken);

        // Drop any mods flagged as misplaced.
        if (misplacedReport.Any)
        {
            serverMods = ExcludeMisplacedMods(serverMods, misplacedReport);
            clientMods = ExcludeMisplacedMods(clientMods, misplacedReport);
        }

        if (serverMods.Count == 0 && clientMods.Count == 0)
        {
            logger.LogInformation("No mods found in SPT installation");
            reporter.NoModsFound();
            return [];
        }

        reporter.Success($"Loaded {serverMods.Count} server mods and {clientMods.Count} client mods.");

        // Fetch API info for mods with warnings.
        var modsWithWarnings = serverMods.Concat(clientMods).Where(m => m.HasWarnings).ToList();
        if (modsWithWarnings.Count > 0)
        {
            await FetchSourceCodeUrlsForModsAsync(modsWithWarnings, sptVersion, cancellationToken);
        }

        reporter.LoadingWarnings(modsWithWarnings);

        reporter.Blank();
        reporter.Rule();
        reporter.Blank();
        reporter.Heading("Reconciling mod components...");

        var result = modReconciliationService.ReconcileMods(serverMods, clientMods);

        // Fetch API info for mods with reconciliation warnings.
        var pairsWithNotes = result.ReconciledPairs.Where(p => p.Notes.Count > 0).ToList();
        if (pairsWithNotes.Count > 0)
        {
            await FetchSourceCodeUrlsForPairedModsAsync(pairsWithNotes, sptVersion, cancellationToken);
        }

        reporter.ReconciliationResults(result);

        return result.Mods.ToList();
    }

    /// <summary>
    /// Returns the mods with any misplaced entries removed: those whose DLL path was flagged as misplaced, plus any
    /// inside a cross-installed directory whose intruder couldn't be identified (the whole folder is excluded).
    /// </summary>
    private static List<Mod> ExcludeMisplacedMods(List<Mod> mods, MisplacedModReport report)
    {
        var excludedFiles = new HashSet<string>(report.ExcludedFilePaths, StringComparer.OrdinalIgnoreCase);
        var excludedDirectories = report.ExcludedDirectories;

        return mods.Where(mod =>
                !excludedFiles.Contains(mod.FilePath)
                && !excludedDirectories.Any(directory => IsWithinDirectory(mod.FilePath, directory))
            )
            .ToList();
    }

    /// <summary>
    /// Determines whether <paramref name="filePath"/> lives inside <paramref name="directory"/> (or is that directory
    /// itself), comparing fully-resolved paths case-insensitively.
    /// </summary>
    private static bool IsWithinDirectory(string filePath, string directory)
    {
        var fullFile = Path.GetFullPath(filePath);
        var fullDirectory = Path.GetFullPath(directory);

        if (string.Equals(fullFile, fullDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var prefix = fullDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? fullDirectory
            : fullDirectory + Path.DirectorySeparatorChar;

        return fullFile.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Fetches source code URLs from the API for mods that have warnings.
    /// </summary>
    private async Task FetchSourceCodeUrlsForModsAsync(
        List<Mod> mods,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        // Dispatch the lookups concurrently and let the rate limiter throttle.
        await Task.WhenAll(mods.Select(mod => FetchSourceCodeUrlForModAsync(mod, sptVersion, cancellationToken)));
    }

    /// <summary>
    /// Fetches source code URLs from the API for paired mods with reconciliation warnings.
    /// Tries both server and client GUIDs to find the mod.
    /// </summary>
    private async Task FetchSourceCodeUrlsForPairedModsAsync(
        List<ModPair> pairs,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        // Dispatch the lookups concurrently and let the rate limiter throttle.
        await Task.WhenAll(pairs.Select(pair => FetchSourceCodeUrlForPairAsync(pair, sptVersion, cancellationToken)));
    }

    /// <summary>
    /// Fetches and applies Forge API info for a single reconciled pair, trying both the server and client GUIDs
    /// before falling back to a fuzzy name search.
    /// </summary>
    private async Task FetchSourceCodeUrlForPairAsync(
        ModPair pair,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken
    )
    {
        var selectedMod = pair.SelectedMod;

        // Skip if already has API info
        if (selectedMod.ApiSourceCodeUrl is not null || selectedMod.ApiUrl is not null)
        {
            return;
        }

        ModSearchResult? apiResult = null;

        // Collect all unique GUIDs to try (server GUID, client GUID)
        List<string> guidsToTry = [];
        if (!string.IsNullOrWhiteSpace(pair.ServerMod.Guid))
        {
            guidsToTry.Add(pair.ServerMod.Guid);
        }

        if (
            !string.IsNullOrWhiteSpace(pair.ClientMod.Guid)
            && !guidsToTry.Contains(pair.ClientMod.Guid, StringComparer.OrdinalIgnoreCase)
        )
        {
            guidsToTry.Add(pair.ClientMod.Guid);
        }

        // Try each GUID until we find a match
        foreach (var guid in guidsToTry)
        {
            var guidResult = await forgeApiService.GetModByGuidAsync(guid, sptVersion, cancellationToken);
            if (!guidResult.TryPickT0(out var match, out _))
            {
                continue;
            }

            apiResult = match;
            break;
        }

        // If not found by any GUID, try searching by name with fuzzy matching
        apiResult ??= await SearchModByNameAsync(selectedMod, sptVersion, cancellationToken);

        if (apiResult is null)
        {
            return;
        }

        selectedMod.UpdateFromApiMatch(apiResult);
    }

    /// <summary>
    /// Fetches source code URL from the API for a single mod.
    /// </summary>
    private async Task FetchSourceCodeUrlForModAsync(
        Mod mod,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        // Skip if already has API info
        if (mod.ApiSourceCodeUrl is not null || mod.ApiUrl is not null)
        {
            return;
        }

        ModSearchResult? apiResult = null;

        // Try to find the mod by GUID first
        if (!string.IsNullOrWhiteSpace(mod.Guid))
        {
            var guidResult = await forgeApiService.GetModByGuidAsync(mod.Guid, sptVersion, cancellationToken);
            if (guidResult.TryPickT0(out var match, out _))
            {
                apiResult = match;
            }
        }

        // If not found by GUID, try searching by name with fuzzy matching
        apiResult ??= await SearchModByNameAsync(mod, sptVersion, cancellationToken);

        if (apiResult is null)
        {
            return;
        }

        mod.UpdateFromApiMatch(apiResult);
    }

    /// <summary>
    /// Searches for a mod by name using fuzzy matching.
    /// </summary>
    private async Task<ModSearchResult?> SearchModByNameAsync(
        Mod mod,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(mod.LocalName))
        {
            return null;
        }

        var searchResult = mod.IsServerMod
            ? await forgeApiService.SearchModsAsync(mod.LocalName, sptVersion, cancellationToken)
            : await forgeApiService.SearchClientModsAsync(mod.LocalName, sptVersion, cancellationToken);

        // Extract search results or empty list on error
        var searchResults = searchResult.Match(
            results => results,
            _ => [] // ApiError
        );

        if (searchResults.Count == 0)
        {
            return null;
        }

        var normalizedLocalName = ModNameNormalizer.Normalize(mod.LocalName);

        // Try exact match first
        var apiResult = searchResults.FirstOrDefault(r =>
            ModNameNormalizer.Normalize(r.Name).Equals(normalizedLocalName, StringComparison.OrdinalIgnoreCase)
        );

        if (apiResult is not null)
        {
            return apiResult;
        }

        // If no exact match, try fuzzy match with high threshold
        var bestMatch = searchResults
            .Select(r => (Result: r, Score: ModNameNormalizer.GetFuzzyMatchScore(mod.LocalName, r.Name)))
            .Where(x => x.Score >= MatchingConstants.NameSearchFuzzyThreshold)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        return bestMatch.Result;
    }

    /// <summary>
    /// Matches mods with the Forge API.
    /// </summary>
    /// <param name="mods">Mods to match.</param>
    /// <param name="sptVersion">SPT version for compatibility filtering.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    private async Task MatchModsWithApiAsync(
        List<Mod> mods,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        reporter.Blank();
        reporter.Heading($"Verifying Forge records for {mods.Count} mods...");

        await reporter.RunForgeQueryProgressAsync(
            mods.Count,
            setValue =>
                modMatchingService.MatchModsAsync(
                    mods,
                    sptVersion,
                    (_, current, _) => setValue(current),
                    cancellationToken
                )
        );

        reporter.Success("Forge verification complete!");
        reporter.Blank();

        // Display warnings for mods that couldn't be verified
        reporter.UnverifiedMods(mods);

        reporter.Rule();
    }

    /// <summary>
    /// Enriches matched mods with version data from the API.
    /// </summary>
    /// <param name="mods">Mods to enrich.</param>
    /// <param name="sptVersion">SPT version for compatibility filtering.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    private async Task EnrichModsWithVersionDataAsync(
        List<Mod> mods,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        var matchedMods = mods.Where(m => m.IsMatched).ToList();

        if (matchedMods.Count == 0)
        {
            return;
        }

        await modEnrichmentService.EnrichAllWithVersionDataAsync(matchedMods, sptVersion, cancellationToken);
    }

    /// <summary>
    /// Checks mod version compatibility with the installed SPT version and displays results.
    /// </summary>
    /// <param name="mods">Mods to check.</param>
    /// <param name="sptVersion">The installed SPT version.</param>
    private void CheckModVersionCompatibility(List<Mod> mods, SemanticVersioning.Version sptVersion)
    {
        reporter.Blank();
        reporter.Heading("Checking mod version compatibility...");

        // Only check mods that are matched with the API and have versions stored, skipping those whose update was
        // dismissed as a false positive.
        var matchedMods = mods.Where(m => m.IsMatched && m.ApiVersions is { Count: > 0 } && !m.UpdateSuppressed)
            .ToList();

        foreach (var mod in matchedMods)
        {
            try
            {
                CheckModSptCompatibility(mod, sptVersion);
            }
            catch (Exception ex)
            {
                // Skip this mod and carry on with the rest.
                logger.LogWarning(ex, "Failed to check SPT compatibility for mod: {ModName}", mod.DisplayName);
                reporter.Warning($"Could not verify SPT compatibility for {mod.DisplayName}.");
            }
        }

        reporter.VersionCompatibilityResults(mods, sptVersion);
    }

    /// <summary>
    /// Evaluates a single matched mod's installed version against the installed SPT version, flagging it when the
    /// constraint can't be parsed or isn't satisfied.
    /// </summary>
    /// <param name="mod">The matched mod to evaluate.</param>
    /// <param name="sptVersion">The installed SPT version.</param>
    private void CheckModSptCompatibility(Mod mod, SemanticVersioning.Version sptVersion)
    {
        // Find the version that matches the installed local version.
        var installedApiVersion = mod.ApiVersions!.FirstOrDefault(v =>
            string.Equals(v.Version, mod.LocalVersion, StringComparison.OrdinalIgnoreCase)
        );

        if (installedApiVersion == null)
        {
            // Couldn't find the installed version in the API versions
            return;
        }

        // Check if the installed version's SPT constraint is compatible with the installed SPT version
        if (string.IsNullOrWhiteSpace(installedApiVersion.SptVersionConstraint))
        {
            return;
        }

        if (!SemanticVersioning.Range.TryParse(installedApiVersion.SptVersionConstraint, out var range))
        {
            // The constraint from Forge can't be parsed; surface a warning.
            reporter.Warning(
                $"Could not verify SPT compatibility for {mod.DisplayName}: Forge reported an invalid version constraint ({installedApiVersion.SptVersionConstraint})."
            );
            return;
        }

        if (range.IsSatisfied(sptVersion))
        {
            // The installed version is compatible - no issue
            return;
        }

        // The installed version is NOT compatible with the installed SPT version
        var reason = $"Version {mod.LocalVersion} requires SPT {installedApiVersion.SptVersionConstraint}";

        // Find the latest compatible version to suggest
        var compatibleApiVersion = mod.ApiVersions!.Where(v =>
                SemVer.SatisfiesRange(v.SptVersionConstraint, sptVersion)
            )
            .OrderByDescending(v => SemVer.ParseOrZero(v.Version))
            .FirstOrDefault();

        mod.SetLocalSptIncompatible(reason, compatibleApiVersion?.Version);
    }

    /// <summary>
    /// Checks mod dependencies and displays a dependency tree with any issues.
    /// </summary>
    /// <param name="mods">Mods to check dependencies for.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    private async Task CheckModDependenciesAsync(List<Mod> mods, CancellationToken cancellationToken = default)
    {
        if (!mods.Any(m => m.IsMatched))
        {
            return;
        }

        reporter.Blank();

        // Build set of installed mod GUIDs
        var installedGuids = mods.Where(m => !string.IsNullOrWhiteSpace(m.Guid))
            .Select(m => m.Guid)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Count matched mods for progress
        var matchedCount = mods.Count(m => m.IsMatched && m.ApiModId.HasValue);

        // Mods with an available update get a second dependency fetch (at the proposed version), deduped by API mod ID.
        // Include those in the progress total.
        var updatableCount = mods.Where(m =>
                m.IsMatched
                && m.ApiModId.HasValue
                && m.UpdateStatus == UpdateStatus.UpdateAvailable
                && !string.IsNullOrWhiteSpace(m.LatestVersion)
            )
            .Select(m => m.ApiModId!.Value)
            .Distinct()
            .Count();

        reporter.Heading($"Checking mod dependencies for {matchedCount} mods...");

        var result = await reporter.RunForgeQueryProgressAsync(
            matchedCount + updatableCount,
            setValue =>
                modDependencyService.AnalyzeDependenciesAsync(
                    mods,
                    installedGuids,
                    (current, _) => setValue(current),
                    cancellationToken
                )
        );

        reporter.DependencyResults(result);
    }

    /// <summary>
    /// Removes the legacy API key file written by previous versions. Best-effort: any failure is logged and ignored.
    /// </summary>
    private void RemoveLegacyApiKeyFile()
    {
        try
        {
            var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configDirectory = Path.GetFullPath(Path.Combine(appDataFolder, "SptCheckMods"));
            var configFilePath = Path.GetFullPath(Path.Combine(configDirectory, "apikey.txt"));

            if (!configFilePath.StartsWith(configDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                return;
            }

            if (!File.Exists(configFilePath))
            {
                return;
            }

            File.Delete(configFilePath);
            logger.LogInformation("Removed legacy API key file.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove legacy API key file");
        }
    }
}
