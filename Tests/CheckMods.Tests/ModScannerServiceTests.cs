using CheckMods.Configuration;
using CheckMods.Models;
using CheckMods.Services;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SemanticVersioning;

namespace CheckMods.Tests;

/// <summary>
/// Tests for <see cref="ModScannerService"/>'s filesystem scanning behavior.
/// </summary>
public sealed class ModScannerServiceTests
{
    [Fact]
    public async Task ScanAllModsAsync_keeps_server_mods_when_plugins_directory_is_missing()
    {
        var sptPath = TempWorkspace.CreateDirectory("checkmods-server-only-spt");
        var reporter = new RecordingModCheckReporter();

        try
        {
            var serverModDirectory = Path.Combine(sptPath, "SPT", "user", "mods", "ServerOnlyMod");
            Directory.CreateDirectory(serverModDirectory);
            File.Copy(typeof(ServerOnlyMetadata).Assembly.Location, Path.Combine(serverModDirectory, "ServerOnlyMod.dll"));

            var logger = new RecordingLogger<ModScannerService>();
            var service = new ModScannerService(Options.Create(new ModScannerOptions()), reporter, logger);

            var (serverMods, clientMods) = await service.ScanAllModsAsync(sptPath);

            var serverMod = Assert.Single(serverMods);
            Assert.True(serverMod.IsServerMod);
            Assert.Equal("com.checkmods.serveronly", serverMod.Guid);
            Assert.Equal("Server Only Mod", serverMod.LocalName);
            Assert.Equal("CheckMods", serverMod.LocalAuthor);
            Assert.Equal("1.2.3", serverMod.LocalVersion);
            Assert.Empty(clientMods);
            Assert.Contains(Path.Combine(sptPath, "BepInEx", "plugins"), reporter.MissingPluginDirectories);
            Assert.Contains(logger.Entries, entry =>
                entry.Level == LogLevel.Information
                && entry.Message.Contains("skipping client-mod scan", StringComparison.Ordinal)
            );
            Assert.DoesNotContain(logger.Entries, entry =>
                entry.Level >= LogLevel.Warning
                && entry.Message.Contains("BepInEx plugins directory not found", StringComparison.Ordinal)
            );
        }
        finally
        {
            Directory.Delete(sptPath, recursive: true);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class RecordingModCheckReporter : IModCheckReporter
    {
        public List<string> MissingPluginDirectories { get; } = [];

        public void Banner() { }
        public void Rule() { }
        public void Blank() { }
        public void Heading(string text) { }
        public void Status(string text) { }
        public void Success(string text) { }
        public void Warning(string text) { }
        public void Error(string text) { }
        public void CouldNotReadModDll(string fileName, string reason) { }
        public void CouldNotReadSptVersion(string reason) { }
        public void PluginsDirectoryNotFound(string path) => MissingPluginDirectories.Add(path);
        public Task RunForgeQueryProgressAsync(int total, Func<Action<int>, Task> work) => work(_ => { });
        public Task<T> RunForgeQueryProgressAsync<T>(int total, Func<Action<int>, Task<T>> work) => work(_ => { });
        public void UsingPath(string path) { }
        public void DirectoryDoesNotExist(string path) { }
        public void ValidatingSptVersion(string version) { }
        public void SptVersionValidated(string version) { }
        public void SptUpdateAvailable(SptVersionResult latest) { }
        public void CheckModsUpdate(CheckModsUpdateResult result, SemanticVersioning.Version sptVersion) { }
        public void NoModsFound() { }
        public void VersionCompatibilityResults(List<Mod> mods, SemanticVersioning.Version sptVersion) { }
        public void Exception(Exception ex) { }
        public void LoadingWarnings(List<Mod> modsWithWarnings) { }
        public void ReconciliationResults(ModReconciliationResult result) { }
        public void MisplacedMods(MisplacedModReport report) { }
        public void UnverifiedMods(List<Mod> mods) { }
        public void DependencyResults(DependencyAnalysisResult result) { }
        public void VersionTable(List<Mod> mods) { }
        public void RemoteIgnoresMerged(int added) { }
        public void RemoteIgnoresUnavailable() { }
        public void UpdatePagesOpened(int opened, int total) { }
        public void IgnoreReportOpened(string url, bool browserOpened, bool prefilled) { }
        public EndOfRunChoice PromptEndOfRun(int openableUpdateCount, bool canManageIgnoredUpdates) => EndOfRunChoice.Exit;
        public IReadOnlyList<Mod> SelectUpdatesToIgnore(IReadOnlyList<Mod> candidates, ISet<int> preIgnoredApiModIds) => [];
        public bool PromptFetchRemoteIgnores() => false;
        public bool PromptReportIgnores() => false;
    }
}

public abstract class AbstractModMetadata
{
    public abstract string ModGuid { get; }
    public abstract string Name { get; }
    public abstract string Author { get; }
    public abstract string Version { get; }
    public abstract string SptVersion { get; }
}

public sealed class ServerOnlyMetadata : AbstractModMetadata
{
    public override string ModGuid => "com.checkmods.serveronly";
    public override string Name => "Server Only Mod";
    public override string Author => "CheckMods";
    public override string Version => "1.2.3";
    public override string SptVersion => "4.0.0";
}
