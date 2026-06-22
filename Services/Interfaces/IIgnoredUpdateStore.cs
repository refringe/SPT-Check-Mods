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
    /// Adds entries from <paramref name="incoming"/> whose key isn't already present, preserving existing entries
    /// verbatim. Added entries are tagged <see cref="IgnoreSource.Remote"/>. Returns the number added.
    /// </summary>
    int MergeWithoutOverwrite(IReadOnlyList<IgnoredUpdate> incoming);
}
