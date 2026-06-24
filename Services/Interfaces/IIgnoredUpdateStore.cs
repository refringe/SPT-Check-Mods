using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Reads and writes the local ignored-updates list and answers whether a given mod's update is currently ignored.
/// </summary>
public interface IIgnoredUpdateStore
{
    /// <summary>Loads the current entries (cached after first read). Returns empty on a missing or unreadable file.</summary>
    IReadOnlyList<IgnoredUpdate> Load();

    /// <summary>Whether this mod's available update has been dismissed (matched on API id + both versions).</summary>
    bool IsIgnored(Mod mod);

    /// <summary>Replaces the stored entries with <paramref name="entries"/>.</summary>
    void Save(IReadOnlyList<IgnoredUpdate> entries);

    /// <summary>
    /// Merges entries from <paramref name="incoming"/> that are not already present, tagging added entries as <see cref="IgnoreSource.Remote"/>; returns the count added.
    /// </summary>
    int MergeWithoutOverwrite(IReadOnlyList<IgnoredUpdate> incoming);
}
