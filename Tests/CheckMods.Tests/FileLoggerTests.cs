using CheckMods.Configuration;
using CheckMods.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="FileLoggerProvider"/>'s file handling: that a transient open failure doesn't permanently
/// disable logging, and that the active log is rotated mid-session once it crosses the size cap. Each test writes to
/// an isolated temp directory via the injectable <see cref="LoggingOptions.LogFilePath"/>.
/// </summary>
public sealed class FileLoggerTests
{
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "checkmods-filelogger-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void SafeDeleteDir(string dir)
    {
        try
        {
            Directory.Delete(dir, recursive: true);
        }
        catch (IOException)
        {
            // Best effort - a lingering handle shouldn't fail the test.
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort.
        }
    }

    private static FileLoggerProvider CreateProvider(string logPath, Action<LoggingOptions>? configure = null)
    {
        var options = new LoggingOptions { LogFilePath = logPath, MinimumLogLevel = LogLevel.Debug };
        configure?.Invoke(options);
        return new FileLoggerProvider(Options.Create(options));
    }

    [Fact]
    public void Writes_log_entries_to_the_configured_file()
    {
        var dir = CreateTempDir();
        try
        {
            var logPath = Path.Combine(dir, "test.log");
            var provider = CreateProvider(logPath);

            provider.CreateLogger("Cat").LogInformation("hello world");
            provider.Dispose();

            Assert.Contains("hello world", File.ReadAllText(logPath));
        }
        finally
        {
            SafeDeleteDir(dir);
        }
    }

    [Fact]
    public void Does_not_create_a_log_file_when_file_logging_is_disabled()
    {
        var dir = CreateTempDir();
        try
        {
            var logPath = Path.Combine(dir, "test.log");
            var provider = CreateProvider(logPath, o => o.EnableFileLogging = false);

            provider.CreateLogger("Cat").LogInformation("should not be written");
            provider.Dispose();

            Assert.False(File.Exists(logPath));
        }
        finally
        {
            SafeDeleteDir(dir);
        }
    }

    [Fact]
    public void Retries_initialization_after_a_transient_open_failure()
    {
        var dir = CreateTempDir();
        var logPath = Path.Combine(dir, "test.log");
        var provider = CreateProvider(logPath);
        try
        {
            // Hold the log file with an exclusive lock so the logger's first open attempt fails and is swallowed.
            using (new FileStream(logPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                provider.CreateLogger("Cat").LogInformation("written while the file is locked");
            }

            // Lock released: a later write must retry and succeed rather than staying permanently dead.
            provider.CreateLogger("Cat").LogInformation("written after the lock is released");
            provider.Dispose();

            Assert.Contains("written after the lock is released", File.ReadAllText(logPath));
        }
        finally
        {
            provider.Dispose();
            SafeDeleteDir(dir);
        }
    }

    [Fact]
    public void Rotates_the_active_log_when_it_exceeds_the_size_cap_mid_session()
    {
        var dir = CreateTempDir();
        try
        {
            var logPath = Path.Combine(dir, "test.log");
            var rotatedPath = Path.Combine(dir, "test.1.log");

            using (var provider = CreateProvider(logPath, o => o.MaxFileSizeBytes = 500))
            {
                var logger = provider.CreateLogger("Cat");
                for (var i = 0; i < 200; i++)
                {
                    logger.LogInformation("Log entry {Index} with enough text to accumulate bytes quickly", i);
                }
            }

            // A rotated file only appears if rotation ran during the session (not just at startup on a fresh file).
            Assert.True(File.Exists(rotatedPath), "expected a rotated log file from mid-session rotation");

            // The active log was rolled, so it stays bounded near the cap instead of growing with every entry.
            Assert.True(new FileInfo(logPath).Length <= 500 + 1024, "active log should be bounded near the size cap");
        }
        finally
        {
            SafeDeleteDir(dir);
        }
    }

    [Fact]
    public void Suppresses_further_rolls_when_rotation_is_blocked()
    {
        var dir = CreateTempDir();
        try
        {
            var logPath = Path.Combine(dir, "test.log");

            // Occupy the rotation target with a directory. File.Exists is false for a directory so the shift logic
            // skips it, but File.Move(active -> test.1.log) fails because the path is taken. This mimics a rotation
            // that can't move the active file aside (e.g. the file held open by another process).
            Directory.CreateDirectory(Path.Combine(dir, "test.1.log"));

            var provider = CreateProvider(logPath, o => o.MaxFileSizeBytes = 200);
            var logger = provider.CreateLogger("Cat");

            for (var i = 0; i < 100; i++)
            {
                logger.LogInformation("Log entry {Index} with enough text to accumulate bytes quickly", i);
            }

            // The first over-cap roll failed to rotate, so further per-line rolls are suppressed rather than retried
            // on every write.
            Assert.True(provider.RotationSuppressed, "a blocked rotation should suppress further roll attempts");

            // Release the active handle before reading the file back.
            provider.Dispose();

            // Logging keeps working: the latest entry is still written despite rotation being stuck.
            Assert.Contains("Log entry 99", File.ReadAllText(logPath));
        }
        finally
        {
            SafeDeleteDir(dir);
        }
    }
}
