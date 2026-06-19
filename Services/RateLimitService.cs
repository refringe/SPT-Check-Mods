using System.Net;
using System.Threading.RateLimiting;
using CheckMods.Configuration;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SPTarkov.DI.Annotations;

namespace CheckMods.Services;

/// <summary>
/// Service that provides rate limiting for API calls. Proactively paces requests beneath the Forge API's two
/// server-side limits (a burst window and a general window) using sliding-window rate limiters, and reacts to
/// rate limiting (429) and transient errors with retries and backoff. All limiter state is shared across requests.
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed class RateLimitService : IRateLimitService, IDisposable
{
    /// <summary>
    /// Upper bound on requests queued behind each window limiter. Large enough to hold every request a single run
    /// could fan out at once, so callers wait their turn rather than being rejected.
    /// </summary>
    private const int QueueLimit = 10_000;

    private readonly RateLimitOptions _options;
    private readonly ILogger<RateLimitService> _logger;

    private readonly SlidingWindowRateLimiter _burstLimiter;
    private readonly SlidingWindowRateLimiter _generalLimiter;
    private readonly SemaphoreSlim _concurrencyGate;

    private readonly SemaphoreSlim _backoffSemaphore = new(1, 1);
    private DateTime _backoffUntil = DateTime.MinValue;
    private int _consecutiveBackoffs;

    public RateLimitService(IOptions<RateLimitOptions> options, ILogger<RateLimitService> logger)
    {
        _options = options.Value;
        _logger = logger;

        _burstLimiter = new SlidingWindowRateLimiter(
            new SlidingWindowRateLimiterOptions
            {
                PermitLimit = _options.BurstLimit,
                Window = TimeSpan.FromSeconds(_options.BurstWindowSeconds),
                SegmentsPerWindow = Math.Max(1, _options.BurstWindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = QueueLimit,
                AutoReplenishment = true,
            }
        );

        _generalLimiter = new SlidingWindowRateLimiter(
            new SlidingWindowRateLimiterOptions
            {
                PermitLimit = _options.GeneralLimit,
                Window = TimeSpan.FromSeconds(_options.GeneralWindowSeconds),
                SegmentsPerWindow = Math.Max(1, _options.GeneralWindowSeconds),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = QueueLimit,
                AutoReplenishment = true,
            }
        );

        _concurrencyGate = new SemaphoreSlim(_options.MaxConcurrentRequests, _options.MaxConcurrentRequests);
    }

    /// <summary>
    /// Executes an HTTP request, pacing it beneath the burst and general rate limits and retrying on rate limiting
    /// (429) and transient failures (timeouts, network errors, and 5xx/408 responses).
    /// </summary>
    /// <param name="requestFunc">Function that executes the HTTP request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The HTTP response from a successful or non-retryable request.</returns>
    /// <exception cref="HttpRequestException">Thrown when retries are exhausted for rate limiting or transient errors.</exception>
    public async Task<HttpResponseMessage> ExecuteWithRetryAsync(
        Func<Task<HttpResponseMessage>> requestFunc,
        CancellationToken cancellationToken = default
    )
    {
        var retryCount = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Wait out any global backoff triggered by a prior 429.
            await WaitForBackoffAsync(cancellationToken);

            HttpResponseMessage? response;
            Exception? transientError = null;

            // Proactively pace beneath both server windows, then cap simultaneous in-flight requests.
            using (await _burstLimiter.AcquireAsync(1, cancellationToken))
            using (await _generalLimiter.AcquireAsync(1, cancellationToken))
            {
                await _concurrencyGate.WaitAsync(cancellationToken);
                try
                {
                    response = await requestFunc();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw; // Caller-requested cancellation.
                }
                catch (OperationCanceledException ex)
                {
                    response = null;
                    transientError = ex; // HttpClient timeout (token not cancelled).
                }
                catch (HttpRequestException ex)
                {
                    response = null;
                    transientError = ex; // Network error.
                }
                finally
                {
                    _concurrencyGate.Release();
                }
            }

            // Network error or timeout: back off locally and retry without stalling other requests.
            if (transientError is not null)
            {
                if (++retryCount > _options.MaxRetries)
                {
                    _logger.LogError("Request failed after {MaxRetries} retries: {Message}", _options.MaxRetries, transientError.Message);
                    throw transientError is OperationCanceledException
                        ? new HttpRequestException("Request timed out after retries", transientError)
                        : new HttpRequestException("Request failed after retries", transientError);
                }

                _logger.LogWarning(
                    "Transient request error. Retry {RetryCount}/{MaxRetries}: {Message}",
                    retryCount,
                    _options.MaxRetries,
                    transientError.Message
                );
                await Task.Delay(GetExponentialDelay(retryCount), cancellationToken);
                continue;
            }

            // Rate limited: honor Retry-After and back off globally so all requests wait together.
            if (response!.StatusCode == HttpStatusCode.TooManyRequests)
            {
                if (++retryCount > _options.MaxRetries)
                {
                    response.Dispose();
                    _logger.LogError("Rate limit exceeded after {MaxRetries} retries", _options.MaxRetries);
                    throw new HttpRequestException($"Rate limit exceeded after {_options.MaxRetries} retries");
                }

                var retryAfter = GetRetryAfterDelay(response);
                response.Dispose();
                _logger.LogWarning(
                    "Rate limited (429). Retry {RetryCount}/{MaxRetries}{RetryAfter}",
                    retryCount,
                    _options.MaxRetries,
                    retryAfter.HasValue ? $", Retry-After {retryAfter.Value.TotalSeconds:0}s" : string.Empty
                );
                await ApplyGlobalBackoffAsync(retryAfter, cancellationToken);
                continue;
            }

            // Transient server error: back off locally and retry.
            if (IsTransientStatusCode(response.StatusCode))
            {
                if (++retryCount > _options.MaxRetries)
                {
                    _logger.LogWarning(
                        "Server error {StatusCode} persisted after {MaxRetries} retries",
                        (int)response.StatusCode,
                        _options.MaxRetries
                    );
                    return response; // Surface to the caller as an ApiError.
                }

                var delay = GetRetryAfterDelay(response) ?? GetExponentialDelay(retryCount);
                _logger.LogWarning(
                    "Server error {StatusCode}. Retry {RetryCount}/{MaxRetries}",
                    (int)response.StatusCode,
                    retryCount,
                    _options.MaxRetries
                );
                response.Dispose();
                await Task.Delay(delay, cancellationToken);
                continue;
            }

            // Success or non-retryable response. Reset global backoff escalation on success.
            if (response.IsSuccessStatusCode)
            {
                ResetBackoff();
            }

            return response;
        }
    }

    /// <summary>
    /// Determines whether a status code represents a transient server error worth retrying.
    /// </summary>
    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode is HttpStatusCode.RequestTimeout // 408
            or HttpStatusCode.InternalServerError // 500
            or HttpStatusCode.BadGateway // 502
            or HttpStatusCode.ServiceUnavailable // 503
            or HttpStatusCode.GatewayTimeout; // 504
    }

    /// <summary>
    /// Waits if there's an active global backoff period.
    /// </summary>
    private async Task WaitForBackoffAsync(CancellationToken cancellationToken)
    {
        var delay = _backoffUntil - DateTime.UtcNow;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }
    }

    /// <summary>
    /// Applies a global backoff after a 429 so all concurrent requests wait. Uses the Retry-After delay when present,
    /// otherwise an escalating exponential backoff with jitter. Guarded by a semaphore so only one thread updates state.
    /// </summary>
    /// <param name="retryAfter">Optional delay from the Retry-After header.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task ApplyGlobalBackoffAsync(TimeSpan? retryAfter, CancellationToken cancellationToken)
    {
        await _backoffSemaphore.WaitAsync(cancellationToken);
        try
        {
            _consecutiveBackoffs++;

            var delay =
                retryAfter is { } value && value > TimeSpan.Zero ? value : GetExponentialDelay(_consecutiveBackoffs);

            var newBackoffUntil = DateTime.UtcNow + delay;
            if (newBackoffUntil > _backoffUntil)
            {
                _backoffUntil = newBackoffUntil;
                _logger.LogDebug(
                    "Global backoff applied: {DelayMs}ms (consecutive: {Count})",
                    delay.TotalMilliseconds,
                    _consecutiveBackoffs
                );
            }
        }
        finally
        {
            _backoffSemaphore.Release();
        }

        await WaitForBackoffAsync(cancellationToken);
    }

    /// <summary>
    /// Computes an exponential backoff delay with jitter, capped at the configured maximum.
    /// </summary>
    /// <param name="attempt">The 1-based attempt number.</param>
    private TimeSpan GetExponentialDelay(int attempt)
    {
        var exponentialDelay = _options.BaseDelayMs * Math.Pow(2, attempt - 1);
        var jitter = Random.Shared.Next(0, Math.Max(1, _options.BaseDelayMs / 2));
        var totalDelayMs = Math.Min(exponentialDelay + jitter, _options.MaxDelayMs);
        return TimeSpan.FromMilliseconds(totalDelayMs);
    }

    /// <summary>
    /// Extracts the Retry-After delay from an HTTP response.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <returns>The delay if a Retry-After header is present, null otherwise.</returns>
    private static TimeSpan? GetRetryAfterDelay(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        // Retry-After can be a delay in seconds or an absolute date.
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
    /// Resets the global backoff escalation after a successful request.
    /// </summary>
    private void ResetBackoff()
    {
        Interlocked.Exchange(ref _consecutiveBackoffs, 0);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _burstLimiter.Dispose();
        _generalLimiter.Dispose();
        _concurrencyGate.Dispose();
        _backoffSemaphore.Dispose();
    }
}
