namespace CheckMods.Services.Interfaces;

/// <summary>
/// Interface for rate-limiting API calls to prevent throttling and ensure compliance with API limits.
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Waits for permission to make an API call according to the rate-limiting policy.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the wait operation.</param>
    /// <returns>A task that completes when permission is granted to make the API call.</returns>
    Task WaitForApiCallAsync(CancellationToken cancellationToken = default);
}
