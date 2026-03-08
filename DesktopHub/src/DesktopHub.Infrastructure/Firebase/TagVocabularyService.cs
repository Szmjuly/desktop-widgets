using System.Collections.Concurrent;
using System.Reflection;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Logging;
using Newtonsoft.Json;

namespace DesktopHub.Infrastructure.Firebase;

/// <summary>
/// Manages the shared tag vocabulary in Firebase RTDB.
/// Firebase node: tag_vocabulary/{fieldKey}/ → list of known values with metadata.
/// Seeds from TagFieldRegistry.SuggestedValues, grows as users add custom entries.
/// </summary>
public class TagVocabularyService : ITagVocabularyService
{
    private const string FirebaseNode = "tag_vocabulary";

    private readonly IFirebaseService _firebase;
    private readonly string _cacheFilePath;
    private readonly string _username;

    // fieldKey → sorted list of known values
    private readonly ConcurrentDictionary<string, SortedSet<string>> _vocabulary = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    public TagVocabularyService(IFirebaseService firebase)
    {
        _firebase = firebase;
        _username = Environment.UserName.ToLowerInvariant();

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopHub");
        Directory.CreateDirectory(appDataDir);
        _cacheFilePath = Path.Combine(appDataDir, "tag_vocabulary_cache.json");
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        // Seed from TagFieldRegistry defaults
        foreach (var field in TagFieldRegistry.Fields)
        {
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var val in field.SuggestedValues)
                set.Add(val);
            _vocabulary[field.Key] = set;
        }

        // Load local cache (merges on top of seeds)
        LoadLocalCache();
        InfraLogger.Log($"TagVocabularyService: Loaded vocabulary with {_vocabulary.Count} fields");

        _initialized = true;

        // Background sync from Firebase
        _ = Task.Run(async () =>
        {
            try
            {
                await SyncFromFirebaseAsync();
                InfraLogger.Log("TagVocabularyService: Background sync completed");
            }
            catch (Exception ex)
            {
                InfraLogger.Log($"TagVocabularyService: Background sync failed: {ex.Message}");
            }
        });

        await Task.CompletedTask;
    }

    public List<string> GetValues(string fieldKey)
    {
        if (_vocabulary.TryGetValue(fieldKey, out var set))
            return set.ToList();
        return new List<string>();
    }

    public async Task AddValueAsync(string fieldKey, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;

        var trimmed = value.Trim();

        // Add to local cache
        var set = _vocabulary.GetOrAdd(fieldKey, _ => new SortedSet<string>(StringComparer.OrdinalIgnoreCase));
        if (!set.Add(trimmed))
            return; // Already exists

        SaveLocalCache();

        // Write to Firebase
        if (!_firebase.IsInitialized) return;

        try
        {
            var safeValue = SanitizeFirebaseKey(trimmed);
            var data = new Dictionary<string, object>
            {
                ["added_by"] = _username,
                ["added_at"] = DateTime.UtcNow.ToString("o")
            };
            await PatchFirebaseDataAsync($"{FirebaseNode}/{fieldKey}/{safeValue}", data);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"TagVocabularyService: Failed to write value to Firebase: {ex.Message}");
        }
    }

    public async Task AddValuesAsync(string fieldKey, IEnumerable<string> values)
    {
        foreach (var value in values)
            await AddValueAsync(fieldKey, value);
    }

    public async Task SyncFromFirebaseAsync()
    {
        if (!_firebase.IsInitialized) return;

        try
        {
            var allData = await GetFirebaseDataAsync<Dictionary<string, Dictionary<string, object>>>(FirebaseNode);
            if (allData == null) return;

            foreach (var (fieldKey, entries) in allData)
            {
                var set = _vocabulary.GetOrAdd(fieldKey, _ => new SortedSet<string>(StringComparer.OrdinalIgnoreCase));

                foreach (var (valueKey, _) in entries)
                {
                    // Firebase keys may be sanitized; restore original
                    var original = UnsanitizeFirebaseKey(valueKey);
                    set.Add(original);
                }
            }

            SaveLocalCache();
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"TagVocabularyService: Sync failed: {ex.Message}");
        }
    }

    // --- Firebase key sanitization ---
    // Firebase RTDB keys cannot contain . $ # [ ] /
    // We encode them as __DOT__ etc.

    private static string SanitizeFirebaseKey(string value)
    {
        return value
            .Replace(".", "__DOT__")
            .Replace("$", "__DOLLAR__")
            .Replace("#", "__HASH__")
            .Replace("[", "__LBRK__")
            .Replace("]", "__RBRK__")
            .Replace("/", "__SLASH__");
    }

    private static string UnsanitizeFirebaseKey(string key)
    {
        return key
            .Replace("__DOT__", ".")
            .Replace("__DOLLAR__", "$")
            .Replace("__HASH__", "#")
            .Replace("__LBRK__", "[")
            .Replace("__RBRK__", "]")
            .Replace("__SLASH__", "/");
    }

    // --- Local cache persistence ---

    private void LoadLocalCache()
    {
        if (!File.Exists(_cacheFilePath)) return;

        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            var data = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(json);
            if (data == null) return;

            foreach (var (key, values) in data)
            {
                var set = _vocabulary.GetOrAdd(key, _ => new SortedSet<string>(StringComparer.OrdinalIgnoreCase));
                foreach (var val in values)
                    set.Add(val);
            }
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"TagVocabularyService: Failed to load cache: {ex.Message}");
        }
    }

    private void SaveLocalCache()
    {
        try
        {
            var data = new Dictionary<string, List<string>>();
            foreach (var (key, set) in _vocabulary)
                data[key] = set.ToList();

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(_cacheFilePath, json);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"TagVocabularyService: Failed to save cache: {ex.Message}");
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
