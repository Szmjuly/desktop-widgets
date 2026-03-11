namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Service for managing the shared registry of custom tag KEY names.
/// When a user creates a new custom tag key (e.g., "Permit Submitted"),
/// it's encrypted and written to Firebase so all users can discover it.
/// Only stores key names — values are per-project and stay in project_tags.
/// </summary>
public interface ITagRegistryService
{
    /// <summary>
    /// Initialize: load local cache, sync from Firebase in background.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Get all known custom tag key names (union of local + Firebase-synced).
    /// </summary>
    IReadOnlySet<string> GetAllKeys();

    /// <summary>
    /// Register a new custom tag key so all users can discover it.
    /// Writes encrypted key to Firebase + local cache. No-op if already known.
    /// </summary>
    Task RegisterKeyAsync(string tagKey);

    /// <summary>
    /// Register multiple custom tag keys at once (batch after a tag save).
    /// </summary>
    Task RegisterKeysAsync(IEnumerable<string> tagKeys);

    /// <summary>
    /// Sync local cache from Firebase (full refresh).
    /// </summary>
    Task SyncFromFirebaseAsync();
}
