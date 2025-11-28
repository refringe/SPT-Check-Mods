namespace CheckMods.Services.Interfaces;

/// <summary>
/// Provides thread-safe access to the Forge API key.
/// This singleton service stores the API key so it can be shared across multiple service instances.
/// </summary>
public interface IApiKeyProvider
{
    /// <summary>
    /// Gets the current API key, or null if not set.
    /// </summary>
    string? ApiKey { get; }

    /// <summary>
    /// Sets the API key for authentication with the Forge API.
    /// </summary>
    /// <param name="apiKey">The Bearer token for API authentication.</param>
    void SetApiKey(string apiKey);

    /// <summary>
    /// Clears the stored API key.
    /// </summary>
    void ClearApiKey();
}
