using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Checks whether a newer version of Check Mods is available on the Forge.
/// </summary>
public interface IUpdateCheckService
{
    /// <summary>
    /// Checks the Forge for a newer version of Check Mods, relative to the running build.
    /// </summary>
    /// <param name="sptVersion">The installed SPT version, used for compatibility filtering by the Forge API.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The update status; API errors resolve to <see cref="CheckModsUpdateStatus.Unavailable"/>.</returns>
    Task<CheckModsUpdateResult> CheckAsync(
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    );
}
