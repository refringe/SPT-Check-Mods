using CheckMods.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="BrowserLauncher"/>'s URL guard. Only the reject path is exercised.
/// </summary>
public sealed class BrowserLauncherTests
{
    [Theory]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///C:/secret")]
    [InlineData("javascript:alert(1)")]
    [InlineData("not a url")]
    [InlineData("")]
    public void TryOpenUrl_refuses_non_http_urls(string url)
    {
        var launcher = new BrowserLauncher(NullLogger<BrowserLauncher>.Instance);

        Assert.False(launcher.TryOpenUrl(url));
    }
}
