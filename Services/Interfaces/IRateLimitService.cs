namespace CheckMods.Services.Interfaces;

/// <summary>
/// Interface for rate-limiting API calls with reactive backoff. Executes requests immediately until rate limited,
/// then applies exponential backoff with retry logic.
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Executes an HTTP request with automatic retry and backoff on rate limiting (429 responses).
    /// Requests are sent immediately until a 429 is received, then backoff is applied globally.
    /// </summary>
    /// <param name="requestFunc">Function that executes the HTTP request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The HTTP response from a successful request.</returns>
    /// <exception cref="HttpRequestException">Thrown when max retries are exceeded.</exception>
    Task<HttpResponseMessage> ExecuteWithRetryAsync(
        Func<Task<HttpResponseMessage>> requestFunc,
        CancellationToken cancellationToken = default
    );
}
