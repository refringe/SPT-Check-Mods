using CheckMods.Services;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="SpectreModCheckReporter"/>'s URL safety guard. A URL embedded in a Spectre [link=...] tag
/// must not contain the markup delimiters '[' or ']', or rendering throws and aborts the run.
/// </summary>
public sealed class SpectreModCheckReporterTests
{
    [Theory]
    [InlineData("https://forge.sp-tarkov.com/mod/123/cool-mod")]
    [InlineData("https://example.com/path?query=1&x=2")]
    public void IsLinkUrlSafe_accepts_normal_urls(string url)
    {
        Assert.True(SpectreModCheckReporter.IsLinkUrlSafe(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://example.com/a[b")]
    [InlineData("https://example.com/a]b")]
    [InlineData("[red]not a url[/]")]
    public void IsLinkUrlSafe_rejects_empty_or_markup_breaking_urls(string? url)
    {
        Assert.False(SpectreModCheckReporter.IsLinkUrlSafe(url));
    }
}
