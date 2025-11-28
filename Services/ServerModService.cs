using CheckMods.Models;
using CheckMods.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using SPTarkov.DI.Annotations;

namespace CheckMods.Services;

/// <summary>
/// Service responsible for SPT installation validation.
/// </summary>
[Injectable(InjectionType.Transient)]
public sealed class ServerModService(
    IForgeApiService forgeApiService,
    IModScannerService scannerService,
    ILogger<ServerModService> logger
) : IServerModService
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
                // Filter to versions newer than the current version
                var newerVersions = versions
                    .Where(v =>
                    {
                        try
                        {
                            var semVer = new SemanticVersioning.Version(v.Version);
                            return semVer > currentVersion;
                        }
                        catch
                        {
                            // Skip versions that can't be parsed
                            return false;
                        }
                    })
                    .OrderByDescending(v =>
                    {
                        try
                        {
                            return new SemanticVersioning.Version(v.Version);
                        }
                        catch
                        {
                            return new SemanticVersioning.Version(0, 0, 0);
                        }
                    })
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
