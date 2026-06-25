using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using CheckMods.Utils;
using Spectre.Console;
using SPTarkov.DI.Annotations;

namespace CheckMods.Services;

/// <summary>
/// Spectre.Console implementation of <see cref="IModCheckReporter"/>.
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
        var tree = new Tree("[yellow]Incompatible mods[/]");

        foreach (var mod in incompatibleMods)
        {
            var nameDisplay = FormatModLink(mod.DisplayName, mod.ApiUrl);

            var modNode = tree.AddNode(nameDisplay);
            modNode.AddNode($"[yellow]{mod.IncompatibilityReason?.EscapeMarkup()}[/]");

            if (string.IsNullOrWhiteSpace(mod.CompatibleVersionString))
            {
                modNode.AddNode($"[red]No compatible version available for SPT {sptVersion}[/]");
                continue;
            }

            modNode.AddNode(
                $"[grey]Latest compatible version:[/] [green]{mod.CompatibleVersionString.EscapeMarkup()}[/]"
            );

            // Use Forge download link format
            if (mod.ApiModId.HasValue && !string.IsNullOrWhiteSpace(mod.ApiSlug))
            {
                var forgeDownloadUrl = ForgeUrls.Download(mod.ApiModId.Value, mod.ApiSlug, mod.CompatibleVersionString);
                modNode.AddNode($"[grey]Download:[/] [link]{forgeDownloadUrl.EscapeMarkup()}[/]");
            }
        }

        AnsiConsole.Write(tree);
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

        var tree = new Tree("[yellow]Mod loading warnings[/]");

        foreach (var mod in modsWithWarnings)
        {
            var modType = mod.IsServerMod ? "Server" : "Client";
            var modName = !string.IsNullOrWhiteSpace(mod.LocalName) ? mod.LocalName : Path.GetFileName(mod.FilePath);

            var nameDisplay = FormatModLink(modName, mod.ApiUrl);

            var modNode = tree.AddNode($"[grey]{modType}:[/] {nameDisplay}");
            foreach (var warning in mod.LoadWarnings)
            {
                modNode.AddNode($"[yellow]{warning.EscapeMarkup()}[/]");
            }

            // Show source code URL if available, otherwise show Forge mod page
            if (!string.IsNullOrWhiteSpace(mod.ApiSourceCodeUrl))
            {
                modNode.AddNode($"[grey]Please report:[/] [link]{mod.ApiSourceCodeUrl.EscapeMarkup()}[/]");
            }
            else if (!string.IsNullOrWhiteSpace(mod.ApiUrl))
            {
                modNode.AddNode($"[grey]Please report:[/] [link]{mod.ApiUrl.EscapeMarkup()}[/]");
            }
        }

        AnsiConsole.Write(tree);
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

                var tree = new Tree("[yellow]Reconciliation warnings[/]");

                foreach (var pair in pairsWithNotes)
                {
                    var modName = pair.SelectedMod.LocalName;

                    var nameDisplay = FormatModLink(modName, pair.SelectedMod.ApiUrl);

                    var modNode = tree.AddNode(nameDisplay);
                    foreach (var note in pair.Notes)
                    {
                        modNode.AddNode($"[yellow]{note.EscapeMarkup()}[/]");
                    }

                    var reportUrl = !string.IsNullOrWhiteSpace(pair.SelectedMod.ApiSourceCodeUrl)
                        ? pair.SelectedMod.ApiSourceCodeUrl
                        : pair.SelectedMod.ApiUrl;

                    // A GUID mismatch only happens on a name match (same name, unrelated IDs).
                    var guidMismatch = !string.Equals(
                        pair.ServerMod.Guid,
                        pair.ClientMod.Guid,
                        StringComparison.OrdinalIgnoreCase
                    );

                    if (guidMismatch)
                    {
                        modNode.AddNode(
                            "[grey]Matched by name, but the GUIDs differ. This is likely a mod packaged with mismatched GUIDs.[/]"
                        );

                        if (!string.IsNullOrWhiteSpace(reportUrl))
                        {
                            modNode.AddNode(
                                $"[grey]Report the issue here:[/] [link]{reportUrl.EscapeMarkup()}[/]"
                            );
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(reportUrl))
                    {
                        modNode.AddNode($"[grey]Please report:[/] [link]{reportUrl.EscapeMarkup()}[/]");
                    }
                }

                AnsiConsole.Write(tree);
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
            var tree = new Tree(
                "[yellow]Server mods found in the client folder[/] [grey](BepInEx/plugins)[/][yellow]. Move them into[/] [grey]SPT/user/mods[/]"
            );
            foreach (var mod in serverInClient)
            {
                AddMisplacedModNode(tree, mod);
            }
            AnsiConsole.Write(tree);
            AnsiConsole.WriteLine();
        }

        if (clientInServer.Count > 0)
        {
            var tree = new Tree(
                "[yellow]Client mods found in the server folder[/] [grey](SPT/user/mods)[/][yellow]. Move them into[/] [grey]BepInEx/plugins[/]"
            );
            foreach (var mod in clientInServer)
            {
                AddMisplacedModNode(tree, mod);
            }
            AnsiConsole.Write(tree);
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
        Tree tree;
        TreeNode directoryNode;

        if (directory.Ambiguous)
        {
            tree = new Tree(
                "[yellow]Unrelated mods share one folder under[/] [grey](BepInEx/plugins)[/][yellow]. One is likely in the wrong place. Review the install instructions for each[/]"
            );
            directoryNode = tree.AddNode($"[grey]{directory.Directory.EscapeMarkup()}[/]");
            foreach (var mod in directory.Mods)
            {
                AddMisplacedModNode(directoryNode, mod);
            }
        }
        else
        {
            tree = new Tree(
                "[yellow]Mods found inside another mod's folder under[/] [grey](BepInEx/plugins)[/][yellow]. Review the mod's installation instructions[/]"
            );
            directoryNode = tree.AddNode($"[grey]{directory.Directory.EscapeMarkup()}[/]");
            foreach (var mod in directory.Misplaced)
            {
                AddMisplacedModNode(directoryNode, mod);
            }
        }

        AnsiConsole.Write(tree);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Adds a single misplaced mod, with its file path as a child, beneath the given tree node.
    /// </summary>
    private static void AddMisplacedModNode(IHasTreeNodes parent, MisplacedMod mod)
    {
        var name = !string.IsNullOrWhiteSpace(mod.Name) ? mod.Name : Path.GetFileName(mod.FilePath);
        var guidSuffix = !string.IsNullOrWhiteSpace(mod.Guid) ? $" [grey]({mod.Guid.EscapeMarkup()})[/]" : string.Empty;

        var modNode = parent.AddNode($"[white]{name.EscapeMarkup()}[/]{guidSuffix}");
        modNode.AddNode($"[grey]{mod.FilePath.EscapeMarkup()}[/]");
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
        var tree = new Tree("[yellow]Mods not found on Forge[/]");

        foreach (var mod in unverifiedMods)
        {
            var modDisplayName = mod.DisplayName.EscapeMarkup();
            if (!string.IsNullOrWhiteSpace(mod.DisplayAuthor))
            {
                modDisplayName += $" by {mod.DisplayAuthor.EscapeMarkup()}";
            }

            var modNode = tree.AddNode($"[white]{modDisplayName}[/]");

            if (!string.IsNullOrWhiteSpace(mod.Guid))
            {
                modNode.AddNode($"[grey]GUID: {mod.Guid.EscapeMarkup()}[/]");
            }

            if (!string.IsNullOrWhiteSpace(mod.FilePath))
            {
                modNode.AddNode($"[grey]Path: {mod.FilePath.EscapeMarkup()}[/]");
            }
        }

        AnsiConsole.Write(tree);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            "[grey]These were not matched to a Forge listing. That's expected for a mod that isn't published on the Forge, or for a mod which includes multiple plugins where only one uses the GUID linked to the Forge. No action is needed unless you expected one of these to be its own mod on Forge.[/]"
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

        DependencyTree(result);

        if (result.Conflicts.Count > 0)
        {
            DependencyConflicts(result.Conflicts);
        }

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
    /// Renders dependency conflicts as a tree.
    /// </summary>
    private static void DependencyConflicts(List<DependencyConflict> conflicts)
    {
        var tree = new Tree("[yellow]Dependency conflicts[/]");

        foreach (var conflict in conflicts)
        {
            var modNode = tree.AddNode($"[white]{conflict.ModName.EscapeMarkup()}[/]");
            modNode.AddNode($"[yellow]{conflict.Description.EscapeMarkup()}[/]");

            if (conflict.DependencyInfo.Id > 0 && !string.IsNullOrWhiteSpace(conflict.DependencyInfo.Slug))
            {
                var url = ForgeUrls.ModPage(conflict.DependencyInfo.Id, conflict.DependencyInfo.Slug);
                modNode.AddNode($"[grey]View on Forge:[/] [link]{url.EscapeMarkup()}[/]");
            }
        }

        AnsiConsole.Write(tree);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Renders missing dependencies as a tree of recommended versions and download links.
    /// </summary>
    private static void MissingDependencies(List<MissingDependency> missingDeps)
    {
        var tree = new Tree("[red]Missing dependencies[/]");

        foreach (var dep in missingDeps)
        {
            // Link mod name to Forge page when a usable URL is available, otherwise show it in plain white.
            var url =
                dep.ModId > 0 && !string.IsNullOrWhiteSpace(dep.Slug) ? ForgeUrls.ModPage(dep.ModId, dep.Slug) : null;
            var nameDisplay = IsLinkUrlSafe(url)
                ? $"[white link={url}]{dep.Name.EscapeMarkup()}[/]"
                : $"[white]{dep.Name.EscapeMarkup()}[/]";

            var depNode = tree.AddNode(nameDisplay);
            depNode.AddNode($"[grey]Recommended version:[/] [green]{dep.RecommendedVersion.EscapeMarkup()}[/]");

            if (!string.IsNullOrWhiteSpace(dep.DownloadLink))
            {
                depNode.AddNode($"[grey]Download:[/] [link]{dep.DownloadLink.EscapeMarkup()}[/]");
            }
        }

        AnsiConsole.Write(tree);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Adds, beneath an available update's tree node, how the proposed version changes the mod's dependencies: newly
    /// required dependencies (with install state and a download link when missing) and any that are no longer required.
    /// </summary>
    private static void AddUpdateDependencyChangeNodes(TreeNode modNode, UpdateDependencyDelta delta)
    {
        var changesNode = modNode.AddNode("[grey]Dependency changes:[/]");

        foreach (var dep in delta.Added)
        {
            // Link the dependency name to its Forge page when a usable URL is available, otherwise show it plain.
            var url =
                dep.ModId > 0 && !string.IsNullOrWhiteSpace(dep.Slug) ? ForgeUrls.ModPage(dep.ModId, dep.Slug) : null;
            var nameDisplay = IsLinkUrlSafe(url)
                ? $"[white link={url}]{dep.Name.EscapeMarkup()}[/]"
                : $"[white]{dep.Name.EscapeMarkup()}[/]";

            var annotation = dep.InstallState switch
            {
                DependencyInstallState.NotInstalled =>
                    $"[red]new - download v{dep.RecommendedVersion.EscapeMarkup()}[/]",
                DependencyInstallState.InstalledOutdated =>
                    $"[yellow]installed v{(dep.InstalledVersion ?? "?").EscapeMarkup()}, update needs v{dep.RecommendedVersion.EscapeMarkup()}[/]",
                _ => $"[grey]already satisfied (v{(dep.InstalledVersion ?? dep.RecommendedVersion).EscapeMarkup()})[/]",
            };

            var depNode = changesNode.AddNode($"[green]+[/] {nameDisplay} {annotation}");

            if (dep.Conflict)
            {
                depNode.AddNode("[red]Version constraint conflict reported by Forge.[/]");
            }

            if (dep.InstallState == DependencyInstallState.NotInstalled && !string.IsNullOrWhiteSpace(dep.DownloadLink))
            {
                depNode.AddNode($"[grey]Download:[/] [link]{dep.DownloadLink.EscapeMarkup()}[/]");
            }
        }

        foreach (var dep in delta.Removed)
        {
            var wasVersion = dep.InstalledVersion ?? dep.RecommendedVersion;
            changesNode.AddNode(
                $"[grey]-[/] [grey]{dep.Name.EscapeMarkup()} no longer required (was v{wasVersion.EscapeMarkup()})[/]"
            );
        }
    }

    /// <inheritdoc />
    public void VersionTable(List<Mod> mods)
    {
        // Group by API mod ID, selecting the one with the highest version
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
            "[white]This tool depends on mod authors to use and update valid version numbers. If you notice a version number in the Current Version column that is incorrect, please contact the author of the mod to have it updated. Additionally, these updates can be ignored by selecting the \"Manage ignored updates\" option at the end of the check.[/]"
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

            var nameDisplay = FormatModLink(displayName, mod.ApiUrl);

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

            var updatesTree = new Tree("[red]Updates available[/]");

            foreach (var mod in modsWithUpdates)
            {
                var nameDisplay = FormatModLink(mod.DisplayName, mod.ApiUrl);

                var modNode = updatesTree.AddNode(nameDisplay);
                modNode.AddNode(
                    $"[grey]{mod.LocalVersion.EscapeMarkup()}[/] [yellow]->[/] [green]{mod.LatestVersion!.EscapeMarkup()}[/]"
                );

                if (!string.IsNullOrWhiteSpace(mod.DownloadLink))
                {
                    modNode.AddNode($"[grey]Download:[/] [link]{mod.DownloadLink.EscapeMarkup()}[/]");
                }

                if (mod.UpdateDependencyChanges?.HasChanges == true)
                {
                    AddUpdateDependencyChangeNodes(modNode, mod.UpdateDependencyChanges);
                }
            }

            AnsiConsole.Write(updatesTree);
        }

        // Display mods with blocked updates
        var modsWithBlockedUpdates = verifiedMods.Where(m => m.UpdateStatus == UpdateStatus.UpdateBlocked).ToList();
        if (modsWithBlockedUpdates.Count > 0)
        {
            AnsiConsole.WriteLine();

            var blockedTree = new Tree("[darkorange]Updates blocked[/]");

            foreach (var mod in modsWithBlockedUpdates)
            {
                var nameDisplay = FormatModLink(mod.DisplayName, mod.ApiUrl);

                var modNode = blockedTree.AddNode(nameDisplay);
                modNode.AddNode(
                    $"[grey]{mod.LocalVersion.EscapeMarkup()}[/] [yellow]->[/] [darkorange]{mod.LatestVersion!.EscapeMarkup()}[/]"
                );

                if (!string.IsNullOrWhiteSpace(mod.BlockReason))
                {
                    modNode.AddNode($"[grey]Reason:[/] {FormatBlockReason(mod.BlockReason).EscapeMarkup()}");
                }

                if (mod.BlockingMods is { Count: > 0 })
                {
                    foreach (var blocker in mod.BlockingMods)
                    {
                        modNode.AddNode(
                            $"[grey]Blocked by:[/] {blocker.Name.EscapeMarkup()} [grey]({blocker.Constraint.EscapeMarkup()})[/]"
                        );
                    }
                }
            }

            AnsiConsole.Write(blockedTree);
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
    public EndOfRunChoice PromptEndOfRun(int openableUpdateCount, bool canManageIgnoredUpdates)
    {
        DrainBufferedKeys();
        AnsiConsole.WriteLine();

        var prompt = new SelectionPrompt<EndOfRunChoice>()
            .Title("[grey]What would you like to do?[/]")
            .HighlightStyle(Style.Parse("blue"))
            .UseConverter(choice => FormatEndOfRunChoice(choice, openableUpdateCount));

        if (openableUpdateCount > 0)
        {
            prompt.AddChoice(EndOfRunChoice.OpenUpdatePages);
        }

        if (canManageIgnoredUpdates)
        {
            prompt.AddChoice(EndOfRunChoice.ManageIgnoredUpdates);
        }

        prompt.AddChoice(EndOfRunChoice.Exit);

        return AnsiConsole.Prompt(prompt);
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
    public void UpdatePagesOpened(int opened, int total)
    {
        if (total == 0)
        {
            return;
        }

        if (opened == total)
        {
            Success($"Opened {opened} mod page{Plural(opened)} in your browser.");
        }
        else if (opened == 0)
        {
            Error("Couldn't open your browser. The mod pages are listed as clickable links in the summary above.");
        }
        else
        {
            Warning(
                $"Opened {opened} of {total} mod pages; couldn't open the rest. The remaining pages are listed as clickable links above."
            );
        }
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
    /// Formats an end-of-run menu entry. The open-pages entry carries the count of pages it will open.
    /// </summary>
    private static string FormatEndOfRunChoice(EndOfRunChoice choice, int openableUpdateCount)
    {
        return choice switch
        {
            EndOfRunChoice.OpenUpdatePages =>
                $"Open {openableUpdateCount} mod page{Plural(openableUpdateCount)} with updates in your browser",
            EndOfRunChoice.ManageIgnoredUpdates => "Manage ignored updates",
            EndOfRunChoice.Exit => "Close Check Mods",
            _ => choice.ToString(),
        };
    }

    /// <summary>
    /// Returns the plural suffix "s" for any count other than one.
    /// </summary>
    private static string Plural(int count)
    {
        return count == 1 ? string.Empty : "s";
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
    /// Discards any keystrokes buffered during the run.
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
        // LatestVersion is non-null here.
        var latestVersion = mod.LatestVersion!;

        // A dismissed false positive renders as a dim, ignored row.
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
    /// Renders a mod name in white, as a clickable Forge link when an API URL is available. The name is markup-escaped
    /// internally.
    /// </summary>
    /// <param name="name">The raw mod name to display.</param>
    /// <param name="apiUrl">The Forge mod page URL, or null/empty when none is known.</param>
    private static string FormatModLink(string name, string? apiUrl)
    {
        var escaped = name.EscapeMarkup();

        if (IsLinkUrlSafe(apiUrl))
        {
            return $"[white link={apiUrl}]{escaped}[/]";
        }

        return $"[white]{escaped}[/]";
    }

    /// <summary>
    /// Wraps already-formatted display markup in a Spectre [link] tag when the URL can be safely embedded, otherwise
    /// returns the markup unlinked.
    /// </summary>
    private static string WithLink(string displayMarkup, string? url)
    {
        return IsLinkUrlSafe(url) ? $"[link={url}]{displayMarkup}[/]" : displayMarkup;
    }

    /// <summary>
    /// Returns true when a URL is safe to embed in a [link=...] tag attribute. A URL containing the markup delimiters
    /// '[' or ']' is treated as unsafe.
    /// </summary>
    internal static bool IsLinkUrlSafe(string? url)
    {
        return !string.IsNullOrWhiteSpace(url) && !url.Contains('[') && !url.Contains(']');
    }
}
