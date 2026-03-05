using DesktopHub.Core.Models;

namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Service for managing project tags stored in Firebase RTDB with HMAC-hashed keys.
/// Tags are cached locally for fast search.
/// </summary>
public interface IProjectTagService
{
    /// <summary>
    /// Initialize the service: load local cache, optionally sync from Firebase.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Get tags for a project by its full number. Returns null if no tags exist.
    /// Reads from local cache first, falls back to Firebase.
    /// </summary>
    Task<ProjectTags?> GetTagsAsync(string projectNumber);

    /// <summary>
    /// Save or update tags for a project. Writes to both Firebase and local cache.
    /// </summary>
    Task SaveTagsAsync(string projectNumber, ProjectTags tags);

    /// <summary>
    /// Delete all tags for a project.
    /// </summary>
    Task DeleteTagsAsync(string projectNumber);

    /// <summary>
    /// Get all cached tags as a dictionary of project number → tags.
    /// Used for search filtering.
    /// </summary>
    IReadOnlyDictionary<string, ProjectTags> GetAllCachedTags();

    /// <summary>
    /// Sync local cache from Firebase (full refresh).
    /// Returns the number of project tag entries synced.
    /// </summary>
    Task<int> SyncFromFirebaseAsync();

    /// <summary>
    /// Check if a project has any tags.
    /// </summary>
    bool HasTags(string projectNumber);

    /// <summary>
    /// Get the tag value for a specific field on a project.
    /// Resolves aliases (e.g. "v" → "voltage").
    /// Returns null if no match.
    /// </summary>
    string? GetTagValue(string projectNumber, string fieldKeyOrAlias);

    /// <summary>
    /// Search all cached projects for those matching a set of tag filters.
    /// Each filter is a (resolvedKey, value) pair with fuzzy value matching.
    /// Returns project numbers that match ALL filters.
    /// </summary>
    List<string> SearchByTags(List<(string key, string value)> filters);

    /// <summary>
    /// Parse a tag query segment like "voltage:208" into (resolvedKey, value).
    /// Returns null if the segment doesn't match a known tag field.
    /// </summary>
    (string key, string value)? ParseTagFilter(string segment);
}
