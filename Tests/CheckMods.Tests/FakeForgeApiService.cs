using CheckMods.Models;
using CheckMods.Services.Interfaces;
using OneOf;

namespace CheckMods.Tests;

/// <summary>
/// In-memory <see cref="IForgeApiService"/> test double. Only the handlers a given test needs are configured; the
/// rest return benign "not found"/empty results, and the unused endpoints throw so accidental reliance is obvious.
/// </summary>
internal sealed class FakeForgeApiService : IForgeApiService
{
    /// <summary>Handler for GUID lookups. Defaults to NotFound. May throw to simulate an unexpected failure.</summary>
    public Func<string, OneOf<ModSearchResult, NotFound, NoCompatibleVersion, ApiError>>? OnGetModByGuid { get; set; }

    /// <summary>Handler for name searches (server and client). Defaults to an empty result list.</summary>
    public Func<string, OneOf<List<ModSearchResult>, ApiError>>? OnSearch { get; set; }

    /// <summary>Handler for the batch updates endpoint. Required by tests that call it.</summary>
    public Func<OneOf<ModUpdatesData, NotFound, ApiError>>? OnGetModUpdates { get; set; }

    /// <summary>Handler for by-ID lookups. Required by tests that call it.</summary>
    public Func<int, OneOf<ModSearchResult, NotFound, InvalidInput, ApiError>>? OnGetModById { get; set; }

    /// <summary>Handler for the dependencies endpoint, keyed by the first requested identifier (the mod ID).</summary>
    public Func<string, OneOf<List<ModDependency>, NotFound, ApiError>>? OnGetModDependencies { get; set; }

    public Task<OneOf<ModSearchResult, NotFound, NoCompatibleVersion, ApiError>> GetModByGuidAsync(
        string modGuid,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        OneOf<ModSearchResult, NotFound, NoCompatibleVersion, ApiError> result =
            OnGetModByGuid is not null ? OnGetModByGuid(modGuid) : new NotFound();
        return Task.FromResult(result);
    }

    public Task<OneOf<List<ModSearchResult>, ApiError>> SearchModsAsync(
        string modName,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        OneOf<List<ModSearchResult>, ApiError> result =
            OnSearch is not null ? OnSearch(modName) : new List<ModSearchResult>();
        return Task.FromResult(result);
    }

    public Task<OneOf<List<ModSearchResult>, ApiError>> SearchClientModsAsync(
        string modName,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        return SearchModsAsync(modName, sptVersion, cancellationToken);
    }

    // Endpoints not exercised by the matching tests.
    public Task<OneOf<bool, InvalidSptVersion, ApiError>> ValidateSptVersionAsync(
        string sptVersion,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    public Task<OneOf<List<SptVersionResult>, ApiError>> GetAllSptVersionsAsync(
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    public Task<OneOf<ModSearchResult, NotFound, InvalidInput, ApiError>> GetModByIdAsync(
        int modId,
        CancellationToken cancellationToken = default
    )
    {
        if (OnGetModById is null)
        {
            throw new NotSupportedException();
        }

        return Task.FromResult(OnGetModById(modId));
    }

    public Task<OneOf<ModUpdatesData, NotFound, ApiError>> GetModUpdatesAsync(
        IEnumerable<(int ModId, string CurrentVersion)> modUpdates,
        SemanticVersioning.Version sptVersion,
        CancellationToken cancellationToken = default
    )
    {
        if (OnGetModUpdates is null)
        {
            throw new NotSupportedException();
        }

        return Task.FromResult(OnGetModUpdates());
    }

    public Task<OneOf<List<ModDependency>, NotFound, ApiError>> GetModDependenciesAsync(
        IEnumerable<(string Identifier, string Version)> modVersions,
        CancellationToken cancellationToken = default
    )
    {
        if (OnGetModDependencies is null)
        {
            throw new NotSupportedException();
        }

        var identifier = modVersions.Select(m => m.Identifier).FirstOrDefault() ?? string.Empty;
        return Task.FromResult(OnGetModDependencies(identifier));
    }
}
