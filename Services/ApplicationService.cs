using CheckMods.Models;
using CheckMods.Services.Interfaces;
using CheckMods.Utils;
using Spectre.Console;

namespace CheckMods.Services;

/// <summary>
/// Main application service that orchestrates the SPT mod checking workflow.
/// </summary>
public class ApplicationService(IForgeApiService forgeApiService, IModService modService, IClientModService clientModService) : IApplicationService
{
    /// <summary>
    /// Main entry point for the application. Orchestrates API key validation, SPT path detection, mod scanning, and
    /// result presentation.
    /// </summary>
    /// <param name="args">Command line arguments. The first argument can be the SPT installation path.</param>
    public async Task RunAsync(string[] args)
    {
        DisplayBanner();

        try
        {
            // API key setup
            var apiKey = await SetupApiKeyAsync();
            if (apiKey == null) return;

            // SPT path validation
            var sptPath = GetValidatedSptPath(args);
            if (sptPath == null) return;
            
            // SPT version validation
            var sptVersion = await ValidateSptInstallationAsync(sptPath);
            if (sptVersion == null) return;

            // Process mods
            var (serverMods, clientMods) = await ProcessAllModsAsync(sptPath, sptVersion);

            // Display results
            await DisplayResultsAsync(serverMods, clientMods, sptVersion);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
        }
    }

    /// <summary>
    /// Displays the application banner and introductory information.
    /// </summary>
    private static void DisplayBanner()
    {
        AnsiConsole.Write(new FigletText("SPT Mod Checker").LeftJustified().Color(Color.Blue));
        AnsiConsole.MarkupLine("[fuchsia]A tool to check for outdated SPT mods using the Forge API.[/]");
        AnsiConsole.MarkupLine("[link]https://forge.sp-tarkov.com[/]");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Sets up the API key by either loading from storage or prompting the user.
    /// </summary>
    /// <returns>Valid API key or null if setup failed.</returns>
    private async Task<string?> SetupApiKeyAsync()
    {
        var apiKey = await GetAndValidateApiKey();
        if (apiKey == null) return null;
        
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
        string sptPath;
        
        if (args.Length > 0)
        {
            var safePath = SecurityHelper.GetSafePath(args[0]);
            if (safePath == null)
            {
                AnsiConsole.MarkupLine("[red]Error: Invalid path provided.[/]");
                return null;
            }
            
            if (!Directory.Exists(safePath))
            {
                AnsiConsole.MarkupLine($"[red]Error: Directory does not exist: {safePath.EscapeMarkup()}[/]");
                return null;
            }
            
            sptPath = safePath;
        }
        else
        {
            sptPath = Directory.GetCurrentDirectory();
        }
        
        AnsiConsole.MarkupLine($"[grey]Using Path:[/] {sptPath.EscapeMarkup()}");
        return sptPath;
    }

    /// <summary>
    /// Validates the SPT installation and returns the version.
    /// </summary>
    /// <param name="sptPath">Path to SPT installation.</param>
    /// <returns>SPT version or null if validation failed.</returns>
    private async Task<SemanticVersioning.Version?> ValidateSptInstallationAsync(string sptPath)
    {
        var sptVersion = await modService.GetAndValidateSptVersionAsync(sptPath);
        if (sptVersion == null) return null;
        
        AnsiConsole.MarkupLine($"[green]Successfully validated SPT Version:[/] [bold]{sptVersion}[/]");
        AnsiConsole.WriteLine();
        return sptVersion;
    }

    /// <summary>
    /// Processes both server and client mods for compatibility.
    /// </summary>
    /// <param name="sptPath">Path to SPT installation.</param>
    /// <param name="sptVersion">SPT version to check compatibility against.</param>
    /// <returns>Tuple of processed server and client mods.</returns>
    private async Task<(List<ProcessedMod> serverMods, List<ProcessedMod> clientMods)> ProcessAllModsAsync(
        string sptPath, SemanticVersioning.Version sptVersion)
    {
        // Process server mods
        var serverMods = await ProcessServerModsAsync(sptPath, sptVersion);
        
        // Process client mods
        var clientMods = await ProcessClientModsAsync(sptPath, sptVersion);
        
        return (serverMods, clientMods);
    }

    /// <summary>
    /// Scans and processes server mods from the user/mods directory.
    /// </summary>
    /// <param name="sptPath">Path to SPT installation.</param>
    /// <param name="sptVersion">SPT version to check compatibility against.</param>
    /// <returns>List of processed server mods.</returns>
    private async Task<List<ProcessedMod>> ProcessServerModsAsync(string sptPath, SemanticVersioning.Version sptVersion)
    {
        var modsDir = Path.Combine(sptPath, "user", "mods");
        var localMods = modService.GetLocalMods(modsDir);
        
        if (localMods.Count > 0)
        {
            return await modService.ProcessModCompatibility(localMods, sptVersion);
        }
        
        AnsiConsole.MarkupLine("[yellow]No server mods found.[/]");
        return [];
    }

    /// <summary>
    /// Scans and processes client mods from the BepInEx/plugins directory.
    /// </summary>
    /// <param name="sptPath">Path to SPT installation.</param>
    /// <param name="sptVersion">SPT version to check compatibility against.</param>
    /// <returns>List of processed client mods.</returns>
    private async Task<List<ProcessedMod>> ProcessClientModsAsync(string sptPath, SemanticVersioning.Version sptVersion)
    {
        var pluginsDir = Path.Combine(sptPath, "BepInEx", "plugins");
        var clientMods = clientModService.GetClientMods(pluginsDir);
        
        if (clientMods.Count > 0)
        {
            AnsiConsole.WriteLine();
            return await clientModService.ProcessClientModCompatibility(clientMods, sptVersion);
        }
        
        AnsiConsole.MarkupLine("[yellow]No client mods found.[/]");
        return [];
    }

    /// <summary>
    /// Displays all results including summary and version update table.
    /// </summary>
    /// <param name="serverMods">Processed server mods.</param>
    /// <param name="clientMods">Processed client mods.</param>
    /// <param name="sptVersion">SPT version for compatibility checking.</param>
    private async Task DisplayResultsAsync(List<ProcessedMod> serverMods, List<ProcessedMod> clientMods, 
        SemanticVersioning.Version sptVersion)
    {
        if (serverMods.Count == 0 && clientMods.Count == 0) return;
        
        ShowCombinedResults(serverMods, clientMods);
        await ShowVersionUpdateTable(serverMods, clientMods, sptVersion);
    }

    /// <summary>
    /// Retrieves and validates the Forge API key from storage or prompts the user for a new one. Stores valid keys in
    /// %APPDATA%/SptModChecker/apikey.txt for future use.
    /// </summary>
    /// <returns>Valid API key or null if the user cancels.</returns>
    private async Task<string?> GetAndValidateApiKey()
    {
        var appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var configFolder = Path.Combine(appDataFolder, "SptModChecker");
        Directory.CreateDirectory(configFolder);
        var configFilePath = Path.Combine(configFolder, "apikey.txt");

        if (File.Exists(configFilePath))
        {
            var savedKey = (await File.ReadAllTextAsync(configFilePath)).Trim();
            if (!string.IsNullOrWhiteSpace(savedKey))
            {
                savedKey = SecurityHelper.SanitizeInput(savedKey);
                AnsiConsole.MarkupLine("Found saved API key. Validating...");
                if (await forgeApiService.ValidateApiKeyAsync(savedKey))
                {
                    AnsiConsole.MarkupLine("[green]Saved API key is valid.[/]");
                    AnsiConsole.WriteLine();
                    return savedKey;
                }
                else
                {
                    AnsiConsole.MarkupLine("[red]The saved API key is invalid or has expired.[/]");
                    File.Delete(configFilePath);
                }
            }
        }

        AnsiConsole.MarkupLine("[yellow]Forge API key not found or was invalid.[/]");
        AnsiConsole.MarkupLine("Please generate an API key from your Forge account:");
        AnsiConsole.MarkupLine("[blue][link]https://forge.sp-tarkov.com/user/api-tokens[/][/]");

        while (true)
        {
            var newKey = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter your [green]API key[/]:")
                    .PromptStyle("green")
                    .Secret());

            if (string.IsNullOrWhiteSpace(newKey)) continue;
            
            // Sanitize API key
            newKey = SecurityHelper.SanitizeInput(newKey);

            AnsiConsole.Markup("Validating entered key... ");
            if (await forgeApiService.ValidateApiKeyAsync(newKey))
            {
                AnsiConsole.MarkupLine("[green]OK[/]");
                await File.WriteAllTextAsync(configFilePath, newKey);
                AnsiConsole.MarkupLine($"[green]API key saved successfully to:[/] [grey]{configFilePath}[/]");
                AnsiConsole.WriteLine();
                return newKey;
            }

            AnsiConsole.MarkupLine("[red]Failed. The entered key is invalid or lacks 'read' permissions. Please try again.[/]");
        }
    }
    
    /// <summary>
    /// Displays a summary of the mod matching results for both server and client mods. Shows counts for verified, no
    /// match, incompatible, and invalid version statuses.
    /// </summary>
    /// <param name="serverMods">Processed server mods.</param>
    /// <param name="clientMods">Processed client mods.</param>
    private static void ShowCombinedResults(List<ProcessedMod> serverMods, List<ProcessedMod> clientMods)
    {
        var allMods = serverMods.Concat(clientMods).ToList();
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold blue]Overall Matching Summary:[/]");
        
        var totalVerified = allMods.Count(m => m.Record.Status == ModStatus.Verified);
        var totalNoMatch = allMods.Count(m => m.Record.Status == ModStatus.NoMatch);
        var totalIncompatible = allMods.Count(m => m.Record.Status == ModStatus.Incompatible);
        var totalInvalidVersion = allMods.Count(m => m.Record.Status == ModStatus.InvalidVersion);
        
        AnsiConsole.MarkupLine($"[green]Verified:[/] {totalVerified} ([grey]Server: {serverMods.Count(m => m.Record.Status == ModStatus.Verified)}, Client: {clientMods.Count(m => m.Record.Status == ModStatus.Verified)}[/])");
        AnsiConsole.MarkupLine($"[red]No Match:[/] {totalNoMatch} ([grey]Server: {serverMods.Count(m => m.Record.Status == ModStatus.NoMatch)}, Client: {clientMods.Count(m => m.Record.Status == ModStatus.NoMatch)}[/])");
        AnsiConsole.MarkupLine($"[maroon]Incompatible:[/] {totalIncompatible} ([grey]Server: {serverMods.Count(m => m.Record.Status == ModStatus.Incompatible)}, Client: {clientMods.Count(m => m.Record.Status == ModStatus.Incompatible)}[/])");
        
        if (totalInvalidVersion > 0)
        {
            AnsiConsole.MarkupLine($"[red]Invalid Version:[/] {totalInvalidVersion} ([grey]Server: {serverMods.Count(m => m.Record.Status == ModStatus.InvalidVersion)}, Client: {clientMods.Count(m => m.Record.Status == ModStatus.InvalidVersion)}[/])");
        }
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold]Total Matched Mods:[/] {allMods.Count} ([grey]Server: {serverMods.Count}, Client: {clientMods.Count}[/])");
    }
    
    /// <summary>
    /// Displays a live-updating table showing version information for all verified mods. Checks each mod against the
    /// Forge API to determine if updates are available.
    /// </summary>
    /// <param name="serverMods">Processed server mods.</param>
    /// <param name="clientMods">Processed client mods.</param>
    /// <param name="sptVersion">SPT version for version compatibility checking.</param>
    private async Task ShowVersionUpdateTable(List<ProcessedMod> serverMods, List<ProcessedMod> clientMods, SemanticVersioning.Version sptVersion)
    {
        // Filter only verified mods
        var verifiedMods = serverMods.Concat(clientMods)
            .Where(m => m.Record.Status == ModStatus.Verified && m.ApiMatch != null)
            .ToList();
            
        if (verifiedMods.Count == 0)
        {
            return;
        }
        
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold blue]Checking for updates...[/]");
        
        // Create the live table
        var table = new Table()
            .Title("[yellow]Version Update Check[/]")
            .BorderColor(Color.Grey)
            .AddColumn("[blue]Name[/]")
            .AddColumn("[blue]Author[/]")
            .AddColumn("[blue]Current Version[/]")
            .AddColumn("[blue]Latest Version[/]");
            
        await AnsiConsole.Live(table)
            .AutoClear(false)
            .StartAsync(async ctx =>
            {
                foreach (var mod in verifiedMods)
                {
                    var modName = mod.ApiMatch!.Name;
                    var author = mod.ApiMatch.Owner?.Name ?? "Unknown";
                    var currentVersion = mod.Mod.Version;
                    
                    // Format display strings
                    var (displayName, displayAuthor) = FormatModDisplayStrings(modName, author);
                    
                    // Add row with the current version
                    table.AddRow(
                        displayName.EscapeMarkup(),
                        displayAuthor.EscapeMarkup(),
                        currentVersion.EscapeMarkup(),
                        "[grey]Checking...[/]"
                    );
                    ctx.Refresh();
                    
                    // Check for updates
                    var latestVersionDisplay = await GetLatestVersionDisplayAsync(mod.ApiMatch.Id, currentVersion, sptVersion);
                    
                    // Update the last cell in the row
                    var rowCount = table.Rows.Count;
                    table.UpdateCell(rowCount - 1, 3, latestVersionDisplay);
                    ctx.Refresh();
                }
            });
        
        AnsiConsole.MarkupLine("[grey]Version colors: [green]Up to date[/] | [red]Update available[/] | [yellow]Newer than latest[/][/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[fuchsia]This tool depends on mod authors to use and update valid version numbers. If you notice a version number in the Current Version column that is incorrect, please contact the author of the mod to have it updated.[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[fuchsia]Find an issue? Find [italic]Refringe[/] on Discord, or submit a bug report here:[/]");
        AnsiConsole.MarkupLine("[link]https://github.com/refringe/SPT-Check-Mods/issues/new[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[fuchsia]>:{}[/]");
    }

    /// <summary>
    /// Formats mod name and author strings for display with proper truncation.
    /// </summary>
    /// <param name="modName">Full mod name.</param>
    /// <param name="author">Full author name.</param>
    /// <returns>Tuple of formatted display strings.</returns>
    private static (string displayName, string displayAuthor) FormatModDisplayStrings(string modName, string author)
    {
        const int maxNameLength = 40;
        const int maxAuthorLength = 20;
        
        var displayName = modName.Length > maxNameLength 
            ? modName[..(maxNameLength - 3)] + "..." 
            : modName;
            
        var displayAuthor = author.Length > maxAuthorLength
            ? author[..(maxAuthorLength - 3)] + "..."
            : author;
            
        return (displayName, displayAuthor);
    }

    /// <summary>
    /// Fetches and formats the latest version display string with the appropriate color coding.
    /// </summary>
    /// <param name="modId">Forge API mod ID.</param>
    /// <param name="currentVersion">Current installed version.</param>
    /// <param name="sptVersion">SPT version for compatibility checking.</param>
    /// <returns>Formatted version string with color markup.</returns>
    private async Task<string> GetLatestVersionDisplayAsync(int modId, string currentVersion, 
        SemanticVersioning.Version sptVersion)
    {
        var versions = await forgeApiService.GetModVersionsAsync(modId, sptVersion);
        
        if (versions.Count == 0)
        {
            return "[grey]No versions found[/]";
        }

        var latestVersion = versions.First().Version;
        return CompareAndFormatVersions(currentVersion, latestVersion);
    }

    /// <summary>
    /// Compares current and latest versions and returns a formatted string with the appropriate color.
    /// </summary>
    /// <param name="currentVersion">Current installed version.</param>
    /// <param name="latestVersion">Latest available version.</param>
    /// <returns>Formatted version string with color markup.</returns>
    private static string CompareAndFormatVersions(string currentVersion, string latestVersion)
    {
        try
        {
            var current = new SemanticVersioning.Version(currentVersion);
            var latest = new SemanticVersioning.Version(latestVersion);
            
            if (latest > current)
            {
                return $"[red]{latestVersion.EscapeMarkup()}[/]";
            }

            if (latest == current)
            {
                return $"[green]{latestVersion.EscapeMarkup()}[/]";
            }

            // Current version is newer than latest
            return $"[yellow]{latestVersion.EscapeMarkup()}[/]";
        }
        catch
        {
            // Fallback to string comparison if version parsing fails
            return latestVersion == currentVersion 
                ? $"[green]{latestVersion.EscapeMarkup()}[/]" 
                : $"[red]{latestVersion.EscapeMarkup()}[/]";
        }
    }
}