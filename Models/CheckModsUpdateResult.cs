namespace CheckMods.Models;

/// <summary>
/// The outcome of a Check Mods self-update check.
/// </summary>
public enum CheckModsUpdateStatus
{
    /// <summary>The running version is the newest compatible release.</summary>
    UpToDate,

    /// <summary>A newer compatible release is available.</summary>
    UpdateAvailable,

    /// <summary>A newer release exists but isn't compatible with the installed SPT version.</summary>
    IncompatibleWithSpt,

    /// <summary>The running version wasn't recognized by the Forge (e.g. a self-compiled/dev build).</summary>
    UnrecognizedBuild,

    /// <summary>The check couldn't be completed (mod not listed, or an API error).</summary>
    Unavailable,
}

/// <summary>
/// Describes the result of a Check Mods self-update check.
/// </summary>
/// <param name="Status">The update status.</param>
/// <param name="CurrentVersion">The running application version.</param>
/// <param name="LatestVersion">The latest/recommended version, when known.</param>
/// <param name="DownloadLink">A link to download the latest version, when known.</param>
public sealed record CheckModsUpdateResult(
    CheckModsUpdateStatus Status,
    string CurrentVersion,
    string? LatestVersion = null,
    string? DownloadLink = null
);
