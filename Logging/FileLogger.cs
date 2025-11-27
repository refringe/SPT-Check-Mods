using System.Collections.Concurrent;
using System.Text;
using CheckMods.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheckMods.Logging;

/// <summary>
/// A simple file logger that writes log messages to a file with rotation support.
/// </summary>
public sealed class FileLogger(string categoryName, FileLoggerProvider provider) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= provider.MinimumLogLevel;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message) && exception is null)
        {
            return;
        }

        var logEntry = new StringBuilder();
        logEntry.Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        logEntry.Append(" [");
        logEntry.Append(GetLogLevelString(logLevel));
        logEntry.Append("] ");
        logEntry.Append(GetCategoryShortName(categoryName));
        logEntry.Append(": ");
        logEntry.AppendLine(message);

        if (exception is not null)
        {
            logEntry.AppendLine(exception.ToString());
        }

        provider.WriteLog(logEntry.ToString());
    }

    private static string GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "UNK",
        };
    }

    private static string GetCategoryShortName(string category)
    {
        var lastDot = category.LastIndexOf('.');
        return lastDot >= 0 ? category[(lastDot + 1)..] : category;
    }
}

/// <summary>
/// Provider for file-based logging with rotation support.
/// </summary>
[ProviderAlias("File")]
public sealed class FileLoggerProvider(IOptions<LoggingOptions> options) : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new(StringComparer.OrdinalIgnoreCase);
    private readonly LoggingOptions _options = options.Value;
    private readonly object _writeLock = new();
    private readonly string _logFilePath = LoggingOptions.CurrentLogFilePath;
    private bool _disposed;
    private bool _initialized;

    public LogLevel MinimumLogLevel
    {
        get { return _options.MinimumLogLevel; }
    }

    private void EnsureInitialized()
    {
        if (_initialized || !_options.EnableFileLogging)
        {
            return;
        }

        _initialized = true;
        EnsureLogDirectoryExists();
        RotateLogsIfNeeded();
        WriteStartupBanner();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));
    }

    public void WriteLog(string message)
    {
        if (_disposed || !_options.EnableFileLogging)
        {
            return;
        }

        lock (_writeLock)
        {
            try
            {
                EnsureInitialized();
                RotateLogsIfNeeded();
                File.AppendAllText(_logFilePath, message);
            }
            catch
            {
                // Silently fail if we can't write to the log file
            }
        }
    }

    private void EnsureLogDirectoryExists()
    {
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private void RotateLogsIfNeeded()
    {
        try
        {
            if (!File.Exists(_logFilePath))
            {
                return;
            }

            var fileInfo = new FileInfo(_logFilePath);
            if (fileInfo.Length < _options.MaxFileSizeBytes)
            {
                return;
            }

            // Rotate existing log files
            var directory = Path.GetDirectoryName(_logFilePath) ?? ".";
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_logFilePath);
            var extension = Path.GetExtension(_logFilePath);

            // Delete oldest log if at limit
            var oldestLog = Path.Combine(directory, $"{fileNameWithoutExt}.{_options.RetainedFileCount}{extension}");
            if (File.Exists(oldestLog))
            {
                File.Delete(oldestLog);
            }

            // Shift existing logs (process in reverse order)
            var logsToShift = Enumerable
                .Range(1, _options.RetainedFileCount - 1)
                .Reverse()
                .Select(i =>
                    (
                        Current: Path.Combine(directory, $"{fileNameWithoutExt}.{i}{extension}"),
                        New: Path.Combine(directory, $"{fileNameWithoutExt}.{i + 1}{extension}")
                    )
                )
                .Where(paths => File.Exists(paths.Current));

            foreach (var (current, newPath) in logsToShift)
            {
                File.Move(current, newPath);
            }

            // Rename current log to .1
            var firstRotation = Path.Combine(directory, $"{fileNameWithoutExt}.1{extension}");
            File.Move(_logFilePath, firstRotation);
        }
        catch
        {
            // Silently fail if rotation fails
        }
    }

    private void WriteStartupBanner()
    {
        var banner = new StringBuilder();
        banner.AppendLine();
        banner.AppendLine("================================================================================");
        banner.AppendLine($"  CheckMods Session Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        banner.AppendLine("================================================================================");
        banner.AppendLine();

        WriteLog(banner.ToString());
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _loggers.Clear();
    }
}

/// <summary>
/// Extension methods for adding file logging to ILoggingBuilder.
/// </summary>
public static class FileLoggerExtensions
{
    public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, Action<LoggingOptions>? configure = null)
    {
        if (configure is not null)
        {
            builder.Services.Configure(configure);
        }

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>());
        return builder;
    }
}
