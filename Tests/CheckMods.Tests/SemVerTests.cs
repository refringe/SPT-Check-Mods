using CheckMods.Utils;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="SemVer"/>, the central version-parsing helper used in place of exception-driven parsing.
/// </summary>
public sealed class SemVerTests
{
    [Theory]
    [InlineData("1.2.3")]
    [InlineData("0.0.1")]
    [InlineData("1.0.0-beta.1")]
    public void TryParse_returns_version_for_valid_input(string input)
    {
        Assert.NotNull(SemVer.TryParse(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-version")]
    [InlineData("abc")]
    public void TryParse_returns_null_for_missing_or_invalid_input(string? input)
    {
        Assert.Null(SemVer.TryParse(input));
    }

    [Fact]
    public void ParseOrZero_parses_valid_input()
    {
        Assert.Equal(new SemanticVersioning.Version("1.2.3"), SemVer.ParseOrZero("1.2.3"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("garbage")]
    public void ParseOrZero_falls_back_to_zero(string? input)
    {
        Assert.Equal(new SemanticVersioning.Version(0, 0, 0), SemVer.ParseOrZero(input));
    }

    [Theory]
    [InlineData(">=1.0.0", "1.2.3", true)]
    [InlineData("~1.2.0", "1.2.5", true)]
    [InlineData("~1.2.0", "1.3.0", false)]
    [InlineData(">=2.0.0", "1.9.9", false)]
    public void SatisfiesRange_evaluates_constraint(string constraint, string version, bool expected)
    {
        Assert.Equal(expected, SemVer.SatisfiesRange(constraint, new SemanticVersioning.Version(version)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void SatisfiesRange_returns_false_for_missing_constraint(string? constraint)
    {
        Assert.False(SemVer.SatisfiesRange(constraint, new SemanticVersioning.Version("1.0.0")));
    }
}
