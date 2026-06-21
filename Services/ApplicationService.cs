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
            ShowVersionUpdateTable(mods);

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
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold blue]Verifying Forge records for {mods.Count} mods...[/]");

        await AnsiConsole
            .Progress()
            .Columns(
                new SpinnerColumn(Spinner.Known.Dots) { Style = Style.Parse("blue") },
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn()
            )
            .StartAsync(async ctx =>
            {
                var progressTask = ctx.AddTask("[grey]Querying Forge API[/]", maxValue: mods.Count);

                await modMatchingService.MatchModsAsync(
                    mods,
                    sptVersion,
                    (_, current, _) =>
                    {
                        progressTask.Value = current;
                    },
                    cancellationToken
                );

                progressTask.StopTask();
            });

        AnsiConsole.MarkupLine("[green]Forge verification complete![/]");

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

        AnsiConsole.WriteLine();

        // Build set of installed mod GUIDs
        var installedGuids = mods.Where(m => !string.IsNullOrWhiteSpace(m.Guid))
            .Select(m => m.Guid)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Count matched mods for progress
        var matchedCount = mods.Count(m => m.IsMatched && m.ApiModId.HasValue);

        AnsiConsole.MarkupLine($"[bold blue]Checking mod dependencies for {matchedCount} mods...[/]");

        var result = await AnsiConsole
            .Progress()
            .Columns(
                new SpinnerColumn(Spinner.Known.Dots) { Style = Style.Parse("blue") },
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn()
            )
            .StartAsync(async ctx =>
            {
                var progressTask = ctx.AddTask("[grey]Querying Forge API[/]", maxValue: matchedCount);

                var analysis = await modDependencyService.AnalyzeDependenciesAsync(
                    mods,
                    installedGuids,
                    (current, _) =>
                    {
                        progressTask.Value = current;
                    },
                    cancellationToken
                );

                progressTask.StopTask();
                return analysis;
            });

        if (result.RootMods.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No dependency information available.[/]");
            AnsiConsole.WriteLine();
            reporter.Rule();
            return;
        }

        AnsiConsole.MarkupLine("[green]Dependency analysis complete.[/]");
        AnsiConsole.WriteLine();

        // Display the dependency tree
        DisplayDependencyTree(result);

        // Display conflicts (warnings section)
        if (result.Conflicts.Count > 0)
        {
            DisplayDependencyConflicts(result.Conflicts);
        }

        // Display missing dependencies (download list section)
        if (result.MissingDependencies.Count > 0)
        {
            DisplayMissingDependencies(result.MissingDependencies);
        }

        if (!result.HasIssues)
        {
            AnsiConsole.MarkupLine("[green]All dependencies are satisfied![/]");
        }

        AnsiConsole.WriteLine();
        reporter.Rule();
    }

    /// <summary>
    /// Displays the dependency tree using Spectre.Console Tree component.
    /// </summary>
    /// <param name="result">The dependency analysis result.</param>
    private static void DisplayDependencyTree(DependencyAnalysisResult result)
    {
        var tree = new Tree("[bold white]Mod Dependencies[/]");

        // Sort mods alphabetically and add each with their dependencies as children
        var sortedMods = result.RootMods.OrderBy(n => n.Mod.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();

        foreach (var node in sortedMods)
        {
            var label = FormatDependencyNodeLabel(node);
            var treeNode = tree.AddNode(label);

            // Add dependencies as children recursively
            if (node.Children.Count > 0)
            {
                AddDependencyChildrenToTree(treeNode, node.Children);
            }
        }

        AnsiConsole.Write(tree);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Recursively adds dependency children to a tree node.
    /// </summary>
    /// <param name="parent">The parent tree node.</param>
    /// <param name="children">The child dependency nodes to add.</param>
    private static void AddDependencyChildrenToTree(TreeNode parent, List<DependencyNode> children)
    {
        foreach (var child in children.OrderBy(c => c.Mod.DisplayName, StringComparer.OrdinalIgnoreCase))
        {
            var label = FormatDependencyNodeLabel(child);
            var childTreeNode = parent.AddNode(label);

            // Recursively add nested dependencies
            if (child.Children.Count > 0)
            {
                AddDependencyChildrenToTree(childTreeNode, child.Children);
            }
        }
    }

    /// <summary>
    /// Formats the label for a dependency tree node.
    /// </summary>
    /// <param name="node">The dependency node.</param>
    /// <returns>Formatted markup string for the node label.</returns>
    private static string FormatDependencyNodeLabel(DependencyNode node)
    {
        var name = node.Mod.DisplayName.EscapeMarkup();
        var version = node.Mod.LocalVersion.EscapeMarkup();

        // Determine status color and indicator
        string statusIndicator;
        string nameColor;

        if (!node.IsInstalled)
        {
            statusIndicator = "[red](missing)[/]";
            nameColor = "red";
        }
        else if (node.DependencyInfo?.Conflict == true)
        {
            statusIndicator = "[yellow](conflict)[/]";
            nameColor = "yellow";
        }
        else
        {
            statusIndicator = "";
            nameColor = "white";
        }

        // Build the label with optional link
        string label;
        if (!string.IsNullOrWhiteSpace(node.Mod.ApiUrl))
        {
            label = $"[link={node.Mod.ApiUrl}][{nameColor}]{name}[/][/] [grey]v{version}[/]";
        }
        else if (node.DependencyInfo != null && node.DependencyInfo.Id > 0)
        {
            var url = ForgeUrls.ModPage(node.DependencyInfo.Id, node.DependencyInfo.Slug);
            label = $"[link={url}][{nameColor}]{name}[/][/] [grey]v{version}[/]";
        }
        else
        {
            label = $"[{nameColor}]{name}[/] [grey]v{version}[/]";
        }

        if (!string.IsNullOrWhiteSpace(statusIndicator))
        {
            label += $" {statusIndicator}";
        }

        return label;
    }

    /// <summary>
    /// Displays dependency conflicts in the warning style.
    /// </summary>
    /// <param name="conflicts">List of conflicts to display.</param>
    private static void DisplayDependencyConflicts(List<DependencyConflict> conflicts)
    {
        AnsiConsole.MarkupLine("[yellow]Dependency conflicts:[/]");

        foreach (var conflict in conflicts)
        {
            var nameDisplay = $"[white]{conflict.ModName.EscapeMarkup()}[/]";

            AnsiConsole.MarkupLine($"  {nameDisplay}");
            AnsiConsole.MarkupLine($"    [yellow]- {conflict.Description.EscapeMarkup()}[/]");

            if (conflict.DependencyInfo.Id > 0)
            {
                var url = ForgeUrls.ModPage(conflict.DependencyInfo.Id, conflict.DependencyInfo.Slug);
                AnsiConsole.MarkupLine($"      [grey]View on Forge:[/] [link]{url}[/]");
            }
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays missing dependencies in the download list style.
    /// </summary>
    /// <param name="missingDeps">List of missing dependencies to display.</param>
    private static void DisplayMissingDependencies(List<MissingDependency> missingDeps)
    {
        AnsiConsole.MarkupLine("[red]Missing dependencies:[/]");

        foreach (var dep in missingDeps)
        {
            // Link mod name to Forge page
            string nameDisplay;
            if (dep.ModId > 0 && !string.IsNullOrWhiteSpace(dep.Slug))
            {
                var url = ForgeUrls.ModPage(dep.ModId, dep.Slug);
                nameDisplay = $"[link={url}]{dep.Name.EscapeMarkup()}[/]";
            }
            else
            {
                nameDisplay = $"[white]{dep.Name.EscapeMarkup()}[/]";
            }

            AnsiConsole.MarkupLine($"  {nameDisplay}");
            AnsiConsole.MarkupLine(
                $"    [grey]Recommended version:[/] [green]{dep.RecommendedVersion.EscapeMarkup()}[/]"
            );

            if (!string.IsNullOrWhiteSpace(dep.DownloadLink))
            {
                AnsiConsole.MarkupLine($"    [grey]Download:[/] [link]{dep.DownloadLink}[/]");
            }
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays a table showing version information for all verified mods.
    /// </summary>
    /// <param name="mods">Processed mods.</param>
    private void ShowVersionUpdateTable(List<Mod> mods)
    {
        // Group by API mod ID to avoid duplicates, select the one with the highest version
        var verifiedMods = mods.Where(m => m.IsMatched && m.LatestVersion is not null)
            .GroupBy(m => m.ApiModId!.Value)
            .Select(g => g.OrderByDescending(m => SemVer.ParseOrZero(m.LocalVersion)).First())
            .ToList();

        if (verifiedMods.Count == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold blue]Checking for mod updates...[/]");
        AnsiConsole.MarkupLine(
            "[white]This tool depends on mod authors to use and update valid version numbers. If you notice a version number in the Current Version column that is incorrect, please contact the author of the mod to have it updated.[/]"
        );
        AnsiConsole.WriteLine();

        var table = new Table()
            .Title("[blue]Mod Version Summary[/]")
            .BorderColor(Color.Grey)
            .AddColumn("[white]Name[/]")
            .AddColumn("[white]Author[/]")
            .AddColumn("[white]Current Version[/]")
            .AddColumn("[white]Latest Version[/]");

        foreach (var mod in verifiedMods)
        {
            var (displayName, displayAuthor) = FormatModDisplayStrings(mod.DisplayName, mod.DisplayAuthor);

            var latestVersionDisplay = FormatVersionDisplay(mod);

            // Link mod name to Forge page if available
            var nameDisplay = !string.IsNullOrWhiteSpace(mod.ApiUrl)
                ? $"[link={mod.ApiUrl}]{displayName.EscapeMarkup()}[/]"
                : displayName.EscapeMarkup();

            table.AddRow(
                nameDisplay,
                displayAuthor.EscapeMarkup(),
                mod.LocalVersion.EscapeMarkup(),
                latestVersionDisplay
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine(
            "[grey]Version colors: [green]Up to date[/] | [red]Update available[/] | [darkorange]Update blocked[/] | [blue]Newer than latest[/][/]"
        );

        // Display mods with available updates
        var modsWithUpdates = verifiedMods.Where(m => m.UpdateStatus == UpdateStatus.UpdateAvailable).ToList();
        if (modsWithUpdates.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]Updates available:[/]");

            foreach (var mod in modsWithUpdates)
            {
                // Link mod name to Forge page if available
                var nameDisplay = !string.IsNullOrWhiteSpace(mod.ApiUrl)
                    ? $"[link={mod.ApiUrl}]{mod.DisplayName.EscapeMarkup()}[/]"
                    : $"[white]{mod.DisplayName.EscapeMarkup()}[/]";

                AnsiConsole.MarkupLine($"  {nameDisplay}");
                AnsiConsole.MarkupLine(
                    $"    [grey]{mod.LocalVersion.EscapeMarkup()}[/] [yellow]->[/] [green]{mod.LatestVersion!.EscapeMarkup()}[/]"
                );

                if (string.IsNullOrWhiteSpace(mod.DownloadLink))
                {
                    continue;
                }

                AnsiConsole.MarkupLine($"    [grey]Download:[/] [link]{mod.DownloadLink}[/]");
            }
        }

        // Display mods with blocked updates
        var modsWithBlockedUpdates = verifiedMods.Where(m => m.UpdateStatus == UpdateStatus.UpdateBlocked).ToList();
        if (modsWithBlockedUpdates.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[darkorange]Updates blocked:[/]");

            foreach (var mod in modsWithBlockedUpdates)
            {
                var nameDisplay = !string.IsNullOrWhiteSpace(mod.ApiUrl)
                    ? $"[link={mod.ApiUrl}]{mod.DisplayName.EscapeMarkup()}[/]"
                    : $"[white]{mod.DisplayName.EscapeMarkup()}[/]";

                AnsiConsole.MarkupLine($"  {nameDisplay}");
                AnsiConsole.MarkupLine(
                    $"    [grey]{mod.LocalVersion.EscapeMarkup()}[/] [yellow]->[/] [darkorange]{mod.LatestVersion!.EscapeMarkup()}[/]"
                );

                if (!string.IsNullOrWhiteSpace(mod.BlockReason))
                {
                    AnsiConsole.MarkupLine($"    [grey]Reason:[/] {FormatBlockReason(mod.BlockReason).EscapeMarkup()}");
                }

                if (mod.BlockingMods is { Count: > 0 })
                {
                    foreach (var blocker in mod.BlockingMods)
                    {
                        AnsiConsole.MarkupLine(
                            $"    [grey]Blocked by:[/] {blocker.Name.EscapeMarkup()} [grey]({blocker.Constraint.EscapeMarkup()})[/]"
                        );
                    }
                }
            }
        }

        AnsiConsole.WriteLine();
        reporter.Rule();
        AnsiConsole.WriteLine();

        AnsiConsole.Write(new FigletText("FIN").LeftJustified().Color(Color.Fuchsia));
        AnsiConsole.MarkupLine("[fuchsia]Scroll up to read details about your mods![/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Pro tip:    Mod names are clickable.[/]");
        AnsiConsole.MarkupLine("[grey]Expert tip: Read the mod page before installing or updating mods.[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            "[white]Find an issue [italic]with this tool[/]? Find Refringe on Discord, or [link=https://github.com/refringe/SPT-Check-Mods/issues/new]submit a bug report[/].[/]"
        );
    }

    /// <summary>
    /// Formats the version display with appropriate color coding.
    /// </summary>
    private static string FormatVersionDisplay(Mod mod)
    {
        if (mod.LatestVersion is null)
        {
            return "[grey]No versions found[/]";
        }

        return mod.UpdateStatus switch
        {
            UpdateStatus.UpToDate => $"[green]{mod.LatestVersion.EscapeMarkup()}[/]",
            UpdateStatus.UpdateAvailable => $"[red]{mod.LatestVersion.EscapeMarkup()}[/]",
            UpdateStatus.UpdateBlocked => $"[darkorange]{mod.LatestVersion.EscapeMarkup()}[/]",
            UpdateStatus.NewerInstalled => $"[blue]{mod.LatestVersion.EscapeMarkup()}[/]",
            UpdateStatus.NoVersionsFound => "[grey]No versions found[/]",
            _ => mod.LatestVersion.EscapeMarkup(),
        };
    }

    /// <summary>
    /// Formats a raw block reason string from the API into a human-readable description.
    /// </summary>
    private static string FormatBlockReason(string reason)
    {
        return reason switch
        {
            "dependency_constraint_violation" => "A dependency has a version constraint that prevents this update",
            "chain_dependency_conflict" => "A dependency chain conflict prevents this update",
            _ => reason.Replace('_', ' '),
        };
    }

    /// <summary>
    /// Formats mod name and author strings for display with proper truncation.
    /// </summary>
    private static (string displayName, string displayAuthor) FormatModDisplayStrings(string modName, string author)
    {
        var displayName =
            modName.Length > MatchingConstants.MaxDisplayNameLength
                ? modName[..(MatchingConstants.MaxDisplayNameLength - 3)] + "..."
                : modName;
        var displayAuthor =
            author.Length > MatchingConstants.MaxDisplayAuthorLength
                ? author[..(MatchingConstants.MaxDisplayAuthorLength - 3)] + "..."
                : author;

        return (displayName, displayAuthor);
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
