using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="IgnoredUpdateStore"/>: file round-tripping, the match predicate (including that a genuinely
/// newer Forge version is NOT suppressed), corruption handling, and the non-overwriting merge.
/// </summary>
public sealed class IgnoredUpdateStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public IgnoredUpdateStoreTests()
    {
        _dir = TempWorkspace.CreateDirectory("checkmods-tests");
        _path = Path.Combine(_dir, "ignored-updates.json");
    }

    public void Dispose()
    {
        TempWorkspace.SafeDelete(_dir);
    }

    private IgnoredUpdateStore CreateStore()
    {
        return new IgnoredUpdateStore(
            Options.Create(new IgnoredUpdateOptions { FilePath = _path, RemoteUrl = null }),
            NullLogger<IgnoredUpdateStore>.Instance
        );
    }

    private static IgnoredUpdate Entry(int id, string local, string latest, IgnoreSource source = IgnoreSource.User)
    {
        return new IgnoredUpdate(id, local, latest, Name: $"Mod {id}", Source: source);
    }

    [Fact]
    public void Load_returns_empty_when_file_missing()
    {
        Assert.Empty(CreateStore().Load());
    }

    [Fact]
    public void Save_then_load_round_trips_entries()
    {
        CreateStore().Save([Entry(1, "1.0.0", "1.0.1"), Entry(2, "2.0.0", "2.1.0")]);

        // New store instance reads from disk.
        var reloaded = CreateStore().Load();

        Assert.Equal(2, reloaded.Count);
        Assert.Contains(reloaded, e => e.ApiModId == 1 && e.IgnoredLatestVersion == "1.0.1");
    }

    [Fact]
    public void IsIgnored_matches_on_id_and_both_versions()
    {
        var store = CreateStore();
        store.Save([Entry(1, "1.0.0", "1.0.1")]);

        Assert.True(store.IsIgnored(1, "1.0.0", "1.0.1"));
        // A genuinely newer Forge release (different latest) must NOT be suppressed.
        Assert.False(store.IsIgnored(1, "1.0.0", "1.0.2"));
        // A different mod id is unrelated.
        Assert.False(store.IsIgnored(99, "1.0.0", "1.0.1"));
    }

    [Fact]
    public void IsIgnored_is_case_insensitive_on_versions()
    {
        var store = CreateStore();
        store.Save([Entry(1, "1.0.0-BETA", "1.0.1-RC")]);

        Assert.True(store.IsIgnored(1, "1.0.0-beta", "1.0.1-rc"));
    }

    [Fact]
    public void Load_returns_empty_on_corrupt_file()
    {
        File.WriteAllText(_path, "{ this is not valid json ");

        Assert.Empty(CreateStore().Load());
    }

    [Fact]
    public void MergeWithoutOverwrite_adds_new_and_preserves_existing()
    {
        CreateStore().Save([Entry(1, "1.0.0", "1.0.1", IgnoreSource.User)]);

        var added = CreateStore()
            .MergeWithoutOverwrite([
                Entry(1, "1.0.0", "1.0.1", IgnoreSource.User), // duplicate key -> skipped
                Entry(2, "2.0.0", "2.1.0", IgnoreSource.User), // new -> added as Remote
            ]);

        Assert.Equal(1, added);

        var all = CreateStore().Load();
        Assert.Equal(2, all.Count);
        Assert.Equal(IgnoreSource.User, all.Single(e => e.ApiModId == 1).Source); // existing not overwritten
        Assert.Equal(IgnoreSource.Remote, all.Single(e => e.ApiModId == 2).Source); // addition tagged remote
    }

    [Fact]
    public void Save_does_not_leave_temp_file()
    {
        CreateStore().Save([Entry(1, "1.0.0", "1.0.1")]);

        Assert.False(File.Exists(_path + ".tmp"));
    }
}
