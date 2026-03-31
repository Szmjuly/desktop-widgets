using System.Collections.Concurrent;
using System.Reflection;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Firebase.Utilities;
using DesktopHub.Infrastructure.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace DesktopHub.Infrastructure.Firebase;

/// <summary>
/// Service for managing the dynamic master field/category structure stored in Firebase RTDB.
/// Follows the same pattern as ProjectTagService and CheatSheetDataService:
/// local JSON cache + Firebase sync + encryption.
/// </summary>
public class MasterStructureService : IMasterStructureService
{
    private const string MasterNode = "tag_master_structure";
    private const string HistoryNode = "tag_master_structure_history";
    private const string ProjectOverrideSubNode = "structure_override";

    private readonly IFirebaseService _firebase;
    private readonly string _cacheFilePath;
    private readonly string _username;

    private MasterStructure _masterStructure = new();
    private readonly ConcurrentDictionary<string, ProjectStructureOverride> _projectOverrides = new(StringComparer.OrdinalIgnoreCase);
    private bool _initialized;

    public event Action? StructureUpdated;

    public MasterStructureService(IFirebaseService firebase)
    {
        _firebase = firebase;
        _username = Environment.UserName.ToLowerInvariant();

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopHub");
        Directory.CreateDirectory(appDataDir);
        _cacheFilePath = Path.Combine(appDataDir, "master_structure_cache.json");
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            LoadLocalCache();
            InfraLogger.Log($"MasterStructureService: Loaded cache with {_masterStructure.Fields.Count} master fields, {_masterStructure.Categories.Count} master categories");

            // Background sync from Firebase
            _ = Task.Run(async () =>
            {
                try
                {
                    await SyncFromFirebaseAsync();
                    InfraLogger.Log("MasterStructureService: Background sync completed");
                }
                catch (Exception ex)
                {
                    InfraLogger.Log($"MasterStructureService: Background sync failed: {ex.Message}");
                }
            });

            _initialized = true;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MasterStructureService: Initialization failed: {ex.Message}");
            _initialized = true;
        }

        await Task.CompletedTask;
    }

    // --- Read operations ---

    public MasterStructure GetMasterStructure() => _masterStructure;

    public List<MasterFieldDefinition> GetMergedFields(string? projectNumber = null)
    {
        // Start with built-in baseline from TagFieldRegistry
        var result = new List<MasterFieldDefinition>();
        int sortOrder = 0;
        foreach (var field in TagFieldRegistry.Fields)
        {
            var merged = MasterFieldDefinition.FromBuiltIn(field, sortOrder++);

            // Apply master extensions to suggested values
            if (_masterStructure.BuiltInFieldExtensions.TryGetValue(field.Key, out var masterExtras))
            {
                var allValues = new List<string>(field.SuggestedValues);
                foreach (var extra in masterExtras)
                {
                    if (!allValues.Contains(extra, StringComparer.OrdinalIgnoreCase))
                        allValues.Add(extra);
                }
                merged.SuggestedValues = allValues.ToArray();
            }

            // Apply project-specific extensions to suggested values
            if (projectNumber != null && _projectOverrides.TryGetValue(projectNumber, out var projOverride))
            {
                if (projOverride.FieldExtensions.TryGetValue(field.Key, out var projExtras))
                {
                    var allValues = new List<string>(merged.SuggestedValues);
                    foreach (var extra in projExtras)
                    {
                        if (!allValues.Contains(extra, StringComparer.OrdinalIgnoreCase))
                            allValues.Add(extra);
                    }
                    merged.SuggestedValues = allValues.ToArray();
                }
            }

            result.Add(merged);
        }

        // Add master structure dynamic fields
        foreach (var field in _masterStructure.Fields)
        {
            field.SortOrder = sortOrder++;
            result.Add(field);
        }

        // Add project-specific dynamic fields
        if (projectNumber != null && _projectOverrides.TryGetValue(projectNumber, out var projOvr))
        {
            foreach (var field in projOvr.ExtraFields)
            {
                field.SortOrder = sortOrder++;
                result.Add(field);
            }
        }

        return result;
    }

    public List<MasterCategoryDefinition> GetMergedCategories(string? projectNumber = null)
    {
        // Built-in categories from TagFieldRegistry
        var builtInCategories = new Dictionary<string, (string icon, int order)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Project Tags"] = ("\U0001F3F7", 0),
            ["Electrical"] = ("\u26A1", 1),
            ["Mechanical"] = ("\u2744\uFE0F", 2),
            ["Building"] = ("\U0001F3E0", 3),
            ["Location"] = ("\U0001F4CD", 4),
            ["People"] = ("\U0001F465", 5),
            ["Code"] = ("\U0001F4D6", 6),
            ["Other"] = ("\U0001F4CB", 7)
        };

        var result = new List<MasterCategoryDefinition>();
        foreach (var (name, (icon, order)) in builtInCategories)
        {
            result.Add(new MasterCategoryDefinition
            {
                Name = name,
                Icon = icon,
                SortOrder = order,
                IsBuiltIn = true
            });
        }

        // Add master structure dynamic categories
        var nextOrder = result.Count;
        foreach (var cat in _masterStructure.Categories)
        {
            if (!result.Any(c => c.Name.Equals(cat.Name, StringComparison.OrdinalIgnoreCase)))
            {
                cat.SortOrder = nextOrder++;
                result.Add(cat);
            }
        }

        // Add project-specific dynamic categories
        if (projectNumber != null && _projectOverrides.TryGetValue(projectNumber, out var projOvr))
        {
            foreach (var cat in projOvr.ExtraCategories)
            {
                if (!result.Any(c => c.Name.Equals(cat.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    cat.SortOrder = nextOrder++;
                    result.Add(cat);
                }
            }
        }

        return result.OrderBy(c => c.SortOrder).ToList();
    }

    public List<string> GetExtendedValues(string fieldKey, string? projectNumber = null)
    {
        // Start with baseline from TagFieldRegistry
        var def = TagFieldRegistry.GetByKey(fieldKey);
        var values = new List<string>(def?.SuggestedValues ?? Array.Empty<string>());

        // Add master extensions
        if (_masterStructure.BuiltInFieldExtensions.TryGetValue(fieldKey, out var masterExtras))
        {
            foreach (var v in masterExtras)
            {
                if (!values.Contains(v, StringComparer.OrdinalIgnoreCase))
                    values.Add(v);
            }
        }

        // Check if this is a dynamic master field
        var masterField = _masterStructure.Fields.FirstOrDefault(f => f.Key.Equals(fieldKey, StringComparison.OrdinalIgnoreCase));
        if (masterField != null)
        {
            foreach (var v in masterField.SuggestedValues)
            {
                if (!values.Contains(v, StringComparer.OrdinalIgnoreCase))
                    values.Add(v);
            }
        }

        // Add project-specific extensions
        if (projectNumber != null && _projectOverrides.TryGetValue(projectNumber, out var projOvr))
        {
            if (projOvr.FieldExtensions.TryGetValue(fieldKey, out var projExtras))
            {
                foreach (var v in projExtras)
                {
                    if (!values.Contains(v, StringComparer.OrdinalIgnoreCase))
                        values.Add(v);
                }
            }

            var projField = projOvr.ExtraFields.FirstOrDefault(f => f.Key.Equals(fieldKey, StringComparison.OrdinalIgnoreCase));
            if (projField != null)
            {
                foreach (var v in projField.SuggestedValues)
                {
                    if (!values.Contains(v, StringComparer.OrdinalIgnoreCase))
                        values.Add(v);
                }
            }
        }

        return values;
    }

    // --- Editor operations: master scope ---

    public async Task AddMasterFieldAsync(MasterFieldDefinition field, string editedBy)
    {
        field.IsBuiltIn = false;
        field.SortOrder = _masterStructure.Fields.Count;

        var prevJson = JsonConvert.SerializeObject(_masterStructure);
        _masterStructure.Fields.Add(field);
        _masterStructure.Version++;
        _masterStructure.UpdatedBy = editedBy;
        _masterStructure.UpdatedAt = DateTime.UtcNow;

        await SaveMasterToFirebaseAsync();
        await WriteHistoryAsync("field_added", "master", editedBy, prevJson,
            $"Added field '{field.DisplayName}' ({field.Key}) to category '{field.Category}'");
        SaveLocalCache();
        StructureUpdated?.Invoke();
    }

    public async Task AddMasterCategoryAsync(MasterCategoryDefinition category, string editedBy)
    {
        category.IsBuiltIn = false;
        category.SortOrder = GetMergedCategories().Count;

        var prevJson = JsonConvert.SerializeObject(_masterStructure);
        _masterStructure.Categories.Add(category);
        _masterStructure.Version++;
        _masterStructure.UpdatedBy = editedBy;
        _masterStructure.UpdatedAt = DateTime.UtcNow;

        await SaveMasterToFirebaseAsync();
        await WriteHistoryAsync("category_added", "master", editedBy, prevJson,
            $"Added category '{category.Name}'");
        SaveLocalCache();
        StructureUpdated?.Invoke();
    }

    public async Task ExtendBuiltInFieldAsync(string fieldKey, List<string> newValues, string editedBy)
    {
        var prevJson = JsonConvert.SerializeObject(_masterStructure);

        if (!_masterStructure.BuiltInFieldExtensions.TryGetValue(fieldKey, out var existing))
        {
            existing = new List<string>();
            _masterStructure.BuiltInFieldExtensions[fieldKey] = existing;
        }

        var added = new List<string>();
        foreach (var v in newValues)
        {
            if (!existing.Contains(v, StringComparer.OrdinalIgnoreCase))
            {
                existing.Add(v);
                added.Add(v);
            }
        }

        if (added.Count == 0) return;

        _masterStructure.Version++;
        _masterStructure.UpdatedBy = editedBy;
        _masterStructure.UpdatedAt = DateTime.UtcNow;

        await SaveMasterToFirebaseAsync();
        await WriteHistoryAsync("dropdown_extended", "master", editedBy, prevJson,
            $"Extended field '{fieldKey}' with {added.Count} new values: {string.Join(", ", added)}");
        SaveLocalCache();
        StructureUpdated?.Invoke();
    }

    // --- Editor operations: project scope ---

    public async Task AddProjectFieldAsync(string projectNumber, MasterFieldDefinition field, string editedBy)
    {
        field.IsBuiltIn = false;
        var ovr = GetOrCreateProjectOverride(projectNumber);
        field.SortOrder = ovr.ExtraFields.Count;

        var prevJson = JsonConvert.SerializeObject(ovr);
        ovr.ExtraFields.Add(field);
        ovr.UpdatedBy = editedBy;
        ovr.UpdatedAt = DateTime.UtcNow;

        await SaveProjectOverrideToFirebaseAsync(projectNumber, ovr);
        var hash = ProjectHasher.HashProjectNumber(projectNumber);
        await WriteHistoryAsync("field_added", $"project:{hash[..8]}", editedBy, prevJson,
            $"Added project-specific field '{field.DisplayName}' ({field.Key}) to category '{field.Category}'");
        SaveLocalCache();
        StructureUpdated?.Invoke();
    }

    public async Task AddProjectCategoryAsync(string projectNumber, MasterCategoryDefinition category, string editedBy)
    {
        category.IsBuiltIn = false;
        var ovr = GetOrCreateProjectOverride(projectNumber);
        category.SortOrder = GetMergedCategories(projectNumber).Count;

        var prevJson = JsonConvert.SerializeObject(ovr);
        ovr.ExtraCategories.Add(category);
        ovr.UpdatedBy = editedBy;
        ovr.UpdatedAt = DateTime.UtcNow;

        await SaveProjectOverrideToFirebaseAsync(projectNumber, ovr);
        var hash = ProjectHasher.HashProjectNumber(projectNumber);
        await WriteHistoryAsync("category_added", $"project:{hash[..8]}", editedBy, prevJson,
            $"Added project-specific category '{category.Name}'");
        SaveLocalCache();
        StructureUpdated?.Invoke();
    }

    public async Task ExtendProjectFieldAsync(string projectNumber, string fieldKey, List<string> newValues, string editedBy)
    {
        var ovr = GetOrCreateProjectOverride(projectNumber);
        var prevJson = JsonConvert.SerializeObject(ovr);

        if (!ovr.FieldExtensions.TryGetValue(fieldKey, out var existing))
        {
            existing = new List<string>();
            ovr.FieldExtensions[fieldKey] = existing;
        }

        var added = new List<string>();
        foreach (var v in newValues)
        {
            if (!existing.Contains(v, StringComparer.OrdinalIgnoreCase))
            {
                existing.Add(v);
                added.Add(v);
            }
        }

        if (added.Count == 0) return;

        ovr.UpdatedBy = editedBy;
        ovr.UpdatedAt = DateTime.UtcNow;

        await SaveProjectOverrideToFirebaseAsync(projectNumber, ovr);
        var hash = ProjectHasher.HashProjectNumber(projectNumber);
        await WriteHistoryAsync("dropdown_extended", $"project:{hash[..8]}", editedBy, prevJson,
            $"Extended field '{fieldKey}' with {added.Count} new values for this project: {string.Join(", ", added)}");
        SaveLocalCache();
        StructureUpdated?.Invoke();
    }

    // --- Editor operations: removal ---

    public async Task RemoveMasterFieldAsync(string fieldKey, string editedBy)
    {
        var field = _masterStructure.Fields.FirstOrDefault(f => f.Key.Equals(fieldKey, StringComparison.OrdinalIgnoreCase));
        if (field == null || field.IsBuiltIn) return;

        var prevJson = JsonConvert.SerializeObject(_masterStructure);
        _masterStructure.Fields.Remove(field);
        _masterStructure.Version++;
        _masterStructure.UpdatedBy = editedBy;
        _masterStructure.UpdatedAt = DateTime.UtcNow;

        await SaveMasterToFirebaseAsync();
        await WriteHistoryAsync("field_removed", "master", editedBy, prevJson,
            $"Removed field '{field.DisplayName}' ({field.Key}) from category '{field.Category}'");
        SaveLocalCache();
        StructureUpdated?.Invoke();
    }

    public async Task RemoveMasterCategoryAsync(string categoryName, string editedBy)
    {
        var cat = _masterStructure.Categories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
        if (cat == null || cat.IsBuiltIn) return;

        var prevJson = JsonConvert.SerializeObject(_masterStructure);
        _masterStructure.Categories.Remove(cat);
        // Also remove any fields in this category
        var fieldsRemoved = _masterStructure.Fields.RemoveAll(f =>
            (f.Category ?? "").Equals(categoryName, StringComparison.OrdinalIgnoreCase) && !f.IsBuiltIn);
        _masterStructure.Version++;
        _masterStructure.UpdatedBy = editedBy;
        _masterStructure.UpdatedAt = DateTime.UtcNow;

        await SaveMasterToFirebaseAsync();
        await WriteHistoryAsync("category_removed", "master", editedBy, prevJson,
            $"Removed category '{categoryName}' and {fieldsRemoved} field(s)");
        SaveLocalCache();
        StructureUpdated?.Invoke();
    }

    public async Task RemoveProjectFieldAsync(string projectNumber, string fieldKey, string editedBy)
    {
        if (!_projectOverrides.TryGetValue(projectNumber, out var ovr)) return;
        var field = ovr.ExtraFields.FirstOrDefault(f => f.Key.Equals(fieldKey, StringComparison.OrdinalIgnoreCase));
        if (field == null) return;

        var prevJson = JsonConvert.SerializeObject(ovr);
        ovr.ExtraFields.Remove(field);
        ovr.UpdatedBy = editedBy;
        ovr.UpdatedAt = DateTime.UtcNow;

        await SaveProjectOverrideToFirebaseAsync(projectNumber, ovr);
        var hash = ProjectHasher.HashProjectNumber(projectNumber);
        await WriteHistoryAsync("field_removed", $"project:{hash[..8]}", editedBy, prevJson,
            $"Removed project-specific field '{field.DisplayName}' ({field.Key})");
        SaveLocalCache();
        StructureUpdated?.Invoke();
    }

    public async Task RemoveProjectCategoryAsync(string projectNumber, string categoryName, string editedBy)
    {
        if (!_projectOverrides.TryGetValue(projectNumber, out var ovr)) return;
        var cat = ovr.ExtraCategories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
        if (cat == null) return;

        var prevJson = JsonConvert.SerializeObject(ovr);
        ovr.ExtraCategories.Remove(cat);
        var fieldsRemoved = ovr.ExtraFields.RemoveAll(f =>
            (f.Category ?? "").Equals(categoryName, StringComparison.OrdinalIgnoreCase));
        ovr.UpdatedBy = editedBy;
        ovr.UpdatedAt = DateTime.UtcNow;

        await SaveProjectOverrideToFirebaseAsync(projectNumber, ovr);
        var hash = ProjectHasher.HashProjectNumber(projectNumber);
        await WriteHistoryAsync("category_removed", $"project:{hash[..8]}", editedBy, prevJson,
            $"Removed project-specific category '{categoryName}' and {fieldsRemoved} field(s)");
        SaveLocalCache();
        StructureUpdated?.Invoke();
    }

    public async Task<ProjectStructureOverride?> GetProjectOverrideAsync(string projectNumber)
    {
        if (_projectOverrides.TryGetValue(projectNumber, out var cached))
            return cached;

        if (!_firebase.IsInitialized)
            return null;

        try
        {
            var hash = ProjectHasher.HashProjectNumber(projectNumber);
            var data = await GetFirebaseDataAsync<Dictionary<string, object>>($"project_tags/{hash}/{ProjectOverrideSubNode}");
            if (data == null) return null;

            var ovr = DeserializeProjectOverride(data);
            _projectOverrides[projectNumber] = ovr;
            SaveLocalCache();
            return ovr;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MasterStructureService: Failed to get project override: {ex.Message}");
            return null;
        }
    }

    public async Task SyncFromFirebaseAsync()
    {
        if (!_firebase.IsInitialized) return;

        try
        {
            // Sync master structure
            var masterData = await GetFirebaseDataAsync<Dictionary<string, object>>(MasterNode);
            if (masterData != null)
            {
                _masterStructure = DeserializeMasterStructure(masterData);
                InfraLogger.Log($"MasterStructureService: Synced master with {_masterStructure.Fields.Count} fields, {_masterStructure.Categories.Count} categories, v{_masterStructure.Version}");
            }

            SaveLocalCache();
            StructureUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MasterStructureService: Sync failed: {ex.Message}");
        }
    }

    // --- Firebase write helpers ---

    private async Task SaveMasterToFirebaseAsync()
    {
        if (!_firebase.IsInitialized)
        {
            InfraLogger.Log("MasterStructureService: Firebase not initialized, saved to local cache only");
            return;
        }

        try
        {
            var data = SerializeMasterStructure(_masterStructure);
            var encrypted = EncryptDictionary(data);
            await PatchFirebaseDataAsync(MasterNode, encrypted);
            InfraLogger.Log($"MasterStructureService: Saved master structure v{_masterStructure.Version}");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MasterStructureService: Firebase save failed: {ex.Message}");
        }
    }

    private async Task SaveProjectOverrideToFirebaseAsync(string projectNumber, ProjectStructureOverride ovr)
    {
        if (!_firebase.IsInitialized)
        {
            InfraLogger.Log("MasterStructureService: Firebase not initialized, saved to local cache only");
            return;
        }

        try
        {
            var hash = ProjectHasher.HashProjectNumber(projectNumber);
            var data = SerializeProjectOverride(ovr);
            var encrypted = EncryptDictionary(data);
            await PatchFirebaseDataAsync($"project_tags/{hash}/{ProjectOverrideSubNode}", encrypted);
            InfraLogger.Log($"MasterStructureService: Saved project override for (hash: {hash[..8]}...)");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MasterStructureService: Firebase project override save failed: {ex.Message}");
        }
    }

    // --- History / Audit ---

    private async Task WriteHistoryAsync(string action, string scope, string editedBy, string previousStateJson, string diffSummary)
    {
        if (!_firebase.IsInitialized) return;

        try
        {
            var historyEntry = new Dictionary<string, object>
            {
                ["action"] = action,
                ["scope"] = scope,
                ["edited_by"] = TagValueEncryptor.Encrypt(editedBy) ?? editedBy,
                ["edited_at"] = DateTime.UtcNow.ToString("o"),
                ["diff_summary"] = TagValueEncryptor.Encrypt(diffSummary) ?? diffSummary,
                ["snapshot"] = TagValueEncryptor.Encrypt(previousStateJson) ?? previousStateJson
            };

            await PostFirebaseDataAsync(HistoryNode, historyEntry);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MasterStructureService: History write failed: {ex.Message}");
        }
    }

    // --- Serialization ---

    private static Dictionary<string, object> SerializeMasterStructure(MasterStructure ms)
    {
        var data = new Dictionary<string, object>
        {
            ["version"] = ms.Version,
            ["updated_at"] = ms.UpdatedAt.ToString("o")
        };

        if (ms.UpdatedBy != null)
            data["updated_by"] = ms.UpdatedBy;

        if (ms.Categories.Count > 0)
            data["categories"] = JsonConvert.SerializeObject(ms.Categories);

        if (ms.Fields.Count > 0)
            data["fields"] = JsonConvert.SerializeObject(ms.Fields);

        if (ms.BuiltInFieldExtensions.Count > 0)
            data["builtin_extensions"] = JsonConvert.SerializeObject(ms.BuiltInFieldExtensions);

        return data;
    }

    private static MasterStructure DeserializeMasterStructure(Dictionary<string, object> data)
    {
        var ms = new MasterStructure();

        if (data.TryGetValue("version", out var ver))
            ms.Version = Convert.ToInt32(ver);

        if (data.TryGetValue("updated_by", out var ub))
            ms.UpdatedBy = DecryptString(ub?.ToString());

        if (data.TryGetValue("updated_at", out var ua) && DateTime.TryParse(ua?.ToString(), out var updatedAt))
            ms.UpdatedAt = updatedAt;

        if (data.TryGetValue("categories", out var catJson))
        {
            var catStr = DecryptString(catJson?.ToString()) ?? catJson?.ToString();
            if (catStr != null)
            {
                try { ms.Categories = JsonConvert.DeserializeObject<List<MasterCategoryDefinition>>(catStr) ?? new(); }
                catch { ms.Categories = new(); }
            }
        }

        if (data.TryGetValue("fields", out var fieldJson))
        {
            var fieldStr = DecryptString(fieldJson?.ToString()) ?? fieldJson?.ToString();
            if (fieldStr != null)
            {
                try { ms.Fields = JsonConvert.DeserializeObject<List<MasterFieldDefinition>>(fieldStr) ?? new(); }
                catch { ms.Fields = new(); }
            }
        }

        if (data.TryGetValue("builtin_extensions", out var extJson))
        {
            var extStr = DecryptString(extJson?.ToString()) ?? extJson?.ToString();
            if (extStr != null)
            {
                try
                {
                    var raw = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(extStr);
                    ms.BuiltInFieldExtensions = raw != null
                        ? new Dictionary<string, List<string>>(raw, StringComparer.OrdinalIgnoreCase)
                        : new(StringComparer.OrdinalIgnoreCase);
                }
                catch { ms.BuiltInFieldExtensions = new(StringComparer.OrdinalIgnoreCase); }
            }
        }

        return ms;
    }

    private static Dictionary<string, object> SerializeProjectOverride(ProjectStructureOverride ovr)
    {
        var data = new Dictionary<string, object>
        {
            ["updated_at"] = ovr.UpdatedAt.ToString("o")
        };

        if (ovr.UpdatedBy != null)
            data["updated_by"] = ovr.UpdatedBy;

        if (ovr.ExtraCategories.Count > 0)
            data["extra_categories"] = JsonConvert.SerializeObject(ovr.ExtraCategories);

        if (ovr.ExtraFields.Count > 0)
            data["extra_fields"] = JsonConvert.SerializeObject(ovr.ExtraFields);

        if (ovr.FieldExtensions.Count > 0)
            data["field_extensions"] = JsonConvert.SerializeObject(ovr.FieldExtensions);

        return data;
    }

    private static ProjectStructureOverride DeserializeProjectOverride(Dictionary<string, object> data)
    {
        var ovr = new ProjectStructureOverride();

        if (data.TryGetValue("updated_by", out var ub))
            ovr.UpdatedBy = DecryptString(ub?.ToString());

        if (data.TryGetValue("updated_at", out var ua) && DateTime.TryParse(ua?.ToString(), out var updatedAt))
            ovr.UpdatedAt = updatedAt;

        if (data.TryGetValue("extra_categories", out var catJson))
        {
            var catStr = DecryptString(catJson?.ToString()) ?? catJson?.ToString();
            if (catStr != null)
            {
                try { ovr.ExtraCategories = JsonConvert.DeserializeObject<List<MasterCategoryDefinition>>(catStr) ?? new(); }
                catch { ovr.ExtraCategories = new(); }
            }
        }

        if (data.TryGetValue("extra_fields", out var fieldJson))
        {
            var fieldStr = DecryptString(fieldJson?.ToString()) ?? fieldJson?.ToString();
            if (fieldStr != null)
            {
                try { ovr.ExtraFields = JsonConvert.DeserializeObject<List<MasterFieldDefinition>>(fieldStr) ?? new(); }
                catch { ovr.ExtraFields = new(); }
            }
        }

        if (data.TryGetValue("field_extensions", out var extJson))
        {
            var extStr = DecryptString(extJson?.ToString()) ?? extJson?.ToString();
            if (extStr != null)
            {
                try
                {
                    var raw = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(extStr);
                    ovr.FieldExtensions = raw != null
                        ? new Dictionary<string, List<string>>(raw, StringComparer.OrdinalIgnoreCase)
                        : new(StringComparer.OrdinalIgnoreCase);
                }
                catch { ovr.FieldExtensions = new(StringComparer.OrdinalIgnoreCase); }
            }
        }

        return ovr;
    }

    // --- Encryption helpers ---

    private static Dictionary<string, object> EncryptDictionary(Dictionary<string, object> data)
    {
        var encrypted = new Dictionary<string, object>();
        foreach (var (key, value) in data)
        {
            if (value is string strVal && key != "version")
                encrypted[key] = TagValueEncryptor.Encrypt(strVal) ?? strVal;
            else
                encrypted[key] = value;
        }
        return encrypted;
    }

    private static string? DecryptString(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return TagValueEncryptor.Decrypt(value);
    }

    // --- Project override cache helper ---

    private ProjectStructureOverride GetOrCreateProjectOverride(string projectNumber)
    {
        return _projectOverrides.GetOrAdd(projectNumber, _ => new ProjectStructureOverride());
    }

    // --- Local cache ---

    private void LoadLocalCache()
    {
        if (!File.Exists(_cacheFilePath)) return;

        try
        {
            var json = File.ReadAllText(_cacheFilePath);
            var cache = JsonConvert.DeserializeObject<MasterStructureCacheFile>(json);
            if (cache == null) return;

            if (cache.MasterStructure != null)
                _masterStructure = cache.MasterStructure;

            if (cache.ProjectOverrides != null)
            {
                foreach (var (key, val) in cache.ProjectOverrides)
                    _projectOverrides[key] = val;
            }
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MasterStructureService: Failed to load cache: {ex.Message}");
        }
    }

    private void SaveLocalCache()
    {
        try
        {
            var cache = new MasterStructureCacheFile
            {
                LastSynced = DateTime.UtcNow,
                MasterStructure = _masterStructure,
                ProjectOverrides = _projectOverrides.ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value,
                    StringComparer.OrdinalIgnoreCase)
            };

            var json = JsonConvert.SerializeObject(cache, Formatting.Indented);
            File.WriteAllText(_cacheFilePath, json);
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MasterStructureService: Failed to save cache: {ex.Message}");
        }
    }

    // --- Firebase HTTP helpers (same reflection pattern as ProjectTagService) ---

    private async Task<T?> GetFirebaseDataAsync<T>(string path)
    {
        var method = _firebase.GetType().GetMethod("GetDataAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
        {
            InfraLogger.Log("MasterStructureService: Cannot find GetDataAsync method on FirebaseService");
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
            InfraLogger.Log("MasterStructureService: Cannot find PatchDataAsync method on FirebaseService");
            return;
        }
        var task = (Task?)method.Invoke(_firebase, new object[] { path, data });
        if (task != null) await task;
    }

    private async Task PostFirebaseDataAsync(string path, object data)
    {
        var method = _firebase.GetType().GetMethod("PostDataAsync",
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (method == null)
        {
            // Fallback: use PatchDataAsync with a timestamp-based key
            var key = DateTime.UtcNow.Ticks.ToString();
            await PatchFirebaseDataAsync($"{path}/{key}", data);
            return;
        }
        var task = (Task?)method.Invoke(_firebase, new object[] { path, data });
        if (task != null) await task;
    }

    // --- Cache file model ---

    private class MasterStructureCacheFile
    {
        public DateTime LastSynced { get; set; }
        public MasterStructure? MasterStructure { get; set; }
        public Dictionary<string, ProjectStructureOverride>? ProjectOverrides { get; set; }
    }
}
