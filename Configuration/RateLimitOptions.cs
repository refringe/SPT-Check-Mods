namespace CheckMods.Configuration;

/// <summary>
/// Configuration options for the rate limiting service.
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// Maximum number of requests that can be dispatched back-to-back before steady-state pacing applies.
    /// </summary>
    public int MaxBurst { get; set; } = 5;

    /// <summary>
    /// Number of request permits replenished each RefillPeriodMs.
    /// </summary>
    public int RefillTokensPerPeriod { get; set; } = 1;

    /// <summary>
    /// How often, in milliseconds, request permits are replenished.
    /// </summary>
    public int RefillPeriodMs { get; set; } = 333;

    /// <summary>
    /// Maximum number of simultaneous in-flight requests.
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
    /// Maximum delay in milliseconds for exponential backoff.
    /// </summary>
    public int MaxDelayMs { get; set; } = 60000;

    /// <summary>
    /// Per-request timeout in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;
}
