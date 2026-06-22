using CheckMods.Models;
using CheckMods.Services;
using CheckMods.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="ModDependencyService.AnalyzeDependenciesAsync"/>: tree building, missing-dependency and
/// conflict tracking, installed-dependency detection, the circular-dependency guard, and per-mod fetch progress.
/// Dependencies are supplied through an in-memory <see cref="FakeForgeApiService"/>.
/// </summary>
public sealed class ModDependencyServiceTests
{
    private static ModDependencyService CreateService(FakeForgeApiService api)
    {
        return new ModDependencyService(api, NullLogger<ModDependencyService>.Instance);
    }

    private static Mod UnmatchedMod(string guid, string name)
    {
        return new Mod
        {
            Guid = guid,
            FilePath = $"{name}.dll",
            IsServerMod = true,
            LocalName = name,
            LocalAuthor = "Author",
            LocalVersion = "1.0.0",
        };
    }

    private static Mod MatchedMod(string guid, string name, int apiModId)
    {
        var mod = UnmatchedMod(guid, name);
        mod.UpdateFromApiMatch(
            new ModSearchResult(apiModId, null, name, "slug", null, null, 0, null, null, null, null)
        );
        return mod;
    }

    private static ModDependency Dep(
        string guid,
        string name = "Dep",
        int id = 0,
        string slug = "dep",
        string? version = null,
        bool conflict = false,
        List<ModDependency>? nested = null
    )
    {
        var latest = version is null ? null : new DependencyVersionInfo(1, version, null, null, null);
        return new ModDependency(id, guid, name, slug, latest, conflict, nested);
    }

    [Fact]
    public async Task Returns_all_mods_as_roots_when_none_are_matched()
    {
        // OnGetModDependencies is intentionally unset: the early-exit path must not call the API.
        var api = new FakeForgeApiService();
        var mod = UnmatchedMod("com.x.mod", "Mod");

        var result = await CreateService(api).AnalyzeDependenciesAsync([mod], []);

        var root = Assert.Single(result.RootMods);
        Assert.Same(mod, root.Mod);
        Assert.Empty(root.Children);
        Assert.Empty(result.MissingDependencies);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task Records_a_missing_dependency_with_a_download_link()
    {
        var api = new FakeForgeApiService
        {
            OnGetModDependencies = _ => new List<ModDependency>
            {
                Dep("com.author.dep", "Dependency", id: 500, slug: "dependency", version: "2.0.0"),
            },
        };

        var result = await CreateService(api)
            .AnalyzeDependenciesAsync([MatchedMod("com.author.main", "Main", 100)], []);

        var missing = Assert.Single(result.MissingDependencies);
        Assert.Equal("com.author.dep", missing.Guid);
        Assert.Equal("2.0.0", missing.RecommendedVersion);
        Assert.Equal(ForgeUrls.Download(500, "dependency", "2.0.0"), missing.DownloadLink);
    }

    [Fact]
    public async Task Does_not_flag_a_dependency_present_in_the_mod_list()
    {
        var main = MatchedMod("com.author.main", "Main", 100);
        var depMod = MatchedMod("com.author.dep", "Dep", 200);
        var api = new FakeForgeApiService
        {
            OnGetModDependencies = id =>
                id == "100" ? new List<ModDependency> { Dep("com.author.dep", id: 200) } : new List<ModDependency>(),
        };

        var result = await CreateService(api).AnalyzeDependenciesAsync([main, depMod], []);

        Assert.Empty(result.MissingDependencies);
    }

    [Fact]
    public async Task Does_not_flag_a_dependency_listed_in_installed_guids()
    {
        var api = new FakeForgeApiService
        {
            OnGetModDependencies = _ => new List<ModDependency> { Dep("com.author.dep", id: 200) },
        };

        var result = await CreateService(api)
            .AnalyzeDependenciesAsync([MatchedMod("com.author.main", "Main", 100)], ["com.author.dep"]);

        Assert.Empty(result.MissingDependencies);
    }

    [Fact]
    public async Task Detects_a_version_conflict()
    {
        var api = new FakeForgeApiService
        {
            OnGetModDependencies = _ => new List<ModDependency>
            {
                Dep("com.author.conf", "Conflicting", id: 500, conflict: true),
            },
        };

        var result = await CreateService(api)
            .AnalyzeDependenciesAsync([MatchedMod("com.author.main", "Main", 100)], []);

        var conflict = Assert.Single(result.Conflicts);
        Assert.Equal("com.author.conf", conflict.ModGuid);
        Assert.Equal("Conflicting", conflict.ModName);
    }

    [Fact]
    public async Task Guards_against_circular_dependencies()
    {
        // main -> A -> B -> A (back-edge). The repeated A must be pruned rather than recursed into forever.
        var backEdgeToA = Dep("com.a", "A");
        var b = Dep("com.b", "B", nested: [backEdgeToA]);
        var a = Dep("com.a", "A", nested: [b]);
        var api = new FakeForgeApiService { OnGetModDependencies = _ => new List<ModDependency> { a } };

        var result = await CreateService(api).AnalyzeDependenciesAsync([MatchedMod("com.main", "Main", 100)], []);

        var root = Assert.Single(result.RootMods);
        var nodeA = Assert.Single(root.Children);
        Assert.Equal("com.a", nodeA.DependencyInfo?.Guid);
        var nodeB = Assert.Single(nodeA.Children);
        Assert.Equal("com.b", nodeB.DependencyInfo?.Guid);
        Assert.Empty(nodeB.Children); // the circular back-edge to com.a was pruned
    }

    [Fact]
    public async Task Invokes_progress_once_per_unique_matched_mod()
    {
        // Two mods sharing one ApiModId should trigger a single dependency fetch.
        var m1 = MatchedMod("com.a.one", "One", 100);
        var m2 = MatchedMod("com.a.two", "Two", 100);
        var calls = new List<(int Fetched, int Total)>();
        var api = new FakeForgeApiService { OnGetModDependencies = _ => new List<ModDependency>() };

        await CreateService(api)
            .AnalyzeDependenciesAsync([m1, m2], [], (fetched, total) => calls.Add((fetched, total)));

        Assert.Equal((1, 1), Assert.Single(calls));
    }

    [Fact]
    public async Task Treats_a_dependency_fetch_error_as_no_dependencies()
    {
        var api = new FakeForgeApiService { OnGetModDependencies = _ => new ApiError("boom") };

        var result = await CreateService(api).AnalyzeDependenciesAsync([MatchedMod("com.main", "Main", 100)], []);

        var root = Assert.Single(result.RootMods);
        Assert.Empty(root.Children);
        Assert.Empty(result.MissingDependencies);
    }
}
