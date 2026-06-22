using System.Text.Json.Serialization;

namespace CheckMods.Models;

/// <summary>
/// Where an <see cref="IgnoredUpdate"/> entry came from.
/// </summary>
public enum IgnoreSource
{
    /// <summary>The user dismissed this update locally.</summary>
    User,

    /// <summary>The entry was merged in from the author-maintained remote base list.</summary>
    Remote,
}

/// <summary>
/// A single dismissed ("ignored") mod update. Identifies a known false-positive update where the Forge version is
/// higher than the installed DLL version but the distributed files are actually the same, so the update prompt should
/// be suppressed. Matching uses the triple (<see cref="ApiModId"/>, <see cref="LocalVersion"/>,
/// <see cref="IgnoredLatestVersion"/>); the remaining fields are metadata for readability.
/// </summary>
public sealed record IgnoredUpdate(
    [property: JsonPropertyName("apiModId")] int ApiModId,
    [property: JsonPropertyName("localVersion")] string LocalVersion,
    [property: JsonPropertyName("ignoredLatestVersion")] string IgnoredLatestVersion,
    [property: JsonPropertyName("name")] string? Name = null,
    [property: JsonPropertyName("guid")] string? Guid = null,
    [property: JsonPropertyName("source")] IgnoreSource Source = IgnoreSource.User,
    [property: JsonPropertyName("dismissedUtc")] DateTimeOffset? DismissedUtc = null
)
{
    /// <summary>
    /// The match key: API mod id plus both version strings. Compared with <see cref="StringComparer.OrdinalIgnoreCase"/>
    /// (by callers) so version casing differences don't create duplicates.
    /// </summary>
    [JsonIgnore]
    public string Key
    {
        get { return $"{ApiModId}|{LocalVersion}|{IgnoredLatestVersion}"; }
    }

    /// <summary>
    /// Whether this entry carries the minimum data needed to match a mod. Guards against partially-written or
    /// hand-edited files (local and remote).
    /// </summary>
    [JsonIgnore]
    public bool IsWellFormed
    {
        get
        {
            return ApiModId > 0
                && !string.IsNullOrWhiteSpace(LocalVersion)
                && !string.IsNullOrWhiteSpace(IgnoredLatestVersion);
        }
    }
}

/// <summary>
/// The on-disk (and remote) document format for the ignored-updates list.
/// </summary>
public sealed record IgnoredUpdatesFile(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("ignored")] List<IgnoredUpdate> Ignored
)
{
    /// <summary>The schema version this build reads and writes.</summary>
    public const int CurrentSchemaVersion = 1;
}
