namespace CheckMods.Configuration;

/// <summary>
/// Configuration options for the mod scanner service.
/// </summary>
public class ModScannerOptions
{
    /// <summary>
    /// The configuration section name for binding from appsettings.
    /// </summary>
    public const string SectionName = "ModScanner";

    /// <summary>
    /// Maximum DLL file size in bytes to scan (default: 100MB).
    /// Files larger than this are skipped.
    /// </summary>
    public long MaxDllSizeBytes { get; set; } = 100 * 1024 * 1024;
}
