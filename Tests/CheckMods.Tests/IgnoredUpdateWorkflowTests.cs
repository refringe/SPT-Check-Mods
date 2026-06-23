using CheckMods.Models;
using CheckMods.Services;
using CheckMods.Utils;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="IgnoredUpdateWorkflow"/>: the ignore-set rewrite logic (<see cref="IgnoredUpdateWorkflow.BuildNewSet"/>)
/// and the update-page URL building used by the "open update pages" menu action.
/// </summary>
public sealed class IgnoredUpdateWorkflowTests
{
    private static IgnoredUpdate Entry(
        int id,
        string local = "1.0.0",
        string latest = "1.0.1",
        IgnoreSource source = IgnoreSource.User
    )
    {
        return new IgnoredUpdate(id, local, latest, Name: $"Mod {id}", Source: source);
    }

    private static Mod MatchedMod(int id, string slug, string? detailUrl)
    {
        var mod = new Mod
        {
            Guid = $"com.author.mod{id}",
            FilePath = $"mods/Mod{id}.dll",
            IsServerMod = true,
            LocalName = $"Mod {id}",
            LocalAuthor = "Author",
            LocalVersion = "1.0.0",
        };

        mod.UpdateFromApiMatch(
            new ModSearchResult(
                Id: id,
                HubId: null,
                Name: $"Mod {id}",
                Slug: slug,
                Teaser: null,
                Thumbnail: null,
                Downloads: 0,
                SourceCodeLinks: null,
                DetailUrl: detailUrl,
                Owner: null,
                Versions: null
            )
        );

        return mod;
    }

    [Fact]
    public void BuildUpdatePageUrls_prefers_the_api_detail_url()
    {
        var mod = MatchedMod(2471, "cool-mod", "https://forge.sp-tarkov.com/mod/2471/cool-mod");

        var urls = IgnoredUpdateWorkflow.BuildUpdatePageUrls([mod]);

        Assert.Equal(new[] { "https://forge.sp-tarkov.com/mod/2471/cool-mod" }, urls);
    }

    [Fact]
    public void BuildUpdatePageUrls_falls_back_to_a_mod_page_built_from_id_and_slug()
    {
        // No detail URL from the API, but the Forge id and slug are enough to build the page link.
        var mod = MatchedMod(2471, "cool-mod", detailUrl: null);

        var urls = IgnoredUpdateWorkflow.BuildUpdatePageUrls([mod]);

        Assert.Equal(new[] { ForgeUrls.ModPage(2471, "cool-mod") }, urls);
    }

    [Fact]
    public void BuildUpdatePageUrls_dedups_components_that_share_a_page()
    {
        // Paired server/client components resolve to the same Forge mod page and should open a single tab.
        var server = MatchedMod(2471, "cool-mod", "https://forge.sp-tarkov.com/mod/2471/cool-mod");
        var client = MatchedMod(2471, "cool-mod", "https://forge.sp-tarkov.com/mod/2471/cool-mod");

        var urls = IgnoredUpdateWorkflow.BuildUpdatePageUrls([server, client]);

        Assert.Single(urls);
    }

    [Fact]
    public void BuildNewSet_preserves_entries_for_mods_not_evaluated_this_run()
    {
        var existing = new List<IgnoredUpdate> { Entry(1), Entry(2) };
        var evaluated = new HashSet<int> { 1 }; // only mod 1 was installed/evaluated this run

        var result = IgnoredUpdateWorkflow.BuildNewSet(existing, evaluated, []);

        // Mod 2 wasn't evaluated, so its entry survives even though it wasn't re-selected.
        Assert.Single(result);
        Assert.Equal(2, result[0].ApiModId);
    }

    [Fact]
    public void BuildNewSet_drops_evaluated_but_unselected_entries()
    {
        var existing = new List<IgnoredUpdate> { Entry(1) };
        var evaluated = new HashSet<int> { 1 };

        // Mod 1 was evaluated and shown, but the user un-checked it -> removed (undo / self-resolved).
        var result = IgnoredUpdateWorkflow.BuildNewSet(existing, evaluated, []);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildNewSet_adds_selected_entries()
    {
        var result = IgnoredUpdateWorkflow.BuildNewSet([], new HashSet<int> { 5 }, [Entry(5, latest: "2.0.0")]);

        Assert.Single(result);
        Assert.Equal(5, result[0].ApiModId);
        Assert.Equal("2.0.0", result[0].IgnoredLatestVersion);
    }

    [Fact]
    public void BuildNewSet_keeps_reselected_entry_without_duplicating()
    {
        var existing = new List<IgnoredUpdate> { Entry(1) };
        var evaluated = new HashSet<int> { 1 };

        // User kept mod 1 checked: it arrives via "selected" and shouldn't duplicate.
        var result = IgnoredUpdateWorkflow.BuildNewSet(existing, evaluated, [Entry(1)]);

        Assert.Single(result);
        Assert.Equal(1, result[0].ApiModId);
    }
}
