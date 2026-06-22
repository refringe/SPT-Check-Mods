namespace CheckMods.Services.Interfaces;

/// <summary>
/// Interface for rate-limiting API calls. Proactively paces requests beneath the API's rate limits and retries on
/// rate limiting (429) and transient failures, applying global backoff that honors the Retry-After header.
/// </summary>
public interface IRateLimitService
{
    /// <summary>
    /// Executes an HTTP request, pacing it beneath the rate limits and retrying on rate limiting (429) and transient
    /// failures (timeouts, network errors, and 5xx/408 responses).
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
