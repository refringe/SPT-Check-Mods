using CheckMods.Models;
using CheckMods.Services.Interfaces;
using CheckMods.Utils;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using SPTarkov.DI.Annotations;

namespace CheckMods.Services;

/// <summary>
/// Service responsible for SPT installation validation.
/// </summary>
[Injectable(InjectionType.Transient)]
public sealed class SptInstallationService(
    IForgeApiService forgeApiService,
    IModScannerService scannerService,
    ILogger<SptInstallationService> logger
) : ISptInstallationService
{
    /// <inheritdoc />
    public async Task<SemanticVersioning.Version?> GetAndValidateSptVersionAsync(
        string sptPath,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Validating SPT installation at: {SptPath}", sptPath);

        var coreDllPath = Path.Combine(sptPath, "SPT", "SPTarkov.Server.Core.dll");
        if (!File.Exists(coreDllPath))
        {
            logger.LogError("SPT core DLL not found: {CoreDllPath}", coreDllPath);
            AnsiConsole.MarkupLine(
                "[red]Error: Could not find SPT installation. Run this file in your root SPT directory, or provide the SPT path as an argument.[/]"
            );
            return null;
        }

        var localSptVersionStr = scannerService.GetSptVersion(sptPath);

        if (string.IsNullOrWhiteSpace(localSptVersionStr))
        {
            logger.LogError("Could not extract SPT version from core DLL");
            AnsiConsole.MarkupLine("[red]Error: SPT version not found in SPTarkov.Server.Core.dll.[/]");
            return null;
        }

        logger.LogDebug("Found local SPT version: {SptVersion}", localSptVersionStr);

        AnsiConsole.Markup(
            $"Found local SPT version [bold blue]{localSptVersionStr}[/]. Validating with Forge API... "
        );

        var validationResult = await forgeApiService.ValidateSptVersionAsync(localSptVersionStr, cancellationToken);

        var isValid = validationResult.Match(
            valid => valid,
            _ => false, // InvalidSptVersion
            _ => false // ApiError - treat as invalid for safety
        );

        if (!isValid)
        {
            // Provide more specific error messages based on the result type
            validationResult.Switch(
                _ => { }, // Success - handled above
                _ => AnsiConsole.MarkupLine("[red]Failed. SPT version not recognized by Forge API.[/]"),
                apiError => AnsiConsole.MarkupLine($"[red]Failed. API error: {apiError.Message.EscapeMarkup()}[/]")
            );

            return null;
        }

        AnsiConsole.MarkupLine("[green]OK[/]");
        return new SemanticVersioning.Version(localSptVersionStr);
    }

    /// <inheritdoc />
    public async Task<List<SptVersionResult>> CheckForSptUpdatesAsync(
        SemanticVersioning.Version currentVersion,
        CancellationToken cancellationToken = default
    )
    {
        logger.LogDebug("Checking for SPT updates. Current version: {CurrentVersion}", currentVersion);

        var versionsResult = await forgeApiService.GetAllSptVersionsAsync(cancellationToken);

        return versionsResult.Match(
            versions =>
            {
                // Filter to versions newer than the current version, skipping any that can't be parsed.
                var newerVersions = versions
                    .Where(v => SemVer.TryParse(v.Version) is { } semVer && semVer > currentVersion)
                    .OrderByDescending(v => SemVer.ParseOrZero(v.Version))
                    .ToList();

                logger.LogDebug("Found {Count} newer SPT versions", newerVersions.Count);
                return newerVersions;
            },
            apiError =>
            {
                logger.LogWarning("Failed to check for SPT updates: {Error}", apiError.Message);
                return [];
            }
        );
    }
}
