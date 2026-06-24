using CheckMods.Models;
using CheckMods.Utils;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="IgnoreReportUrl"/>: pre-filling the GitHub issue-form field with the entries, and dropping the
/// pre-fill (returning the bare form URL) when the entries are too large to embed.
/// </summary>
public sealed class IgnoreReportUrlTests
{
    private const string BareUrl =
        "https://github.com/refringe/SPT-Check-Mods/issues/new?template=report-ignored-updates.yml";

    private static IgnoredUpdate Entry(int id, string name, string local, string latest)
    {
        return new IgnoredUpdate(id, local, latest, Name: name, Guid: $"com.test.{id}");
    }

    [Fact]
    public void Build_prefills_url_with_entries()
    {
        var url = IgnoreReportUrl.Build([Entry(1234, "Some Mod", "1.2.3", "1.2.4")], out var prefilled);

        Assert.True(prefilled);
        Assert.StartsWith($"{BareUrl}&ignored_versions=", url);

        // The pre-filled field round-trips back to the entry data, wrapped in a fenced JSON block.
        var field = Uri.UnescapeDataString(url.Split("ignored_versions=")[1]);
        Assert.Contains("```json", field);
        Assert.Contains("1234", field);
        Assert.Contains("1.2.3", field);
        Assert.Contains("1.2.4", field);
    }

    [Fact]
    public void Build_drops_prefill_when_too_large()
    {
        var entries = Enumerable.Range(1, 400).Select(i => Entry(i, new string('x', 60), "1.0.0", "1.0.1")).ToList();

        var url = IgnoreReportUrl.Build(entries, out var prefilled);

        Assert.False(prefilled);
        Assert.Equal(BareUrl, url);
    }
}
