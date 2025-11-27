using System.Text.Json.Serialization;

namespace CheckMods.Models;

/// <summary>
/// Response from the Forge API authentication abilities endpoint.
/// </summary>
public record AuthAbilitiesResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] List<string>? Data
);

/// <summary>
/// Response from the Forge API mod search endpoint.
/// </summary>
public record ModSearchApiResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] List<ModSearchResult>? Data
);

/// <summary>
/// Represents a mod search result from the Forge API.
/// </summary>
public record ModSearchResult(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("hub_id")] int? HubId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("teaser")] string? Teaser,
    [property: JsonPropertyName("thumbnail")] string? Thumbnail,
    [property: JsonPropertyName("downloads")] int Downloads,
    [property: JsonPropertyName("source_code_links")] List<SourceCodeLink>? SourceCodeLinks,
    [property: JsonPropertyName("detail_url")] string? DetailUrl,
    [property: JsonPropertyName("owner")] ModAuthor? Owner,
    [property: JsonPropertyName("versions")] List<ModVersion>? Versions
)
{
    /// <summary>
    /// Gets the primary source code URL (first link if available).
    /// </summary>
    public string? SourceCodeUrl
    {
        get { return SourceCodeLinks?.FirstOrDefault()?.Url; }
    }
}

/// <summary>
/// Represents the author/owner of a mod.
/// </summary>
public record ModAuthor(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("profile_photo_url")] string? ProfilePhotoUrl
);

/// <summary>
/// Represents a link to a source code repository.
/// </summary>
public record SourceCodeLink(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("label")] string? Label
);

/// <summary>
/// Represents a specific version of a mod.
/// </summary>
public record ModVersion(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("hub_id")] int? HubId,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("link")] string? Link,
    [property: JsonPropertyName("spt_version_constraint")] string SptVersionConstraint,
    [property: JsonPropertyName("virus_total_link")] string? VirusTotalLink,
    [property: JsonPropertyName("downloads")] int Downloads,
    [property: JsonPropertyName("published_at")] string? PublishedAt,
    [property: JsonPropertyName("created_at")] string? CreatedAt,
    [property: JsonPropertyName("updated_at")] string? UpdatedAt
);

/// <summary>
/// Response from the Forge API mod versions' endpoint.
/// </summary>
public record ModVersionsApiResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] List<ModVersion>? Data
);

#region Batch Updates Endpoint Models

/// <summary>
/// Response from the Forge API batch mod updates endpoint.
/// </summary>
public record ModUpdatesApiResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] ModUpdatesData? Data
);

/// <summary>
/// Categorized mod update information from the batch updates endpoint.
/// </summary>
public record ModUpdatesData(
    [property: JsonPropertyName("updates")] List<SafeToUpdateMod>? SafeToUpdate,
    [property: JsonPropertyName("blocked_updates")] List<BlockedUpdateMod>? Blocked,
    [property: JsonPropertyName("up_to_date")] List<UpToDateMod>? UpToDate,
    [property: JsonPropertyName("incompatible_with_spt")] List<IncompatibleMod>? Incompatible
);

/// <summary>
/// Version information containing version string and metadata.
/// </summary>
public record ModVersionInfo2(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("mod_id")] int ModId,
    [property: JsonPropertyName("guid")] string? Guid,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("slug")] string? Slug,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("link")] string? Link,
    [property: JsonPropertyName("spt_versions")] List<string>? SptVersions
);

/// <summary>
/// A mod that has an update available and is safe to update.
/// </summary>
public record SafeToUpdateMod(
    [property: JsonPropertyName("current_version")] ModVersionInfo2? CurrentVersion,
    [property: JsonPropertyName("recommended_version")] ModVersionInfo2? RecommendedVersion,
    [property: JsonPropertyName("update_reason")] string? UpdateReason
)
{
    /// <summary>
    /// Helper property to get the mod ID from current version.
    /// </summary>
    public int ModId
    {
        get { return CurrentVersion?.ModId ?? 0; }
    }
}

/// <summary>
/// A mod that has an update available but is blocked by dependency constraints.
/// </summary>
public record BlockedUpdateMod(
    [property: JsonPropertyName("current_version")] ModVersionInfo2? CurrentVersion,
    [property: JsonPropertyName("latest_version")] ModVersionInfo2? LatestVersion,
    [property: JsonPropertyName("block_reason")] string? BlockReason,
    [property: JsonPropertyName("blocking_mods")] List<BlockingModInfo>? BlockingMods
)
{
    /// <summary>
    /// Helper property to get the mod ID from current version.
    /// </summary>
    public int ModId
    {
        get { return CurrentVersion?.ModId ?? 0; }
    }
}

/// <summary>
/// Information about a mod that is blocking an update due to dependency constraints.
/// </summary>
public record BlockingModInfo(
    [property: JsonPropertyName("mod_id")] int ModId,
    [property: JsonPropertyName("mod_guid")] string? ModGuid,
    [property: JsonPropertyName("mod_name")] string Name,
    [property: JsonPropertyName("current_version")] string? CurrentVersion,
    [property: JsonPropertyName("constraint")] string Constraint,
    [property: JsonPropertyName("incompatible_with")] string? IncompatibleWith
);

/// <summary>
/// A mod that is already up to date.
/// </summary>
public record UpToDateMod(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("mod_id")] int ModId,
    [property: JsonPropertyName("guid")] string? Guid,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("spt_versions")] List<string>? SptVersions
);

/// <summary>
/// A mod that has no compatible version for the current SPT version.
/// </summary>
public record IncompatibleMod(
    [property: JsonPropertyName("id")] int? Id,
    [property: JsonPropertyName("mod_id")] int ModId,
    [property: JsonPropertyName("guid")] string? Guid,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("latest_compatible_version")] ModVersionInfo2? LatestCompatibleVersion
);

#endregion

#region Dependencies Endpoint Models

/// <summary>
/// Response from the Forge API mod dependencies endpoint.
/// </summary>
public record ModDependenciesApiResponse(
    [property: JsonPropertyName("success")] bool Success,
    [property: JsonPropertyName("data")] List<ModDependency>? Data
);

/// <summary>
/// Represents a dependency from the Forge API dependencies endpoint.
/// </summary>
public record ModDependency(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("guid")] string Guid,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("slug")] string Slug,
    [property: JsonPropertyName("latest_compatible_version")] DependencyVersionInfo? LatestCompatibleVersion,
    [property: JsonPropertyName("conflict")] bool Conflict,
    [property: JsonPropertyName("dependencies")] List<ModDependency>? Dependencies
);

/// <summary>
/// Version information for a dependency.
/// </summary>
public record DependencyVersionInfo(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("link")] string? Link,
    [property: JsonPropertyName("content_length")] long? ContentLength,
    [property: JsonPropertyName("fika_compatibility")] string? FikaCompatibility
);

#endregion
