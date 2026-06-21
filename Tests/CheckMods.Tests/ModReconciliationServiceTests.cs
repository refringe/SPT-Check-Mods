using CheckMods.Models;
using CheckMods.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="ModReconciliationService"/>, which pairs server/client mod components and selects the best
/// version. Pure logic with no console or I/O dependency.
/// </summary>
public sealed class ModReconciliationServiceTests
{
    private static ModReconciliationService CreateService()
    {
        return new ModReconciliationService(NullLogger<ModReconciliationService>.Instance);
    }

    private static Mod ServerMod(string guid, string name, string version)
    {
        return new Mod
        {
            Guid = guid,
            FilePath = $"server/{name}.dll",
            IsServerMod = true,
            LocalName = name,
            LocalAuthor = "Author",
            LocalVersion = version,
        };
    }

    private static Mod ClientMod(string guid, string name, string version)
    {
        return new Mod
        {
            Guid = guid,
            FilePath = $"client/{name}.dll",
            IsServerMod = false,
            LocalName = name,
            LocalAuthor = "Author",
            LocalVersion = version,
        };
    }

    [Fact]
    public void Pairs_server_and_client_by_matching_guid()
    {
        var service = CreateService();

        var result = service.ReconcileMods(
            [ServerMod("com.author.mod", "Mod", "1.0.0")],
            [ClientMod("com.author.mod", "Mod", "1.0.0")]
        );

        Assert.Single(result.ReconciledPairs);
        Assert.Empty(result.UnmatchedServerMods);
        Assert.Empty(result.UnmatchedClientMods);
        Assert.Single(result.Mods);
    }

    [Fact]
    public void Selects_higher_version_when_components_differ()
    {
        var service = CreateService();

        var result = service.ReconcileMods(
            [ServerMod("com.author.mod", "Mod", "1.0.0")],
            [ClientMod("com.author.mod", "Mod", "1.2.0")]
        );

        var pair = Assert.Single(result.ReconciledPairs);
        Assert.Equal("1.2.0", pair.SelectedMod.LocalVersion);
        Assert.Contains(pair.Notes, n => n.Contains("Version mismatch"));
    }

    [Fact]
    public void Leaves_unrelated_mods_unmatched()
    {
        var service = CreateService();

        var result = service.ReconcileMods(
            [ServerMod("com.author.alpha", "Alpha", "1.0.0")],
            [ClientMod("com.other.beta", "Beta", "1.0.0")]
        );

        Assert.Empty(result.ReconciledPairs);
        Assert.Single(result.UnmatchedServerMods);
        Assert.Single(result.UnmatchedClientMods);
        Assert.Equal(2, result.Mods.Count);
    }

    [Fact]
    public void Matches_by_normalized_name_when_guids_differ_and_notes_the_mismatch()
    {
        var service = CreateService();

        var result = service.ReconcileMods(
            [ServerMod("com.author.coolmod", "Cool Mod", "1.0.0")],
            [ClientMod("net.other.coolmod", "Cool-Mod", "1.0.0")]
        );

        var pair = Assert.Single(result.ReconciledPairs);
        Assert.Contains(pair.Notes, n => n.Contains("GUID mismatch"));
    }
}
