namespace CheckMods.Configuration;

/// <summary>
/// Configuration options for the Forge API service.
/// </summary>
public class ForgeApiOptions
{
    /// <summary>
    /// The base URL for the Forge API.
    /// </summary>
    public string BaseUrl { get; set; } = "https://forge.sp-tarkov.com/api/v0/";
}
