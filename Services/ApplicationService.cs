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
    IServerModService serverModService,
    IModScannerService modScannerService,
    IModReconciliationService modReconciliationService,
    IModMatchingService modMatchingService,
    IModEnrichmentService modEnrichmentService,
    IModDependencyService modDependencyService,
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
        DisplayBanner();

        try
        {
            // API key setup
            logger.LogDebug("Setting up API key");
            var apiKey = await SetupApiKeyAsync(cancellationToken);
            if (apiKey is null)
            {
                logger.LogWarning("API key setup failed, exiting");
                return;
            }

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
            await CheckModVersionCompatibilityAsync(mods, sptVersion);

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
    /// Funny taglines displayed randomly in the banner.
    /// </summary>
    private static readonly string[] _bannerTaglines =
    [
        "Cheeki breeki, your mods are peaky!",
        "No FiR tag required.",
        "Opachki! Your mods are showing.",
        "Warning: May cause gear fear.",
        "Fence would sell this for 3x the price.",
        "Not responsible for any leg meta incidents.",
        "Ref approved.",
        "Scav karma not affected by usage.",
        "No insurance fraud detected.",
        "Jaeger would make this a daily quest.",
        "Tested on scavs!.",
        "More reliable than a PM pistol.",
        "Killa can't spawn here. You're safe.",
        "Side effects may include mod addiction.",
        "Lighthouse rogues hate this one simple trick!",
        "Your stash is safe. Your mods? Let's see...",
        "Better odds than finding a GPU in raid.",
        "Tagilla tested, Tagilla approved.",
        "No extract campers were consulted.",
        "Mechanic charges extra for this service.",
        "Labs keycard not required.",
        "Results may vary based on desync.",
        "Powered by strong coffee.",
    ];

    /// <summary>
    /// Displays the application banner and introductory information.
    /// </summary>
    private static void DisplayBanner()
    {
        var tagline = _bannerTaglines[Random.Shared.Next(_bannerTaglines.Length)];

        AnsiConsole.Write(new FigletText("Check Mods").LeftJustified().Color(Color.Blue));
        AnsiConsole.MarkupLine("[fuchsia]A tool to check for mod issues and updates.[/]");
        AnsiConsole.MarkupLine($"[grey]{tagline}[/]");
        AnsiConsole.MarkupLine("[link]https://forge.sp-tarkov.com[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Writes a horizontal rule separator to the console.
    /// </summary>
    private static void WriteRule()
    {
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
    }

    /// <summary>
    /// Sets up the API key by either loading from storage or prompting the user.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Valid API key or null if setup failed.</returns>
    private async Task<string?> SetupApiKeyAsync(CancellationToken cancellationToken = default)
    {
        var apiKey = await GetAndValidateApiKey(cancellationToken);
        if (apiKey is null)
        {
            return null;
        }

        forgeApiService.SetApiKey(apiKey);
        return apiKey;
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
        var sptVersion = await serverModService.GetAndValidateSptVersionAsync(sptPath, cancellationToken);
        if (sptVersion is null)
        {
            return null;
        }

        AnsiConsole.MarkupLine($"[green]Successfully validated SPT Version:[/] [bold]{sptVersion}[/]");

        // Check for SPT updates
        await CheckForSptUpdatesAsync(sptVersion, cancellationToken);

        AnsiConsole.WriteLine();
        WriteRule();
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

        var availableUpdates = await serverModService.CheckForSptUpdatesAsync(currentVersion, cancellationToken);

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
        AnsiConsole.MarkupLine("[bold blue]Loading mods...[/]");

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

        DisplayLoadingWarnings(serverMods, clientMods);

        AnsiConsole.WriteLine();
        WriteRule();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold blue]Reconciling mod components...[/]");

        var result = modReconciliationService.ReconcileMods(serverMods, clientMods);

        // Fetch API info for mods with reconciliation warnings (try both GUIDs for paired mods)
        var pairsWithNotes = result.ReconciledPairs.Where(p => p.Notes.Count > 0).ToList();
        if (pairsWithNotes.Count > 0)
        {
            await FetchSourceCodeUrlsForPairedModsAsync(pairsWithNotes, sptVersion, cancellationToken);
        }

        DisplayReconciliationResults(result);

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

            selectedMod.UpdateFromApiMatch(apiResult, MatchingConstants.ExactGuidConfidence, MatchMethod.ExactGuid);
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

        mod.UpdateFromApiMatch(apiResult, MatchingConstants.ExactGuidConfidence, MatchMethod.ExactGuid);
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
    /// Displays warnings for mods with loading issues.
    /// </summary>
    private static void DisplayLoadingWarnings(List<Mod> serverMods, List<Mod> clientMods)
    {
        var modsWithWarnings = serverMods.Concat(clientMods).Where(m => m.HasWarnings).ToList();

        if (modsWithWarnings.Count == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Mod loading warnings:[/]");

        foreach (var mod in modsWithWarnings)
        {
            var modType = mod.IsServerMod ? "Server" : "Client";
            var modName = !string.IsNullOrWhiteSpace(mod.LocalName) ? mod.LocalName : Path.GetFileName(mod.FilePath);

            // Link mod name to Forge page if available
            var nameDisplay = !string.IsNullOrWhiteSpace(mod.ApiUrl)
                ? $"[link={mod.ApiUrl}]{modName.EscapeMarkup()}[/]"
                : $"[white]{modName.EscapeMarkup()}[/]";

            AnsiConsole.MarkupLine($"  [grey]{modType}:[/] {nameDisplay}");
            foreach (var warning in mod.LoadWarnings)
            {
                AnsiConsole.MarkupLine($"    [yellow]- {warning.EscapeMarkup()}[/]");
            }

            // Show source code URL if available, otherwise show Forge mod page
            if (!string.IsNullOrWhiteSpace(mod.ApiSourceCodeUrl))
            {
                AnsiConsole.MarkupLine($"      [grey]Please report:[/] [link]{mod.ApiSourceCodeUrl}[/]");
            }
            else if (!string.IsNullOrWhiteSpace(mod.ApiUrl))
            {
                AnsiConsole.MarkupLine($"      [grey]Please report:[/] [link]{mod.ApiUrl}[/]");
            }
        }
    }

    /// <summary>
    /// Displays the results of mod reconciliation.
    /// </summary>
    private static void DisplayReconciliationResults(ModReconciliationResult result)
    {
        var serverCount = result.ReconciledPairs.Count + result.UnmatchedServerMods.Count;
        var clientCount = result.ReconciledPairs.Count + result.UnmatchedClientMods.Count;
        AnsiConsole.MarkupLine($"[grey]Comparing {serverCount} server mods with {clientCount} client mods...[/]");

        if (result.ReconciledPairs.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No matching server/client mod pairs found.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Matched {result.ReconciledPairs.Count} server/client mod pairs.[/]");

            var pairsWithNotes = result.ReconciledPairs.Where(p => p.Notes.Count > 0).ToList();
            if (pairsWithNotes.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Reconciliation warnings:[/]");

                foreach (var pair in pairsWithNotes)
                {
                    var modName = pair.SelectedMod.LocalName;

                    // Link mod name to Forge page if available
                    var nameDisplay = !string.IsNullOrWhiteSpace(pair.SelectedMod.ApiUrl)
                        ? $"[link={pair.SelectedMod.ApiUrl}]{modName.EscapeMarkup()}[/]"
                        : $"[white]{modName.EscapeMarkup()}[/]";

                    AnsiConsole.MarkupLine($"  {nameDisplay}");
                    foreach (var note in pair.Notes)
                    {
                        AnsiConsole.MarkupLine($"    [yellow]- {note.EscapeMarkup()}[/]");
                    }

                    // Show source code URL if available, otherwise show Forge mod page
                    if (!string.IsNullOrWhiteSpace(pair.SelectedMod.ApiSourceCodeUrl))
                    {
                        AnsiConsole.MarkupLine(
                            $"      [grey]Please report:[/] [link]{pair.SelectedMod.ApiSourceCodeUrl}[/]"
                        );
                    }
                    else if (!string.IsNullOrWhiteSpace(pair.SelectedMod.ApiUrl))
                    {
                        AnsiConsole.MarkupLine($"      [grey]Please report:[/] [link]{pair.SelectedMod.ApiUrl}[/]");
                    }
                }

                AnsiConsole.WriteLine();
            }
        }

        AnsiConsole.MarkupLine(
            $"[grey]Final mod count: {result.Mods.Count} "
                + $"(matched pairs: {result.ReconciledPairs.Count}, "
                + $"server-only: {result.UnmatchedServerMods.Count}, "
                + $"client-only: {result.UnmatchedClientMods.Count})[/]"
        );
        AnsiConsole.WriteLine();
        WriteRule();
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
        DisplayUnverifiedModWarnings(mods);

        WriteRule();
    }

    /// <summary>
    /// Displays warnings for mods that could not be verified against the Forge API.
    /// </summary>
    /// <param name="mods">Mods to check for verification status.</param>
    private static void DisplayUnverifiedModWarnings(List<Mod> mods)
    {
        var unverifiedMods = mods.Where(m => m.Status == ModStatus.NoMatch).ToList();

        if (unverifiedMods.Count == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Unverified mods:[/]");

        foreach (var mod in unverifiedMods)
        {
            var modDisplayName = mod.DisplayName.EscapeMarkup();
            if (!string.IsNullOrWhiteSpace(mod.DisplayAuthor))
            {
                modDisplayName += $" by {mod.DisplayAuthor.EscapeMarkup()}";
            }

            AnsiConsole.MarkupLine($"  [white]{modDisplayName}[/]");
            AnsiConsole.MarkupLine($"    [yellow]- Could not find matching record on Forge[/]");
        }

        AnsiConsole.WriteLine();
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
    private Task CheckModVersionCompatibilityAsync(List<Mod> mods, SemanticVersioning.Version sptVersion)
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

            try
            {
                var range = new SemanticVersioning.Range(installedApiVersion.SptVersionConstraint);
                if (range.IsSatisfied(sptVersion))
                {
                    // The installed version is compatible - no issue
                    continue;
                }

                // The installed version is NOT compatible with the installed SPT version
                var reason = $"Version {mod.LocalVersion} requires SPT {installedApiVersion.SptVersionConstraint}";

                // Find a compatible version to suggest
                string? compatibleVersion = null;
                string? downloadLink = null;

                var compatibleApiVersion = mod.ApiVersions!.Where(v =>
                    {
                        if (string.IsNullOrWhiteSpace(v.SptVersionConstraint))
                        {
                            return false;
                        }

                        try
                        {
                            var versionRange = new SemanticVersioning.Range(v.SptVersionConstraint);
                            return versionRange.IsSatisfied(sptVersion);
                        }
                        catch
                        {
                            return false;
                        }
                    })
                    .OrderByDescending(v =>
                    {
                        try
                        {
                            return new SemanticVersioning.Version(v.Version);
                        }
                        catch
                        {
                            return new SemanticVersioning.Version(0, 0, 0);
                        }
                    })
                    .FirstOrDefault();

                if (compatibleApiVersion is not null)
                {
                    compatibleVersion = compatibleApiVersion.Version;
                    downloadLink = compatibleApiVersion.Link;
                }

                mod.SetLocalSptIncompatible(reason, compatibleVersion, downloadLink);
            }
            catch
            {
                // Invalid version constraint format from API - add a warning
                mod.LoadWarnings.Add(
                    $"Invalid SPT version constraint from Forge: {installedApiVersion.SptVersionConstraint}"
                );
            }
        }

        // Display results
        var incompatibleMods = mods.Where(m => m.IsLocalSptIncompatible).ToList();

        if (incompatibleMods.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]All mod versions are compatible![/]");
            AnsiConsole.WriteLine();
            WriteRule();
            return Task.CompletedTask;
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
                var forgeDownloadUrl =
                    $"https://forge.sp-tarkov.com/mod/download/{mod.ApiModId}/{mod.ApiSlug}/{mod.CompatibleVersionString}";
                AnsiConsole.MarkupLine($"      [grey]Download:[/] [link]{forgeDownloadUrl}[/]");
            }
        }

        AnsiConsole.WriteLine();
        WriteRule();

        return Task.CompletedTask;
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

        DependencyAnalysisResult result = null!;

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
                var progressTask = ctx.AddTask("[grey]Querying Forge API[/]", maxValue: matchedCount);

                result = await modDependencyService.AnalyzeDependenciesAsync(
                    mods,
                    installedGuids,
                    (current, _) =>
                    {
                        progressTask.Value = current;
                    },
                    cancellationToken
                );

                progressTask.StopTask();
            });

        if (result.RootMods.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No dependency information available.[/]");
            AnsiConsole.WriteLine();
            WriteRule();
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
        WriteRule();
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
            var url = $"https://forge.sp-tarkov.com/mod/{node.DependencyInfo.Id}/{node.DependencyInfo.Slug}";
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
                var url =
                    $"https://forge.sp-tarkov.com/mod/{conflict.DependencyInfo.Id}/{conflict.DependencyInfo.Slug}";
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
                var url = $"https://forge.sp-tarkov.com/mod/{dep.ModId}/{dep.Slug}";
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
    private static void ShowVersionUpdateTable(List<Mod> mods)
    {
        // Group by API mod ID to avoid duplicates, select the one with the highest version
        var verifiedMods = mods.Where(m => m.IsMatched && m.LatestVersion is not null)
            .GroupBy(m => m.ApiModId!.Value)
            .Select(g => g.OrderByDescending(m => GetVersionForComparison(m.LocalVersion)).First())
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
            "[grey]Version colors: [green]Up to date[/] | [red]Update available[/] | [yellow]Newer than latest[/][/]"
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

        AnsiConsole.WriteLine();
        WriteRule();
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
            UpdateStatus.NewerInstalled => $"[yellow]{mod.LatestVersion.EscapeMarkup()}[/]",
            UpdateStatus.NoVersionsFound => "[grey]No versions found[/]",
            _ => mod.LatestVersion.EscapeMarkup(),
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
    /// Parses a version string for comparison purposes.
    /// </summary>
    private static SemanticVersioning.Version GetVersionForComparison(string versionString)
    {
        if (string.IsNullOrWhiteSpace(versionString))
        {
            return new SemanticVersioning.Version(0, 0, 0);
        }

        try
        {
            return new SemanticVersioning.Version(versionString);
        }
        catch
        {
            return new SemanticVersioning.Version(0, 0, 0);
        }
    }

    /// <summary>
    /// Retrieves and validates the Forge API key from storage or prompts the user for a new one.
    /// Stores valid keys in %APPDATA%/SptCheckMods/apikey.txt for future use.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Valid API key or null if the user cancels.</returns>
    private async Task<string?> GetAndValidateApiKey(CancellationToken cancellationToken = default)
    {
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configFolder = SecurityHelper.GetSafePath(Path.Combine(appDataFolder, "SptCheckMods"));
        if (configFolder is null)
        {
            AnsiConsole.MarkupLine("[red]Error: Could not determine a safe configuration folder path.[/]");
            return null;
        }

        Directory.CreateDirectory(configFolder);
        var configFilePath = SecurityHelper.GetSafePath(Path.Combine(configFolder, "apikey.txt"), configFolder);
        if (configFilePath is null)
        {
            AnsiConsole.MarkupLine("[red]Error: Could not determine a safe configuration file path.[/]");
            return null;
        }

        if (!File.Exists(configFilePath))
        {
            return await PromptForNewApiKeyAsync(configFilePath, cancellationToken);
        }

        var savedKey = (await File.ReadAllTextAsync(configFilePath, cancellationToken)).Trim();
        if (string.IsNullOrWhiteSpace(savedKey))
        {
            return await PromptForNewApiKeyAsync(configFilePath, cancellationToken);
        }

        savedKey = SecurityHelper.SanitizeInput(savedKey);
        AnsiConsole.MarkupLine("[bold blue]Validating Forge API key...[/]");
        AnsiConsole.MarkupLine("Found saved API key. Validating...");

        var validationResult = await forgeApiService.ValidateApiKeyAsync(savedKey, cancellationToken);

        var outcome = validationResult.Match(
            _ =>
            {
                AnsiConsole.MarkupLine("[green]Saved API key is valid.[/]");
                AnsiConsole.WriteLine();
                WriteRule();
                AnsiConsole.WriteLine();
                return (string?)savedKey;
            },
            invalidKey =>
            {
                if (invalidKey.ShouldDeleteKey)
                {
                    AnsiConsole.MarkupLine("[red]The saved API key is invalid or has expired.[/]");
                    File.Delete(configFilePath);
                }

                return null;
            },
            _ =>
            {
                // Transient error - use saved key anyway
                AnsiConsole.MarkupLine(
                    "[yellow]Could not validate API key (API may be unavailable). Using saved key.[/]"
                );
                AnsiConsole.WriteLine();
                WriteRule();
                AnsiConsole.WriteLine();
                return (string?)savedKey;
            }
        );

        if (outcome is not null)
        {
            return outcome;
        }

        return await PromptForNewApiKeyAsync(configFilePath, cancellationToken);
    }

    /// <summary>
    /// Prompts the user to enter a new API key and validates it.
    /// </summary>
    /// <param name="configFilePath">Path to save the API key.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Valid API key or null if user cancels.</returns>
    private async Task<string?> PromptForNewApiKeyAsync(string configFilePath, CancellationToken cancellationToken)
    {
        AnsiConsole.MarkupLine("[yellow]Forge API key not found or was invalid.[/]");
        AnsiConsole.MarkupLine("Please generate an API key from your Forge account:");
        AnsiConsole.MarkupLine("[blue][link]https://forge.sp-tarkov.com/user/api-tokens[/][/]");

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var newKey = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your [green]API key[/]:").PromptStyle("green").Secret()
            );

            if (string.IsNullOrWhiteSpace(newKey))
            {
                continue;
            }

            newKey = SecurityHelper.SanitizeInput(newKey);

            AnsiConsole.Markup("Validating entered key... ");

            var newKeyResult = await forgeApiService.ValidateApiKeyAsync(newKey, cancellationToken);

            var isKeyValid = newKeyResult.Match(
                isValid => isValid,
                _ => false, // InvalidApiKey
                _ => false // ApiError
            );

            if (!isKeyValid)
            {
                AnsiConsole.MarkupLine(
                    "[red]Failed. The entered key is invalid or lacks 'read' permissions. Please try again.[/]"
                );
                continue;
            }

            AnsiConsole.MarkupLine("[green]OK[/]");
            await File.WriteAllTextAsync(configFilePath, newKey, cancellationToken);
            AnsiConsole.MarkupLine($"[green]API key saved successfully to:[/] [grey]{configFilePath}[/]");
            AnsiConsole.WriteLine();
            WriteRule();
            AnsiConsole.WriteLine();
            return newKey;
        }
    }
}
