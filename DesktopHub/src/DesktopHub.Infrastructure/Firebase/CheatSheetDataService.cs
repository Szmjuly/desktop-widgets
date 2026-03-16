using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Firebase.Utilities;
using DesktopHub.Infrastructure.Logging;
using Newtonsoft.Json;

namespace DesktopHub.Infrastructure.Firebase;

/// <summary>
/// Firebase-backed cheat sheet data service with AES-256 encryption, local JSON cache,
/// version-based polling, unlimited edit history, and Q-Drive backup.
/// </summary>
public class CheatSheetDataService : ICheatSheetDataService
{
    private const string FirebaseRoot = "cheat_sheet_data";
    private const string QDriveBackupPath = @"Q:\_Resources\Programs\DesktopHub\cheatsheets_backup.json";

    private readonly IFirebaseService _firebase;
    private readonly string _cacheFilePath;
    private readonly string _username;

    private readonly ConcurrentDictionary<string, SheetEntry> _sheets = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, CodeBook> _codeBooks = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, JurisdictionCodeAdoption> _jurisdictions = new(StringComparer.OrdinalIgnoreCase);
    private int _cachedVersion;
    private bool _initialized;
    private System.Threading.Timer? _pollTimer;

    private static readonly JsonSerializerOptions SysJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public int CachedVersion => _cachedVersion;
    public event Action? DataUpdated;

    public CheatSheetDataService(IFirebaseService firebase)
    {
        _firebase = firebase;
        _username = Environment.UserName.ToLowerInvariant();

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopHub");
        Directory.CreateDirectory(appDataDir);
        _cacheFilePath = Path.Combine(appDataDir, "cheatsheet_data_cache.json");
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            LoadLocalCache();
            InfraLogger.Log($"CheatSheetDataService: Loaded {_sheets.Count} cached sheets, version={_cachedVersion}");

            _initialized = true;

            // Background sync from Firebase (non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    if (!_firebase.IsInitialized)
                    {
                        InfraLogger.Log("CheatSheetDataService: Firebase not initialized, using local cache only");
                        return;
                    }

                    // Check if Firebase has any data; if not, seed from hardcoded defaults
                    var remoteVersion = await GetFirebaseDataAsync<object>($"{FirebaseRoot}/meta/version");
                    if (remoteVersion == null)
                    {
                        InfraLogger.Log("CheatSheetDataService: No remote data found, seeding from defaults...");
                        await SeedFromDefaultsAsync();
                    }
                    else
                    {
                        var count = await SyncFromFirebaseAsync();
                        InfraLogger.Log($"CheatSheetDataService: Background sync completed, {count} sheets");
                    }

                    // Start polling timer — check for version changes every 5 minutes
                    _pollTimer = new System.Threading.Timer(
                        async _ => await PollForUpdatesAsync(),
                        null,
                        TimeSpan.FromMinutes(5),
                        TimeSpan.FromMinutes(5));
                    InfraLogger.Log("CheatSheetDataService: Version polling started (every 5 min)");
                }
                catch (Exception ex)
                {
                    InfraLogger.Log($"CheatSheetDataService: Background sync failed: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: Initialization failed: {ex.Message}");
            _initialized = true; // Mark initialized to avoid re-trying
        }

        await Task.CompletedTask;
    }

    // --- Public API ---

    public List<CheatSheet> GetCachedSheets(bool includeDisabled = false)
    {
        return _sheets.Values
            .Where(e => includeDisabled || e.Enabled)
            .Select(e => e.Sheet)
            .ToList();
    }

    public CheatSheet? GetCachedSheet(string id)
    {
        return _sheets.TryGetValue(id, out var entry) ? entry.Sheet : null;
    }

    public List<CodeBook> GetCachedCodeBooks()
        => _codeBooks.Values.ToList();

    public List<JurisdictionCodeAdoption> GetCachedJurisdictions()
        => _jurisdictions.Values.ToList();

    public async Task SaveSheetAsync(CheatSheet sheet, string editedBy)
    {
        if (string.IsNullOrWhiteSpace(sheet.Id))
            return;

        // Capture previous state for history
        CheatSheet? previous = null;
        if (_sheets.TryGetValue(sheet.Id, out var existing))
            previous = existing.Sheet;

        var action = previous == null ? "created" : "updated";
        var diffSummary = previous == null
            ? $"Created sheet '{sheet.Title}'"
            : BuildDiffSummary(previous, sheet);

        // Update local cache immediately
        _sheets[sheet.Id] = new SheetEntry { Sheet = sheet, Enabled = true };
        SaveLocalCache();

        if (!_firebase.IsInitialized)
        {
            InfraLogger.Log("CheatSheetDataService: Firebase not initialized, saved to local cache only");
            return;
        }

        try
        {
            // Encrypt and write sheet data
            var sheetJson = System.Text.Json.JsonSerializer.Serialize(sheet, SysJsonOptions);
            var encrypted = TagValueEncryptor.Encrypt(sheetJson);

            await PatchFirebaseDataAsync($"{FirebaseRoot}/sheets/{sheet.Id}", new Dictionary<string, object>
            {
                ["data"] = encrypted ?? sheetJson,
                ["enabled"] = true,
                ["updated_by"] = TagValueEncryptor.Encrypt(editedBy) ?? editedBy,
                ["updated_at"] = DateTime.UtcNow.ToString("o")
            });

            // Write history entry
            await WriteHistoryAsync(sheet.Id, action, editedBy, previous, diffSummary);

            // Increment version counter
            await IncrementVersionAsync(editedBy);

            InfraLogger.Log($"CheatSheetDataService: Saved sheet '{sheet.Id}' by {editedBy}");

            // Q-Drive backup (non-blocking)
            _ = Task.Run(() => ExportQDriveBackup());
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: Firebase save failed (cached locally): {ex.Message}");
        }
    }

    public async Task DisableSheetAsync(string id, string editedBy)
    {
        if (!_sheets.TryGetValue(id, out var entry)) return;

        entry.Enabled = false;
        SaveLocalCache();

        if (!_firebase.IsInitialized) return;

        try
        {
            await PatchFirebaseDataAsync($"{FirebaseRoot}/sheets/{id}", new Dictionary<string, object>
            {
                ["enabled"] = false,
                ["updated_by"] = TagValueEncryptor.Encrypt(editedBy) ?? editedBy,
                ["updated_at"] = DateTime.UtcNow.ToString("o")
            });
            await WriteHistoryAsync(id, "disabled", editedBy, entry.Sheet, $"Disabled sheet '{entry.Sheet.Title}'");
            await IncrementVersionAsync(editedBy);
            InfraLogger.Log($"CheatSheetDataService: Disabled sheet '{id}'");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: Disable failed: {ex.Message}");
        }
    }

    public async Task EnableSheetAsync(string id, string editedBy)
    {
        if (!_sheets.TryGetValue(id, out var entry)) return;

        entry.Enabled = true;
        SaveLocalCache();

        if (!_firebase.IsInitialized) return;

        try
        {
            await PatchFirebaseDataAsync($"{FirebaseRoot}/sheets/{id}", new Dictionary<string, object>
            {
                ["enabled"] = true,
                ["updated_by"] = TagValueEncryptor.Encrypt(editedBy) ?? editedBy,
                ["updated_at"] = DateTime.UtcNow.ToString("o")
            });
            await WriteHistoryAsync(id, "enabled", editedBy, entry.Sheet, $"Enabled sheet '{entry.Sheet.Title}'");
            await IncrementVersionAsync(editedBy);
            InfraLogger.Log($"CheatSheetDataService: Enabled sheet '{id}'");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: Enable failed: {ex.Message}");
        }
    }

    public async Task DeleteSheetAsync(string id, string editedBy)
    {
        CheatSheet? previous = null;
        if (_sheets.TryRemove(id, out var removed))
            previous = removed.Sheet;

        SaveLocalCache();

        if (!_firebase.IsInitialized) return;

        try
        {
            await WriteHistoryAsync(id, "deleted", editedBy, previous, $"Deleted sheet '{previous?.Title ?? id}'");
            await DeleteFirebaseDataAsync($"{FirebaseRoot}/sheets/{id}");
            await IncrementVersionAsync(editedBy);
            InfraLogger.Log($"CheatSheetDataService: Deleted sheet '{id}'");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: Delete failed: {ex.Message}");
        }
    }

    public async Task<int> SyncFromFirebaseAsync()
    {
        if (!_firebase.IsInitialized) return 0;

        try
        {
            // Read remote version
            var versionObj = await GetFirebaseDataAsync<object>($"{FirebaseRoot}/meta/version");
            if (versionObj != null && int.TryParse(versionObj.ToString(), out var remoteVersion))
                _cachedVersion = remoteVersion;

            // Read all sheets
            var allSheets = await GetFirebaseDataAsync<Dictionary<string, Dictionary<string, object>>>($"{FirebaseRoot}/sheets");
            if (allSheets == null) return 0;

            var synced = 0;
            foreach (var (sheetId, entry) in allSheets)
            {
                try
                {
                    if (!entry.ContainsKey("data")) continue;

                    var dataObj = entry["data"];
                    var encryptedBlob = dataObj?.ToString();
                    if (string.IsNullOrEmpty(encryptedBlob)) continue;

                    var decrypted = TagValueEncryptor.Decrypt(encryptedBlob);
                    if (string.IsNullOrEmpty(decrypted)) continue;

                    var sheet = System.Text.Json.JsonSerializer.Deserialize<CheatSheet>(decrypted, SysJsonOptions);
                    if (sheet == null) continue;

                    var enabled = true;
                    if (entry.TryGetValue("enabled", out var enabledObj))
                    {
                        if (enabledObj is bool b) enabled = b;
                        else if (bool.TryParse(enabledObj?.ToString(), out var parsed)) enabled = parsed;
                    }

                    _sheets[sheetId] = new SheetEntry { Sheet = sheet, Enabled = enabled };
                    synced++;
                }
                catch (Exception ex)
                {
                    InfraLogger.Log($"CheatSheetDataService: Failed to parse sheet '{sheetId}': {ex.Message}");
                }
            }

            // Sync code books
            var codeBooks = await GetFirebaseDataAsync<Dictionary<string, string>>($"{FirebaseRoot}/code_books");
            if (codeBooks != null)
            {
                foreach (var (id, encrypted) in codeBooks)
                {
                    try
                    {
                        var json = TagValueEncryptor.Decrypt(encrypted);
                        if (string.IsNullOrEmpty(json)) continue;
                        var cb = System.Text.Json.JsonSerializer.Deserialize<CodeBook>(json, SysJsonOptions);
                        if (cb != null) _codeBooks[id] = cb;
                    }
                    catch (Exception ex)
                    {
                        InfraLogger.Log($"CheatSheetDataService: Failed to parse code book '{id}': {ex.Message}");
                    }
                }
            }

            // Sync jurisdictions
            var jurisdictions = await GetFirebaseDataAsync<Dictionary<string, string>>($"{FirebaseRoot}/jurisdictions");
            if (jurisdictions != null)
            {
                foreach (var (id, encrypted) in jurisdictions)
                {
                    try
                    {
                        var json = TagValueEncryptor.Decrypt(encrypted);
                        if (string.IsNullOrEmpty(json)) continue;
                        var j = System.Text.Json.JsonSerializer.Deserialize<JurisdictionCodeAdoption>(json, SysJsonOptions);
                        if (j != null) _jurisdictions[id] = j;
                    }
                    catch (Exception ex)
                    {
                        InfraLogger.Log($"CheatSheetDataService: Failed to parse jurisdiction '{id}': {ex.Message}");
                    }
                }
            }

            SaveLocalCache();
            DataUpdated?.Invoke();
            return synced;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: Sync failed: {ex.Message}");
            return 0;
        }
    }

    public async Task<bool> CheckForUpdatesAsync()
    {
        if (!_firebase.IsInitialized) return false;

        try
        {
            var versionObj = await GetFirebaseDataAsync<object>($"{FirebaseRoot}/meta/version");
            if (versionObj != null && int.TryParse(versionObj.ToString(), out var remoteVersion))
            {
                return remoteVersion > _cachedVersion;
            }
            return false;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: Version check failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Called by the polling timer. Checks remote version and triggers sync if changed.
    /// </summary>
    private async Task PollForUpdatesAsync()
    {
        try
        {
            var hasUpdates = await CheckForUpdatesAsync();
            if (hasUpdates)
            {
                InfraLogger.Log("CheatSheetDataService: Remote version changed, syncing...");
                var count = await SyncFromFirebaseAsync();
                InfraLogger.Log($"CheatSheetDataService: Poll sync completed, {count} sheets");
            }
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: Poll check failed: {ex.Message}");
        }
    }

    public async Task SaveCodeBookAsync(CodeBook cb)
    {
        _codeBooks[cb.Id] = cb;
        SaveLocalCache();

        if (!_firebase.IsInitialized) return;

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(cb, SysJsonOptions);
            var encrypted = TagValueEncryptor.Encrypt(json);
            await PutFirebaseDataAsync($"{FirebaseRoot}/code_books/{cb.Id}", encrypted ?? json);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: Code book save failed: {ex.Message}");
        }
    }

    public async Task SaveJurisdictionAsync(JurisdictionCodeAdoption j)
    {
        _jurisdictions[j.JurisdictionId] = j;
        SaveLocalCache();

        if (!_firebase.IsInitialized) return;

        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(j, SysJsonOptions);
            var encrypted = TagValueEncryptor.Encrypt(json);
            await PutFirebaseDataAsync($"{FirebaseRoot}/jurisdictions/{j.JurisdictionId}", encrypted ?? json);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: Jurisdiction save failed: {ex.Message}");
        }
    }

    // --- Seeding ---

    private async Task SeedFromDefaultsAsync()
    {
        // Use the existing hardcoded defaults as seed data
        var store = CreateDefaultData();

        InfraLogger.Log($"CheatSheetDataService: Seeding {store.Sheets.Count} sheets, {store.CodeBooks.Count} code books, {store.Jurisdictions.Count} jurisdictions");

        // Upload code books
        foreach (var cb in store.CodeBooks)
        {
            _codeBooks[cb.Id] = cb;
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(cb, SysJsonOptions);
                var encrypted = TagValueEncryptor.Encrypt(json);
                await PutFirebaseDataAsync($"{FirebaseRoot}/code_books/{cb.Id}", encrypted ?? json);
            }
            catch (Exception ex)
            {
                InfraLogger.Log($"CheatSheetDataService: Seed code book '{cb.Id}' failed: {ex.Message}");
            }
        }

        // Upload jurisdictions
        foreach (var j in store.Jurisdictions)
        {
            _jurisdictions[j.JurisdictionId] = j;
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(j, SysJsonOptions);
                var encrypted = TagValueEncryptor.Encrypt(json);
                await PutFirebaseDataAsync($"{FirebaseRoot}/jurisdictions/{j.JurisdictionId}", encrypted ?? json);
            }
            catch (Exception ex)
            {
                InfraLogger.Log($"CheatSheetDataService: Seed jurisdiction '{j.JurisdictionId}' failed: {ex.Message}");
            }
        }

        // Upload sheets
        foreach (var sheet in store.Sheets)
        {
            _sheets[sheet.Id] = new SheetEntry { Sheet = sheet, Enabled = true };
            try
            {
                var sheetJson = System.Text.Json.JsonSerializer.Serialize(sheet, SysJsonOptions);
                var encrypted = TagValueEncryptor.Encrypt(sheetJson);
                await PatchFirebaseDataAsync($"{FirebaseRoot}/sheets/{sheet.Id}", new Dictionary<string, object>
                {
                    ["data"] = encrypted ?? sheetJson,
                    ["enabled"] = true,
                    ["updated_by"] = TagValueEncryptor.Encrypt("system") ?? "system",
                    ["updated_at"] = DateTime.UtcNow.ToString("o")
                });
            }
            catch (Exception ex)
            {
                InfraLogger.Log($"CheatSheetDataService: Seed sheet '{sheet.Id}' failed: {ex.Message}");
            }
        }

        // Set initial version
        _cachedVersion = 1;
        await PatchFirebaseDataAsync($"{FirebaseRoot}/meta", new Dictionary<string, object>
        {
            ["version"] = 1,
            ["last_updated_by"] = TagValueEncryptor.Encrypt("system") ?? "system",
            ["last_updated_at"] = DateTime.UtcNow.ToString("o")
        });

        SaveLocalCache();
        DataUpdated?.Invoke();
        InfraLogger.Log("CheatSheetDataService: Seeding complete");
    }

    /// <summary>
    /// Creates the default cheat sheet data store from hardcoded providers.
    /// Used for initial Firebase seeding and offline fallback.
    /// </summary>
    private static CheatSheetDataStore CreateDefaultData()
    {
        var store = new CheatSheetDataStore();

        // These are the same static methods used by the old CheatSheetService
        // They live in DesktopHub.UI.Services namespace, but we invoke them
        // via a delegate pattern to avoid cross-project dependency.
        // For now, we'll load from the local cache file or return empty.
        // The actual seeding is triggered by CheatSheetService which has access
        // to the default providers in the UI project.
        return store;
    }

    /// <summary>
    /// Seed Firebase from a provided data store (called from UI layer which has access to defaults).
    /// </summary>
    public async Task SeedFromDataStoreAsync(CheatSheetDataStore store)
    {
        if (!_firebase.IsInitialized) return;

        InfraLogger.Log($"CheatSheetDataService: Seeding {store.Sheets.Count} sheets from provided data store");

        foreach (var cb in store.CodeBooks)
        {
            _codeBooks[cb.Id] = cb;
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(cb, SysJsonOptions);
                var encrypted = TagValueEncryptor.Encrypt(json);
                await PutFirebaseDataAsync($"{FirebaseRoot}/code_books/{cb.Id}", encrypted ?? json);
            }
            catch (Exception ex)
            {
                InfraLogger.Log($"CheatSheetDataService: Seed code book '{cb.Id}' failed: {ex.Message}");
            }
        }

        foreach (var j in store.Jurisdictions)
        {
            _jurisdictions[j.JurisdictionId] = j;
            try
            {
                var json = System.Text.Json.JsonSerializer.Serialize(j, SysJsonOptions);
                var encrypted = TagValueEncryptor.Encrypt(json);
                await PutFirebaseDataAsync($"{FirebaseRoot}/jurisdictions/{j.JurisdictionId}", encrypted ?? json);
            }
            catch (Exception ex)
            {
                InfraLogger.Log($"CheatSheetDataService: Seed jurisdiction '{j.JurisdictionId}' failed: {ex.Message}");
            }
        }

        foreach (var sheet in store.Sheets)
        {
            _sheets[sheet.Id] = new SheetEntry { Sheet = sheet, Enabled = true };
            try
            {
                var sheetJson = System.Text.Json.JsonSerializer.Serialize(sheet, SysJsonOptions);
                var encrypted = TagValueEncryptor.Encrypt(sheetJson);
                await PatchFirebaseDataAsync($"{FirebaseRoot}/sheets/{sheet.Id}", new Dictionary<string, object>
                {
                    ["data"] = encrypted ?? sheetJson,
                    ["enabled"] = true,
                    ["updated_by"] = TagValueEncryptor.Encrypt("system") ?? "system",
                    ["updated_at"] = DateTime.UtcNow.ToString("o")
                });
            }
            catch (Exception ex)
            {
                InfraLogger.Log($"CheatSheetDataService: Seed sheet '{sheet.Id}' failed: {ex.Message}");
            }
        }

        _cachedVersion = 1;
        await PatchFirebaseDataAsync($"{FirebaseRoot}/meta", new Dictionary<string, object>
        {
            ["version"] = 1,
            ["last_updated_by"] = TagValueEncryptor.Encrypt("system") ?? "system",
            ["last_updated_at"] = DateTime.UtcNow.ToString("o")
        });

        SaveLocalCache();
        DataUpdated?.Invoke();
        InfraLogger.Log("CheatSheetDataService: Seeding from data store complete");
    }

    // --- History ---

    private async Task WriteHistoryAsync(string sheetId, string action, string editedBy, CheatSheet? previousState, string diffSummary)
    {
        try
        {
            var historyEntry = new Dictionary<string, object>
            {
                ["action"] = action,
                ["edited_by"] = TagValueEncryptor.Encrypt(editedBy) ?? editedBy,
                ["edited_at"] = DateTime.UtcNow.ToString("o"),
                ["diff_summary"] = TagValueEncryptor.Encrypt(diffSummary) ?? diffSummary
            };

            if (previousState != null)
            {
                var prevJson = System.Text.Json.JsonSerializer.Serialize(previousState, SysJsonOptions);
                historyEntry["snapshot"] = TagValueEncryptor.Encrypt(prevJson) ?? prevJson;
            }

            await PostFirebaseDataAsync($"{FirebaseRoot}/history/{sheetId}", historyEntry);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: History write failed for '{sheetId}': {ex.Message}");
        }
    }

    private async Task IncrementVersionAsync(string editedBy)
    {
        _cachedVersion++;
        try
        {
            await PatchFirebaseDataAsync($"{FirebaseRoot}/meta", new Dictionary<string, object>
            {
                ["version"] = _cachedVersion,
                ["last_updated_by"] = TagValueEncryptor.Encrypt(editedBy) ?? editedBy,
                ["last_updated_at"] = DateTime.UtcNow.ToString("o")
            });
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: Version increment failed: {ex.Message}");
        }
    }

    private static string BuildDiffSummary(CheatSheet before, CheatSheet after)
    {
        var changes = new List<string>();

        if (before.Title != after.Title) changes.Add("Title");
        if (before.Subtitle != after.Subtitle) changes.Add("Subtitle");
        if (before.Description != after.Description) changes.Add("Description");
        if (before.Discipline != after.Discipline) changes.Add("Discipline");
        if (before.SheetType != after.SheetType) changes.Add("SheetType");
        if (before.Layout != after.Layout) changes.Add("Layout");
        if (before.NoteContent != after.NoteContent) changes.Add("NoteContent");

        if (before.Columns.Count != after.Columns.Count)
            changes.Add($"Columns ({before.Columns.Count}\u2192{after.Columns.Count})");

        if (before.Rows.Count != after.Rows.Count)
            changes.Add($"Rows ({before.Rows.Count}\u2192{after.Rows.Count})");
        else if (!RowsEqual(before.Rows, after.Rows))
            changes.Add("Row data");

        if (before.Steps.Count != after.Steps.Count)
            changes.Add($"Steps ({before.Steps.Count}\u2192{after.Steps.Count})");

        if (!TagsEqual(before.Tags, after.Tags))
            changes.Add("Tags");

        return changes.Count == 0 ? "No visible changes" : $"Changed: {string.Join(", ", changes)}";
    }

    private static bool RowsEqual(List<List<string>> a, List<List<string>> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
        {
            if (a[i].Count != b[i].Count) return false;
            for (var j = 0; j < a[i].Count; j++)
                if (a[i][j] != b[i][j]) return false;
        }
        return true;
    }

    private static bool TagsEqual(List<string> a, List<string> b)
    {
        if (a.Count != b.Count) return false;
        for (var i = 0; i < a.Count; i++)
            if (!a[i].Equals(b[i], StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    // --- Q-Drive Backup ---

    private void ExportQDriveBackup()
    {
        try
        {
            var dir = Path.GetDirectoryName(QDriveBackupPath);
            if (dir != null && !Directory.Exists(dir))
            {
                // Q-Drive not available — skip silently
                return;
            }

            var store = new CheatSheetDataStore
            {
                CodeBooks = _codeBooks.Values.ToList(),
                Jurisdictions = _jurisdictions.Values.ToList(),
                Sheets = _sheets.Values.Where(e => e.Enabled).Select(e => e.Sheet).ToList()
            };

            var json = System.Text.Json.JsonSerializer.Serialize(store, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            });
            File.WriteAllText(QDriveBackupPath, json);
            InfraLogger.Log($"CheatSheetDataService: Q-Drive backup exported ({store.Sheets.Count} sheets)");
        }
        catch (Exception ex)
        {
            // Non-critical — Q-Drive may be unavailable (VPN, permissions, etc.)
            InfraLogger.Log($"CheatSheetDataService: Q-Drive backup failed (non-critical): {ex.Message}");
        }
    }

    // --- Local Cache ---

    private void LoadLocalCache()
    {
        if (!File.Exists(_cacheFilePath)) return;

        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            var cache = JsonConvert.DeserializeObject<CheatSheetCache>(json);
            if (cache == null) return;

            _cachedVersion = cache.Version;

            if (cache.Sheets != null)
            {
                foreach (var entry in cache.Sheets)
                {
                    if (entry.Sheet != null && !string.IsNullOrEmpty(entry.Sheet.Id))
                        _sheets[entry.Sheet.Id] = entry;
                }
            }

            if (cache.CodeBooks != null)
            {
                foreach (var cb in cache.CodeBooks)
                    if (!string.IsNullOrEmpty(cb.Id)) _codeBooks[cb.Id] = cb;
            }

            if (cache.Jurisdictions != null)
            {
                foreach (var j in cache.Jurisdictions)
                    if (!string.IsNullOrEmpty(j.JurisdictionId)) _jurisdictions[j.JurisdictionId] = j;
            }
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: Failed to load cache: {ex.Message}");
        }
    }

    private void SaveLocalCache()
    {
        try
        {
            var cache = new CheatSheetCache
            {
                Version = _cachedVersion,
                LastSynced = DateTime.UtcNow,
                Sheets = _sheets.Values.ToList(),
                CodeBooks = _codeBooks.Values.ToList(),
                Jurisdictions = _jurisdictions.Values.ToList()
            };

            var json = JsonConvert.SerializeObject(cache, Formatting.Indented);
            File.WriteAllText(_cacheFilePath, json);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"CheatSheetDataService: Failed to save cache: {ex.Message}");
        }
    }

    // --- Firebase HTTP helpers (delegate to FirebaseService via reflection) ---

    private async Task<T?> GetFirebaseDataAsync<T>(string path)
    {
        var method = _firebase.GetType().GetMethod("GetDataAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null) return default;
        var genericMethod = method.MakeGenericMethod(typeof(T));
        var task = (Task?)genericMethod.Invoke(_firebase, new object[] { path });
        if (task == null) return default;
        await task;
        var resultProp = task.GetType().GetProperty("Result");
        return (T?)resultProp?.GetValue(task);
    }

    private async Task PatchFirebaseDataAsync(string path, object data)
    {
        var method = _firebase.GetType().GetMethod("PatchDataAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null) return;
        var task = (Task?)method.Invoke(_firebase, new object[] { path, data });
        if (task != null) await task;
    }

    private async Task PutFirebaseDataAsync(string path, object data)
    {
        var method = _firebase.GetType().GetMethod("PutDataAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null) return;
        var task = (Task?)method.Invoke(_firebase, new object[] { path, data });
        if (task != null) await task;
    }

    private async Task PostFirebaseDataAsync(string path, object data)
    {
        var method = _firebase.GetType().GetMethod("PostDataAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null) return;
        var task = (Task?)method.Invoke(_firebase, new object[] { path, data });
        if (task != null) await task;
    }

    private async Task DeleteFirebaseDataAsync(string path)
    {
        var method = _firebase.GetType().GetMethod("PutDataAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null) return;
        var task = (Task?)method.Invoke(_firebase, new object[] { path, null! });
        if (task != null) await task;
    }

    // --- Cache Models ---

    internal class SheetEntry
    {
        public CheatSheet Sheet { get; set; } = new();
        public bool Enabled { get; set; } = true;
    }

    private class CheatSheetCache
    {
        public int Version { get; set; }
        public DateTime LastSynced { get; set; }
        public List<SheetEntry>? Sheets { get; set; }
        public List<CodeBook>? CodeBooks { get; set; }
        public List<JurisdictionCodeAdoption>? Jurisdictions { get; set; }
    }
}
