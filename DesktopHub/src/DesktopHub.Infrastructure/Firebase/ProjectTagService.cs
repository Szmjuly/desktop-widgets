using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Firebase.Utilities;
using DesktopHub.Infrastructure.Logging;
using Newtonsoft.Json;

namespace DesktopHub.Infrastructure.Firebase;

/// <summary>
/// Service for managing project tags in Firebase RTDB with HMAC-hashed keys
/// and a local JSON cache for fast search.
/// </summary>
public class ProjectTagService : IProjectTagService
{
    private const string FirebaseNode = "project_tags";

    private readonly IFirebaseService _firebase;
    private readonly string _cacheFilePath;
    private readonly string _username;

    // Local cache: projectNumber → ProjectTags
    private readonly ConcurrentDictionary<string, ProjectTags> _cache = new(StringComparer.OrdinalIgnoreCase);

    // Hash → projectNumber reverse map (populated from cache file)
    private readonly ConcurrentDictionary<string, string> _hashToNumber = new(StringComparer.OrdinalIgnoreCase);

    private bool _initialized;

    public ProjectTagService(IFirebaseService firebase)
    {
        _firebase = firebase;
        _username = Environment.UserName.ToLowerInvariant();

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopHub");
        Directory.CreateDirectory(appDataDir);
        _cacheFilePath = Path.Combine(appDataDir, "tag_cache.json");
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            LoadLocalCache();
            InfraLogger.Log($"ProjectTagService: Loaded {_cache.Count} cached tag entries");

            // Background sync from Firebase (non-blocking)
            _ = Task.Run(async () =>
            {
                try
                {
                    var count = await SyncFromFirebaseAsync();
                    InfraLogger.Log($"ProjectTagService: Background sync completed, {count} entries");
                }
                catch (Exception ex)
                {
                    InfraLogger.Log($"ProjectTagService: Background sync failed: {ex.Message}");
                }
            });

            _initialized = true;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"ProjectTagService: Initialization failed: {ex.Message}");
            _initialized = true; // Still mark as initialized to avoid re-trying
        }

        await Task.CompletedTask;
    }

    public async Task<ProjectTags?> GetTagsAsync(string projectNumber)
    {
        if (string.IsNullOrWhiteSpace(projectNumber))
            return null;

        // Check local cache first
        if (_cache.TryGetValue(projectNumber, out var cached))
            return cached;

        // Try Firebase
        if (!_firebase.IsInitialized)
            return null;

        try
        {
            var hash = ProjectHasher.HashProjectNumber(projectNumber);
            var data = await GetFirebaseDataAsync<Dictionary<string, object>>($"{FirebaseNode}/{hash}/tags");
            if (data == null)
                return null;

            var tags = DeserializeTags(data);
            _cache[projectNumber] = tags;
            _hashToNumber[hash] = projectNumber;
            SaveLocalCache();
            return tags;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"ProjectTagService: Failed to get tags for {projectNumber}: {ex.Message}");
            return null;
        }
    }

    public async Task SaveTagsAsync(string projectNumber, ProjectTags tags)
    {
        if (string.IsNullOrWhiteSpace(projectNumber))
            return;

        tags.UpdatedBy = _username;
        tags.UpdatedAt = DateTime.UtcNow;

        // Update local cache immediately
        _cache[projectNumber] = tags;
        var hash = ProjectHasher.HashProjectNumber(projectNumber);
        _hashToNumber[hash] = projectNumber;
        SaveLocalCache();

        // Write to Firebase
        if (!_firebase.IsInitialized)
        {
            InfraLogger.Log("ProjectTagService: Firebase not initialized, saved to local cache only");
            return;
        }

        try
        {
            var firebaseData = SerializeTags(tags);
            await PatchFirebaseDataAsync($"{FirebaseNode}/{hash}", new Dictionary<string, object>
            {
                ["tags"] = firebaseData,
                ["updated_by"] = tags.UpdatedBy,
                ["updated_at"] = tags.UpdatedAt.ToString("o")
            });

            InfraLogger.Log($"ProjectTagService: Saved tags for project (hash: {hash[..8]}...)");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"ProjectTagService: Firebase save failed (cached locally): {ex.Message}");
        }
    }

    public async Task DeleteTagsAsync(string projectNumber)
    {
        if (string.IsNullOrWhiteSpace(projectNumber))
            return;

        _cache.TryRemove(projectNumber, out _);
        var hash = ProjectHasher.HashProjectNumber(projectNumber);
        _hashToNumber.TryRemove(hash, out _);
        SaveLocalCache();

        if (_firebase.IsInitialized)
        {
            try
            {
                await DeleteFirebaseDataAsync($"{FirebaseNode}/{hash}");
                InfraLogger.Log($"ProjectTagService: Deleted tags for project (hash: {hash[..8]}...)");
            }
            catch (Exception ex)
            {
                InfraLogger.Log($"ProjectTagService: Firebase delete failed: {ex.Message}");
            }
        }
    }

    public IReadOnlyDictionary<string, ProjectTags> GetAllCachedTags()
        => _cache;

    public async Task<int> SyncFromFirebaseAsync()
    {
        if (!_firebase.IsInitialized)
            return 0;

        try
        {
            var allData = await GetFirebaseDataAsync<Dictionary<string, Dictionary<string, object>>>(FirebaseNode);
            if (allData == null)
                return 0;

            var synced = 0;
            foreach (var (hash, entry) in allData)
            {
                try
                {
                    if (!entry.ContainsKey("tags"))
                        continue;

                    var tagsObj = entry["tags"];
                    Dictionary<string, object>? tagsDict;

                    if (tagsObj is Newtonsoft.Json.Linq.JObject jObj)
                        tagsDict = jObj.ToObject<Dictionary<string, object>>();
                    else if (tagsObj is Dictionary<string, object> dict)
                        tagsDict = dict;
                    else
                        continue;

                    if (tagsDict == null) continue;

                    var tags = DeserializeTags(tagsDict);

                    if (entry.TryGetValue("updated_by", out var ub))
                        tags.UpdatedBy = ub?.ToString();
                    if (entry.TryGetValue("updated_at", out var ua) && DateTime.TryParse(ua?.ToString(), out var updatedAt))
                        tags.UpdatedAt = updatedAt;

                    // If we have a reverse mapping for this hash, update the cache
                    if (_hashToNumber.TryGetValue(hash, out var projectNumber))
                    {
                        _cache[projectNumber] = tags;
                        synced++;
                    }
                    else
                    {
                        // We don't know the project number for this hash
                        // It was created by another machine or the side script
                        // We'll store it with the hash as the key; the app will
                        // re-map it when it encounters the project during search
                        _hashToNumber[hash] = hash; // Placeholder
                    }
                }
                catch (Exception ex)
                {
                    InfraLogger.Log($"ProjectTagService: Failed to parse tag entry {hash[..8]}...: {ex.Message}");
                }
            }

            SaveLocalCache();
            return synced;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"ProjectTagService: Sync failed: {ex.Message}");
            return 0;
        }
    }

    public bool HasTags(string projectNumber)
        => _cache.ContainsKey(projectNumber);

    public string? GetTagValue(string projectNumber, string fieldKeyOrAlias)
    {
        if (!_cache.TryGetValue(projectNumber, out var tags))
            return null;

        var def = TagFieldRegistry.Resolve(fieldKeyOrAlias);
        if (def == null)
        {
            // Check custom tags
            tags.Custom.TryGetValue(fieldKeyOrAlias, out var customVal);
            return customVal;
        }

        return GetTagValueByKey(tags, def.Key);
    }

    public List<string> SearchByTags(List<(string key, string value)> filters)
    {
        if (filters.Count == 0)
            return new List<string>();

        var matches = new List<string>();

        foreach (var (projectNumber, tags) in _cache)
        {
            var allMatch = true;
            foreach (var (key, value) in filters)
            {
                // For list-based fields, check individual items with space-normalized matching
                if (key.Equals("code_refs", StringComparison.OrdinalIgnoreCase))
                {
                    if (!FuzzyListMatch(tags.CodeReferences, value))
                    { allMatch = false; break; }
                    continue;
                }
                if (key.Equals("engineers", StringComparison.OrdinalIgnoreCase))
                {
                    if (!FuzzyListMatch(tags.Engineers, value))
                    { allMatch = false; break; }
                    continue;
                }

                var tagValue = GetTagValueByKey(tags, key);
                if (tagValue == null)
                {
                    // Check custom tags
                    tags.Custom.TryGetValue(key, out tagValue);
                }

                if (tagValue == null || !FuzzyTagMatch(tagValue, value))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
                matches.Add(projectNumber);
        }

        return matches;
    }

    public (string key, string value)? ParseTagFilter(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return null;

        // Match pattern: key:value (single colon)
        var colonIdx = segment.IndexOf(':');
        if (colonIdx <= 0 || colonIdx >= segment.Length - 1)
            return null;

        var rawKey = segment[..colonIdx].Trim();
        var rawValue = segment[(colonIdx + 1)..].Trim();

        if (string.IsNullOrEmpty(rawKey) || string.IsNullOrEmpty(rawValue))
            return null;

        // Resolve alias
        var def = TagFieldRegistry.Resolve(rawKey);

        if (def != null)
        {
            // If the key resolved to code_refs via a specific code prefix (not the generic aliases),
            // combine the prefix with the value so "NEC:2020" searches for "NEC 2020" / "NEC2020"
            if (def.Key == "code_refs" && !IsGenericCodeAlias(rawKey))
                return ("code_refs", $"{rawKey} {rawValue}");

            return (def.Key, rawValue);
        }

        // Fallback: check if the prefix matches any code reference value across cached projects.
        // This lets users search "NEC:2020" even if "NEC" isn't a registered alias.
        var normalizedPrefix = rawKey.Replace(" ", "");
        var isCodePrefix = _cache.Values.Any(t =>
            t.CodeReferences.Any(cr =>
                cr.Replace(" ", "").StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase)));

        if (isCodePrefix)
            return ("code_refs", $"{rawKey} {rawValue}");

        // Unknown prefix — still return as a generic filter (may match custom tags)
        return (rawKey, rawValue);
    }

    private static bool IsGenericCodeAlias(string rawKey)
    {
        return rawKey.Equals("code", StringComparison.OrdinalIgnoreCase)
            || rawKey.Equals("codes", StringComparison.OrdinalIgnoreCase)
            || rawKey.Equals("ref", StringComparison.OrdinalIgnoreCase)
            || rawKey.Equals("refs", StringComparison.OrdinalIgnoreCase)
            || rawKey.Equals("code_refs", StringComparison.OrdinalIgnoreCase);
    }

    // --- Private helpers ---

    private static string? GetTagValueByKey(ProjectTags tags, string key)
    {
        return key.ToLowerInvariant() switch
        {
            "voltage" => tags.Voltage,
            "phase" => tags.Phase,
            "amperage_service" => tags.AmperageService,
            "amperage_generator" => tags.AmperageGenerator,
            "generator_brand" => tags.GeneratorBrand,
            "generator_load_kw" => tags.GeneratorLoadKw,
            "hvac_type" => tags.HvacType,
            "hvac_brand" => tags.HvacBrand,
            "hvac_tonnage" => tags.HvacTonnage,
            "hvac_load_kw" => tags.HvacLoadKw,
            "square_footage" => tags.SquareFootage,
            "build_type" => tags.BuildType,
            "location_city" => tags.LocationCity,
            "location_state" => tags.LocationState,
            "location_municipality" => tags.LocationMunicipality,
            "location_address" => tags.LocationAddress,
            "stamping_engineer" => tags.StampingEngineer,
            "engineers" => tags.Engineers.Count > 0 ? string.Join(", ", tags.Engineers) : null,
            "code_refs" => tags.CodeReferences.Count > 0 ? string.Join(", ", tags.CodeReferences) : null,
            _ => null
        };
    }

    private static void SetTagValueByKey(ProjectTags tags, string key, object? value)
    {
        var strVal = value?.ToString();
        switch (key.ToLowerInvariant())
        {
            case "voltage": tags.Voltage = strVal; break;
            case "phase": tags.Phase = strVal; break;
            case "amperage_service": tags.AmperageService = strVal; break;
            case "amperage_generator": tags.AmperageGenerator = strVal; break;
            case "generator_brand": tags.GeneratorBrand = strVal; break;
            case "generator_load_kw": tags.GeneratorLoadKw = strVal; break;
            case "hvac_type": tags.HvacType = strVal; break;
            case "hvac_brand": tags.HvacBrand = strVal; break;
            case "hvac_tonnage": tags.HvacTonnage = strVal; break;
            case "hvac_load_kw": tags.HvacLoadKw = strVal; break;
            case "square_footage": tags.SquareFootage = strVal; break;
            case "build_type": tags.BuildType = strVal; break;
            case "location_city": tags.LocationCity = strVal; break;
            case "location_state": tags.LocationState = strVal; break;
            case "location_municipality": tags.LocationMunicipality = strVal; break;
            case "location_address": tags.LocationAddress = strVal; break;
            case "stamping_engineer": tags.StampingEngineer = strVal; break;
            case "engineers":
                if (value is Newtonsoft.Json.Linq.JArray engArr)
                    tags.Engineers = engArr.ToObject<List<string>>() ?? new();
                else if (strVal != null)
                    tags.Engineers = strVal.Split(',', StringSplitOptions.TrimEntries).ToList();
                break;
            case "code_refs":
                if (value is Newtonsoft.Json.Linq.JArray codeArr)
                    tags.CodeReferences = codeArr.ToObject<List<string>>() ?? new();
                else if (strVal != null)
                    tags.CodeReferences = strVal.Split(',', StringSplitOptions.TrimEntries).ToList();
                break;
        }
    }

    private static bool FuzzyTagMatch(string tagValue, string query)
    {
        // Case-insensitive contains match
        return tagValue.Contains(query, StringComparison.OrdinalIgnoreCase)
            || query.Contains(tagValue, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// For list fields (code_refs, engineers), check if any individual item matches the query.
    /// Supports space-normalized matching so "NEC 2020" matches "NEC2020" and vice versa.
    /// </summary>
    private static bool FuzzyListMatch(List<string> items, string query)
    {
        if (items.Count == 0) return false;

        var normalizedQuery = query.Replace(" ", "");
        foreach (var item in items)
        {
            if (item.Contains(query, StringComparison.OrdinalIgnoreCase))
                return true;
            // Space-normalized: "NEC 2020" ↔ "NEC2020"
            if (item.Replace(" ", "").Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Also check the joined comma-separated form for broad matches
        var joined = string.Join(", ", items);
        return joined.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static ProjectTags DeserializeTags(Dictionary<string, object> data)
    {
        var tags = new ProjectTags();
        foreach (var (key, value) in data)
        {
            var def = TagFieldRegistry.GetByKey(key);
            if (def != null)
            {
                SetTagValueByKey(tags, key, value);
            }
            else if (key == "custom" && value is Newtonsoft.Json.Linq.JObject customObj)
            {
                tags.Custom = customObj.ToObject<Dictionary<string, string>>()
                    ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else if (key != "custom")
            {
                // Unknown key → treat as custom
                tags.Custom[key] = value?.ToString() ?? "";
            }
        }
        return tags;
    }

    private static Dictionary<string, object> SerializeTags(ProjectTags tags)
    {
        var data = new Dictionary<string, object>();

        void AddIfNotNull(string key, string? value) { if (!string.IsNullOrEmpty(value)) data[key] = value; }

        AddIfNotNull("voltage", tags.Voltage);
        AddIfNotNull("phase", tags.Phase);
        AddIfNotNull("amperage_service", tags.AmperageService);
        AddIfNotNull("amperage_generator", tags.AmperageGenerator);
        AddIfNotNull("generator_brand", tags.GeneratorBrand);
        AddIfNotNull("generator_load_kw", tags.GeneratorLoadKw);
        AddIfNotNull("hvac_type", tags.HvacType);
        AddIfNotNull("hvac_brand", tags.HvacBrand);
        AddIfNotNull("hvac_tonnage", tags.HvacTonnage);
        AddIfNotNull("hvac_load_kw", tags.HvacLoadKw);
        AddIfNotNull("square_footage", tags.SquareFootage);
        AddIfNotNull("build_type", tags.BuildType);
        AddIfNotNull("location_city", tags.LocationCity);
        AddIfNotNull("location_state", tags.LocationState);
        AddIfNotNull("location_municipality", tags.LocationMunicipality);
        AddIfNotNull("location_address", tags.LocationAddress);
        AddIfNotNull("stamping_engineer", tags.StampingEngineer);

        if (tags.Engineers.Count > 0)
            data["engineers"] = tags.Engineers;
        if (tags.CodeReferences.Count > 0)
            data["code_refs"] = tags.CodeReferences;
        if (tags.Custom.Count > 0)
            data["custom"] = tags.Custom;

        return data;
    }

    // --- Local cache persistence ---

    private void LoadLocalCache()
    {
        if (!File.Exists(_cacheFilePath))
            return;

        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            var cacheData = JsonConvert.DeserializeObject<TagCacheFile>(json);
            if (cacheData?.Entries == null) return;

            foreach (var entry in cacheData.Entries)
            {
                if (string.IsNullOrEmpty(entry.ProjectNumber)) continue;
                _cache[entry.ProjectNumber] = entry.Tags ?? new ProjectTags();
                if (!string.IsNullOrEmpty(entry.Hash))
                    _hashToNumber[entry.Hash] = entry.ProjectNumber;
            }
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"ProjectTagService: Failed to load cache: {ex.Message}");
        }
    }

    private void SaveLocalCache()
    {
        try
        {
            var cacheData = new TagCacheFile
            {
                LastSynced = DateTime.UtcNow,
                Entries = _cache.Select(kvp =>
                {
                    var hash = ProjectHasher.HashProjectNumber(kvp.Key);
                    return new TagCacheEntry
                    {
                        ProjectNumber = kvp.Key,
                        Hash = hash,
                        Tags = kvp.Value
                    };
                }).ToList()
            };

            var json = JsonConvert.SerializeObject(cacheData, Formatting.Indented);
            File.WriteAllText(_cacheFilePath, json);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"ProjectTagService: Failed to save cache: {ex.Message}");
        }
    }

    // --- Firebase HTTP helpers (delegate to FirebaseService internals via reflection) ---
    // We reuse the same authenticated HTTP client from FirebaseService.

    private async Task<T?> GetFirebaseDataAsync<T>(string path)
    {
        // Use reflection to call private GetDataAsync on FirebaseService
        var method = _firebase.GetType().GetMethod("GetDataAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
        {
            InfraLogger.Log("ProjectTagService: Cannot find GetDataAsync method on FirebaseService");
            return default;
        }
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
        if (method == null)
        {
            InfraLogger.Log("ProjectTagService: Cannot find PatchDataAsync method on FirebaseService");
            return;
        }
        var task = (Task?)method.Invoke(_firebase, new object[] { path, data });
        if (task != null) await task;
    }

    private async Task DeleteFirebaseDataAsync(string path)
    {
        // Firebase RTDB delete = PUT null, or use DELETE HTTP method
        var method = _firebase.GetType().GetMethod("PutDataAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
        {
            InfraLogger.Log("ProjectTagService: Cannot find PutDataAsync method on FirebaseService");
            return;
        }
        // Setting to null effectively deletes the node
        var task = (Task?)method.Invoke(_firebase, new object[] { path, null! });
        if (task != null) await task;
    }

    // --- Cache file models ---

    private class TagCacheFile
    {
        public DateTime LastSynced { get; set; }
        public List<TagCacheEntry> Entries { get; set; } = new();
    }

    private class TagCacheEntry
    {
        public string ProjectNumber { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public ProjectTags? Tags { get; set; }
    }
}
