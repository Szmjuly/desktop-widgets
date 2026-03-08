namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Service for managing the shared tag vocabulary — the master list of known values
/// for each tag field. Backed by Firebase RTDB so all users share the same vocabulary.
/// When a user enters a custom value, it's automatically added to the vocabulary.
/// </summary>
public interface ITagVocabularyService
{
    /// <summary>
    /// Initialize: load local cache, sync from Firebase in background.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Get all known values for a tag field key (e.g., "voltage" → ["120", "208", "240", ...]).
    /// Merges seed defaults + Firebase-synced custom values. Sorted alphabetically.
    /// </summary>
    List<string> GetValues(string fieldKey);

    /// <summary>
    /// Add a custom value to a tag field's vocabulary. Writes to Firebase + local cache.
    /// No-op if the value already exists.
    /// </summary>
    Task AddValueAsync(string fieldKey, string value);

    /// <summary>
    /// Add multiple custom values at once (batch after a tag save).
    /// </summary>
    Task AddValuesAsync(string fieldKey, IEnumerable<string> values);

    /// <summary>
    /// Sync local cache from Firebase (full refresh).
    /// </summary>
    Task SyncFromFirebaseAsync();
}
