using CheckMods.Models;
using CheckMods.Services.Interfaces;
using CheckMods.Utils;
using SPTarkov.DI.Annotations;

namespace CheckMods.Services;

/// <summary>
/// Default <see cref="IIgnoredUpdateWorkflow"/>. Presents the currently-flagged updates in a checklist (already-ignored
/// ones pre-checked), rewrites the visible decisions while preserving ignores for mods not evaluated this run, and
/// offers to report the result as a GitHub issue so others benefit.
/// </summary>
[Injectable(InjectionType.Transient)]
public sealed class IgnoredUpdateWorkflow(
    IIgnoredUpdateStore store,
    IModCheckReporter reporter,
    IBrowserLauncher browserLauncher
) : IIgnoredUpdateWorkflow
{
    /// <inheritdoc />
    public Task RunAsync(IReadOnlyList<Mod>? mods, CancellationToken cancellationToken = default)
    {
        // One row per Forge mod id (paired server/client mods share an id and a single table row).
        var candidates = (mods ?? [])
            .Where(m => m is { UpdateStatus: UpdateStatus.UpdateAvailable, ApiModId: not null })
            .GroupBy(m => m.ApiModId!.Value)
            .Select(g => g.First())
            .ToList();

        // The mods actually shown as "Updates available" — dismissed false positives are treated as up to date and
        // aren't opened in the browser.
        var openable = candidates.Where(m => !m.UpdateSuppressed).ToList();

        // The end-of-run menu loops until the user chooses to close: opening pages or managing ignores returns here so
        // they can do both before exiting. The counts reflect this run's results and don't change as ignores are
        // edited — those take effect on the next run.
        while (true)
        {
            var choice = reporter.PromptEndOfRun(openable.Count, canManageIgnoredUpdates: candidates.Count > 0);

            switch (choice)
            {
                case EndOfRunChoice.OpenUpdatePages:
                    OpenUpdatePages(openable);
                    break;

                case EndOfRunChoice.ManageIgnoredUpdates:
                    ManageIgnoredUpdates(mods!, candidates);
                    break;

                default:
                    return Task.CompletedTask;
            }
        }
    }

    /// <summary>
    /// Opens each updatable mod's Forge page in the browser, then reports how many opened.
    /// </summary>
    private void OpenUpdatePages(IReadOnlyList<Mod> openable)
    {
        var urls = BuildUpdatePageUrls(openable);

        var opened = 0;
        foreach (var url in urls)
        {
            if (browserLauncher.TryOpenUrl(url))
            {
                opened++;
            }
        }

        reporter.UpdatePagesOpened(opened, urls.Count);
    }

    /// <summary>
    /// Builds the deduplicated list of Forge mod-page URLs to open for the given updatable mods. Prefers the mod's
    /// known detail URL, falling back to one constructed from its Forge id and slug.
    /// </summary>
    internal static IReadOnlyList<string> BuildUpdatePageUrls(IReadOnlyList<Mod> openable)
    {
        var urls = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mod in openable)
        {
            var url = !string.IsNullOrWhiteSpace(mod.ApiUrl)
                ? mod.ApiUrl
                : mod.ApiModId.HasValue
                    ? ForgeUrls.ModPage(mod.ApiModId.Value, mod.ApiSlug)
                    : null;

            if (!string.IsNullOrWhiteSpace(url) && seen.Add(url))
            {
                urls.Add(url);
            }
        }

        return urls;
    }

    /// <summary>
    /// Runs the ignore-management checklist for the available updates and persists the result, then offers to
    /// contribute the just-confirmed ignores. The new ignores take effect the next time the app is run; we deliberately
    /// don't re-check or re-render the table here.
    /// </summary>
    private void ManageIgnoredUpdates(IReadOnlyList<Mod> mods, IReadOnlyList<Mod> candidates)
    {
        var preIgnoredIds = candidates.Where(m => m.UpdateSuppressed).Select(m => m.ApiModId!.Value).ToHashSet();
        var selected = reporter.SelectUpdatesToIgnore(candidates, preIgnoredIds);
        var chosen = selected.Select(ToIgnoredUpdate).ToList();

        PersistSelection(mods, chosen);

        OfferReport(chosen);
    }

    private void PersistSelection(IReadOnlyList<Mod> mods, IReadOnlyList<IgnoredUpdate> chosen)
    {
        var evaluatedIds = mods.Where(m => m.IsMatched).Select(m => m.ApiModId!.Value).ToHashSet();
        var newSet = BuildNewSet(store.Load(), evaluatedIds, chosen);
        store.Save(newSet);
    }

    /// <summary>
    /// Offers to report the just-confirmed ignores as a templated GitHub issue, opening the user's browser to a
    /// pre-filled new-issue form when they accept. Opt-in with a default of "no"; skipped when nothing was selected.
    /// </summary>
    /// <param name="chosen">The entries the user confirmed as ignored this run.</param>
    private void OfferReport(IReadOnlyList<IgnoredUpdate> chosen)
    {
        if (chosen.Count == 0)
        {
            return;
        }

        if (!reporter.PromptReportIgnores())
        {
            return;
        }

        var url = IgnoreReportUrl.Build(chosen, out var prefilled);
        var opened = browserLauncher.TryOpenUrl(url);
        reporter.IgnoreReportOpened(url, opened, prefilled);
    }

    /// <summary>
    /// Builds the new ignore set: keep existing entries for mods that weren't evaluated this run, then add this run's
    /// selected entries. Entries for evaluated-but-unselected mods are intentionally dropped — the user un-ignored them,
    /// or they resolved on their own (now genuinely up to date).
    /// </summary>
    internal static List<IgnoredUpdate> BuildNewSet(
        IReadOnlyList<IgnoredUpdate> existing,
        ISet<int> evaluatedApiModIds,
        IReadOnlyList<IgnoredUpdate> selected
    )
    {
        var result = existing.Where(e => !evaluatedApiModIds.Contains(e.ApiModId)).ToList();
        var keys = result.Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in selected)
        {
            if (keys.Add(entry.Key))
            {
                result.Add(entry);
            }
        }

        return result;
    }

    private static IgnoredUpdate ToIgnoredUpdate(Mod mod)
    {
        return new IgnoredUpdate(
            ApiModId: mod.ApiModId!.Value,
            LocalVersion: mod.LocalVersion,
            IgnoredLatestVersion: mod.LatestVersion!,
            Name: mod.DisplayName,
            Guid: string.IsNullOrWhiteSpace(mod.Guid) ? null : mod.Guid,
            Source: IgnoreSource.User,
            DismissedUtc: DateTimeOffset.UtcNow
        );
    }
}
