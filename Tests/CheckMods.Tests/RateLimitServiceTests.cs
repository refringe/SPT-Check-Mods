using System.Net;
using System.Net.Http.Headers;
using CheckMods.Configuration;
using CheckMods.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CheckMods.Tests;

/// <summary>
/// Tests for RateLimitService's retry and backoff behaviour. The token-bucket pacer is configured with a large burst
/// that does not throttle at the test's request volume. Backoff delays are configured to a few milliseconds.
/// </summary>
public sealed class RateLimitServiceTests
{
    private const int MaxRetries = 3;

    private static RateLimitService CreateService()
    {
        var options = new RateLimitOptions
        {
            MaxBurst = 1000,
            RefillTokensPerPeriod = 1000,
            RefillPeriodMs = 1,
            MaxConcurrentRequests = 8,
            MaxRetries = MaxRetries,
            BaseDelayMs = 1,
            MaxDelayMs = 5,
            RequestTimeoutSeconds = 30,
        };

        return new RateLimitService(Options.Create(options), NullLogger<RateLimitService>.Instance);
    }

    private static HttpResponseMessage Response(HttpStatusCode statusCode, TimeSpan? retryAfter = null)
    {
        var message = new HttpResponseMessage(statusCode) { Content = new StringContent("{}") };
        if (retryAfter is not null)
        {
            message.Headers.RetryAfter = new RetryConditionHeaderValue(retryAfter.Value);
        }

        return message;
    }

    [Fact]
    public async Task Returns_success_without_retrying()
    {
        using var service = CreateService();
        var calls = 0;

        var result = await service.ExecuteWithRetryAsync(() =>
        {
            calls++;
            return Task.FromResult(Response(HttpStatusCode.OK));
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Retries_after_rate_limit_then_succeeds()
    {
        using var service = CreateService();
        var queue = new Queue<HttpResponseMessage>([
            Response(HttpStatusCode.TooManyRequests, TimeSpan.FromMilliseconds(1)),
            Response(HttpStatusCode.OK),
        ]);
        var calls = 0;

        var result = await service.ExecuteWithRetryAsync(() =>
        {
            calls++;
            return Task.FromResult(queue.Dequeue());
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Retries_after_server_error_then_succeeds()
    {
        using var service = CreateService();
        var queue = new Queue<HttpResponseMessage>([
            Response(HttpStatusCode.ServiceUnavailable),
            Response(HttpStatusCode.OK),
        ]);
        var calls = 0;

        var result = await service.ExecuteWithRetryAsync(() =>
        {
            calls++;
            return Task.FromResult(queue.Dequeue());
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Retries_after_network_error_then_succeeds()
    {
        using var service = CreateService();
        var calls = 0;

        var result = await service.ExecuteWithRetryAsync(() =>
        {
            calls++;
            if (calls == 1)
            {
                throw new HttpRequestException("network down");
            }

            return Task.FromResult(Response(HttpStatusCode.OK));
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Retries_after_timeout_then_succeeds()
    {
        using var service = CreateService();
        var calls = 0;

        // A TaskCanceledException with a token that is NOT cancelled represents an HttpClient timeout.
        var result = await service.ExecuteWithRetryAsync(() =>
        {
            calls++;
            if (calls == 1)
            {
                throw new TaskCanceledException("timed out");
            }

            return Task.FromResult(Response(HttpStatusCode.OK));
        });

        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Throws_after_exhausting_rate_limit_retries()
    {
        using var service = CreateService();
        var calls = 0;

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.ExecuteWithRetryAsync(() =>
            {
                calls++;
                return Task.FromResult(Response(HttpStatusCode.TooManyRequests, TimeSpan.FromMilliseconds(1)));
            })
        );

        // The initial attempt plus MaxRetries retries.
        Assert.Equal(MaxRetries + 1, calls);
    }

    [Fact]
    public async Task Returns_last_response_after_exhausting_server_error_retries()
    {
        using var service = CreateService();
        var calls = 0;

        var result = await service.ExecuteWithRetryAsync(() =>
        {
            calls++;
            return Task.FromResult(Response(HttpStatusCode.ServiceUnavailable));
        });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, result.StatusCode);
        Assert.Equal(MaxRetries + 1, calls);
    }

    [Fact]
    public async Task Does_not_retry_non_transient_client_error()
    {
        using var service = CreateService();
        var calls = 0;

        var result = await service.ExecuteWithRetryAsync(() =>
        {
            calls++;
            return Task.FromResult(Response(HttpStatusCode.BadRequest));
        });

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Honors_caller_cancellation()
    {
        using var service = CreateService();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var calls = 0;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ExecuteWithRetryAsync(
                () =>
                {
                    calls++;
                    return Task.FromResult(Response(HttpStatusCode.OK));
                },
                cts.Token
            )
        );

        Assert.Equal(0, calls);
    }
}
