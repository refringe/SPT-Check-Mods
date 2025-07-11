namespace CheckMods.Models;

/// <summary>
/// Represents a mod record that combines local mod information with API match data. Used to track the verification
/// status and matching confidence of installed mods.
/// </summary>
public class ModRecord
{
    /// <summary>
    /// The name of the locally installed mod.
    /// </summary>
    public string LocalModName { get; set; } = string.Empty;

    /// <summary>
    /// The author of the locally installed mod.
    /// </summary>
    public string LocalModAuthor { get; set; } = string.Empty;

    /// <summary>
    /// The version of the locally installed mod.
    /// </summary>
    public string LocalModVersion { get; set; } = string.Empty;

    /// <summary>
    /// The file system path to the locally installed mod.
    /// </summary>
    public string LocalModPath { get; set; } = string.Empty;

    /// <summary>
    /// The unique identifier of the matched mod from the Forge API.
    /// </summary>
    public int? ApiModId { get; set; }

    /// <summary>
    /// The name of the matched mod from the Forge API.
    /// </summary>
    public string? ApiModName { get; set; }

    /// <summary>
    /// The author of the matched mod from the Forge API.
    /// </summary>
    public string? ApiModAuthor { get; set; }

    /// <summary>
    /// The latest available version of the matched mod from the Forge API.
    /// </summary>
    public string? ApiModLatestVersion { get; set; }

    /// <summary>
    /// The URL to the matched mod's detail page.
    /// </summary>
    public string? ApiModUrl { get; set; }

    /// <summary>
    /// The current verification status of the mod.
    /// </summary>
    public ModStatus Status { get; set; }

    /// <summary>
    /// Whether the user has confirmed the API match.
    /// </summary>
    public bool IsConfirmed { get; set; }

    /// <summary>
    /// The confidence score (0-100) of the API match based on fuzzy matching.
    /// </summary>
    public int ConfidenceScore { get; set; }

    /// <summary>
    /// The SPT version requirement for this mod.
    /// </summary>
    public string? SptVersionRequirement { get; set; }

    /// <summary>
    /// Private parameterless constructor for object initialization.
    /// </summary>
    private ModRecord()
    {
        //
    }

    /// <summary>
    /// Creates a new ModRecord from a local mod package.
    /// </summary>
    /// <param name="localMod">The local mod package information.</param>
    /// <param name="status">The initial verification status.</param>
    public ModRecord(ModPackage localMod, ModStatus status)
        : this()
    {
        LocalModName = localMod.Name;
        LocalModAuthor = localMod.Author;
        LocalModVersion = localMod.Version;
        SptVersionRequirement = localMod.SptVersion;
        Status = status;
    }

    /// <summary>
    /// Updates the record with API match information.
    /// </summary>
    /// <param name="apiMatch">The matched mod from the Forge API.</param>
    /// <param name="confidenceScore">The confidence score of the match (0-100).</param>
    public void UpdateFromApiMatch(ModSearchResult apiMatch, int confidenceScore)
    {
        ApiModId = apiMatch.Id;
        ApiModName = apiMatch.Name;
        ApiModAuthor = apiMatch.Owner?.Name;
        ConfidenceScore = confidenceScore;
    }

    /// <summary>
    /// Updates the confirmation status of the API match.
    /// </summary>
    /// <param name="confirmed">Whether the user confirmed the match.</param>
    public void UpdateConfirmation(bool confirmed)
    {
        IsConfirmed = confirmed;
        if (confirmed)
        {
            Status = ModStatus.Verified;
        }
        else
        {
            Status = ModStatus.NoMatch;
            ApiModId = null;
            ApiModName = null;
            ApiModAuthor = null;
            ApiModLatestVersion = null;
            ApiModUrl = null;
        }
    }
}
