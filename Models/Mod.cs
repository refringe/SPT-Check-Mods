namespace CheckMods.Models;

/// <summary>
/// Master record representing a mod throughout the entire processing lifecycle.
/// Progressively enriched as data flows through scanning, reconciliation, API matching, and version checking.
/// </summary>
public sealed class Mod
{
    #region Identity (from scanning)

    /// <summary>
    /// The unique GUID identifier of the mod (e.g., "com.author.modname").
    /// </summary>
    public required string Guid { get; init; }

    /// <summary>
    /// The file system path to the mod's DLL.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Whether this is a server mod (true) or client/BepInEx mod (false).
    /// </summary>
    public required bool IsServerMod { get; init; }

    /// <summary>
    /// Path to the paired server/client component, if reconciled with another component.
    /// </summary>
    public string? PairedComponentPath { get; set; }

    /// <summary>
    /// Alternate GUIDs from other DLLs in the same mod package (for multi-DLL client mods).
    /// Used as fallbacks when the primary GUID doesn't match in the API.
    /// </summary>
    public List<string> AlternateGuids { get; init; } = [];

    #endregion

    #region Local Metadata (from scanning)

    /// <summary>
    /// The name extracted from the mod's DLL or manifest.
    /// </summary>
    public required string LocalName { get; init; }

    /// <summary>
    /// The author extracted from the mod's DLL or manifest.
    /// </summary>
    public required string LocalAuthor { get; init; }

    /// <summary>
    /// The version string extracted from the mod's DLL or manifest.
    /// </summary>
    public required string LocalVersion { get; init; }

    /// <summary>
    /// The SPT version constraint for this mod (server mods only, e.g., "~4.0.0").
    /// </summary>
    public string? LocalSptVersion { get; init; }

    #endregion

    #region API Metadata (populated when matched via API)

    /// <summary>
    /// The unique identifier of the mod from the Forge API.
    /// </summary>
    public int? ApiModId { get; private set; }

    /// <summary>
    /// The official name from the Forge API.
    /// </summary>
    public string? ApiName { get; private set; }

    /// <summary>
    /// The author information from the Forge API.
    /// </summary>
    public ModAuthor? ApiAuthor { get; private set; }

    /// <summary>
    /// The URL slug for the mod on Forge.
    /// </summary>
    public string? ApiSlug { get; private set; }

    /// <summary>
    /// The URL to the mod's detail page on Forge.
    /// </summary>
    public string? ApiUrl { get; private set; }

    /// <summary>
    /// A short description or teaser for the mod.
    /// </summary>
    public string? ApiTeaser { get; private set; }

    /// <summary>
    /// URL to the mod's thumbnail image.
    /// </summary>
    public string? ApiThumbnail { get; private set; }

    /// <summary>
    /// Total download count for the mod.
    /// </summary>
    public int? ApiDownloads { get; private set; }

    /// <summary>
    /// URL to the mod's source code repository.
    /// </summary>
    public string? ApiSourceCodeUrl { get; private set; }

    /// <summary>
    /// All available versions of this mod from the Forge API.
    /// </summary>
    public IReadOnlyList<ModVersion>? ApiVersions { get; private set; }

    #endregion

    #region Version Information (populated during version enrichment)

    /// <summary>
    /// The latest SPT-compatible version available on Forge.
    /// </summary>
    public string? LatestVersion { get; private set; }

    /// <summary>
    /// When the latest version was published.
    /// </summary>
    public DateTime? LatestVersionDate { get; private set; }

    /// <summary>
    /// The update status compared to the latest available version.
    /// </summary>
    public UpdateStatus UpdateStatus { get; private set; } = UpdateStatus.Unknown;

    /// <summary>
    /// Download link for the latest version (when update is available).
    /// </summary>
    public string? DownloadLink { get; private set; }

    /// <summary>
    /// List of mods blocking an update due to dependency constraints.
    /// </summary>
    public IReadOnlyList<BlockingModInfo>? BlockingMods { get; private set; }

    /// <summary>
    /// Reason the mod is incompatible with the current SPT version.
    /// </summary>
    public string? IncompatibilityReason { get; private set; }

    /// <summary>
    /// Whether the locally declared SPT version constraint is incompatible with the installed SPT version.
    /// </summary>
    public bool IsLocalSptIncompatible { get; private set; }

    /// <summary>
    /// Download link for a compatible version when the current install is SPT-incompatible.
    /// </summary>
    public string? CompatibleVersionDownloadLink { get; private set; }

    /// <summary>
    /// The compatible version string when a compatible version exists.
    /// </summary>
    public string? CompatibleVersionString { get; private set; }

    #endregion

    #region Processing State

    /// <summary>
    /// The current processing/verification status of the mod.
    /// </summary>
    public ModStatus Status { get; set; } = ModStatus.NoMatch;

    /// <summary>
    /// The confidence score (0-100) of the API match.
    /// </summary>
    public int MatchConfidence { get; private set; }

    /// <summary>
    /// The method used to match this mod with the API.
    /// </summary>
    public MatchMethod MatchMethod { get; private set; } = MatchMethod.None;

    /// <summary>
    /// Whether the user has confirmed a low-confidence match.
    /// </summary>
    public bool IsConfirmed { get; set; }

    /// <summary>
    /// Warnings encountered during scanning/loading.
    /// </summary>
    public List<string> LoadWarnings { get; init; } = [];

    #endregion

    #region Display Properties (computed)

    /// <summary>
    /// The preferred display name (API name if available, otherwise local name).
    /// </summary>
    public string DisplayName
    {
        get { return ApiName ?? LocalName; }
    }

    /// <summary>
    /// The preferred display author (API author if available, otherwise local author).
    /// </summary>
    public string DisplayAuthor
    {
        get { return ApiAuthor?.Name ?? LocalAuthor; }
    }

    /// <summary>
    /// The installed version for display purposes.
    /// </summary>
    public string DisplayVersion
    {
        get { return LocalVersion; }
    }

    /// <summary>
    /// Whether the mod has any loading warnings.
    /// </summary>
    public bool HasWarnings
    {
        get { return LoadWarnings.Count > 0; }
    }

    /// <summary>
    /// Whether the mod has been successfully matched with the API.
    /// </summary>
    public bool IsMatched
    {
        get { return Status == ModStatus.Verified && ApiModId.HasValue; }
    }

    #endregion

    #region Update Methods

    /// <summary>
    /// Updates the mod with API match data from a search result.
    /// </summary>
    /// <param name="apiResult">The API search result to populate from.</param>
    /// <param name="confidence">The confidence score of the match (0-100).</param>
    /// <param name="method">The method used to find this match.</param>
    public void UpdateFromApiMatch(ModSearchResult apiResult, int confidence, MatchMethod method)
    {
        ApiModId = apiResult.Id;
        ApiName = apiResult.Name;
        ApiAuthor = apiResult.Owner;
        ApiSlug = apiResult.Slug;
        ApiUrl = apiResult.DetailUrl;
        ApiTeaser = apiResult.Teaser;
        ApiThumbnail = apiResult.Thumbnail;
        ApiDownloads = apiResult.Downloads;
        ApiSourceCodeUrl = apiResult.SourceCodeUrl;
        ApiVersions = apiResult.Versions?.ToList().AsReadOnly();

        MatchConfidence = confidence;
        MatchMethod = method;
        Status = ModStatus.Verified;
        IsConfirmed = confidence >= 100;
    }

    /// <summary>
    /// Updates the mod with safe-to-update information from the batch updates endpoint.
    /// </summary>
    /// <param name="update">The update information from the API.</param>
    public void UpdateFromSafeToUpdate(SafeToUpdateMod update)
    {
        LatestVersion = update.RecommendedVersion?.Version;
        DownloadLink = update.RecommendedVersion?.Link;
        UpdateStatus = UpdateStatus.UpdateAvailable;
    }

    /// <summary>
    /// Updates the mod with blocked update information from the batch updates endpoint.
    /// </summary>
    /// <param name="blocked">The blocked update information from the API.</param>
    public void UpdateFromBlocked(BlockedUpdateMod blocked)
    {
        LatestVersion = blocked.LatestVersion?.Version;
        BlockingMods = blocked.BlockingMods;
        UpdateStatus = UpdateStatus.UpdateBlocked;
    }

    /// <summary>
    /// Updates the mod with up-to-date information from the batch updates endpoint.
    /// </summary>
    /// <param name="upToDate">The up-to-date information from the API.</param>
    public void UpdateFromUpToDate(UpToDateMod upToDate)
    {
        LatestVersion = upToDate.Version;
        UpdateStatus = UpdateStatus.UpToDate;
    }

    /// <summary>
    /// Updates the mod with incompatibility information from the batch updates endpoint.
    /// </summary>
    /// <param name="incompatible">The incompatibility information from the API.</param>
    public void UpdateFromIncompatible(IncompatibleMod incompatible)
    {
        IncompatibilityReason = incompatible.Reason;
        UpdateStatus = UpdateStatus.Incompatible;
    }

    /// <summary>
    /// Marks the mod as locally incompatible with the installed SPT version.
    /// </summary>
    /// <param name="reason">The reason for incompatibility.</param>
    /// <param name="compatibleVersion">The version string of a compatible version, if available.</param>
    /// <param name="downloadLink">Download link for the compatible version, if available.</param>
    public void SetLocalSptIncompatible(string reason, string? compatibleVersion = null, string? downloadLink = null)
    {
        IsLocalSptIncompatible = true;
        IncompatibilityReason = reason;
        CompatibleVersionString = compatibleVersion;
        CompatibleVersionDownloadLink = downloadLink;
    }

    /// <summary>
    /// Clears API match data (used when user rejects a low-confidence match).
    /// </summary>
    public void ClearApiMatch()
    {
        ApiModId = null;
        ApiName = null;
        ApiAuthor = null;
        ApiSlug = null;
        ApiUrl = null;
        ApiTeaser = null;
        ApiThumbnail = null;
        ApiDownloads = null;
        ApiSourceCodeUrl = null;
        ApiVersions = null;

        MatchConfidence = 0;
        MatchMethod = MatchMethod.None;
        Status = ModStatus.NoMatch;
        IsConfirmed = false;

        LatestVersion = null;
        LatestVersionDate = null;
        UpdateStatus = UpdateStatus.Unknown;
        DownloadLink = null;
        BlockingMods = null;
        IncompatibilityReason = null;
    }

    #endregion
}
