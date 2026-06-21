using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="UpdateCheckService"/>: the static <see cref="UpdateCheckService.InterpretUpdates"/> mapper, and
/// the <see cref="UpdateCheckService.CheckAsync"/> flow - in particular the unrecognized-build fallback that selects
/// the latest stable version. The mod-id used mirrors the real Forge listing (2471).
/// </summary>
public sealed class UpdateCheckServiceTests
{
    private const int ModId = 2471;

    private static readonly SemanticVersioning.Version SptVersion = new("3.0.0");

    private static UpdateCheckService CreateService(FakeForgeApiService api)
    {
        return new UpdateCheckService(
            api,
            Options.Create(new UpdateCheckOptions { ForgeModId = ModId }),
            NullLogger<UpdateCheckService>.Instance
        );
    }

    private static ModVersion ApiModVersion(string version, string? link = null)
    {
        return new ModVersion(
            Id: 1,
            HubId: null,
            Version: version,
            Description: null,
            Link: link,
            SptVersionConstraint: ">=1.0.0",
            VirusTotalLink: null,
            Downloads: 0,
            PublishedAt: null,
            CreatedAt: null,
            UpdatedAt: null
        );
    }

    private static ModSearchResult ModWithVersions(string? detailUrl, params ModVersion[] versions)
    {
        return new ModSearchResult(
            Id: ModId,
            HubId: null,
            Name: "Check Mods",
            Slug: "check-mods",
            Teaser: null,
            Thumbnail: null,
            Downloads: 0,
            SourceCodeLinks: null,
            DetailUrl: detailUrl,
            Owner: null,
            Versions: versions.ToList()
        );
    }

    private static ModUpdateVersion Version(int modId, string version, string? link = null)
    {
        return new ModUpdateVersion(
            Id: null,
            ModId: modId,
            Guid: "com.refringe.checkmods",
            Name: "Check Mods",
            Slug: "check-mods",
            Version: version,
            Link: link,
            SptVersions: null
        );
    }

    [Fact]
    public void Returns_update_available_when_mod_in_updates()
    {
        var data = new ModUpdatesData(
            SafeToUpdate:
            [
                new SafeToUpdateMod(
                    Version(ModId, "1.0.0"),
                    Version(ModId, "1.0.1", "https://forge.sp-tarkov.com/mod/download/2471/check-mods/1.0.1"),
                    "newer_version_available"
                ),
            ],
            Blocked: null,
            UpToDate: null,
            Incompatible: null
        );

        var result = UpdateCheckService.InterpretUpdates(data, ModId, "1.0.0");

        Assert.NotNull(result);
        Assert.Equal(CheckModsUpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("1.0.1", result.LatestVersion);
        Assert.Equal("https://forge.sp-tarkov.com/mod/download/2471/check-mods/1.0.1", result.DownloadLink);
    }

    [Fact]
    public void Returns_up_to_date_when_mod_in_up_to_date()
    {
        var data = new ModUpdatesData(
            SafeToUpdate: null,
            Blocked: null,
            UpToDate: [new UpToDateMod(null, ModId, "com.refringe.checkmods", "Check Mods", "1.0.1", null)],
            Incompatible: null
        );

        var result = UpdateCheckService.InterpretUpdates(data, ModId, "1.0.1");

        Assert.NotNull(result);
        Assert.Equal(CheckModsUpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public void Returns_incompatible_when_mod_in_incompatible()
    {
        var data = new ModUpdatesData(
            SafeToUpdate: null,
            Blocked: null,
            UpToDate: null,
            Incompatible:
            [
                new IncompatibleMod(
                    null,
                    ModId,
                    "com.refringe.checkmods",
                    "Check Mods",
                    "1.0.1",
                    "no_version_for_spt",
                    null
                ),
            ]
        );

        var result = UpdateCheckService.InterpretUpdates(data, ModId, "1.0.1");

        Assert.NotNull(result);
        Assert.Equal(CheckModsUpdateStatus.IncompatibleWithSpt, result.Status);
    }

    [Fact]
    public void Returns_null_when_all_categories_empty()
    {
        var data = new ModUpdatesData([], [], [], []);

        var result = UpdateCheckService.InterpretUpdates(data, ModId, "0.0.1");

        Assert.Null(result);
    }

    [Fact]
    public void Returns_null_when_mod_absent_from_populated_categories()
    {
        var data = new ModUpdatesData(
            SafeToUpdate:
            [
                new SafeToUpdateMod(
                    Version(9999, "1.0.0"),
                    Version(9999, "2.0.0", "https://x"),
                    "newer_version_available"
                ),
            ],
            Blocked: null,
            UpToDate: [new UpToDateMod(null, 8888, "com.other", "Another", "1.0.0", null)],
            Incompatible: null
        );

        var result = UpdateCheckService.InterpretUpdates(data, ModId, "1.0.0");

        Assert.Null(result);
    }

    [Fact]
    public async Task CheckAsync_reports_update_available_from_the_updates_endpoint()
    {
        var data = new ModUpdatesData(
            SafeToUpdate:
            [
                new SafeToUpdateMod(
                    Version(ModId, "1.0.0"),
                    Version(ModId, "9.9.9", "https://download/9.9.9"),
                    "newer_version_available"
                ),
            ],
            Blocked: null,
            UpToDate: null,
            Incompatible: null
        );
        var api = new FakeForgeApiService { OnGetModUpdates = () => data };

        var result = await CreateService(api).CheckAsync(SptVersion);

        Assert.Equal(CheckModsUpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("9.9.9", result.LatestVersion);
    }

    [Fact]
    public async Task CheckAsync_reports_unavailable_on_api_error()
    {
        var api = new FakeForgeApiService { OnGetModUpdates = () => new ApiError("boom") };

        var result = await CreateService(api).CheckAsync(SptVersion);

        Assert.Equal(CheckModsUpdateStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task CheckAsync_recommends_latest_stable_version_for_an_unrecognized_build()
    {
        var api = new FakeForgeApiService
        {
            OnGetModUpdates = () => new NotFound(),
            OnGetModById = _ =>
                ModWithVersions("https://detail", ApiModVersion("1.0.0"), ApiModVersion("2.0.0-beta.1"), ApiModVersion("1.2.0")),
        };

        var result = await CreateService(api).CheckAsync(SptVersion);

        Assert.Equal(CheckModsUpdateStatus.UnrecognizedBuild, result.Status);
        Assert.Equal("1.2.0", result.LatestVersion); // 2.0.0-beta.1 is excluded as a prerelease
    }

    [Fact]
    public async Task CheckAsync_orders_unrecognized_versions_by_semver_not_string()
    {
        var api = new FakeForgeApiService
        {
            OnGetModUpdates = () => new NotFound(),
            OnGetModById = _ =>
                ModWithVersions(null, ApiModVersion("1.2.0"), ApiModVersion("1.10.0"), ApiModVersion("1.9.0")),
        };

        var result = await CreateService(api).CheckAsync(SptVersion);

        // A string sort would wrongly pick "1.9.0"; semver ordering selects "1.10.0".
        Assert.Equal("1.10.0", result.LatestVersion);
    }

    [Fact]
    public async Task CheckAsync_reports_unavailable_when_only_prereleases_exist()
    {
        var api = new FakeForgeApiService
        {
            OnGetModUpdates = () => new NotFound(),
            OnGetModById = _ => ModWithVersions(null, ApiModVersion("1.0.0-alpha"), ApiModVersion("2.0.0-beta")),
        };

        var result = await CreateService(api).CheckAsync(SptVersion);

        Assert.Equal(CheckModsUpdateStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task CheckAsync_uses_the_version_link_as_download_link_when_present()
    {
        var api = new FakeForgeApiService
        {
            OnGetModUpdates = () => new NotFound(),
            OnGetModById = _ => ModWithVersions("https://detail", ApiModVersion("1.0.0", "https://download/1.0.0")),
        };

        var result = await CreateService(api).CheckAsync(SptVersion);

        Assert.Equal("https://download/1.0.0", result.DownloadLink);
    }

    [Fact]
    public async Task CheckAsync_falls_back_to_detail_url_when_the_version_has_no_link()
    {
        var api = new FakeForgeApiService
        {
            OnGetModUpdates = () => new NotFound(),
            OnGetModById = _ => ModWithVersions("https://detail-page", ApiModVersion("1.0.0", link: null)),
        };

        var result = await CreateService(api).CheckAsync(SptVersion);

        Assert.Equal("https://detail-page", result.DownloadLink);
    }

    [Fact]
    public async Task CheckAsync_reports_unavailable_when_the_mod_is_not_found()
    {
        var api = new FakeForgeApiService
        {
            OnGetModUpdates = () => new NotFound(),
            OnGetModById = _ => new NotFound(),
        };

        var result = await CreateService(api).CheckAsync(SptVersion);

        Assert.Equal(CheckModsUpdateStatus.Unavailable, result.Status);
    }

    [Fact]
    public async Task CheckAsync_reports_unavailable_when_the_mod_has_no_versions()
    {
        var api = new FakeForgeApiService
        {
            OnGetModUpdates = () => new NotFound(),
            OnGetModById = _ => ModWithVersions(detailUrl: "https://detail"),
        };

        var result = await CreateService(api).CheckAsync(SptVersion);

        Assert.Equal(CheckModsUpdateStatus.Unavailable, result.Status);
    }
}
