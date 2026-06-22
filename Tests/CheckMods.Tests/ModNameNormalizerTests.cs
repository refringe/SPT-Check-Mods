using CheckMods.Utils;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="ModNameNormalizer"/>, which underpins name-based mod matching.
/// </summary>
public sealed class ModNameNormalizerTests
{
    [Theory]
    [InlineData("My-Mod_Name", "mymodname")]
    [InlineData("My Mod.Name", "mymodname")]
    [InlineData("SVM", "svm")]
    public void Normalize_strips_separators_and_lowercases(string input, string expected)
    {
        Assert.Equal(expected, ModNameNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("MyModServer", "mymod")]
    [InlineData("MyModClient", "mymod")]
    public void Normalize_removes_component_suffix_when_requested(string input, string expected)
    {
        Assert.Equal(expected, ModNameNormalizer.Normalize(input, removeComponentSuffixes: true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Normalize_returns_empty_for_missing_input(string? input)
    {
        Assert.Equal(string.Empty, ModNameNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("com.author.modname", "modname")]
    [InlineData("com.author.mod-name", "name")]
    [InlineData("", "")]
    public void ExtractNameFromGuid_returns_last_segment(string guid, string expected)
    {
        Assert.Equal(expected, ModNameNormalizer.ExtractNameFromGuid(guid));
    }

    [Fact]
    public void IsExactMatch_matches_after_normalization()
    {
        Assert.True(ModNameNormalizer.IsExactMatch("My-Mod", "my mod"));
        Assert.False(ModNameNormalizer.IsExactMatch("ModA", "ModB"));
    }

    [Fact]
    public void IsExactMatch_with_suffix_removal_matches_server_and_client()
    {
        Assert.True(ModNameNormalizer.IsExactMatch("CoolModServer", "CoolModClient", removeComponentSuffixes: true));
    }

    [Fact]
    public void GetFuzzyMatchScore_is_100_for_identical_normalized_names()
    {
        Assert.Equal(100, ModNameNormalizer.GetFuzzyMatchScore("My Mod", "my-mod"));
    }
}
