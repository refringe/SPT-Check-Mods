namespace CheckMods.Configuration;

/// <summary>
/// Configuration options for the rate limiting service.
/// </summary>
public class RateLimitOptions
{
    /// <summary>
    /// The configuration section name for binding from appsettings.
    /// </summary>
    public const string SectionName = "RateLimit";

    /// <summary>
    /// Maximum number of retry attempts before giving up.
    /// </summary>
    public int MaxRetries { get; set; } = 5;

    /// <summary>
    /// Base delay in milliseconds for exponential backoff.
    /// </summary>
    public int BaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay in milliseconds for exponential backoff.
    /// </summary>
    public int MaxDelayMs { get; set; } = 30000;

    /// <summary>
    /// Minimum interval between requests in milliseconds (e.g., 250ms = 4 requests/second max).
    /// </summary>
    public int MinRequestIntervalMs { get; set; } = 250;
}
