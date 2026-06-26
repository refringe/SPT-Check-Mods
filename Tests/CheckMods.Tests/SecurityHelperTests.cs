using CheckMods.Utils;

namespace CheckMods.Tests;

public sealed class SecurityHelperTests
{
    [Fact]
    public void IsWithinDirectory_accepts_directory_itself()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmods-base");

        Assert.True(SecurityHelper.IsWithinDirectory(directory, directory));
    }

    [Fact]
    public void IsWithinDirectory_accepts_child_path()
    {
        var directory = Path.Combine(Path.GetTempPath(), "checkmods-base");
        var child = Path.Combine(directory, "child", "mod.dll");

        Assert.True(SecurityHelper.IsWithinDirectory(child, directory));
    }

    [Fact]
    public void IsWithinDirectory_rejects_sibling_with_matching_prefix()
    {
        var root = Path.GetTempPath();
        var directory = Path.Combine(root, "checkmods-base");
        var sibling = Path.Combine(root, "checkmods-base-sibling", "mod.dll");

        Assert.False(SecurityHelper.IsWithinDirectory(sibling, directory));
    }

    [Fact]
    public void GetSafePath_rejects_traversal_outside_base_directory()
    {
        var root = Path.GetTempPath();
        var directory = Path.Combine(root, "checkmods-base");
        var traversal = Path.Combine(directory, "..", "checkmods-base-sibling", "mod.dll");

        Assert.Null(SecurityHelper.GetSafePath(traversal, directory));
    }

    [Fact]
    public void Path_comparison_is_case_sensitive_on_linux_and_case_insensitive_on_windows()
    {
        var root = Path.GetTempPath();
        var directory = Path.Combine(root, "CheckModsBase");
        var differentCaseChild = Path.Combine(root, "checkmodsbase", "mod.dll");

        var result = SecurityHelper.IsWithinDirectory(differentCaseChild, directory);

        if (OperatingSystem.IsWindows())
        {
            Assert.True(result);
        }
        else
        {
            Assert.False(result);
        }
    }
}
