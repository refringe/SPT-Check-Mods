using System.Net;
using CheckMods.Configuration;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheckMods.Services;

/// <summary>
/// Service that provides rate limiting for API calls. Throttles requests to a sustainable rate and applies
/// exponential backoff with jitter if rate limiting (429) is encountered. Backoff state is shared across all requests.
/// </summary>
public class RateLimitService(
    IOptions<RateLimitOptions> options,
    ILogger<RateLimitService> logger) : IRateLimitService
{
    private readonly RateLimitOptions _options = options.Value;
    private readonly SemaphoreSlim _backoffSemaphore = new(1, 1);
    private readonly SemaphoreSlim _throttleSemaphore = new(1, 1);

    private DateTime _backoffUntil = DateTime.MinValue;
    private DateTime _lastRequestTime = DateTime.MinValue;
    private int _consecutiveFailures;

    /// <summary>
    /// Executes an HTTP request with automatic retry and backoff on rate limiting (429 responses).
    /// Throttles requests to 2 per second to avoid triggering rate limits.
    /// </summary>
    /// <param name="requestFunc">Function that executes the HTTP request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The HTTP response from a successful request.</returns>
    /// <exception cref="HttpRequestException">Thrown when max retries are exceeded.</exception>
    public async Task<HttpResponseMessage> ExecuteWithRetryAsync(
        Func<Task<HttpResponseMessage>> requestFunc,
        CancellationToken cancellationToken = default)
    {
        var retryCount = 0;

        while (true)
        {
            // Wait if we're in a global backoff period
            await WaitForBackoffAsync(cancellationToken);

            // Throttle requests to avoid hitting rate limits
            await ThrottleRequestAsync(cancellationToken);

            HttpResponseMessage response;
            try
            {
                response = await requestFunc();
            }
            catch (HttpRequestException) when (retryCount < _options.MaxRetries)
            {
                // Network error - apply backoff and retry
                retryCount++;
                await ApplyBackoffAsync(null, cancellationToken);
                continue;
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                retryCount++;
                logger.LogWarning("Rate limited (429). Retry {RetryCount}/{MaxRetries}", retryCount, _options.MaxRetries);

                if (retryCount > _options.MaxRetries)
                {
                    logger.LogError("Rate limit exceeded after {MaxRetries} retries", _options.MaxRetries);
                    throw new HttpRequestException($"Rate limit exceeded after {_options.MaxRetries} retries");
                }

                // Extract Retry-After header if present
                var retryAfter = GetRetryAfterDelay(response);
                await ApplyBackoffAsync(retryAfter, cancellationToken);
                continue;
            }

            // Success or non-retryable error - reset backoff state on success
            if (response.IsSuccessStatusCode)
            {
                ResetBackoff();
            }

            return response;
        }
    }

    /// <summary>
    /// Throttles requests to maintain a minimum interval between API calls.
    /// </summary>
    private async Task ThrottleRequestAsync(CancellationToken cancellationToken)
    {
        await _throttleSemaphore.WaitAsync(cancellationToken);
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            var remainingDelay = TimeSpan.FromMilliseconds(_options.MinRequestIntervalMs) - timeSinceLastRequest;

            if (remainingDelay > TimeSpan.Zero)
            {
                await Task.Delay(remainingDelay, cancellationToken);
            }

            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _throttleSemaphore.Release();
        }
    }

    /// <summary>
    /// Waits if there's an active global backoff period.
    /// </summary>
    private async Task WaitForBackoffAsync(CancellationToken cancellationToken)
    {
        var waitUntil = _backoffUntil;
        var delay = waitUntil - DateTime.UtcNow;

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }

    /// <summary>
    /// Applies exponential backoff after a rate limit response.
    /// Uses a semaphore to ensure only one thread updates the backoff state.
    /// </summary>
    /// <param name="retryAfter">Optional delay from Retry-After header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ApplyBackoffAsync(TimeSpan? retryAfter, CancellationToken cancellationToken)
    {
        await _backoffSemaphore.WaitAsync(cancellationToken);
        try
        {
            _consecutiveFailures++;

            // Calculate delay: use Retry-After if provided, otherwise exponential backoff with jitter
            TimeSpan delay;
            if (!retryAfter.HasValue || retryAfter.Value <= TimeSpan.Zero)
            {
                // Exponential backoff: baseDelay * 2^failures + random jitter
                var exponentialDelay = _options.BaseDelayMs * Math.Pow(2, _consecutiveFailures - 1);
                var jitter = Random.Shared.Next(0, _options.BaseDelayMs / 2);
                var totalDelayMs = Math.Min(exponentialDelay + jitter, _options.MaxDelayMs);
                delay = TimeSpan.FromMilliseconds(totalDelayMs);
            }
            else
            {
                delay = retryAfter.Value;
            }

            // Set global backoff time
            var newBackoffUntil = DateTime.UtcNow + delay;
            if (newBackoffUntil > _backoffUntil)
            {
                _backoffUntil = newBackoffUntil;
                logger.LogDebug("Backoff applied: {DelayMs}ms (failures: {FailureCount})", delay.TotalMilliseconds, _consecutiveFailures);
            }
        }
        finally
        {
            _backoffSemaphore.Release();
        }

        // Wait for the backoff period
        await WaitForBackoffAsync(cancellationToken);
    }

    /// <summary>
    /// Extracts the Retry-After delay from an HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <returns>The delay if Retry-After header is present, null otherwise.</returns>
    private static TimeSpan? GetRetryAfterDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter == null)
        {
            return null;
        }

        // Retry-After can be a delay in seconds or an absolute date
        if (retryAfter.Delta.HasValue)
        {
            return retryAfter.Delta.Value;
        }

        if (!retryAfter.Date.HasValue)
        {
            return null;
        }

        var delay = retryAfter.Date.Value - DateTimeOffset.UtcNow;
        return delay > TimeSpan.Zero ? delay : null;
    }

    /// <summary>
    /// Resets the backoff state after a successful request.
    /// </summary>
    private void ResetBackoff()
    {
        Interlocked.Exchange(ref _consecutiveFailures, 0);
    }
}
