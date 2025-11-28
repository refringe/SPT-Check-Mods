using CheckMods.Services.Interfaces;
using SPTarkov.DI.Annotations;

namespace CheckMods.Services;

/// <summary>
/// Thread-safe singleton service that stores the Forge API key.
/// This allows the API key to be shared across multiple ForgeApiService instances.
/// </summary>
[Injectable(InjectionType.Singleton)]
public sealed class ApiKeyProvider : IApiKeyProvider
{
    private string? _apiKey;
    private readonly Lock _lock = new();

    /// <inheritdoc />
    public string? ApiKey
    {
        get
        {
            lock (_lock)
            {
                return _apiKey;
            }
        }
    }

    /// <inheritdoc />
    public void SetApiKey(string apiKey)
    {
        lock (_lock)
        {
            _apiKey = apiKey;
        }
    }

    /// <inheritdoc />
    public void ClearApiKey()
    {
        lock (_lock)
        {
            _apiKey = null;
        }
    }
}
