using CheckMods.Utils;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="ForgeUrls"/>, which builds links to the Forge website. These links are shown to users and
/// clicked, so the exact format matters.
/// </summary>
public sealed class ForgeUrlsTests
{
    [Fact]
    public void ModPage_builds_detail_url()
    {
        Assert.Equal("https://forge.sp-tarkov.com/mod/123/cool-mod", ForgeUrls.ModPage(123, "cool-mod"));
    }

    [Fact]
    public void Download_builds_versioned_download_url()
    {
        Assert.Equal(
            "https://forge.sp-tarkov.com/mod/download/123/cool-mod/1.2.0",
            ForgeUrls.Download(123, "cool-mod", "1.2.0")
        );
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ModPage_with_missing_slug_yields_a_trailing_slash_url(string? slug)
    {
        // Documents that the helper itself does not guard a missing slug - it produces a malformed trailing-slash
        // URL. Callers are responsible for only building a link when a slug is present (see the dependency/conflict
        // link sites in SpectreModCheckReporter).
        Assert.Equal("https://forge.sp-tarkov.com/mod/123/", ForgeUrls.ModPage(123, slug));
    }
}
