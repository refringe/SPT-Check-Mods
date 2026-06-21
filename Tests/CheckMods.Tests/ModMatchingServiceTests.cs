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

    private static ModSearchResult Match(int id, string name, string slug)
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
            Owner: null,
            Versions: null
        );
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
}
