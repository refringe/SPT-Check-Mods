using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using CheckMods.Utils;
using Microsoft.Extensions.Logging;
using Spectre.Console;
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
    IModCheckReporter reporter,
    ILogger<ApplicationService> logger
) : IApplicationService
{
    /// <summary>
    /// Main entry point for the application. Runs the mod checking workflow.
    /// </summary>
    /// <param name="args">Command line arguments. The first argument can be the SPT installation path.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    public async Task RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Starting mod check workflow");
        reporter.Banner();

        try
        {
            // Remove any legacy API key file from previous versions; the Forge API is now open and keyless.
            RemoveLegacyApiKeyFile();

            // SPT path validation
            logger.LogDebug("Validating SPT path");
            var sptPath = GetValidatedSptPath(args);
            if (sptPath is null)
            {
                logger.LogWarning("SPT path validation failed, exiting");
                return;
            }

            logger.LogInformation("Using SPT path: {SptPath}", sptPath);

            // SPT version validation
            logger.LogDebug("Validating SPT installation");
            var sptVersion = await ValidateSptInstallationAsync(sptPath, cancellationToken);
            if (sptVersion is null)
            {
                logger.LogWarning("SPT version validation failed, exiting");
                return;
            }

            logger.LogInformation("SPT version validated: {SptVersion}", sptVersion);

            // Check for Check Mods updates (Must run after the SPT update check)
            logger.LogDebug("Checking for Check Mods updates");
            await CheckForCheckModsUpdateAsync(sptVersion, cancellationToken);

            AnsiConsole.MarkupLine("[bold blue]Loading mods...[/]");

            // Detect improperly installed mods
            logger.LogDebug("Checking for improperly installed mods");
            AnsiConsole.MarkupLine("[grey]Checking mod installation locations...[/]");
            var misplacedReport = modScannerService.DetectMisplacedMods(sptPath, cancellationToken);
            if (misplacedReport.Any)
            {
                logger.LogWarning(
                    "Found {WrongFolder} misplaced mods and {CrossInstalled} cross-installed directories; halting",
                    misplacedReport.WrongFolder.Count,
                    misplacedReport.CrossInstalled.Count
                );
                reporter.MisplacedMods(misplacedReport);

                // Full stop
                Environment.ExitCode = 1;
                return;
            }

            // Load and reconcile local mods
            logger.LogDebug("Scanning and reconciling mods");
            var mods = await ScanAndReconcileModsAsync(sptPath, sptVersion, cancellationToken);
            if (mods.Count == 0)
            {
                logger.LogInformation("No mods remaining after reconciliation");
                AnsiConsole.MarkupLine("[yellow]No mods remaining after reconciliation.[/]");
                return;
            }

            logger.LogInformation("Found {ModCount} mods after reconciliation", mods.Count);

            // Match mods with Forge API
            logger.LogDebug("Matching mods with Forge API");
            await MatchModsWithApiAsync(mods, sptVersion, cancellationToken);

            // Check SPT version compatibility for matched mods
            logger.LogDebug("Checking mod version compatibility");
            CheckModVersionCompatibility(mods, sptVersion);

            // Enrich matched mods with version data and display results
            logger.LogDebug("Enriching mods with version data");
            await EnrichModsWithVersionDataAsync(mods, sptVersion, cancellationToken);

            // Check mod dependencies
            logger.LogDebug("Checking mod dependencies");
            await CheckModDependenciesAsync(mods, cancellationToken);

            // Display results
            logger.LogDebug("Displaying results");
            reporter.VersionTable(mods);

            logger.LogInformation("Mod check workflow completed successfully");
        }
        catch (OperationCanceledException)
        {
            logger.LogInformation("Operation was cancelled");
            AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during mod check workflow");
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
        }
    }

    /// <summary>
    /// Validates and returns the SPT installation path from arguments or current directory.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Validated SPT path or null if validation failed.</returns>
    private static string? GetValidatedSptPath(string[] args)
    {
        AnsiConsole.MarkupLine("[bold blue]Validating SPT installation...[/]");

        if (args.Length == 0)
        {
            var currentPath = Directory.GetCurrentDirectory();
            AnsiConsole.MarkupLine($"[grey]Using Path:[/] {currentPath.EscapeMarkup()}");
            return currentPath;
        }

        var safePath = SecurityHelper.GetSafePath(args[0]);
        if (safePath is null)
        {
            AnsiConsole.MarkupLine("[red]Error: Invalid path provided.[/]");
            return null;
        }

        if (!Directory.Exists(safePath))
        {
            AnsiConsole.MarkupLine($"[red]Error: Directory does not exist: {safePath.EscapeMarkup()}[/]");
            return null;
        }

        AnsiConsole.MarkupLine($"[grey]Using Path:[/] {safePath.EscapeMarkup()}");
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

        AnsiConsole.MarkupLine($"[green]Successfully validated SPT Version:[/] [bold]{sptVersion}[/]");

        // Check for SPT updates
        await CheckForSptUpdatesAsync(sptVersion, cancellationToken);

        AnsiConsole.WriteLine();
        reporter.Rule();
        AnsiConsole.WriteLine();
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
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Checking for SPT updates...[/]");

        var availableUpdates = await sptInstallationService.CheckForSptUpdatesAsync(currentVersion, cancellationToken);

        if (availableUpdates.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]You are running the latest version of SPT![/]");
            return;
        }

        // Show only the latest available update
        var latestUpdate = availableUpdates[0];
        var versionDisplay = $"[bold]{latestUpdate.Version.EscapeMarkup()}[/]";

        // Add mod count if available
        if (latestUpdate.ModCount > 0)
        {
            versionDisplay += $" [grey]({latestUpdate.ModCount} mods)[/]";
        }

        AnsiConsole.MarkupLine($"[yellow]SPT update available:[/] {versionDisplay}");

        // Add link on new line if available
        if (!string.IsNullOrWhiteSpace(latestUpdate.Link))
        {
            AnsiConsole.MarkupLine($"[grey]{latestUpdate.Link}[/]");
        }
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
        AnsiConsole.MarkupLine("[bold blue]Checking for Check Mods updates...[/]");

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
            AnsiConsole.MarkupLine("[grey]Could not check for Check Mods updates.[/]");
            AnsiConsole.WriteLine();
            reporter.Rule();
            return;
        }

        switch (result.Status)
        {
            case CheckModsUpdateStatus.UpdateAvailable:
                AnsiConsole.MarkupLine(
                    $"[yellow]A new version of Check Mods is available:[/] [bold]v{(result.LatestVersion ?? "?").EscapeMarkup()}[/] [grey](you have v{result.CurrentVersion.EscapeMarkup()})[/]"
                );
                if (!string.IsNullOrWhiteSpace(result.DownloadLink))
                {
                    AnsiConsole.MarkupLine($"[grey]Download:[/] [link]{result.DownloadLink}[/]");
                }
                break;

            case CheckModsUpdateStatus.UpToDate:
                AnsiConsole.MarkupLine(
                    $"[green]Check Mods is up to date (v{result.CurrentVersion.EscapeMarkup()}).[/]"
                );
                break;

            case CheckModsUpdateStatus.IncompatibleWithSpt:
                AnsiConsole.MarkupLine(
                    $"[grey]A newer version of Check Mods exists but isn't compatible with SPT {sptVersion.ToString().EscapeMarkup()}.[/]"
                );
                break;

            case CheckModsUpdateStatus.UnrecognizedBuild:
                AnsiConsole.MarkupLine(
                    $"[grey]You're running an unrecognized Check Mods build (v{result.CurrentVersion.EscapeMarkup()}). Consider the stable version on the Forge: v{(result.LatestVersion ?? "?").EscapeMarkup()}.[/]"
                );
                if (!string.IsNullOrWhiteSpace(result.DownloadLink))
                {
                    AnsiConsole.MarkupLine($"[grey]Download:[/] [link]{result.DownloadLink}[/]");
                }

                break;

            default:
                AnsiConsole.MarkupLine("[grey]Could not check for Check Mods updates.[/]");
                break;
        }

        AnsiConsole.WriteLine();
        reporter.Rule();
    }

    /// <summary>
    /// Scans mods from disk and reconciles server/client components.
    /// </summary>
    /// <param name="sptPath">Path to SPT installation.</param>
    /// <param name="sptVersion">SPT version for API lookups.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>List of reconciled mods.</returns>
    private async Task<List<Mod>> ScanAndReconcileModsAsync(
        string sptPath,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        var (serverMods, clientMods) = await modScannerService.ScanAllModsAsync(sptPath, cancellationToken);

        // Early exit if no mods found at all
        if (serverMods.Count == 0 && clientMods.Count == 0)
        {
            logger.LogInformation("No mods found in SPT installation");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]No mods found.[/]");
            AnsiConsole.MarkupLine("[grey]Server mods should be located in:[/] SPT/user/mods");
            AnsiConsole.MarkupLine("[grey]Client mods should be located in:[/] BepInEx/plugins");
            AnsiConsole.WriteLine();
            return [];
        }

        AnsiConsole.MarkupLine($"[green]Loaded {serverMods.Count} server mods and {clientMods.Count} client mods.[/]");

        // Fetch API info for mods with warnings to get source code URLs
        var modsWithWarnings = serverMods.Concat(clientMods).Where(m => m.HasWarnings).ToList();
        if (modsWithWarnings.Count > 0)
        {
            await FetchSourceCodeUrlsForModsAsync(modsWithWarnings, sptVersion, cancellationToken);
        }

        reporter.LoadingWarnings(serverMods, clientMods);

        AnsiConsole.WriteLine();
        reporter.Rule();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold blue]Reconciling mod components...[/]");

        var result = modReconciliationService.ReconcileMods(serverMods, clientMods);

        // Fetch API info for mods with reconciliation warnings (try both GUIDs for paired mods)
        var pairsWithNotes = result.ReconciledPairs.Where(p => p.Notes.Count > 0).ToList();
        if (pairsWithNotes.Count > 0)
        {
            await FetchSourceCodeUrlsForPairedModsAsync(pairsWithNotes, sptVersion, cancellationToken);
        }

        reporter.ReconciliationResults(result);

        return result.Mods.ToList();
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
        foreach (var mod in mods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await FetchSourceCodeUrlForModAsync(mod, sptVersion, cancellationToken);
        }
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
        foreach (var pair in pairs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var selectedMod = pair.SelectedMod;

            // Skip if already has API info
            if (selectedMod.ApiSourceCodeUrl is not null || selectedMod.ApiUrl is not null)
            {
                continue;
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
                continue;
            }

            selectedMod.UpdateFromApiMatch(apiResult);
        }
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
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold blue]Checking mod version compatibility...[/]");

        // Only check mods that are matched with the API and have versions stored
        var matchedMods = mods.Where(m => m.IsMatched && m.ApiVersions is { Count: > 0 }).ToList();

        foreach (var mod in matchedMods)
        {
            // Find the version that matches the installed local version
            var installedApiVersion = mod.ApiVersions!.FirstOrDefault(v =>
                v.Version.Equals(mod.LocalVersion, StringComparison.OrdinalIgnoreCase)
            );

            if (installedApiVersion == null)
            {
                // Couldn't find the installed version in the API versions
                continue;
            }

            // Check if the installed version's SPT constraint is compatible with the installed SPT version
            if (string.IsNullOrWhiteSpace(installedApiVersion.SptVersionConstraint))
            {
                continue;
            }

            if (!SemanticVersioning.Range.TryParse(installedApiVersion.SptVersionConstraint, out var range))
            {
                // Invalid version constraint format from API - add a warning
                mod.LoadWarnings.Add(
                    $"Invalid SPT version constraint from Forge: {installedApiVersion.SptVersionConstraint}"
                );
                continue;
            }

            if (range.IsSatisfied(sptVersion))
            {
                // The installed version is compatible - no issue
                continue;
            }

            // The installed version is NOT compatible with the installed SPT version
            var reason = $"Version {mod.LocalVersion} requires SPT {installedApiVersion.SptVersionConstraint}";

            // Find the latest compatible version to suggest
            var compatibleApiVersion = mod
                .ApiVersions!.Where(v => SemVer.SatisfiesRange(v.SptVersionConstraint, sptVersion))
                .OrderByDescending(v => SemVer.ParseOrZero(v.Version))
                .FirstOrDefault();

            mod.SetLocalSptIncompatible(reason, compatibleApiVersion?.Version);
        }

        // Display results
        var incompatibleMods = mods.Where(m => m.IsLocalSptIncompatible).ToList();

        if (incompatibleMods.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]All mod versions are compatible![/]");
            AnsiConsole.WriteLine();
            reporter.Rule();
            return;
        }

        AnsiConsole.MarkupLine($"[yellow]Found {incompatibleMods.Count} incompatible mod(s).[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Incompatible mods:[/]");

        foreach (var mod in incompatibleMods)
        {
            // Link mod name to Forge page if available
            var nameDisplay = !string.IsNullOrWhiteSpace(mod.ApiUrl)
                ? $"[link={mod.ApiUrl}]{mod.DisplayName.EscapeMarkup()}[/]"
                : $"[white]{mod.DisplayName.EscapeMarkup()}[/]";

            AnsiConsole.MarkupLine($"  {nameDisplay}");
            AnsiConsole.MarkupLine($"    [yellow]- {mod.IncompatibilityReason?.EscapeMarkup()}[/]");

            if (string.IsNullOrWhiteSpace(mod.CompatibleVersionString))
            {
                AnsiConsole.MarkupLine($"      [red]No compatible version available for SPT {sptVersion}[/]");
                continue;
            }

            AnsiConsole.MarkupLine(
                $"      [grey]Latest compatible version:[/] [green]{mod.CompatibleVersionString.EscapeMarkup()}[/]"
            );

            // Use Forge download link format
            if (mod.ApiModId.HasValue && !string.IsNullOrWhiteSpace(mod.ApiSlug))
            {
                var forgeDownloadUrl = ForgeUrls.Download(mod.ApiModId.Value, mod.ApiSlug, mod.CompatibleVersionString);
                AnsiConsole.MarkupLine($"      [grey]Download:[/] [link]{forgeDownloadUrl}[/]");
            }
        }

        AnsiConsole.WriteLine();
        reporter.Rule();
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

        reporter.Heading($"Checking mod dependencies for {matchedCount} mods...");

        var result = await reporter.RunForgeQueryProgressAsync(
            matchedCount,
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
    /// Removes the legacy API key file written by previous versions. The Forge API is now open and read-only,
    /// so no API key is stored. Best-effort: any failure is logged and ignored.
    /// </summary>
    private void RemoveLegacyApiKeyFile()
    {
        try
        {
            var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var configFilePath = Path.Combine(appDataFolder, "SptCheckMods", "apikey.txt");
            if (!File.Exists(configFilePath))
            {
                return;
            }

            File.Delete(configFilePath);
            logger.LogInformation("Removed legacy API key file: {Path}", configFilePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove legacy API key file");
        }
    }
}
