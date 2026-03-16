using DesktopHub.Core.Models;

namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Service for managing cheat sheet data stored in Firebase RTDB with AES-256 encryption.
/// Data is cached locally for fast search and offline resilience.
/// </summary>
public interface ICheatSheetDataService
{
    /// <summary>
    /// Initialize: load local cache, background sync from Firebase.
    /// If Firebase has no data, seeds from hardcoded defaults.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Get all sheets from cache.
    /// </summary>
    /// <param name="includeDisabled">If true, includes soft-disabled sheets.</param>
    List<CheatSheet> GetCachedSheets(bool includeDisabled = false);

    /// <summary>
    /// Get a single sheet by ID from cache.
    /// </summary>
    CheatSheet? GetCachedSheet(string id);

    /// <summary>
    /// Save or update a cheat sheet. Writes to Firebase (encrypted), local cache (plaintext),
    /// records edit history, increments version counter, and exports Q-Drive backup.
    /// </summary>
    Task SaveSheetAsync(CheatSheet sheet, string editedBy);

    /// <summary>
    /// Soft-disable a sheet (hidden from non-editors).
    /// </summary>
    Task DisableSheetAsync(string id, string editedBy);

    /// <summary>
    /// Re-enable a previously disabled sheet.
    /// </summary>
    Task EnableSheetAsync(string id, string editedBy);

    /// <summary>
    /// Hard-delete a sheet (admin only). Records deletion in history.
    /// </summary>
    Task DeleteSheetAsync(string id, string editedBy);

    /// <summary>
    /// Full refresh from Firebase. Returns number of sheets synced.
    /// </summary>
    Task<int> SyncFromFirebaseAsync();

    /// <summary>
    /// Lightweight check: compare Firebase meta/version with cached version.
    /// Returns true if remote version is newer (caller should trigger SyncFromFirebaseAsync).
    /// </summary>
    Task<bool> CheckForUpdatesAsync();

    /// <summary>
    /// Get the locally cached version number (from meta/version).
    /// </summary>
    int CachedVersion { get; }

    /// <summary>
    /// Get cached code books.
    /// </summary>
    List<CodeBook> GetCachedCodeBooks();

    /// <summary>
    /// Get cached jurisdictions.
    /// </summary>
    List<JurisdictionCodeAdoption> GetCachedJurisdictions();

    /// <summary>
    /// Save or update a code book.
    /// </summary>
    Task SaveCodeBookAsync(CodeBook cb);

    /// <summary>
    /// Save or update a jurisdiction.
    /// </summary>
    Task SaveJurisdictionAsync(JurisdictionCodeAdoption j);

    /// <summary>
    /// Raised after a sync brings new data from Firebase.
    /// Subscribers (e.g. CheatSheetService, CheatSheetWidget) should refresh their views.
    /// </summary>
    event Action? DataUpdated;
}
