using CheckMods.Models;
using CheckMods.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="ModMatchingService.MatchModsAsync"/>, focusing on per-mod failure isolation and the systemic
/// failure surfacing (when every mod throws, the batch raises an error instead of silently reporting all as
/// not-found). Drives matching through an in-memory <see cref="FakeForgeApiService"/>.
/// </summary>
public sealed class ModMatchingServiceTests
{
    private static readonly SemanticVersioning.Version SptVersion = new("3.0.0");

    private static ModMatchingService CreateService(FakeForgeApiService api)
    {
        return new ModMatchingService(api, NullLogger<ModMatchingService>.Instance);
    }

    private static Mod ClientMod(string guid, string name = "Mod", string version = "1.0.0")
    {
        return new Mod
        {
            Guid = guid,
            FilePath = $"plugins/{name}.dll",
            IsServerMod = false,
            LocalName = name,
            LocalAuthor = "Author",
            LocalVersion = version,
        };
    }

    private static ModSearchResult Match(int id, string name, string slug, string? ownerName = null)
    {
        return new ModSearchResult(
            Id: id,
            HubId: null,
            Name: name,
            Slug: slug,
            Teaser: null,
            Thumbnail: null,
            Downloads: 0,
            SourceCodeLinks: null,
            DetailUrl: $"https://forge.sp-tarkov.com/mod/{id}/{slug}",
            Owner: ownerName is null ? null : new ModAuthor(1, ownerName, null),
            Versions: null
        );
    }

    private static Mod ClientModFull(string guid, string name, string author)
    {
        return new Mod
        {
            Guid = guid,
            FilePath = $"plugins/{name}.dll",
            IsServerMod = false,
            LocalName = name,
            LocalAuthor = author,
            LocalVersion = "1.0.0",
        };
    }

    [Fact]
    public async Task Matches_a_mod_by_guid()
    {
        var api = new FakeForgeApiService { OnGetModByGuid = _ => Match(2471, "Cool Mod", "cool-mod") };
        var mod = ClientMod("com.author.coolmod", "Cool Mod");

        await CreateService(api).MatchModsAsync([mod], SptVersion);

        Assert.True(mod.IsMatched);
        Assert.Equal(2471, mod.ApiModId);
    }

    [Fact]
    public async Task Falls_back_to_name_search_when_guid_misses()
    {
        var api = new FakeForgeApiService
        {
            OnGetModByGuid = _ => new NotFound(),
            OnSearch = _ => new List<ModSearchResult> { Match(2471, "Cool Mod", "cool-mod") },
        };
        var mod = ClientMod("com.author.coolmod", "Cool Mod");

        await CreateService(api).MatchModsAsync([mod], SptVersion);

        Assert.True(mod.IsMatched);
        Assert.Equal(2471, mod.ApiModId);
    }

    [Fact]
    public async Task Marks_a_mod_unmatched_when_nothing_matches()
    {
        var api = new FakeForgeApiService
        {
            OnGetModByGuid = _ => new NotFound(),
            OnSearch = _ => new List<ModSearchResult>(),
        };
        var mod = ClientMod("com.author.coolmod", "Cool Mod");

        await CreateService(api).MatchModsAsync([mod], SptVersion);

        Assert.False(mod.IsMatched);
        Assert.Equal(ModStatus.NoMatch, mod.Status);
    }

    [Fact]
    public async Task Throws_when_every_mod_fails()
    {
        var boom = new InvalidOperationException("metadata parse failure");
        var api = new FakeForgeApiService { OnGetModByGuid = _ => throw boom };
        var mods = new[] { ClientMod("com.a.one"), ClientMod("com.a.two"), ClientMod("com.a.three") };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService(api).MatchModsAsync(mods, SptVersion)
        );

        // The systemic failure wraps the underlying exception rather than swallowing it.
        Assert.Same(boom, ex.InnerException);
    }

    [Fact]
    public async Task Isolates_a_single_failure_and_matches_the_rest()
    {
        var api = new FakeForgeApiService
        {
            OnGetModByGuid = guid =>
                guid.Contains("boom") ? throw new InvalidOperationException("boom") : Match(1, "Mod", "mod"),
        };
        var good1 = ClientMod("com.a.good1");
        var bad = ClientMod("com.a.boom");
        var good2 = ClientMod("com.a.good2");

        await CreateService(api).MatchModsAsync([good1, bad, good2], SptVersion);

        Assert.True(good1.IsMatched);
        Assert.True(good2.IsMatched);
        Assert.False(bad.IsMatched);
        Assert.Equal(ModStatus.NoMatch, bad.Status);
    }

    [Fact]
    public async Task Does_not_throw_on_partial_failure()
    {
        var api = new FakeForgeApiService
        {
            OnGetModByGuid = guid =>
                guid.Contains("boom") ? throw new InvalidOperationException("boom") : Match(1, "Mod", "mod"),
        };
        var mods = new[]
        {
            ClientMod("com.a.boom1"),
            ClientMod("com.a.good1"),
            ClientMod("com.a.boom2"),
            ClientMod("com.a.good2"),
        };

        var results = await CreateService(api).MatchModsAsync(mods, SptVersion);

        Assert.Equal(4, results.Count);
        Assert.Equal(2, results.Count(m => m.IsMatched));
    }

    [Fact]
    public async Task Returns_empty_for_no_mods()
    {
        var api = new FakeForgeApiService { OnGetModByGuid = _ => throw new InvalidOperationException("should not run") };

        var results = await CreateService(api).MatchModsAsync([], SptVersion);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Invokes_progress_callback_once_per_mod()
    {
        var api = new FakeForgeApiService { OnGetModByGuid = _ => Match(1, "Mod", "mod") };
        var mods = new[] { ClientMod("com.a.one"), ClientMod("com.a.two"), ClientMod("com.a.three") };
        var progressCalls = new List<(int Current, int Total)>();

        await CreateService(api)
            .MatchModsAsync(
                mods,
                SptVersion,
                (_, current, total) =>
                {
                    lock (progressCalls)
                    {
                        progressCalls.Add((current, total));
                    }
                }
            );

        Assert.Equal(3, progressCalls.Count);
        Assert.All(progressCalls, c => Assert.Equal(3, c.Total));
        Assert.Contains((3, 3), progressCalls);
    }

    // --- FindBestMatch strategies (GUID misses, so matching falls through to the name search) ---

    [Fact]
    public async Task FindBestMatch_matches_by_exact_normalized_name()
    {
        // Name normalizes to the local name; slug deliberately does not, so only strategy 1 can match.
        var api = new FakeForgeApiService
        {
            OnGetModByGuid = _ => new NotFound(),
            OnSearch = _ => new List<ModSearchResult> { Match(10, "super-mod", "zzz") },
        };
        var mod = ClientMod("com.x.super", "Super Mod");

        await CreateService(api).MatchModsAsync([mod], SptVersion);

        Assert.True(mod.IsMatched);
        Assert.Equal(10, mod.ApiModId);
    }

    [Fact]
    public async Task FindBestMatch_matches_after_removing_component_suffix()
    {
        var api = new FakeForgeApiService
        {
            OnGetModByGuid = _ => new NotFound(),
            OnSearch = _ => new List<ModSearchResult> { Match(20, "CoolMod", "zzz") },
        };
        var mod = ClientMod("com.x.coolmod", "CoolModServer");

        await CreateService(api).MatchModsAsync([mod], SptVersion);

        Assert.True(mod.IsMatched);
        Assert.Equal(20, mod.ApiModId);
    }

    [Fact]
    public async Task FindBestMatch_matches_by_slug_when_name_differs()
    {
        var api = new FakeForgeApiService
        {
            OnGetModByGuid = _ => new NotFound(),
            OnSearch = _ => new List<ModSearchResult> { Match(30, "Totally Different", "awesome-thing") },
        };
        var mod = ClientMod("com.x.thing", "Awesome Thing");

        await CreateService(api).MatchModsAsync([mod], SptVersion);

        Assert.True(mod.IsMatched);
        Assert.Equal(30, mod.ApiModId);
    }

    [Fact]
    public async Task FindBestMatch_matches_by_author_and_name()
    {
        // Name matches only after suffix removal and the slug is blank, so strategies 1-3 miss; the owner+name
        // strategy is what links them.
        var api = new FakeForgeApiService
        {
            OnGetModByGuid = _ => new NotFound(),
            OnSearch = _ => new List<ModSearchResult> { Match(40, "HeroModClient", "", ownerName: "JaneDoe") },
        };
        var mod = ClientModFull("com.jane.hero", "HeroMod", "JaneDoe");

        await CreateService(api).MatchModsAsync([mod], SptVersion);

        Assert.True(mod.IsMatched);
        Assert.Equal(40, mod.ApiModId);
    }

    [Fact]
    public async Task FindBestMatch_matches_by_fuzzy_score_above_threshold()
    {
        // One-character typo: well above the fuzzy threshold but not an exact/slug/author match.
        var api = new FakeForgeApiService
        {
            OnGetModByGuid = _ => new NotFound(),
            OnSearch = _ => new List<ModSearchResult> { Match(50, "Inventory Managr", "zzz") },
        };
        var mod = ClientMod("com.x.inventory", "Inventory Manager");

        await CreateService(api).MatchModsAsync([mod], SptVersion);

        Assert.True(mod.IsMatched);
        Assert.Equal(50, mod.ApiModId);
    }

    [Fact]
    public async Task FindBestMatch_returns_no_match_for_unrelated_results()
    {
        var api = new FakeForgeApiService
        {
            OnGetModByGuid = _ => new NotFound(),
            OnSearch = _ => new List<ModSearchResult> { Match(60, "Completely Unrelated Zeta", "zzz") },
        };
        var mod = ClientMod("com.x.alpha", "Alpha Mod");

        await CreateService(api).MatchModsAsync([mod], SptVersion);

        Assert.False(mod.IsMatched);
        Assert.Equal(ModStatus.NoMatch, mod.Status);
    }

    // --- BuildSearchTerms (captured via the queries the search handler receives) ---

    [Fact]
    public async Task BuildSearchTerms_includes_name_suffix_guid_and_author_variants()
    {
        var queries = new List<string>();
        var api = new FakeForgeApiService
        {
            OnGetModByGuid = _ => new NotFound(),
            OnSearch = q =>
            {
                lock (queries)
                {
                    queries.Add(q);
                }

                return new List<ModSearchResult>();
            },
        };
        var mod = ClientModFull("com.jane.differentname", "CoolModServer", "Jane");

        await CreateService(api).MatchModsAsync([mod], SptVersion);

        Assert.Contains("CoolModServer", queries); // local name
        Assert.Contains("CoolMod", queries); // component suffix removed
        Assert.Contains("differentname", queries); // extracted from the GUID
        Assert.Contains("Jane CoolModServer", queries); // author + name
    }

    [Fact]
    public async Task BuildSearchTerms_excludes_author_term_for_unknown_author()
    {
        var queries = new List<string>();
        var api = new FakeForgeApiService
        {
            OnGetModByGuid = _ => new NotFound(),
            OnSearch = q =>
            {
                lock (queries)
                {
                    queries.Add(q);
                }

                return new List<ModSearchResult>();
            },
        };
        var mod = ClientModFull("com.x.mod", "Mod", "Unknown");

        await CreateService(api).MatchModsAsync([mod], SptVersion);

        Assert.DoesNotContain(queries, q => q.Contains("Unknown"));
    }
}
