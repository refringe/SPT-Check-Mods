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

    private static Mod MatchedModWithVersion(string guid, string name, int apiModId, string localVersion)
    {
        var mod = new Mod
        {
            Guid = guid,
            FilePath = $"{name}.dll",
            IsServerMod = true,
            LocalName = name,
            LocalAuthor = "Author",
            LocalVersion = localVersion,
        };
        mod.UpdateFromApiMatch(
            new ModSearchResult(apiModId, null, name, "slug", null, null, 0, null, null, null, null)
        );
        return mod;
    }

    /// <summary>A matched mod (installed at 1.0.0) with an available update to <paramref name="latestVersion"/>.</summary>
    private static Mod UpdatableMod(string guid, string name, int apiModId, string latestVersion)
    {
        var mod = MatchedMod(guid, name, apiModId);
        mod.UpdateFromSafeToUpdate(
            new SafeToUpdateMod(
                null,
                new ModUpdateVersion(null, apiModId, guid, name, "slug", latestVersion, null, null),
                null
            )
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

    [Fact]
    public async Task Update_reports_a_newly_required_missing_dependency_with_a_download_link()
    {
        var main = UpdatableMod("com.author.main", "Main", 100, "2.0.0");
        var api = new FakeForgeApiService
        {
            OnGetModDependenciesVersioned = key =>
                key switch
                {
                    ("100", "2.0.0") => new List<ModDependency>
                    {
                        Dep("com.author.dep", "Dependency", id: 500, slug: "dependency", version: "3.0.0"),
                    },
                    _ => new List<ModDependency>(),
                },
        };

        await CreateService(api).AnalyzeDependenciesAsync([main], []);

        var delta = main.UpdateDependencyChanges;
        Assert.NotNull(delta);
        var added = Assert.Single(delta.Added);
        Assert.Equal("com.author.dep", added.Guid);
        Assert.Equal(DependencyInstallState.NotInstalled, added.InstallState);
        Assert.Equal("3.0.0", added.RecommendedVersion);
        Assert.Equal(ForgeUrls.Download(500, "dependency", "3.0.0"), added.DownloadLink);
        Assert.Empty(delta.Removed);
    }

    [Fact]
    public async Task Update_marks_an_installed_adequate_dependency_as_satisfied()
    {
        var main = UpdatableMod("com.author.main", "Main", 100, "2.0.0");
        var dep = MatchedModWithVersion("com.author.dep", "Dep", 500, "3.0.0");
        var api = new FakeForgeApiService
        {
            OnGetModDependenciesVersioned = key =>
                key switch
                {
                    ("100", "2.0.0") => new List<ModDependency> { Dep("com.author.dep", id: 500, version: "3.0.0") },
                    _ => new List<ModDependency>(),
                },
        };

        await CreateService(api).AnalyzeDependenciesAsync([main, dep], []);

        var added = Assert.Single(main.UpdateDependencyChanges!.Added);
        Assert.Equal(DependencyInstallState.InstalledOk, added.InstallState);
        Assert.Equal("3.0.0", added.InstalledVersion);
    }

    [Fact]
    public async Task Update_flags_an_installed_dependency_that_is_out_of_date()
    {
        var main = UpdatableMod("com.author.main", "Main", 100, "2.0.0");
        var dep = MatchedModWithVersion("com.author.dep", "Dep", 500, "2.0.0");
        var api = new FakeForgeApiService
        {
            OnGetModDependenciesVersioned = key =>
                key switch
                {
                    ("100", "2.0.0") => new List<ModDependency> { Dep("com.author.dep", id: 500, version: "3.0.0") },
                    _ => new List<ModDependency>(),
                },
        };

        await CreateService(api).AnalyzeDependenciesAsync([main, dep], []);

        var added = Assert.Single(main.UpdateDependencyChanges!.Added);
        Assert.Equal(DependencyInstallState.InstalledOutdated, added.InstallState);
        Assert.Equal("2.0.0", added.InstalledVersion);
        Assert.Equal("3.0.0", added.RecommendedVersion);
    }

    [Fact]
    public async Task Update_detects_a_transitively_added_dependency()
    {
        var main = UpdatableMod("com.author.main", "Main", 100, "2.0.0");
        var nested = Dep("com.b", "B", id: 601, version: "1.0.0");
        var api = new FakeForgeApiService
        {
            OnGetModDependenciesVersioned = key =>
                key switch
                {
                    ("100", "2.0.0") => new List<ModDependency>
                    {
                        Dep("com.a", "A", id: 600, version: "1.0.0", nested: [nested]),
                    },
                    _ => new List<ModDependency>(),
                },
        };

        await CreateService(api).AnalyzeDependenciesAsync([main], []);

        var added = main.UpdateDependencyChanges!.Added;
        Assert.Equal(2, added.Count);
        Assert.Contains(added, c => c.Guid == "com.a");
        Assert.Contains(added, c => c.Guid == "com.b");
    }

    [Fact]
    public async Task Update_reports_a_no_longer_required_dependency()
    {
        var main = UpdatableMod("com.author.main", "Main", 100, "2.0.0");
        var api = new FakeForgeApiService
        {
            OnGetModDependenciesVersioned = key =>
                key switch
                {
                    ("100", "1.0.0") => new List<ModDependency> { Dep("com.old", "Old", id: 700, version: "1.0.0") },
                    _ => new List<ModDependency>(),
                },
        };

        await CreateService(api).AnalyzeDependenciesAsync([main], []);

        var removed = Assert.Single(main.UpdateDependencyChanges!.Removed);
        Assert.Equal("com.old", removed.Guid);
        Assert.Empty(main.UpdateDependencyChanges.Added);
    }

    [Fact]
    public async Task Does_not_attach_dependency_changes_when_no_update_is_available()
    {
        var main = MatchedMod("com.author.main", "Main", 100);
        var api = new FakeForgeApiService { OnGetModDependencies = _ => new List<ModDependency>() };

        await CreateService(api).AnalyzeDependenciesAsync([main], []);

        Assert.Null(main.UpdateDependencyChanges);
    }

    [Fact]
    public async Task A_target_version_fetch_error_leaves_changes_unset_but_keeps_the_current_analysis()
    {
        var main = UpdatableMod("com.author.main", "Main", 100, "2.0.0");
        var api = new FakeForgeApiService
        {
            OnGetModDependenciesVersioned = key =>
                key switch
                {
                    // Installed version still surfaces its missing dependency...
                    ("100", "1.0.0") => new List<ModDependency>
                    {
                        Dep("com.author.dep", "Dependency", id: 500, slug: "dependency", version: "2.0.0"),
                    },
                    // ...but the proposed-version fetch fails, so no delta can be computed.
                    ("100", "2.0.0") => new ApiError("boom"),
                    _ => new List<ModDependency>(),
                },
        };

        var result = await CreateService(api).AnalyzeDependenciesAsync([main], []);

        Assert.Null(main.UpdateDependencyChanges);
        Assert.Single(result.MissingDependencies);
    }

    [Fact]
    public async Task Update_surfaces_a_conflicting_new_dependency()
    {
        var main = UpdatableMod("com.author.main", "Main", 100, "2.0.0");
        var api = new FakeForgeApiService
        {
            OnGetModDependenciesVersioned = key =>
                key switch
                {
                    ("100", "2.0.0") => new List<ModDependency>
                    {
                        Dep("com.conf", "Conflicting", id: 800, version: "1.0.0", conflict: true),
                    },
                    _ => new List<ModDependency>(),
                },
        };

        await CreateService(api).AnalyzeDependenciesAsync([main], []);

        var added = Assert.Single(main.UpdateDependencyChanges!.Added);
        Assert.True(added.Conflict);
    }
}
