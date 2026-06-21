using CheckMods.Models;
using CheckMods.Services;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="UpdateCheckService.InterpretUpdates"/>, which maps a categorized mod-updates response to an
/// update result for Check Mods. The mod-id used mirrors the real Forge listing (2471).
/// </summary>
public sealed class UpdateCheckServiceTests
{
    private const int ModId = 2471;

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
}
