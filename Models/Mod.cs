namespace CheckMods.Models;

/// <summary>
/// Master record representing a mod throughout the entire processing lifecycle.
/// </summary>
public sealed class Mod
{
    #region Identity (from scanning)

    /// <summary>
    /// The mod's GUID (e.g., "com.author.modname").
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

    public int? ApiModId { get; private set; }

    public string? ApiName { get; private set; }

    public ModAuthor? ApiAuthor { get; private set; }

    public string? ApiSlug { get; private set; }

    /// <summary>
    /// The URL to the mod's detail page on Forge.
    /// </summary>
    public string? ApiUrl { get; private set; }

    public string? ApiSourceCodeUrl { get; private set; }

    public IReadOnlyList<ModVersion>? ApiVersions { get; private set; }

    #endregion

    #region Version Information (populated during version enrichment)

    /// <summary>
    /// The latest SPT-compatible version available on Forge.
    /// </summary>
    public string? LatestVersion { get; private set; }

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
    /// Reason the update is blocked by dependency constraints.
    /// </summary>
    public string? BlockReason { get; private set; }

    /// <summary>
    /// Reason the mod is incompatible with the current SPT version.
    /// </summary>
    public string? IncompatibilityReason { get; private set; }

    /// <summary>
    /// Whether the locally declared SPT version constraint is incompatible with the installed SPT version.
    /// </summary>
    public bool IsLocalSptIncompatible { get; private set; }

    /// <summary>
    /// The compatible version string when a compatible version exists.
    /// </summary>
    public string? CompatibleVersionString { get; private set; }

    /// <summary>
    /// Whether this mod's available update has been dismissed as a false positive and should be shown as ignored
    /// (treated as up to date).
    /// </summary>
    public bool UpdateSuppressed { get; private set; }

    /// <summary>
    /// How the proposed update changes this mod's dependencies compared to the installed version. Null when there is
    /// no available update or the comparison couldn't be made.
    /// </summary>
    public UpdateDependencyDelta? UpdateDependencyChanges { get; private set; }

    #endregion

    #region Processing State

    public ModStatus Status { get; private set; } = ModStatus.NoMatch;

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

    public bool HasWarnings
    {
        get { return LoadWarnings.Count > 0; }
    }

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
    public void UpdateFromApiMatch(ModSearchResult apiResult)
    {
        ApiModId = apiResult.Id;
        ApiName = apiResult.Name;
        ApiAuthor = apiResult.Owner;
        ApiSlug = apiResult.Slug;
        ApiUrl = apiResult.DetailUrl;
        ApiSourceCodeUrl = apiResult.SourceCodeUrl;
        ApiVersions = apiResult.Versions?.ToList().AsReadOnly();

        Status = ModStatus.Verified;
    }

    /// <summary>
    /// Marks the mod as having no Forge API match.
    /// </summary>
    public void MarkUnmatched()
    {
        Status = ModStatus.NoMatch;
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
        BlockReason = blocked.BlockReason;
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
    public void SetLocalSptIncompatible(string reason, string? compatibleVersion = null)
    {
        IsLocalSptIncompatible = true;
        IncompatibilityReason = reason;
        CompatibleVersionString = compatibleVersion;
    }

    /// <summary>
    /// Sets whether this mod's available update is suppressed (dismissed as a false positive).
    /// </summary>
    /// <param name="suppressed">True to treat the available update as ignored; false to show it normally.</param>
    public void SetUpdateSuppressed(bool suppressed)
    {
        UpdateSuppressed = suppressed;
    }

    /// <summary>
    /// Records how the proposed update changes this mod's dependencies compared to the installed version.
    /// </summary>
    /// <param name="delta">The added/removed dependency changes introduced by the update.</param>
    public void SetUpdateDependencyChanges(UpdateDependencyDelta delta)
    {
        UpdateDependencyChanges = delta;
    }

    #endregion
}
