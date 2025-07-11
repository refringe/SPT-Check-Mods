using System.Text.Json;
using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using Spectre.Console;

namespace CheckMods.Services;

/// <summary>
/// Service responsible for server-side mod discovery, validation, and compatibility checking. Handles reading
/// package.json files, SPT version validation, and coordinating with the Forge API.
/// </summary>
public class ModService(IForgeApiService forgeApiService, ModMatchingService matchingService) : IModService
{
    private readonly ModMatchingService _matchingService = matchingService;

    /// <summary>
    /// Reads and validates the SPT version from the core.json configuration file.
    /// </summary>
    /// <param name="sptPath">Path to the SPT installation directory.</param>
    /// <returns>Validated SPT version or null if validation fails.</returns>
    public async Task<SemanticVersioning.Version?> GetAndValidateSptVersionAsync(string sptPath)
    {
        var coreJsonPath = Path.Combine(sptPath, "SPT_Data", "Server", "configs", "core.json");
        if (!File.Exists(coreJsonPath))
        {
            AnsiConsole.MarkupLine(
                $"[red]Error: Could not find SPT installation. Run this file in the same directory as [italic]SPT.Server.exe[/].[/]"
            );
            return null;
        }

        string? localSptVersionStr;
        try
        {
            var jsonContent = await File.ReadAllTextAsync(coreJsonPath);
            var coreConfig = JsonSerializer.Deserialize<CoreConfig>(jsonContent);
            localSptVersionStr = coreConfig?.SptVersion;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error reading or parsing 'core.json':[/] {ex.Message}");
            return null;
        }

        if (string.IsNullOrWhiteSpace(localSptVersionStr))
        {
            AnsiConsole.MarkupLine("[red]Error: SPT version not found in 'core.json'.[/]");
            return null;
        }

        AnsiConsole.Markup(
            $"Found local SPT version [bold blue]{localSptVersionStr}[/]. Validating with Forge API... "
        );
        var isValid = await forgeApiService.ValidateSptVersionAsync(localSptVersionStr);

        if (isValid)
        {
            AnsiConsole.MarkupLine("[green]OK[/]");
            return new SemanticVersioning.Version(localSptVersionStr);
        }

        AnsiConsole.MarkupLine("[red]Failed.[/]");
        return null;
    }

    /// <summary>
    /// Scans the mods directory for server mods and reads their package.json files.
    /// </summary>
    /// <param name="modsDirPath">Path to the user/mods directory.</param>
    /// <returns>List of discovered mod packages.</returns>
    public List<ModPackage> GetLocalMods(string modsDirPath)
    {
        var mods = new List<ModPackage>();
        if (!Directory.Exists(modsDirPath))
            return mods;

        foreach (var dir in Directory.GetDirectories(modsDirPath))
        {
            var packageJsonPath = Path.Combine(dir, "package.json");
            if (!File.Exists(packageJsonPath))
                continue;
            try
            {
                var jsonContent = File.ReadAllText(packageJsonPath);
                var package = JsonSerializer.Deserialize<ModPackage>(
                    jsonContent,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                if (package != null)
                    mods.Add(package);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"[orange1]Warning:[/] Could not parse [grey]{packageJsonPath}[/]. Skipping. Reason: {ex.Message}"
                );
            }
        }
        return mods;
    }

    /// <summary>
    /// Processes a list of local mods for compatibility with the specified SPT version. Performs parallel processing
    /// with progress reporting and handles user confirmations for low-confidence matches.
    /// </summary>
    /// <param name="localMods">List of local mods to process.</param>
    /// <param name="sptVersion">SPT version to check compatibility against.</param>
    /// <returns>List of processed mods with their compatibility status.</returns>
    public async Task<List<ProcessedMod>> ProcessModCompatibility(
        List<ModPackage> localMods,
        SemanticVersioning.Version sptVersion
    )
    {
        AnsiConsole.MarkupLine(
            $"[blue]Checking [bold]{localMods.Count}[/] server mods for compatibility with SPT {sptVersion}...[/]"
        );

        // Process mods in parallel
        var (processedMods, pendingConfirmations) = await ProcessModsInParallelAsync(localMods, sptVersion);

        // Sort by original order for consistent output
        var orderedResults = processedMods.OrderBy(p => localMods.IndexOf(p.Mod)).ToList();

        AnsiConsole.MarkupLine("[green]Server mod matching complete![/]");
        AnsiConsole.WriteLine();

        // Handle pending confirmations
        if (pendingConfirmations.Count != 0)
        {
            await HandlePendingConfirmationsAsync(pendingConfirmations, orderedResults);
        }

        // Show final results - count from actual records
        AnsiConsole.MarkupLine("[bold blue]Server mod matches complete! Results:[/]");
        var finalVerifiedCount = orderedResults.Count(m => m.Record.Status == ModStatus.Verified);
        var finalNoMatchCount = orderedResults.Count(m => m.Record.Status == ModStatus.NoMatch);
        var finalIncompatibleCount = orderedResults.Count(m => m.Record.Status == ModStatus.Incompatible);
        var finalInvalidVersionCount = orderedResults.Count(m => m.Record.Status == ModStatus.InvalidVersion);

        AnsiConsole.MarkupLine($"- [green]Verified:[/] {finalVerifiedCount}");
        AnsiConsole.MarkupLine($"- [red]No Match:[/] {finalNoMatchCount}");
        AnsiConsole.MarkupLine($"- [maroon]Incompatible:[/] {finalIncompatibleCount}");
        if (finalInvalidVersionCount > 0)
        {
            AnsiConsole.MarkupLine($"- [red]Invalid Version:[/] {finalInvalidVersionCount}");
        }
        AnsiConsole.WriteLine();

        return orderedResults;
    }

    /// <summary>
    /// Processes mods in parallel with progress reporting.
    /// </summary>
    /// <param name="localMods">Mods to process.</param>
    /// <param name="sptVersion">SPT version for compatibility.</param>
    /// <returns>Tuple of processed mods and pending confirmations.</returns>
    private async Task<(
        List<ProcessedMod> processedMods,
        List<PendingConfirmation> pendingConfirmations
    )> ProcessModsInParallelAsync(List<ModPackage> localMods, SemanticVersioning.Version sptVersion)
    {
        var processedMods = new List<ProcessedMod>();
        var pendingConfirmations = new List<PendingConfirmation>();
        var completedCount = 0;
        var totalCount = localMods.Count;

        var tasks = localMods.Select(
            async (mod, index) =>
            {
                var result = await ProcessSingleModWithConfirmationAsync(mod, sptVersion, index);

                // Thread-safe progress reporting and result collection
                var current = Interlocked.Increment(ref completedCount);
                lock (processedMods)
                {
                    processedMods.Add(result.ProcessedMod);

                    if (result.PendingConfirmation != null)
                    {
                        pendingConfirmations.Add(result.PendingConfirmation);
                    }

                    // Update progress
                    var percentage = (current * 100) / totalCount;
                    AnsiConsole.MarkupLine(
                        $"[grey]Progress: {current}/{totalCount} ({percentage}%) - {result.ProcessedMod.Mod.Name.EscapeMarkup()} - {result.ProcessedMod.Status.ToDisplayString()}[/]"
                    );
                }

                return result;
            }
        );

        await Task.WhenAll(tasks);
        return (processedMods, pendingConfirmations);
    }

    /// <summary>
    /// Handles user confirmations for low-confidence mod matches.
    /// </summary>
    /// <param name="pendingConfirmations">List of pending confirmations.</param>
    /// <param name="orderedResults">An ordered list of processed mods to update.</param>
    private static async Task HandlePendingConfirmationsAsync(
        List<PendingConfirmation> pendingConfirmations,
        List<ProcessedMod> orderedResults
    )
    {
        AnsiConsole.MarkupLine($"[yellow]Found {pendingConfirmations.Count} match(es) that need confirmation...[/]");

        // Display summary table
        DisplayConfirmationTable(pendingConfirmations);

        // Process confirmations
        var sortedPendingConfirmations = pendingConfirmations.OrderBy(p => p.ResultIndex).ToList();
        foreach (var pending in sortedPendingConfirmations)
        {
            var displayMod = pending.OriginalMod;
            var confirmation = await AnsiConsole.ConfirmAsync(
                $"[yellow]Is '[white]{displayMod.Name.EscapeMarkup()}[/]' by '[white]{displayMod.Author.EscapeMarkup()}[/]' the same as '[white]{pending.ApiMatch.Name.EscapeMarkup()}[/]' by '[white]{pending.ApiMatch.Owner?.Name.EscapeMarkup() ?? "N/A"}[/]'? ([grey]Confidence: {pending.ConfidenceScore}%[/])[/]"
            );

            // Find the corresponding result and update it
            var resultToUpdate = orderedResults.FirstOrDefault(r => r.Mod == pending.OriginalMod);
            resultToUpdate?.UpdateConfirmation(confirmation, pending.ApiMatch);
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays a table of pending confirmations.
    /// </summary>
    /// <param name="pendingConfirmations">List of pending confirmations to display.</param>
    private static void DisplayConfirmationTable(List<PendingConfirmation> pendingConfirmations)
    {
        var table = new Table();
        table.AddColumn("Local Server Mod");
        table.AddColumn("Author");
        table.AddColumn("API Match");
        table.AddColumn("API Author");
        table.AddColumn("Confidence");

        var sortedPendingConfirmations = pendingConfirmations.OrderBy(p => p.ResultIndex).ToList();

        foreach (var pending in sortedPendingConfirmations)
        {
            table.AddRow(
                pending.OriginalMod.Name.EscapeMarkup(),
                pending.OriginalMod.Author.EscapeMarkup(),
                pending.ApiMatch.Name.EscapeMarkup(),
                pending.ApiMatch.Owner?.Name.EscapeMarkup() ?? "N/A",
                $"{pending.ConfidenceScore}%"
            );
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Processes a single mod with support for low-confidence match confirmation.
    /// </summary>
    /// <param name="mod">The mod to process.</param>
    /// <param name="sptVersion">SPT version for compatibility checking.</param>
    /// <param name="index">Index for ordering results.</param>
    /// <returns>A processing result with optional pending confirmation.</returns>
    private async Task<ProcessingResult> ProcessSingleModWithConfirmationAsync(
        ModPackage mod,
        SemanticVersioning.Version sptVersion,
        int index
    )
    {
        // Apply any name/author updates from configuration
        var modToCheck = ApplyModUpdates(mod);

        ModStatus status;
        ModSearchResult? matchedMod = null;
        PendingConfirmation? pendingConfirmation = null;
        var confidenceScore = 0;

        // Check blacklist
        if (IsModBlacklisted(modToCheck))
        {
            status = ModStatus.Incompatible;
        }
        else
        {
            // Process mod compatibility
            var result = await ProcessModCompatibilityStatusAsync(modToCheck, mod, sptVersion, index);
            status = result.Status;
            matchedMod = result.MatchedMod;
            pendingConfirmation = result.PendingConfirmation;
            confidenceScore = result.ConfidenceScore;
        }

        var processedMod = new ProcessedMod(mod, status, matchedMod);
        if (matchedMod != null && status is ModStatus.Verified or ModStatus.NeedsConfirmation)
        {
            processedMod.UpdateFromApiMatch(matchedMod, confidenceScore);
        }

        return new ProcessingResult(processedMod, pendingConfirmation);
    }

    /// <summary>
    /// Applies configured mod name/author updates.
    /// </summary>
    /// <param name="mod">Original mod package.</param>
    /// <returns>Updated mod package or original if no updates apply.</returns>
    private static ModPackage ApplyModUpdates(ModPackage mod)
    {
        var update = AppConstants.ModUpdates.FirstOrDefault(u => u.FromName == mod.Name && u.FromAuthor == mod.Author);

        if (update == null)
            return mod;

        return new ModPackage
        {
            Name = update.ToName,
            Author = update.ToAuthor,
            Version = mod.Version,
            SptVersion = mod.SptVersion,
        };
    }

    /// <summary>
    /// Checks if a mod is blacklisted.
    /// </summary>
    /// <param name="mod">Mod to check.</param>
    /// <returns>True if blacklisted.</returns>
    private static bool IsModBlacklisted(ModPackage mod)
    {
        return AppConstants.BlacklistedMods.Any(b =>
            b.Name.Equals(mod.Name, StringComparison.OrdinalIgnoreCase)
            && b.Author.Equals(mod.Author, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// Processes mod compatibility status including version checking and API verification.
    /// </summary>
    /// <param name="modToCheck">Mod to check (may have updates applied).</param>
    /// <param name="originalMod">Original mod package.</param>
    /// <param name="sptVersion">SPT version.</param>
    /// <param name="index">Result index.</param>
    /// <returns>Compatibility status result.</returns>
    private async Task<(
        ModStatus Status,
        ModSearchResult? MatchedMod,
        PendingConfirmation? PendingConfirmation,
        int ConfidenceScore
    )> ProcessModCompatibilityStatusAsync(
        ModPackage modToCheck,
        ModPackage originalMod,
        SemanticVersioning.Version sptVersion,
        int index
    )
    {
        try
        {
            // Check version compatibility
            var range = new SemanticVersioning.Range(modToCheck.SptVersion);
            if (!range.IsSatisfied(sptVersion.ToString()))
            {
                return (ModStatus.Incompatible, null, null, 0);
            }

            // Verify with API
            var verificationResult = await VerifyModWithApiAsync(modToCheck, sptVersion);

            if (verificationResult.IsVerified)
            {
                return (ModStatus.Verified, verificationResult.MatchedMod, null, verificationResult.ConfidenceScore);
            }

            if (!verificationResult.RequiresConfirmation)
                return (ModStatus.NoMatch, null, null, verificationResult.ConfidenceScore);

            var pendingConfirmation = new PendingConfirmation(
                modToCheck,
                originalMod,
                verificationResult.MatchedMod!,
                verificationResult.ConfidenceScore,
                index
            );
            return (
                ModStatus.NeedsConfirmation,
                verificationResult.MatchedMod,
                pendingConfirmation,
                verificationResult.ConfidenceScore
            );
        }
        catch
        {
            return (ModStatus.InvalidVersion, null, null, 0);
        }
    }

    /// <summary>
    /// Verifies a mod against the Forge API using fuzzy matching.
    /// </summary>
    /// <param name="mod">Mod to verify.</param>
    /// <param name="sptVersion">SPT version for an API query.</param>
    /// <returns>Verification result with match details.</returns>
    private async Task<ModVerificationResult> VerifyModWithApiAsync(
        ModPackage mod,
        SemanticVersioning.Version sptVersion
    )
    {
        try
        {
            var searchResults = await forgeApiService.SearchModsAsync(mod.Name, sptVersion);
            if (searchResults.Count == 0)
                return new ModVerificationResult(false, null);

            var bestMatch = ModMatchingService.FindBestMatch(mod, searchResults);
            if (bestMatch == null)
                return new ModVerificationResult(false, null);

            return new ModVerificationResult(
                bestMatch.IsHighConfidence,
                bestMatch.ApiResult,
                bestMatch.IsMediumConfidence,
                bestMatch.Score
            );
        }
        catch
        {
            return new ModVerificationResult(false, null);
        }
    }
}
