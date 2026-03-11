namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Service for providing dropdown/autocomplete suggestions for tag fields.
/// Derives suggestions locally from TagFieldRegistry defaults + values seen
/// in the local project_tags cache. No data is written to Firebase.
/// </summary>
public interface ITagVocabularyService
{
    /// <summary>
    /// Get all known values for a tag field key (e.g., "voltage" → ["120", "208", "240", ...]).
    /// Merges seed defaults from TagFieldRegistry + values observed in the local tag cache.
    /// Sorted alphabetically.
    /// </summary>
    List<string> GetValues(string fieldKey);

    /// <summary>
    /// Rebuild suggestions by scanning the local project_tags cache for unique values per field.
    /// Call after tag service initialization or after saving tags.
    /// </summary>
    void RefreshFromCache(IReadOnlyDictionary<string, Models.ProjectTags> cachedTags);
}
