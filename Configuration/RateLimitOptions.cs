namespace CheckMods.Configuration;

/// <summary>
/// Configuration options for the rate limiting service.
/// </summary>
/// <remarks>
/// The Forge API enforces two server-side limits: a burst limit of 40 requests per 10 seconds and a general limit of
/// 200 requests per 60 seconds. Exceeding either triggers a temporary block (30s and 60s respectively). Rather than
/// race up to those ceilings and recover from the blocks, the client paces itself with a single token bucket that
/// smooths dispatch to a steady sustained rate with only a small burst, keeping it comfortably under both limits.
/// </remarks>
public class RateLimitOptions
{
    /// <summary>
    /// Maximum number of requests that can be dispatched back-to-back before steady-state pacing applies. Sized so a
    /// full burst plus the sustained refill stays under the server's burst window (40 / 10s).
    /// </summary>
    public int MaxBurst { get; set; } = 5;

    /// <summary>
    /// Number of request permits replenished each RefillPeriodMs. Sets the sustained request rate.
    /// </summary>
    public int RefillTokensPerPeriod { get; set; } = 1;

    /// <summary>
    /// How often, in milliseconds, request permits are replenished. The default of one token per 333ms yields ~3
    /// requests/second, which sits under both the general limit (200 / 60s = 3.3/s) and the burst limit
    /// (40 / 10s = 4/s). A small period keeps pacing smooth rather than releasing permits in clumps.
    /// </summary>
    public int RefillPeriodMs { get; set; } = 333;

    /// <summary>
    /// Maximum number of simultaneous in-flight requests. A safety ceiling that bounds concurrency if responses are
    /// slow; with smooth pacing in place it is rarely the binding constraint.
    /// </summary>
    public int MaxConcurrentRequests { get; set; } = 5;

    /// <summary>
    /// Maximum number of retry attempts before giving up.
    /// </summary>
    public int MaxRetries { get; set; } = 8;

    /// <summary>
    /// Base delay in milliseconds for exponential backoff.
    /// </summary>
    public int BaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay in milliseconds for exponential backoff. Large enough to wait out a 60-second general block.
    /// </summary>
    public int MaxDelayMs { get; set; } = 60000;

    /// <summary>
    /// Per-request timeout in seconds. Also configures the underlying HttpClient timeout so hung requests become
    /// retryable rather than hanging indefinitely.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;
}
