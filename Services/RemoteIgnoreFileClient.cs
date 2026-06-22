using System.Text.Json;
using System.Text.Json.Serialization;
using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheckMods.Services;

/// <summary>
/// HTTP client for the author-maintained remote base ignore list. Registered via AddHttpClient (like
/// <see cref="ForgeApiService"/>) for proper HttpClient lifecycle management, so it carries no [Injectable] attribute.
/// </summary>
public sealed class RemoteIgnoreFileClient(
    HttpClient httpClient,
    IOptions<IgnoredUpdateOptions> options,
    ILogger<RemoteIgnoreFileClient> logger
) : IRemoteIgnoreFileClient
{
    private readonly IgnoredUpdateOptions _options = options.Value;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <inheritdoc />
    public bool IsConfigured
    {
        get { return !string.IsNullOrWhiteSpace(_options.RemoteUrl); }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IgnoredUpdate>?> FetchAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        try
        {
            logger.LogDebug("Fetching remote ignore list: GET {Url}", _options.RemoteUrl);
            using var response = await httpClient.GetAsync(_options.RemoteUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogInformation("Remote ignore list returned {Status}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var file = JsonSerializer.Deserialize<IgnoredUpdatesFile>(json, _jsonOptions);
            if (file?.Ignored is null)
            {
                return null;
            }

            // Refuse to parse a format newer than we understand rather than risk misreading it.
            if (file.SchemaVersion > IgnoredUpdatesFile.CurrentSchemaVersion)
            {
                logger.LogWarning(
                    "Remote ignore list schema v{Remote} is newer than supported v{Supported}; skipping",
                    file.SchemaVersion,
                    IgnoredUpdatesFile.CurrentSchemaVersion
                );
                return null;
            }

            return file.Ignored.Where(e => e.IsWellFormed).ToList();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not fetch the remote ignore list");
            return null;
        }
    }
}
