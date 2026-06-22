using CheckMods.Models;
using CheckMods.Services;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="IgnoredUpdateWorkflow.BuildNewSet"/>: the rewrite logic that preserves ignores for mods not
/// evaluated this run while overwriting the decisions the user could actually see.
/// </summary>
public sealed class IgnoredUpdateWorkflowTests
{
    private static IgnoredUpdate Entry(
        int id,
        string local = "1.0.0",
        string latest = "1.0.1",
        IgnoreSource source = IgnoreSource.User
    )
    {
        return new IgnoredUpdate(id, local, latest, Name: $"Mod {id}", Source: source);
    }

    [Fact]
    public void BuildNewSet_preserves_entries_for_mods_not_evaluated_this_run()
    {
        var existing = new List<IgnoredUpdate> { Entry(1), Entry(2) };
        var evaluated = new HashSet<int> { 1 }; // only mod 1 was installed/evaluated this run

        var result = IgnoredUpdateWorkflow.BuildNewSet(existing, evaluated, []);

        // Mod 2 wasn't evaluated, so its entry survives even though it wasn't re-selected.
        Assert.Single(result);
        Assert.Equal(2, result[0].ApiModId);
    }

    [Fact]
    public void BuildNewSet_drops_evaluated_but_unselected_entries()
    {
        var existing = new List<IgnoredUpdate> { Entry(1) };
        var evaluated = new HashSet<int> { 1 };

        // Mod 1 was evaluated and shown, but the user un-checked it -> removed (undo / self-resolved).
        var result = IgnoredUpdateWorkflow.BuildNewSet(existing, evaluated, []);

        Assert.Empty(result);
    }

    [Fact]
    public void BuildNewSet_adds_selected_entries()
    {
        var result = IgnoredUpdateWorkflow.BuildNewSet([], new HashSet<int> { 5 }, [Entry(5, latest: "2.0.0")]);

        Assert.Single(result);
        Assert.Equal(5, result[0].ApiModId);
        Assert.Equal("2.0.0", result[0].IgnoredLatestVersion);
    }

    [Fact]
    public void BuildNewSet_keeps_reselected_entry_without_duplicating()
    {
        var existing = new List<IgnoredUpdate> { Entry(1) };
        var evaluated = new HashSet<int> { 1 };

        // User kept mod 1 checked: it arrives via "selected" and shouldn't duplicate.
        var result = IgnoredUpdateWorkflow.BuildNewSet(existing, evaluated, [Entry(1)]);

        Assert.Single(result);
        Assert.Equal(1, result[0].ApiModId);
    }
}
