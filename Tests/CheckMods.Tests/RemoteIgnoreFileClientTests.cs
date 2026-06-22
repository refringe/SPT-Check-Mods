using System.Net;
using System.Text.Json;
using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="RemoteIgnoreFileClient"/>: parsing valid files, rejecting unsupported/newer schemas and
/// malformed bodies, dropping incomplete entries, and treating an unconfigured URL as a no-op.
/// </summary>
public sealed class RemoteIgnoreFileClientTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }

    private static RemoteIgnoreFileClient CreateClient(
        HttpStatusCode status,
        string body,
        string? url = "https://example.test/ignored-updates.json"
    )
    {
        return new RemoteIgnoreFileClient(
            new HttpClient(new StubHandler(status, body)),
            Options.Create(new IgnoredUpdateOptions { RemoteUrl = url }),
            NullLogger<RemoteIgnoreFileClient>.Instance
        );
    }

    private static string Json(int schemaVersion, params (int id, string local, string latest)[] entries)
    {
        var file = new IgnoredUpdatesFile(
            schemaVersion,
            entries.Select(e => new IgnoredUpdate(e.id, e.local, e.latest)).ToList()
        );
        return JsonSerializer.Serialize(file);
    }

    [Fact]
    public async Task FetchAsync_parses_valid_file()
    {
        var client = CreateClient(HttpStatusCode.OK, Json(1, (1, "1.0.0", "1.0.1"), (2, "2.0.0", "2.1.0")));

        var result = await client.FetchAsync();

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
    }

    [Fact]
    public async Task FetchAsync_returns_null_on_newer_schema()
    {
        var client = CreateClient(
            HttpStatusCode.OK,
            Json(IgnoredUpdatesFile.CurrentSchemaVersion + 1, (1, "1.0.0", "1.0.1"))
        );

        Assert.Null(await client.FetchAsync());
    }

    [Fact]
    public async Task FetchAsync_returns_null_on_malformed_json()
    {
        Assert.Null(await CreateClient(HttpStatusCode.OK, "not json").FetchAsync());
    }

    [Fact]
    public async Task FetchAsync_returns_null_on_error_status()
    {
        Assert.Null(await CreateClient(HttpStatusCode.NotFound, "").FetchAsync());
    }

    [Fact]
    public async Task FetchAsync_drops_malformed_entries()
    {
        // One valid entry and one missing its versions.
        const string body =
            "{\"schemaVersion\":1,\"ignored\":["
            + "{\"apiModId\":1,\"localVersion\":\"1.0.0\",\"ignoredLatestVersion\":\"1.0.1\"},"
            + "{\"apiModId\":2}]}";

        var result = await CreateClient(HttpStatusCode.OK, body).FetchAsync();

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal(1, result![0].ApiModId);
    }

    [Fact]
    public async Task FetchAsync_returns_null_when_not_configured()
    {
        var client = CreateClient(HttpStatusCode.OK, Json(1, (1, "1.0.0", "1.0.1")), url: null);

        Assert.False(client.IsConfigured);
        Assert.Null(await client.FetchAsync());
    }
}
