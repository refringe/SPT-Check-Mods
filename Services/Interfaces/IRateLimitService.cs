namespace CheckMods.Services.Interfaces;

/// <summary>
/// Rate-limits and retries API calls, applying backoff that honors the Retry-After header.
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Executes an HTTP request with rate limiting and retry on transient failures.
    /// </summary>
    /// <param name="requestFunc">Function that executes the HTTP request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The HTTP response from a successful or non-retryable request.</returns>
    /// <exception cref="HttpRequestException">Thrown when retries are exhausted.</exception>
    Task<HttpResponseMessage> ExecuteWithRetryAsync(
        Func<Task<HttpResponseMessage>> requestFunc,
        CancellationToken cancellationToken = default
    );
}
