using System.Text.Json;
using System.Text.Json.Serialization;
using CheckMods.Models;

namespace CheckMods.Utils;

/// <summary>
/// Builds the GitHub "new issue" URL for reporting ignored-update suggestions. Pre-fills the ignore-suggestion issue
/// form's <c>ignored_versions</c> field with the entries as a fenced JSON block, so the maintainer can paste them
/// straight into the remote list.
/// </summary>
internal static class IgnoreReportUrl
{
    /// <summary>Base "new issue" URL targeting the ignore-suggestion issue form.</summary>
    private const string BaseUrl =
        "https://github.com/refringe/SPT-Check-Mods/issues/new?template=report-ignored-updates.yml";

    /// <summary>Issue-form field id that receives the pre-filled JSON (must match the template's field id).</summary>
    private const string FieldId = "ignored_versions";

    /// <summary>
    /// Maximum total URL length. GitHub rejects very long URLs (~8 KB), so beyond this we drop the pre-fill and return
    /// the bare form URL, signalling the caller to ask the user to paste their list manually.
    /// </summary>
    private const int MaxUrlLength = 7000;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Builds the report URL for the given entries. <paramref name="prefilled"/> is false when the entries were too
    /// large to embed and the returned URL is the bare form (no data).
    /// </summary>
    /// <param name="entries">The ignored-update entries to report.</param>
    /// <param name="prefilled">True when the returned URL carries the pre-filled entries.</param>
    /// <returns>The GitHub new-issue URL.</returns>
    public static string Build(IReadOnlyList<IgnoredUpdate> entries, out bool prefilled)
    {
        var json = JsonSerializer.Serialize(entries.Select(ToReportEntry).ToList(), _jsonOptions);

        // Wrap in a fenced code block so the field renders as JSON in the submitted issue.
        var field = $"```json\n{json}\n```";
        var url = $"{BaseUrl}&{FieldId}={Uri.EscapeDataString(field)}";

        if (url.Length <= MaxUrlLength)
        {
            prefilled = true;
            return url;
        }

        prefilled = false;
        return BaseUrl;
    }

    /// <summary>Projects an entry to the minimal shape the maintainer pastes into the remote list.</summary>
    private static ReportEntry ToReportEntry(IgnoredUpdate entry)
    {
        return new ReportEntry(entry.ApiModId, entry.Name, entry.Guid, entry.LocalVersion, entry.IgnoredLatestVersion);
    }

    private sealed record ReportEntry(
        [property: JsonPropertyName("apiModId")] int ApiModId,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("guid")] string? Guid,
        [property: JsonPropertyName("localVersion")] string LocalVersion,
        [property: JsonPropertyName("ignoredLatestVersion")] string IgnoredLatestVersion
    );
}
