using CheckMods.Models;

namespace CheckMods.Services.Interfaces;

/// <summary>
/// Fetches the optional author-maintained remote base ignore list over HTTP.
/// </summary>
public interface IRemoteIgnoreFileClient
{
    /// <summary>Whether a remote URL is configured. When false, no fetch should be attempted or offered.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Fetches and parses the remote list, returning null on any failure.
    /// </summary>
    Task<IReadOnlyList<IgnoredUpdate>?> FetchAsync(CancellationToken cancellationToken = default);
}
