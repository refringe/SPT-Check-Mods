namespace CheckMods.Configuration;

/// <summary>
/// Configuration options for the rate limiting service.
/// </summary>
/// <remarks>
/// The Forge API enforces two server-side limits: a burst limit of 40 requests per 10 seconds and a general limit of
/// 200 requests per 60 seconds. Exceeding either triggers a temporary block (30s and 60s respectively). The client
/// paces itself just under both limits so it rarely, if ever, trips them.
/// </remarks>
public class RateLimitOptions
{
    /// <summary>
    /// The configuration section name for binding from appsettings.
    /// </summary>
    public const string SectionName = "RateLimit";

    /// <summary>
    /// Maximum requests allowed within the burst window. Kept below the server's burst limit (40 / 10s) for margin.
    /// </summary>
    public int BurstLimit { get; set; } = 35;

    /// <summary>
    /// Length of the burst window in seconds. Mirrors the server's 10-second burst window.
    /// </summary>
    public int BurstWindowSeconds { get; set; } = 10;

    /// <summary>
    /// Maximum requests allowed within the general window. Kept below the server's general limit (200 / 60s) for margin.
    /// </summary>
    public int GeneralLimit { get; set; } = 180;

    /// <summary>
    /// Length of the general window in seconds. Mirrors the server's 60-second general window.
    /// </summary>
    public int GeneralWindowSeconds { get; set; } = 60;

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
    /// Maximum delay in milliseconds for exponential backoff. Large enough to wait out a 60-second general block.
    /// </summary>
    public int MaxDelayMs { get; set; } = 60000;

    /// <summary>
    /// Per-request timeout in seconds. Also configures the underlying HttpClient timeout so hung requests become
    /// retryable rather than hanging indefinitely.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;
}
