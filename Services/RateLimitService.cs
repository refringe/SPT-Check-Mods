using System.Threading.RateLimiting;
using CheckMods.Services.Interfaces;

namespace CheckMods.Services;

/// <summary>
/// Service that provides rate limiting for API calls using a token bucket algorithm. Configured to allow 2 requests per
/// second with a burst capacity of 4 tokens.
/// </summary>
public class RateLimitService : IRateLimitService, IDisposable
{
    /// <summary>
    /// Token bucket rate limiter configured for API throttling.
    /// - Token limit: 4 (burst capacity)
    /// - Tokens per period: 2 (sustained rate)
    /// - Replenishment period: 1 second
    /// - Queue limit: 100 pending requests
    /// </summary>
    private readonly RateLimiter _rateLimiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
    {
        TokenLimit = 4,
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = 100,
        ReplenishmentPeriod = TimeSpan.FromSeconds(1),
        TokensPerPeriod = 2,
        AutoReplenishment = true
    });

    /// <summary>
    /// Waits for permission to make an API call according to the rate-limiting policy. This method will block until a
    /// token is available or the operation is canceled.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the wait operation.</param>
    /// <returns>A task that completes when permission is granted to make the API call.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the rate limit is exceeded and no token can be acquired.</exception>
    public async Task WaitForApiCallAsync(CancellationToken cancellationToken = default)
    {
        using var lease = await _rateLimiter.AcquireAsync(1, cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException("Rate limit exceeded");
        }
    }

    /// <summary>
    /// Disposes the rate limiter resources.
    /// </summary>
    public void Dispose()
    {
        _rateLimiter.Dispose();
    }
}