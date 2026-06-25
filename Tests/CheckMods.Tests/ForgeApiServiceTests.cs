using System.Net;
using CheckMods.Configuration;
using CheckMods.Services;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="ForgeApiService.GetModUpdatesAsync"/>, which batches the mods/updates request into chunks and
/// merges the results. The request is atomic: successful chunks are merged, but any chunk error fails the whole call.
/// Backed by a stub <see cref="HttpMessageHandler"/> and a pass-through rate limiter.
/// </summary>
public sealed class ForgeApiServiceTests
{
    private static readonly SemanticVersioning.Version SptVersion = new("3.0.0");

    private static ForgeApiService CreateService(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var httpClient = new HttpClient(new StubHandler(responder));
        var cache = new MemoryCache(new MemoryCacheOptions());
        var options = Options.Create(new ForgeApiOptions());

        return new ForgeApiService(
            httpClient,
            new PassThroughRateLimitService(),
            cache,
            options,
            NullLogger<ForgeApiService>.Instance
        );
    }

    private static List<(int ModId, string CurrentVersion)> Mods(params int[] ids)
    {
        return ids.Select(id => (id, "1.0.0")).ToList();
    }

    /// <summary>
    /// 51 mods spread over two chunks (50 + 1). Marker IDs 1001 (chunk 1) and 2002 (chunk 2) identify which request a
    /// stub is answering.
    /// </summary>
    private static List<(int ModId, string CurrentVersion)> TwoChunkMods()
    {
        var mods = new List<(int, string)> { (1001, "1.0.0") };
        mods.AddRange(Enumerable.Range(1, 49).Select(i => (i, "1.0.0")));
        mods.Add((2002, "1.0.0"));
        return mods;
    }

    private static bool IsSecondChunk(HttpRequestMessage request)
    {
        return Uri.UnescapeDataString(request.RequestUri!.Query).Contains("2002:");
    }

    private static HttpResponseMessage Ok(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
    }

    private static HttpResponseMessage ServerError()
    {
        return new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("{}") };
    }

    private static string UpToDateJson(params int[] modIds)
    {
        var entries = string.Join(",", modIds.Select(id => $"{{\"mod_id\":{id},\"version\":\"1.0.0\"}}"));
        return $"{{\"success\":true,\"data\":{{\"up_to_date\":[{entries}]}}}}";
    }

    [Fact]
    public async Task Returns_not_found_for_empty_input_without_calling_the_api()
    {
        var service = CreateService(_ => throw new InvalidOperationException("no request expected"));

        var result = await service.GetModUpdatesAsync([], SptVersion);

        Assert.True(result.IsT1); // NotFound
    }

    [Fact]
    public async Task Returns_merged_data_for_a_single_chunk()
    {
        var service = CreateService(_ => Ok(UpToDateJson(1, 2, 3)));

        var result = await service.GetModUpdatesAsync(Mods(1, 2, 3), SptVersion);

        Assert.True(result.IsT0);
        Assert.Equal(3, result.AsT0.UpToDate?.Count);
    }

    [Fact]
    public async Task Merges_data_across_multiple_chunks()
    {
        var service = CreateService(req => IsSecondChunk(req) ? Ok(UpToDateJson(2002)) : Ok(UpToDateJson(1001)));

        var result = await service.GetModUpdatesAsync(TwoChunkMods(), SptVersion);

        Assert.True(result.IsT0);
        var upToDate = result.AsT0.UpToDate!;
        Assert.Equal(2, upToDate.Count);
        Assert.Contains(upToDate, m => m.ModId == 1001);
        Assert.Contains(upToDate, m => m.ModId == 2002);
    }

    [Fact]
    public async Task Surfaces_an_error_when_any_chunk_fails()
    {
        var service = CreateService(req => IsSecondChunk(req) ? ServerError() : Ok(UpToDateJson(1001)));

        var result = await service.GetModUpdatesAsync(TwoChunkMods(), SptVersion);

        Assert.True(result.IsT2); // ApiError
    }

    [Fact]
    public async Task Surfaces_an_error_when_every_chunk_fails()
    {
        var service = CreateService(_ => ServerError());

        var result = await service.GetModUpdatesAsync(TwoChunkMods(), SptVersion);

        Assert.True(result.IsT2); // ApiError
    }

    [Fact]
    public async Task Returns_not_found_when_chunks_have_no_data()
    {
        var service = CreateService(_ => Ok("{\"success\":true,\"data\":null}"));

        var result = await service.GetModUpdatesAsync(Mods(1, 2, 3), SptVersion);

        Assert.True(result.IsT1); // NotFound
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(responder(request));
        }
    }

    private sealed class PassThroughRateLimitService : IRateLimitService
    {
        public Task<HttpResponseMessage> ExecuteWithRetryAsync(
            Func<Task<HttpResponseMessage>> requestFunc,
            CancellationToken cancellationToken = default
        )
        {
            return requestFunc();
        }
    }
}
