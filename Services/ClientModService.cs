using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using Spectre.Console;

namespace CheckMods.Services;

/// <summary>
/// Service responsible for client-side mod discovery, validation, and compatibility checking. Handles scanning BepInEx
/// plugin DLLs and coordinating with the Forge API for client mod verification.
/// </summary>
public class ClientModService(IForgeApiService forgeApiService, BepInExScannerService scannerService) : IClientModService
{

    /// <summary>
    /// Scans the BepInEx plugins directory for client mods and extracts their metadata.
    /// </summary>
    /// <param name="pluginsPath">Path to the BepInEx plugins directory.</param>
    /// <returns>List of discovered client mod packages.</returns>
    public List<ClientModPackage> GetClientMods(string pluginsPath)
    {
        return scannerService.ScanPluginsDirectory(pluginsPath);
    }
    
    /// <summary>
    /// Applies configured client mod name/author updates to improve API matching.
    /// </summary>
    /// <param name="originalMod">Original client mod package.</param>
    /// <returns>Updated client mod package or original if no updates apply.</returns>
    private static ClientModPackage ApplyClientModUpdates(ClientModPackage originalMod)
    {
        var updateInfo = AppConstants.ClientModUpdates.FirstOrDefault(u =>
            u.FromName.Equals(originalMod.Name, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(u.FromAuthor) || u.FromAuthor.Equals(originalMod.Author, StringComparison.OrdinalIgnoreCase)));
        
        if (updateInfo == null) return originalMod;
        
        return new ClientModPackage
        {
            Name = updateInfo.ToName,
            Author = updateInfo.ToAuthor,
            Version = originalMod.Version,
            FilePath = originalMod.FilePath,
            PluginGuid = originalMod.PluginGuid
        };
    }
    
    /// <summary>
    /// Processes a list of client mods for compatibility with the specified SPT version. Performs parallel processing
    /// with progress reporting and handles user confirmations for low-confidence matches.
    /// </summary>
    /// <param name="clientMods">List of client mods to process.</param>
    /// <param name="sptVersion">SPT version to check compatibility against.</param>
    /// <returns>List of processed mods with their compatibility status.</returns>
    public async Task<List<ProcessedMod>> ProcessClientModCompatibility(List<ClientModPackage> clientMods, SemanticVersioning.Version sptVersion)
    {
        AnsiConsole.MarkupLine($"[blue]Checking [bold]{clientMods.Count}[/] client mods for compatibility...[/]");

        // Process mods in parallel
        var (processedMods, pendingConfirmations) = await ProcessModsInParallelAsync(clientMods, sptVersion);
        
        // Sort by original order for consistent output
        var modToIndexMap = clientMods.Select((mod, index) => new { mod, index })
            .ToDictionary(x => $"{x.mod.Name}|{x.mod.Author}", x => x.index);
            
        var orderedResults = processedMods.OrderBy(p => 
        {
            var key = $"{p.Mod.Name}|{p.Mod.Author}";
            return modToIndexMap.GetValueOrDefault(key, int.MaxValue);
        }).ToList();
        
        AnsiConsole.MarkupLine("[green]Client mod processing complete![/]");
        AnsiConsole.WriteLine();
        
        // Handle pending confirmations
        if (pendingConfirmations.Count != 0)
        {
            await HandlePendingConfirmationsAsync(pendingConfirmations, orderedResults);
        }
        
        // Show final results for client mods
        AnsiConsole.MarkupLine("[bold blue]Client mod matches complete! Results:[/]");
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
        
        return orderedResults;
    }
    
    /// <summary>
    /// Processes client mods in parallel with progress reporting.
    /// </summary>
    /// <param name="clientMods">Client mods to process.</param>
    /// <param name="sptVersion">SPT version for compatibility.</param>
    /// <returns>Tuple of processed mods and pending confirmations.</returns>
    private async Task<(List<ProcessedMod> processedMods, List<PendingConfirmation> pendingConfirmations)> 
        ProcessModsInParallelAsync(List<ClientModPackage> clientMods, SemanticVersioning.Version sptVersion)
    {
        var processedMods = new List<ProcessedMod>();
        var pendingConfirmations = new List<PendingConfirmation>();
        var completedCount = 0;
        var totalCount = clientMods.Count;
        
        var tasks = clientMods.Select(async (mod, index) =>
        {
            var result = await ProcessSingleClientModAsync(mod, index, sptVersion);
            
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
                AnsiConsole.MarkupLine($"[grey]Progress: {current}/{totalCount} ({percentage}%) - {result.ProcessedMod.Mod.Name.EscapeMarkup()} - {result.ProcessedMod.Status.ToDisplayString()}[/]");
            }
            
            return result;
        });

        await Task.WhenAll(tasks);
        return (processedMods, pendingConfirmations);
    }

    /// <summary>
    /// Handles user confirmations for low-confidence client mod matches.
    /// </summary>
    /// <param name="pendingConfirmations">List of pending confirmations.</param>
    /// <param name="orderedResults">An ordered list of processed mods to update.</param>
    private async Task HandlePendingConfirmationsAsync(List<PendingConfirmation> pendingConfirmations, 
        List<ProcessedMod> orderedResults)
    {
        AnsiConsole.MarkupLine($"[yellow]Found {pendingConfirmations.Count} client mod match(es) that need confirmation...[/]");
        
        // Display summary table
        DisplayConfirmationTable(pendingConfirmations);
        
        // Process confirmations
        var sortedPendingConfirmations = pendingConfirmations.OrderBy(p => p.ResultIndex).ToList();
        foreach (var pending in sortedPendingConfirmations)
        {
            var displayMod = pending.OriginalMod;
            var prompt = CreateConfirmationPrompt(displayMod, pending);
            var confirmation = await AnsiConsole.ConfirmAsync(prompt);
            
            // Find the corresponding result and update it
            var resultToUpdate = orderedResults.FirstOrDefault(r => r.Mod == pending.OriginalMod);
            resultToUpdate?.UpdateConfirmation(confirmation, pending.ApiMatch);
        }
        
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays a table of pending confirmations for client mods.
    /// </summary>
    /// <param name="pendingConfirmations">List of pending confirmations to display.</param>
    private void DisplayConfirmationTable(List<PendingConfirmation> pendingConfirmations)
    {
        var table = new Table();
        table.AddColumn("Local Client Mod");
        table.AddColumn("Author");
        table.AddColumn("API Match");
        table.AddColumn("API Author");
        table.AddColumn("Confidence");
        
        var sortedPendingConfirmations = pendingConfirmations.OrderBy(p => p.ResultIndex).ToList();
        
        foreach (var pending in sortedPendingConfirmations)
        {
            table.AddRow(
                pending.OriginalMod.Name.EscapeMarkup(),
                string.IsNullOrWhiteSpace(pending.OriginalMod.Author) ? "[grey]N/A[/]" : pending.OriginalMod.Author.EscapeMarkup(),
                pending.ApiMatch.Name.EscapeMarkup(),
                pending.ApiMatch.Owner?.Name.EscapeMarkup() ?? "[grey]N/A[/]",
                $"{pending.ConfidenceScore}%"
            );
        }
        
        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Creates a confirmation prompt for a client mod match.
    /// </summary>
    /// <param name="displayMod">The mod to display in the prompt.</param>
    /// <param name="pending">The pending confirmation details.</param>
    /// <returns>Formatted confirmation prompt.</returns>
    private static string CreateConfirmationPrompt(ModPackage displayMod, PendingConfirmation pending)
    {
        var apiAuthor = pending.ApiMatch.Owner?.Name.EscapeMarkup() ?? "N/A";
        
        if (string.IsNullOrWhiteSpace(displayMod.Author))
        {
            return $"[yellow]Is '[white]{displayMod.Name.EscapeMarkup()}[/]' the same as '[white]{pending.ApiMatch.Name.EscapeMarkup()}[/]' by '[white]{apiAuthor}[/]'? ([grey]Confidence: {pending.ConfidenceScore}%[/])[/]";
        }
        
        return $"[yellow]Is '[white]{displayMod.Name.EscapeMarkup()}[/]' by '[white]{displayMod.Author.EscapeMarkup()}[/]' the same as '[white]{pending.ApiMatch.Name.EscapeMarkup()}[/]' by '[white]{apiAuthor}[/]'? ([grey]Confidence: {pending.ConfidenceScore}%[/])[/]";
    }

    /// <summary>
    /// Processes a single client mod for API compatibility.
    /// </summary>
    /// <param name="mod">The client mod to process.</param>
    /// <param name="index">Index for ordering results.</param>
    /// <param name="sptVersion">SPT version for compatibility checking.</param>
    /// <returns>The processing result with optional pending confirmation.</returns>
    private async Task<ProcessingResult> ProcessSingleClientModAsync(ClientModPackage mod, int index, SemanticVersioning.Version sptVersion)
    {
        // Convert ClientModPackage to ModPackage for compatibility with existing processing (for display purposes)
        var originalModPackage = new ModPackage
        {
            Name = mod.Name,
            Author = mod.Author,
            Version = mod.Version,
            SptVersion = "0.0.0" // Not applicable for client mods
        };
        
        ModStatus status;
        ModSearchResult? matchedMod = null;
        PendingConfirmation? pendingConfirmation = null;
        var confidenceScore = 0;

        // Check blacklist
        if (IsClientModBlacklisted(mod))
        {
            status = ModStatus.Incompatible;
        }
        else
        {
            // Process compatibility
            var result = await ProcessClientModCompatibilityAsync(mod, originalModPackage, sptVersion, index);
            status = result.Status;
            matchedMod = result.MatchedMod;
            pendingConfirmation = result.PendingConfirmation;
            confidenceScore = result.ConfidenceScore;
        }

        var processedMod = new ProcessedMod(originalModPackage, status, matchedMod);
        if (matchedMod != null && status is ModStatus.Verified or ModStatus.NeedsConfirmation)
        {
            processedMod.UpdateFromApiMatch(matchedMod, confidenceScore);
        }
        
        return new ProcessingResult(processedMod, pendingConfirmation);
    }
    
    /// <summary>
    /// Checks if a client mod is blacklisted.
    /// </summary>
    /// <param name="mod">Client mod to check.</param>
    /// <returns>True if blacklisted.</returns>
    private static bool IsClientModBlacklisted(ClientModPackage mod)
    {
        return AppConstants.BlacklistedClientMods.Any(b => 
            b.Name.Equals(mod.Name, StringComparison.OrdinalIgnoreCase) && 
            b.Author.Equals(mod.Author, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Processes client mod compatibility including API verification.
    /// </summary>
    /// <param name="mod">Client mod to process.</param>
    /// <param name="originalModPackage">Original mod package for display.</param>
    /// <param name="sptVersion">SPT version.</param>
    /// <param name="index">Result index.</param>
    /// <returns>Compatibility processing result.</returns>
    private async Task<(ModStatus Status, ModSearchResult? MatchedMod, PendingConfirmation? PendingConfirmation, int ConfidenceScore)> 
        ProcessClientModCompatibilityAsync(ClientModPackage mod, ModPackage originalModPackage, SemanticVersioning.Version sptVersion, int index)
    {
        try
        {
            // Apply updates and verify
            var searchMod = ApplyClientModUpdates(mod);
            var verificationResult = await VerifyClientModWithApiAsync(searchMod, sptVersion);
            
            if (verificationResult.IsVerified)
            {
                return (ModStatus.Verified, verificationResult.MatchedMod, null, verificationResult.ConfidenceScore);
            }
            
            if (verificationResult.RequiresConfirmation)
            {
                var pendingConfirmation = new PendingConfirmation(
                    originalModPackage,
                    originalModPackage,
                    verificationResult.MatchedMod!,
                    verificationResult.ConfidenceScore,
                    index
                );
                return (ModStatus.NeedsConfirmation, verificationResult.MatchedMod, pendingConfirmation, verificationResult.ConfidenceScore);
            }
            
            return (ModStatus.NoMatch, null, null, verificationResult.ConfidenceScore);
        }
        catch
        {
            return (ModStatus.NoMatch, null, null, 0);
        }
    }

    /// <summary>
    /// Verifies a client mod against the Forge API using specialized client mod fuzzy matching.
    /// </summary>
    /// <param name="mod">Client mod to verify.</param>
    /// <param name="sptVersion">SPT version for an API query.</param>
    /// <returns>Verification result with match details.</returns>
    private async Task<ModVerificationResult> VerifyClientModWithApiAsync(ClientModPackage mod, SemanticVersioning.Version sptVersion)
    {
        const int highConfidenceThreshold = 75;
        const int mediumConfidenceThreshold = 1;

        try
        {
            var searchResults = await forgeApiService.SearchClientModsAsync(mod.Name, sptVersion);
            if (searchResults.Count == 0) return new ModVerificationResult(false, null);

            var bestMatch = FindBestClientModMatch(mod, searchResults);
            if (bestMatch == null) return new ModVerificationResult(false, null);

            return bestMatch.Score switch
            {
                >= highConfidenceThreshold => new ModVerificationResult(true, bestMatch.ApiResult, false, bestMatch.Score),
                >= mediumConfidenceThreshold => new ModVerificationResult(false, bestMatch.ApiResult, true, bestMatch.Score),
                _ => new ModVerificationResult(false, null, false, bestMatch.Score)
            };
        }
        catch
        {
            return new ModVerificationResult(false, null);
        }
    }

    /// <summary>
    /// Finds the best matching client mod from API search results, handling cases where the author may be unknown.
    /// </summary>
    /// <param name="mod">Client mod to match.</param>
    /// <param name="searchResults">API search results.</param>
    /// <returns>Best match result or null if no suitable match found.</returns>
    private MatchResult? FindBestClientModMatch(ClientModPackage mod, List<ModSearchResult> searchResults)
    {
        if (searchResults.Count == 0) return null;

        var scoredResults = searchResults.Select(result =>
        {
            // For client mods, handle cases where the author is unknown
            if (string.IsNullOrWhiteSpace(mod.Author))
            {
                // Only use the name score
                var nameOnlyScore = FuzzySharp.Fuzz.Ratio(mod.Name, result.Name);
                return new MatchResult
                {
                    ApiResult = result,
                    Score = nameOnlyScore,
                    IsHighConfidence = nameOnlyScore >= 75,
                    IsMediumConfidence = nameOnlyScore >= 1 && nameOnlyScore < 75
                };
            }
            
            // Use standard matching when the author is available
            var nameScore = ModMatchingService.CalculateMatchConfidence(new ModPackage { Name = mod.Name, Author = mod.Author, Version = mod.Version, SptVersion = "" }, result);
            return nameScore;
        }).OrderByDescending(x => x.Score).ToList();

        return scoredResults.FirstOrDefault();
    }
}