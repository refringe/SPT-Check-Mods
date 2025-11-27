namespace CheckMods.Models;

/// <summary>
/// Represents a successful absence of data - the resource was not found but no error occurred.
/// </summary>
public readonly record struct NotFound;

/// <summary>
/// Represents a mod that exists but has no version compatible with the requested SPT version.
/// </summary>
public readonly record struct NoCompatibleVersion;

/// <summary>
/// Represents a rate limit being exceeded after retries.
/// </summary>
public readonly record struct RateLimited;

/// <summary>
/// Represents an invalid input parameter.
/// </summary>
/// <param name="ParameterName">The name of the invalid parameter.</param>
/// <param name="Message">A description of what's wrong with the input.</param>
public readonly record struct InvalidInput(string ParameterName, string Message);

/// <summary>
/// Represents an invalid or expired API key.
/// </summary>
/// <param name="ShouldDeleteKey">Whether the stored key should be deleted.</param>
public readonly record struct InvalidApiKey(bool ShouldDeleteKey);

/// <summary>
/// Represents an invalid SPT version that doesn't exist in the Forge API.
/// </summary>
public readonly record struct InvalidSptVersion;

/// <summary>
/// Represents an API error with details about what went wrong.
/// </summary>
/// <param name="Message">A description of the error.</param>
/// <param name="StatusCode">The HTTP status code, if applicable.</param>
/// <param name="Exception">The underlying exception, if any.</param>
public readonly record struct ApiError(
    string Message,
    int? StatusCode = null,
    Exception? Exception = null);
