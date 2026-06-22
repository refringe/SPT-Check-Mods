using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using CheckMods.Utils;
using Spectre.Console;
using SPTarkov.DI.Annotations;

namespace CheckMods.Services;

/// <summary>
/// Spectre.Console implementation of <see cref="IModCheckReporter"/>. This is the only type that talks to the console
/// directly; all workflow logic renders output through the <see cref="IModCheckReporter"/> abstraction.
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed class SpectreModCheckReporter : IModCheckReporter
{
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
        "Tested on scavs!",
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

    /// <inheritdoc />
    public void Banner()
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

    /// <inheritdoc />
    public void Rule()
    {
        AnsiConsole.Write(new Rule().RuleStyle("grey"));
    }

    /// <inheritdoc />
    public void Blank()
    {
        AnsiConsole.WriteLine();
    }

    /// <inheritdoc />
    public void Heading(string text)
    {
        AnsiConsole.MarkupLine($"[bold blue]{text.EscapeMarkup()}[/]");
    }

    /// <inheritdoc />
    public void Status(string text)
    {
        AnsiConsole.MarkupLine($"[grey]{text.EscapeMarkup()}[/]");
    }

    /// <inheritdoc />
    public void Success(string text)
    {
        AnsiConsole.MarkupLine($"[green]{text.EscapeMarkup()}[/]");
    }

    /// <inheritdoc />
    public void Warning(string text)
    {
        AnsiConsole.MarkupLine($"[yellow]{text.EscapeMarkup()}[/]");
    }

    /// <inheritdoc />
    public void Error(string text)
    {
        AnsiConsole.MarkupLine($"[red]{text.EscapeMarkup()}[/]");
    }

    /// <inheritdoc />
    public void CouldNotReadModDll(string fileName, string reason)
    {
        AnsiConsole.MarkupLine(
            $"[orange1]Warning:[/] Could not read mod DLL [grey]{fileName.EscapeMarkup()}[/]. Reason: {reason.EscapeMarkup()}"
        );
    }

    /// <inheritdoc />
    public void CouldNotReadSptVersion(string reason)
    {
        AnsiConsole.MarkupLine($"[orange1]Warning:[/] Could not read SPT version. Reason: {reason.EscapeMarkup()}");
    }

    /// <inheritdoc />
    public void PluginsDirectoryNotFound(string path)
    {
        AnsiConsole.MarkupLine(
            $"[orange1]Warning:[/] BepInEx plugins directory not found: [grey]{path.EscapeMarkup()}[/]"
        );
    }

    /// <inheritdoc />
    public void CouldNotExtractClientMod(string fileName, string reason)
    {
        AnsiConsole.MarkupLine(
            $"[orange1]Warning:[/] Could not extract mod metadata from [grey]{fileName.EscapeMarkup()}[/]. Reason: {reason.EscapeMarkup()}"
        );
    }

    /// <inheritdoc />
    public async Task RunForgeQueryProgressAsync(int total, Func<Action<int>, Task> work)
    {
        await CreateForgeProgress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[grey]Querying Forge API[/]", maxValue: total);
                await work(current => task.Value = current);
                task.StopTask();
            });
    }

    /// <inheritdoc />
    public async Task<T> RunForgeQueryProgressAsync<T>(int total, Func<Action<int>, Task<T>> work)
    {
        return await CreateForgeProgress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[grey]Querying Forge API[/]", maxValue: total);
                var result = await work(current => task.Value = current);
                task.StopTask();
                return result;
            });
    }

    private static Progress CreateForgeProgress()
    {
        return AnsiConsole
            .Progress()
            .Columns(
                new SpinnerColumn(Spinner.Known.Dots) { Style = Style.Parse("blue") },
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn()
            );
    }

    /// <inheritdoc />
    public void UsingPath(string path)
    {
        AnsiConsole.MarkupLine($"[grey]Using Path:[/] {path.EscapeMarkup()}");
    }

    /// <inheritdoc />
    public void DirectoryDoesNotExist(string path)
    {
        AnsiConsole.MarkupLine($"[red]Error: Directory does not exist: {path.EscapeMarkup()}[/]");
    }

    /// <inheritdoc />
    public void ValidatingSptVersion(string version)
    {
        AnsiConsole.Markup(
            $"Found local SPT version [bold blue]{version.EscapeMarkup()}[/]. Validating with Forge API... "
        );
    }

    /// <inheritdoc />
    public void SptVersionValidated(string version)
    {
        AnsiConsole.MarkupLine($"[green]Successfully validated SPT Version:[/] [bold]{version.EscapeMarkup()}[/]");
    }

    /// <inheritdoc />
    public void SptUpdateAvailable(SptVersionResult latest)
    {
        var versionDisplay = $"[bold]{latest.Version.EscapeMarkup()}[/]";

        // Add mod count if available
        if (latest.ModCount > 0)
        {
            versionDisplay += $" [grey]({latest.ModCount} mods)[/]";
        }

        AnsiConsole.MarkupLine($"[yellow]SPT update available:[/] {versionDisplay}");

        // Add link on new line if available
        if (!string.IsNullOrWhiteSpace(latest.Link))
        {
            AnsiConsole.MarkupLine($"[grey]{latest.Link.EscapeMarkup()}[/]");
        }
    }

    /// <inheritdoc />
    public void CheckModsUpdate(CheckModsUpdateResult result, SemanticVersioning.Version sptVersion)
    {
        switch (result.Status)
        {
            case CheckModsUpdateStatus.UpdateAvailable:
                AnsiConsole.MarkupLine(
                    $"[yellow]A new version of Check Mods is available:[/] [bold]v{(result.LatestVersion ?? "?").EscapeMarkup()}[/] [grey](you have v{result.CurrentVersion.EscapeMarkup()})[/]"
                );
                if (!string.IsNullOrWhiteSpace(result.DownloadLink))
                {
                    AnsiConsole.MarkupLine($"[grey]Download:[/] [link]{result.DownloadLink.EscapeMarkup()}[/]");
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
                    AnsiConsole.MarkupLine($"[grey]Download:[/] [link]{result.DownloadLink.EscapeMarkup()}[/]");
                }

                break;

            default:
                AnsiConsole.MarkupLine("[grey]Could not check for Check Mods updates.[/]");
                break;
        }

        AnsiConsole.WriteLine();
        Rule();
    }

    /// <inheritdoc />
    public void NoModsFound()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]No mods found.[/]");
        AnsiConsole.MarkupLine("[grey]Server mods should be located in:[/] SPT/user/mods");
        AnsiConsole.MarkupLine("[grey]Client mods should be located in:[/] BepInEx/plugins");
        AnsiConsole.WriteLine();
    }

    /// <inheritdoc />
    public void VersionCompatibilityResults(List<Mod> mods, SemanticVersioning.Version sptVersion)
    {
        var incompatibleMods = mods.Where(m => m.IsLocalSptIncompatible).ToList();

        if (incompatibleMods.Count == 0)
        {
            AnsiConsole.MarkupLine("[green]All mod versions are compatible![/]");
            AnsiConsole.WriteLine();
            Rule();
            return;
        }

        AnsiConsole.MarkupLine($"[yellow]Found {incompatibleMods.Count} incompatible mod(s).[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Incompatible mods:[/]");

        foreach (var mod in incompatibleMods)
        {
            var nameDisplay = FormatModLink(mod.DisplayName, mod.ApiUrl);

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
                AnsiConsole.MarkupLine($"      [grey]Download:[/] [link]{forgeDownloadUrl.EscapeMarkup()}[/]");
            }
        }

        AnsiConsole.WriteLine();
        Rule();
    }

    /// <inheritdoc />
    public void Exception(Exception ex)
    {
        AnsiConsole.WriteException(ex, ExceptionFormats.ShortenPaths);
    }

    /// <inheritdoc />
    public void LoadingWarnings(List<Mod> modsWithWarnings)
    {
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

            var nameDisplay = FormatModLink(modName, mod.ApiUrl);

            AnsiConsole.MarkupLine($"  [grey]{modType}:[/] {nameDisplay}");
            foreach (var warning in mod.LoadWarnings)
            {
                AnsiConsole.MarkupLine($"    [yellow]- {warning.EscapeMarkup()}[/]");
            }

            // Show source code URL if available, otherwise show Forge mod page
            if (!string.IsNullOrWhiteSpace(mod.ApiSourceCodeUrl))
            {
                AnsiConsole.MarkupLine($"      [grey]Please report:[/] [link]{mod.ApiSourceCodeUrl.EscapeMarkup()}[/]");
            }
            else if (!string.IsNullOrWhiteSpace(mod.ApiUrl))
            {
                AnsiConsole.MarkupLine($"      [grey]Please report:[/] [link]{mod.ApiUrl.EscapeMarkup()}[/]");
            }
        }
    }

    /// <inheritdoc />
    public void ReconciliationResults(ModReconciliationResult result)
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

                    var nameDisplay = FormatModLink(modName, pair.SelectedMod.ApiUrl);

                    AnsiConsole.MarkupLine($"  {nameDisplay}");
                    foreach (var note in pair.Notes)
                    {
                        AnsiConsole.MarkupLine($"    [yellow]- {note.EscapeMarkup()}[/]");
                    }

                    var reportUrl = !string.IsNullOrWhiteSpace(pair.SelectedMod.ApiSourceCodeUrl)
                        ? pair.SelectedMod.ApiSourceCodeUrl
                        : pair.SelectedMod.ApiUrl;

                    // A GUID mismatch only happens on a name match (same name, unrelated IDs). Likely mismatched
                    // packaging or two mods in one folder, so soften the report prompt.
                    var guidMismatch = !string.Equals(
                        pair.ServerMod.Guid,
                        pair.ClientMod.Guid,
                        StringComparison.OrdinalIgnoreCase
                    );

                    if (guidMismatch)
                    {
                        AnsiConsole.MarkupLine(
                            "      [grey]Matched by name, but the IDs differ. This is either a mod packaged with mismatched GUIDs or, more likely, two different mods with one copied into the other's folder. Check that each mod sits in its own folder under BepInEx/plugins.[/]"
                        );

                        if (!string.IsNullOrWhiteSpace(reportUrl))
                        {
                            AnsiConsole.MarkupLine(
                                $"      [grey]If this is wrong, report it here:[/] [link]{reportUrl.EscapeMarkup()}[/]"
                            );
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(reportUrl))
                    {
                        AnsiConsole.MarkupLine($"      [grey]Please report:[/] [link]{reportUrl.EscapeMarkup()}[/]");
                    }
                }

                AnsiConsole.WriteLine();
            }
        }

        AnsiConsole.MarkupLine(
            $"[grey]Final mod count: {result.Mods.Count} (matched pairs: {result.ReconciledPairs.Count}, server-only: {result.UnmatchedServerMods.Count}, client-only: {result.UnmatchedClientMods.Count})[/]"
        );
        AnsiConsole.WriteLine();
        Rule();
    }

    /// <inheritdoc />
    public void MisplacedMods(MisplacedModReport report)
    {
        AnsiConsole.WriteLine();
        Rule();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[red bold]Improperly installed mods detected.[/]");
        AnsiConsole.MarkupLine(
            "[grey]It appears that the following mods are installed incorrectly. Review the mod pages for install instructions and ensure they are correctly installed.[/]"
        );
        AnsiConsole.WriteLine();

        var serverInClient = report.WrongFolder.Where(m => m.IsServerMod).ToList();
        var clientInServer = report.WrongFolder.Where(m => !m.IsServerMod).ToList();

        if (serverInClient.Count > 0)
        {
            AnsiConsole.MarkupLine(
                "[yellow]Server mods found in the client folder[/] [grey](BepInEx/plugins)[/][yellow]. Move them into[/] [grey]SPT/user/mods[/][yellow]:[/]"
            );
            foreach (var mod in serverInClient)
            {
                PrintMisplacedMod(mod);
            }
            AnsiConsole.WriteLine();
        }

        if (clientInServer.Count > 0)
        {
            AnsiConsole.MarkupLine(
                "[yellow]Client mods found in the server folder[/] [grey](SPT/user/mods)[/][yellow]. Move them into[/] [grey]BepInEx/plugins[/][yellow]:[/]"
            );
            foreach (var mod in clientInServer)
            {
                PrintMisplacedMod(mod);
            }
            AnsiConsole.WriteLine();
        }

        foreach (var directory in report.CrossInstalled)
        {
            PrintCrossInstalledDirectory(directory);
        }

        AnsiConsole.MarkupLine(
            "[red]These mods are being skipped for the rest of this check. Move them to the correct location and run this tool again to have them included.[/]"
        );
        AnsiConsole.MarkupLine("[grey]If this incorrect, please create a Github issue and provide logs.[/]");
        AnsiConsole.WriteLine();
        Rule();
    }

    /// <summary>
    /// Prints a plugins subdirectory that contains unrelated mods. When the intruder is known it is named and the user
    /// is told to give it its own folder. When it is ambiguous, the whole directory is surfaced for review.
    /// </summary>
    private void PrintCrossInstalledDirectory(CrossInstalledDirectory directory)
    {
        if (directory.Ambiguous)
        {
            AnsiConsole.MarkupLine(
                "[yellow]Unrelated mods share one folder under[/] [grey](BepInEx/plugins)[/][yellow]. One is likely in the wrong place. Review the install instructions for each:[/]"
            );
            AnsiConsole.MarkupLine($"  [grey]{directory.Directory.EscapeMarkup()}[/]");
            foreach (var mod in directory.Mods)
            {
                PrintMisplacedMod(mod);
            }
        }
        else
        {
            AnsiConsole.MarkupLine(
                "[yellow]Mods found inside another mod's folder under[/] [grey](BepInEx/plugins)[/][yellow]. Review the mod's installation instructions:[/]"
            );
            AnsiConsole.MarkupLine($"  [grey]{directory.Directory.EscapeMarkup()}[/]");
            foreach (var mod in directory.Misplaced)
            {
                PrintMisplacedMod(mod);
            }
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Prints a single misplaced mod entry.
    /// </summary>
    private static void PrintMisplacedMod(MisplacedMod mod)
    {
        var name = !string.IsNullOrWhiteSpace(mod.Name) ? mod.Name : Path.GetFileName(mod.FilePath);
        var guidSuffix = !string.IsNullOrWhiteSpace(mod.Guid) ? $" [grey]({mod.Guid.EscapeMarkup()})[/]" : string.Empty;

        AnsiConsole.MarkupLine($"  [white]{name.EscapeMarkup()}[/]{guidSuffix}");
        AnsiConsole.MarkupLine($"    [grey]{mod.FilePath.EscapeMarkup()}[/]");
    }

    /// <inheritdoc />
    public void UnverifiedMods(List<Mod> mods)
    {
        var unverifiedMods = mods.Where(m => m.Status == ModStatus.NoMatch).ToList();

        if (unverifiedMods.Count == 0)
        {
            return;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Mods not found on Forge:[/]");

        foreach (var mod in unverifiedMods)
        {
            var modDisplayName = mod.DisplayName.EscapeMarkup();
            if (!string.IsNullOrWhiteSpace(mod.DisplayAuthor))
            {
                modDisplayName += $" by {mod.DisplayAuthor.EscapeMarkup()}";
            }

            AnsiConsole.MarkupLine($"  [white]{modDisplayName}[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            "  [grey]These weren't matched to a Forge listing. That's expected for a mod that isn't published on Forge, or for a plugin bundled inside another mod you already have installed. No action is needed unless you expected one of these to be its own mod on Forge.[/]"
        );
        AnsiConsole.WriteLine();
    }

    /// <inheritdoc />
    public void DependencyResults(DependencyAnalysisResult result)
    {
        if (result.RootMods.Count == 0)
        {
            AnsiConsole.MarkupLine("[grey]No dependency information available.[/]");
            AnsiConsole.WriteLine();
            Rule();
            return;
        }

        AnsiConsole.MarkupLine("[green]Dependency analysis complete.[/]");
        AnsiConsole.WriteLine();

        // Display the dependency tree
        DependencyTree(result);

        // Display conflicts (warnings section)
        if (result.Conflicts.Count > 0)
        {
            DependencyConflicts(result.Conflicts);
        }

        // Display missing dependencies (download list section)
        if (result.MissingDependencies.Count > 0)
        {
            MissingDependencies(result.MissingDependencies);
        }

        if (!result.HasIssues)
        {
            AnsiConsole.MarkupLine("[green]All dependencies are satisfied![/]");
        }

        AnsiConsole.WriteLine();
        Rule();
    }

    /// <summary>
    /// Displays the dependency tree using Spectre.Console Tree component.
    /// </summary>
    private static void DependencyTree(DependencyAnalysisResult result)
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

        // Build the label with an optional Forge link: prefer the mod's own API URL, falling back to a mod-page URL
        // constructed from the dependency info. WithLink renders the name unlinked when no usable URL is available.
        string? linkUrl = null;
        if (!string.IsNullOrWhiteSpace(node.Mod.ApiUrl))
        {
            linkUrl = node.Mod.ApiUrl;
        }
        else if (
            node.DependencyInfo != null
            && node.DependencyInfo.Id > 0
            && !string.IsNullOrWhiteSpace(node.DependencyInfo.Slug)
        )
        {
            linkUrl = ForgeUrls.ModPage(node.DependencyInfo.Id, node.DependencyInfo.Slug);
        }

        var label = $"{WithLink($"[{nameColor}]{name}[/]", linkUrl)} [grey]v{version}[/]";

        if (!string.IsNullOrWhiteSpace(statusIndicator))
        {
            label += $" {statusIndicator}";
        }

        return label;
    }

    /// <summary>
    /// Displays dependency conflicts in the warning style.
    /// </summary>
    private static void DependencyConflicts(List<DependencyConflict> conflicts)
    {
        AnsiConsole.MarkupLine("[yellow]Dependency conflicts:[/]");

        foreach (var conflict in conflicts)
        {
            var nameDisplay = $"[white]{conflict.ModName.EscapeMarkup()}[/]";

            AnsiConsole.MarkupLine($"  {nameDisplay}");
            AnsiConsole.MarkupLine($"    [yellow]- {conflict.Description.EscapeMarkup()}[/]");

            if (conflict.DependencyInfo.Id > 0 && !string.IsNullOrWhiteSpace(conflict.DependencyInfo.Slug))
            {
                var url = ForgeUrls.ModPage(conflict.DependencyInfo.Id, conflict.DependencyInfo.Slug);
                AnsiConsole.MarkupLine($"      [grey]View on Forge:[/] [link]{url.EscapeMarkup()}[/]");
            }
        }

        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays missing dependencies in the download list style.
    /// </summary>
    private static void MissingDependencies(List<MissingDependency> missingDeps)
    {
        AnsiConsole.MarkupLine("[red]Missing dependencies:[/]");

        foreach (var dep in missingDeps)
        {
            // Link mod name to Forge page when a usable URL is available, otherwise show it in plain white.
            var url =
                dep.ModId > 0 && !string.IsNullOrWhiteSpace(dep.Slug) ? ForgeUrls.ModPage(dep.ModId, dep.Slug) : null;
            var nameDisplay = IsLinkUrlSafe(url)
                ? $"[link={url}]{dep.Name.EscapeMarkup()}[/]"
                : $"[white]{dep.Name.EscapeMarkup()}[/]";

            AnsiConsole.MarkupLine($"  {nameDisplay}");
            AnsiConsole.MarkupLine(
                $"    [grey]Recommended version:[/] [green]{dep.RecommendedVersion.EscapeMarkup()}[/]"
            );

            if (!string.IsNullOrWhiteSpace(dep.DownloadLink))
            {
                AnsiConsole.MarkupLine($"    [grey]Download:[/] [link]{dep.DownloadLink.EscapeMarkup()}[/]");
            }
        }

        AnsiConsole.WriteLine();
    }

    /// <inheritdoc />
    public void VersionTable(List<Mod> mods)
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

            var nameDisplay = FormatModLink(displayName, mod.ApiUrl, colorPlainNameWhite: false);

            table.AddRow(
                nameDisplay,
                displayAuthor.EscapeMarkup(),
                mod.LocalVersion.EscapeMarkup(),
                latestVersionDisplay
            );
        }

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine(
            "[grey]Version colors: [green]Up to date[/] | [red]Update available[/] | [darkorange]Update blocked[/] | [blue]Newer than latest[/] | [grey]Ignored[/][/]"
        );

        // Display mods with available updates (excluding ones the user has dismissed as false positives)
        var modsWithUpdates = verifiedMods
            .Where(m => m.UpdateStatus == UpdateStatus.UpdateAvailable && !m.UpdateSuppressed)
            .ToList();
        if (modsWithUpdates.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[red]Updates available:[/]");

            foreach (var mod in modsWithUpdates)
            {
                var nameDisplay = FormatModLink(mod.DisplayName, mod.ApiUrl);

                AnsiConsole.MarkupLine($"  {nameDisplay}");
                AnsiConsole.MarkupLine(
                    $"    [grey]{mod.LocalVersion.EscapeMarkup()}[/] [yellow]->[/] [green]{mod.LatestVersion!.EscapeMarkup()}[/]"
                );

                if (string.IsNullOrWhiteSpace(mod.DownloadLink))
                {
                    continue;
                }

                AnsiConsole.MarkupLine($"    [grey]Download:[/] [link]{mod.DownloadLink.EscapeMarkup()}[/]");
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
                var nameDisplay = FormatModLink(mod.DisplayName, mod.ApiUrl);

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
        Rule();
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
        AnsiConsole.WriteLine();
    }

    /// <inheritdoc />
    public bool PromptFetchRemoteIgnores()
    {
        return AnsiConsole.Prompt(
            new ConfirmationPrompt("Fetch the latest community ignore list from the Forge?") { DefaultValue = false }
        );
    }

    /// <inheritdoc />
    public void RemoteIgnoresMerged(int added)
    {
        AnsiConsole.MarkupLine(
            added > 0
                ? $"[green]Added {added} ignored version(s) from the community list.[/]"
                : "[grey]Your ignore list is already up to date.[/]"
        );
    }

    /// <inheritdoc />
    public void RemoteIgnoresUnavailable()
    {
        AnsiConsole.MarkupLine("[red]Couldn't fetch the community ignore list; your local entries are unchanged.[/]");
    }

    /// <inheritdoc />
    public bool PromptManageIgnoredUpdates()
    {
        DrainBufferedKeys();
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press [[I]] to manage ignored updates, or any other key to exit...[/]");
        var key = Console.ReadKey(intercept: true);
        return key.Key == ConsoleKey.I;
    }

    /// <inheritdoc />
    public IReadOnlyList<Mod> SelectUpdatesToIgnore(IReadOnlyList<Mod> candidates, ISet<int> preIgnoredApiModIds)
    {
        AnsiConsole.WriteLine();

        var prompt = new MultiSelectionPrompt<Mod>()
            .Title("Select the updates to [grey]ignore[/] (checked = treated as up to date):")
            .NotRequired()
            .PageSize(15)
            .MoreChoicesText("[grey](Move up and down to see more mods.)[/]")
            .InstructionsText("[grey](Space to toggle, enter to confirm. Checked entries are ignored.)[/]")
            .UseConverter(FormatIgnoreChoice);

        foreach (var mod in candidates)
        {
            var item = prompt.AddChoice(mod);
            if (mod.ApiModId.HasValue && preIgnoredApiModIds.Contains(mod.ApiModId.Value))
            {
                item.Select();
            }
        }

        return AnsiConsole.Prompt(prompt);
    }

    /// <inheritdoc />
    public void PressAnyKeyToExit()
    {
        DrainBufferedKeys();
        AnsiConsole.MarkupLine("[grey]Press any key to exit...[/]");
        Console.ReadKey();
    }

    /// <inheritdoc />
    public bool PromptReportIgnores()
    {
        return AnsiConsole.Prompt(
            new ConfirmationPrompt("Report these ignored versions so other users benefit?") { DefaultValue = false }
        );
    }

    /// <inheritdoc />
    public void IgnoreReportOpened(string url, bool browserOpened, bool prefilled)
    {
        if (browserOpened)
        {
            AnsiConsole.MarkupLine("[green]Opening your browser to file the report. Thank you for contributing![/]");
        }
        else
        {
            AnsiConsole.MarkupLine(
                "[yellow]Couldn't open your browser automatically. Use this link to file the report:[/]"
            );
            AnsiConsole.MarkupLine($"[grey]{url.EscapeMarkup()}[/]");
        }

        if (!prefilled)
        {
            AnsiConsole.MarkupLine(
                "[grey]Your list was too large to pre-fill; paste the contents of your ignored-updates.json into the issue.[/]"
            );
        }
    }

    /// <summary>
    /// Formats a single mod as a multi-select choice: name plus the local-to-latest version transition.
    /// </summary>
    private static string FormatIgnoreChoice(Mod mod)
    {
        var name = mod.DisplayName.EscapeMarkup();
        var local = mod.LocalVersion.EscapeMarkup();
        var latest = (mod.LatestVersion ?? "?").EscapeMarkup();
        return $"{name}  [grey]{local} -> {latest}[/]";
    }

    /// <summary>
    /// Discards any keystrokes buffered during the run so a following ReadKey actually waits for fresh input.
    /// </summary>
    private static void DrainBufferedKeys()
    {
        while (Console.KeyAvailable)
        {
            Console.ReadKey(intercept: true);
        }
    }

    /// <summary>
    /// Formats the version display with appropriate color coding.
    /// </summary>
    internal static string FormatVersionDisplay(Mod mod)
    {
        // VersionTable only passes mods whose LatestVersion is resolved (it filters on LatestVersion is not null), so
        // it is non-null here.
        var latestVersion = mod.LatestVersion!;

        // A dismissed false positive renders as a dim, ignored row rather than as an available update.
        if (mod.UpdateSuppressed)
        {
            return $"[grey]{latestVersion.EscapeMarkup()} (ignored)[/]";
        }

        return mod.UpdateStatus switch
        {
            UpdateStatus.UpToDate => $"[green]{latestVersion.EscapeMarkup()}[/]",
            UpdateStatus.UpdateAvailable => $"[red]{latestVersion.EscapeMarkup()}[/]",
            UpdateStatus.UpdateBlocked => $"[darkorange]{latestVersion.EscapeMarkup()}[/]",
            UpdateStatus.NewerInstalled => $"[blue]{latestVersion.EscapeMarkup()}[/]",
            _ => latestVersion.EscapeMarkup(),
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
    /// Renders a mod name as a clickable Forge link when an API URL is available, otherwise as plain text. The name
    /// is markup-escaped internally, so callers pass the raw name.
    /// </summary>
    /// <param name="name">The raw mod name to display.</param>
    /// <param name="apiUrl">The Forge mod page URL, or null/empty when none is known.</param>
    /// <param name="colorPlainNameWhite">
    /// When true (the default), the non-linked fallback is wrapped in white; table cells that rely on the default
    /// cell color pass false.
    /// </param>
    private static string FormatModLink(string name, string? apiUrl, bool colorPlainNameWhite = true)
    {
        var escaped = name.EscapeMarkup();

        if (IsLinkUrlSafe(apiUrl))
        {
            return $"[link={apiUrl}]{escaped}[/]";
        }

        return colorPlainNameWhite ? $"[white]{escaped}[/]" : escaped;
    }

    /// <summary>
    /// Wraps already-formatted display markup in a Spectre [link] tag when the URL can be safely embedded, otherwise
    /// returns the markup unlinked. Use this for the [link={url}] attribute form where the URL sits inside the tag.
    /// </summary>
    private static string WithLink(string displayMarkup, string? url)
    {
        return IsLinkUrlSafe(url) ? $"[link={url}]{displayMarkup}[/]" : displayMarkup;
    }

    /// <summary>
    /// Returns true when a URL is safe to embed in a [link=...] tag attribute. A URL containing the markup delimiters
    /// '[' or ']' would corrupt the tag and throw when Spectre renders it; EscapeMarkup can't help here because it
    /// escapes content, not attribute values, so such URLs are dropped and the text is rendered without a link.
    /// </summary>
    internal static bool IsLinkUrlSafe(string? url)
    {
        return !string.IsNullOrWhiteSpace(url) && !url.Contains('[') && !url.Contains(']');
    }
}
