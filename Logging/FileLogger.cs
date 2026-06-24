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
    private readonly string _logFilePath = options.Value.LogFilePath;
    private StreamWriter? _writer;
    private bool _disposed;
    private bool _initialized;
    private bool _rotationSuppressed;

    public LogLevel MinimumLogLevel
    {
        get { return _options.MinimumLogLevel; }
    }

    /// <summary>
    /// True once a mid-session roll left the active file still over the size cap, meaning rotation is blocked.
    /// Further automatic rolls are then suppressed.
    /// </summary>
    internal bool RotationSuppressed
    {
        get { return _rotationSuppressed; }
    }

    private void EnsureInitialized()
    {
        if (_initialized || !_options.EnableFileLogging)
        {
            return;
        }

        EnsureLogDirectoryExists();
        OpenLogFile();
        WriteStartupBanner();
    }

    /// <summary>
    /// Rotates the log if it is over the size cap, then opens a fresh auto-flushing handle for the current log
    /// file. Shared by first-time setup and mid-session rollover.
    /// </summary>
    private void OpenLogFile()
    {
        RotateLogsIfNeeded();

        // Opens a single shared, auto-flushing handle for the session.
        // FileShare.ReadWrite lets other processes (e.g. a second instance) read or append concurrently.
        var stream = new FileStream(_logFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream) { AutoFlush = true };

        // Latch only after the handle is open. If the open above throws, WriteLog's catch swallows it but
        // _initialized stays false, so the next write retries. Setting it here also precedes the startup banner,
        // whose nested WriteLog re-enters EnsureInitialized and would otherwise recurse.
        _initialized = true;
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
                if (_writer is null)
                {
                    return;
                }

                _writer.Write(message);

                // Once the active file passes the limit, roll it over. Skip if rotation is known to be blocked
                // (see RollActiveLog).
                if (!_rotationSuppressed && _writer.BaseStream.Length >= _options.MaxFileSizeBytes)
                {
                    RollActiveLog();
                }
            }
            catch
            {
                // Silently fail if we can't write to the log file
            }
        }
    }

    /// <summary>
    /// Closes the active log file once it reaches the size cap and reopens a fresh one. Called only from
    /// <see cref="WriteLog"/> while holding <see cref="_writeLock"/>.
    /// </summary>
    private void RollActiveLog()
    {
        _writer?.Flush();
        _writer?.Dispose();
        _writer = null;

        // Reopen through the shared path. If the reopen throws, _initialized stays false so the next write
        // re-initializes.
        _initialized = false;
        OpenLogFile();

        // If the reopened handle is still at or over the cap, RotateLogsIfNeeded couldn't move the active file
        // (e.g. another process holds it open). Suppress further automatic rolls for the session.
        if (_writer is not null && _writer.BaseStream.Length >= _options.MaxFileSizeBytes)
        {
            _rotationSuppressed = true;
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

            var directory = Path.GetDirectoryName(_logFilePath) ?? ".";
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_logFilePath);
            var extension = Path.GetExtension(_logFilePath);

            var oldestLog = Path.Combine(directory, $"{fileNameWithoutExt}.{_options.RetainedFileCount}{extension}");
            if (File.Exists(oldestLog))
            {
                File.Delete(oldestLog);
            }

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

        lock (_writeLock)
        {
            _writer?.Dispose();
            _writer = null;
        }

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
