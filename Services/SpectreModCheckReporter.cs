using CheckMods.Models;
using CheckMods.Services.Interfaces;
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
    public void LoadingWarnings(List<Mod> serverMods, List<Mod> clientMods)
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

                    // Link mod name to Forge page if available
                    var nameDisplay = !string.IsNullOrWhiteSpace(pair.SelectedMod.ApiUrl)
                        ? $"[link={pair.SelectedMod.ApiUrl}]{modName.EscapeMarkup()}[/]"
                        : $"[white]{modName.EscapeMarkup()}[/]";

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
                                $"      [grey]If this is wrong, report it here:[/] [link]{reportUrl}[/]"
                            );
                        }
                    }
                    else if (!string.IsNullOrWhiteSpace(reportUrl))
                    {
                        AnsiConsole.MarkupLine($"      [grey]Please report:[/] [link]{reportUrl}[/]");
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
            "[grey]The following mods are possibly installed incorrectly. Review the mod page for install instructions, move them to the correct location, and run this tool again.[/]"
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
            "[red]Halting. You need to resolve the mod installation issues before this tool can continue.[/]"
        );
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
            "  [grey]These weren't matched to a Forge listing. That's expected for a mod that isn't "
                + "published on Forge, or for a plugin bundled inside another mod you already have "
                + "installed. No action is needed unless you expected one of these to be its own mod "
                + "on Forge.[/]"
        );
        AnsiConsole.WriteLine();
    }
}
