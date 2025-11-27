using Microsoft.Extensions.Logging;

namespace CheckMods.Configuration;

/// <summary>
/// Configuration options for the logging infrastructure.
/// </summary>
public class LoggingOptions
{
    /// <summary>
    /// The configuration section name for binding from appsettings.
    /// </summary>
    public const string SectionName = "Logging";

    /// <summary>
    /// Whether file logging is enabled. Default is true.
    /// </summary>
    public bool EnableFileLogging { get; set; } = true;

    /// <summary>
    /// The minimum log level for file logging. Default is Debug.
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Debug;

    /// <summary>
    /// Maximum size of the log file in bytes before rotation. Default is 10 MB.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Number of log files to retain. Default is 3.
    /// </summary>
    public int RetainedFileCount { get; set; } = 3;

    /// <summary>
    /// Gets the path to the log directory (same location as API key file).
    /// </summary>
    public static string LogDirectory
    {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SptModChecker", "logs"); }
    }

    /// <summary>
    /// Gets the current log file path.
    /// </summary>
    public static string CurrentLogFilePath
    {
        get { return Path.Combine(LogDirectory, "checkmod.log"); }
    }
}
