using System.Text.Json;
using System.Text.Json.Serialization;
using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SPTarkov.DI.Annotations;

namespace CheckMods.Services;

/// <summary>
/// File-backed <see cref="IIgnoredUpdateStore"/>. Stores the ignored-updates list as JSON under the app-data folder and
/// caches it in memory for the lifetime of the run.
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed class IgnoredUpdateStore(IOptions<IgnoredUpdateOptions> options, ILogger<IgnoredUpdateStore> logger)
    : IIgnoredUpdateStore
{
    private readonly IgnoredUpdateOptions _options = options.Value;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    private List<IgnoredUpdate>? _cache;
    private HashSet<string> _keys = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public IReadOnlyList<IgnoredUpdate> Load()
    {
        if (_cache is not null)
        {
            return _cache;
        }

        _cache = ReadFromDisk();
        RebuildKeys();
        return _cache;
    }

    /// <inheritdoc />
    public bool IsIgnored(Mod mod)
    {
        if (!mod.ApiModId.HasValue || mod.LatestVersion is null)
        {
            return false;
        }

        return IsIgnored(mod.ApiModId.Value, mod.LocalVersion, mod.LatestVersion);
    }

    /// <summary>Value-based overload of <see cref="IsIgnored(Mod)"/>.</summary>
    internal bool IsIgnored(int apiModId, string localVersion, string latestVersion)
    {
        Load();
        return _keys.Contains(MakeKey(apiModId, localVersion, latestVersion));
    }

    /// <inheritdoc />
    public void Save(IReadOnlyList<IgnoredUpdate> entries)
    {
        var list = entries.ToList();
        WriteToDisk(list);
        _cache = list;
        RebuildKeys();
    }

    /// <inheritdoc />
    public int MergeWithoutOverwrite(IReadOnlyList<IgnoredUpdate> incoming)
    {
        var current = Load().ToList();
        var keys = current.Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var entry in incoming)
        {
            // Skip entries already present by key.
            if (!keys.Add(entry.Key))
            {
                continue;
            }

            current.Add(
                entry with
                {
                    Source = IgnoreSource.Remote,
                    DismissedUtc = entry.DismissedUtc ?? DateTimeOffset.UtcNow,
                }
            );
            added++;
        }

        if (added > 0)
        {
            Save(current);
        }

        return added;
    }

    private List<IgnoredUpdate> ReadFromDisk()
    {
        try
        {
            if (!File.Exists(_options.FilePath))
            {
                return [];
            }

            var json = File.ReadAllText(_options.FilePath);
            var file = JsonSerializer.Deserialize<IgnoredUpdatesFile>(json, _jsonOptions);
            if (file?.Ignored is null)
            {
                return [];
            }

            // Keep only well-formed entries.
            return file.Ignored.Where(e => e.IsWellFormed).ToList();
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not read ignored-updates file at {Path}; treating it as empty",
                _options.FilePath
            );
            return [];
        }
    }

    private void WriteToDisk(List<IgnoredUpdate> entries)
    {
        try
        {
            var directory = Path.GetDirectoryName(_options.FilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var file = new IgnoredUpdatesFile(IgnoredUpdatesFile.CurrentSchemaVersion, entries);
            var json = JsonSerializer.Serialize(file, _jsonOptions);

            // Atomic write: stage to a temp file then move into place.
            var tempPath = _options.FilePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _options.FilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not write ignored-updates file at {Path}", _options.FilePath);
        }
    }

    private void RebuildKeys()
    {
        _keys = (_cache ?? []).Select(e => e.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static string MakeKey(int apiModId, string localVersion, string latestVersion)
    {
        return $"{apiModId}|{localVersion}|{latestVersion}";
    }
}
