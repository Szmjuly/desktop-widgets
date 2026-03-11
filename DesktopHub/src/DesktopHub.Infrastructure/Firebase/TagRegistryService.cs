using System.Collections.Concurrent;
using System.Reflection;
using DesktopHub.Core.Abstractions;
using DesktopHub.Infrastructure.Firebase.Utilities;
using DesktopHub.Infrastructure.Logging;
using Newtonsoft.Json;

namespace DesktopHub.Infrastructure.Firebase;

/// <summary>
/// Manages the shared registry of custom tag KEY names in Firebase RTDB.
/// Firebase node: tag_registry/{encrypted_key_hash} → encrypted key name + metadata.
/// When a user creates a new custom tag key, it's encrypted and shared with all users.
/// Only stores key names — values are per-project and stay in project_tags.
/// </summary>
public class TagRegistryService : ITagRegistryService
{
    private const string FirebaseNode = "tag_registry";

    private readonly IFirebaseService _firebase;
    private readonly string _cacheFilePath;
    private readonly string _username;

    // All known custom tag key names (plaintext, local only)
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    public TagRegistryService(IFirebaseService firebase)
    {
        _firebase = firebase;
        _username = Environment.UserName.ToLowerInvariant();

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopHub");
        Directory.CreateDirectory(appDataDir);
        _cacheFilePath = Path.Combine(appDataDir, "tag_registry_cache.json");
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        // Load local cache
        LoadLocalCache();
        InfraLogger.Log($"TagRegistryService: Loaded {_keys.Count} cached custom tag keys");

        _initialized = true;

        // Background sync from Firebase
        _ = Task.Run(async () =>
        {
            try
            {
                await SyncFromFirebaseAsync();
                InfraLogger.Log($"TagRegistryService: Background sync completed, {_keys.Count} keys total");
            }
            catch (Exception ex)
            {
                InfraLogger.Log($"TagRegistryService: Background sync failed: {ex.Message}");
            }
        });

        await Task.CompletedTask;
    }

    public IReadOnlySet<string> GetAllKeys()
    {
        return new HashSet<string>(_keys.Keys, StringComparer.OrdinalIgnoreCase);
    }

    public async Task RegisterKeyAsync(string tagKey)
    {
        if (string.IsNullOrWhiteSpace(tagKey)) return;

        var trimmed = tagKey.Trim();

        // Guard: reject .NET type names or absurdly long keys
        if (trimmed.StartsWith("System.", StringComparison.Ordinal) || trimmed.Length > 100)
            return;

        // Add to local cache
        if (!_keys.TryAdd(trimmed, 0))
            return; // Already known

        SaveLocalCache();

        // Write encrypted key to Firebase
        if (!_firebase.IsInitialized) return;

        try
        {
            // Use HMAC hash of the key name as the Firebase node key (deterministic, obfuscated)
            var nodeKey = ProjectHasher.HashProjectNumber(trimmed);
            // Encrypt the actual key name
            var encryptedName = TagValueEncryptor.Encrypt(trimmed);

            var data = new Dictionary<string, object>
            {
                ["name"] = encryptedName ?? trimmed,
                ["added_by"] = TagValueEncryptor.Encrypt(_username) ?? _username,
                ["added_at"] = DateTime.UtcNow.ToString("o")
            };
            await PatchFirebaseDataAsync($"{FirebaseNode}/{nodeKey}", data);
            InfraLogger.Log($"TagRegistryService: Registered new custom tag key (hash: {nodeKey[..8]}...)");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"TagRegistryService: Failed to write key to Firebase: {ex.Message}");
        }
    }

    public async Task RegisterKeysAsync(IEnumerable<string> tagKeys)
    {
        foreach (var key in tagKeys)
            await RegisterKeyAsync(key);
    }

    public async Task SyncFromFirebaseAsync()
    {
        if (!_firebase.IsInitialized) return;

        try
        {
            var allData = await GetFirebaseDataAsync<Dictionary<string, Dictionary<string, object>>>(FirebaseNode);
            if (allData == null) return;

            var added = 0;
            foreach (var (_, entry) in allData)
            {
                if (!entry.TryGetValue("name", out var nameObj))
                    continue;

                var nameStr = nameObj?.ToString();
                if (string.IsNullOrEmpty(nameStr)) continue;

                // Decrypt the key name
                var decrypted = TagValueEncryptor.Decrypt(nameStr);
                if (!string.IsNullOrEmpty(decrypted) && _keys.TryAdd(decrypted, 0))
                    added++;
            }

            if (added > 0)
            {
                SaveLocalCache();
                InfraLogger.Log($"TagRegistryService: Synced {added} new keys from Firebase");
            }
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"TagRegistryService: Sync failed: {ex.Message}");
        }
    }

    // --- Local cache persistence ---

    private void LoadLocalCache()
    {
        if (!File.Exists(_cacheFilePath)) return;

        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            var keys = JsonConvert.DeserializeObject<List<string>>(json);
            if (keys == null) return;

            foreach (var key in keys)
                _keys.TryAdd(key, 0);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"TagRegistryService: Failed to load cache: {ex.Message}");
        }
    }

    private void SaveLocalCache()
    {
        try
        {
            var json = JsonConvert.SerializeObject(_keys.Keys.OrderBy(k => k).ToList(), Formatting.Indented);
            File.WriteAllText(_cacheFilePath, json);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"TagRegistryService: Failed to save cache: {ex.Message}");
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
}
