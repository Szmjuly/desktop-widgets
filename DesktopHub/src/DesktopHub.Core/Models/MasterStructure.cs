namespace DesktopHub.Core.Models;

/// <summary>
/// A field definition that can be stored in Firebase as part of the master structure
/// or a project-specific override. Serializable version of <see cref="TagFieldDefinition"/>.
/// </summary>
public class MasterFieldDefinition
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string[] Aliases { get; set; } = Array.Empty<string>();
    public string[] SuggestedValues { get; set; } = Array.Empty<string>();
    public string? Category { get; set; }
    public TagInputMode InputMode { get; set; } = TagInputMode.Dropdown;

    /// <summary>True for the original 19 hardcoded fields from TagFieldRegistry.</summary>
    public bool IsBuiltIn { get; set; }

    /// <summary>Display order within the category.</summary>
    public int SortOrder { get; set; }

    /// <summary>Convert a static TagFieldDefinition to a MasterFieldDefinition.</summary>
    public static MasterFieldDefinition FromBuiltIn(TagFieldDefinition def, int sortOrder) => new()
    {
        Key = def.Key,
        DisplayName = def.DisplayName,
        Aliases = def.Aliases,
        SuggestedValues = def.SuggestedValues,
        Category = def.Category,
        InputMode = def.InputMode,
        IsBuiltIn = true,
        SortOrder = sortOrder
    };
}

/// <summary>
/// A category definition that can be stored in Firebase.
/// </summary>
public class MasterCategoryDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "\U0001F4CB"; // clipboard emoji
    public int SortOrder { get; set; }
    public bool IsBuiltIn { get; set; }
}

/// <summary>
/// The top-level master structure stored in Firebase. Contains category definitions,
/// field definitions, and extension data for built-in fields (extra dropdown options).
/// </summary>
public class MasterStructure
{
    public int Version { get; set; } = 1;
    public List<MasterCategoryDefinition> Categories { get; set; } = new();
    public List<MasterFieldDefinition> Fields { get; set; } = new();

    /// <summary>
    /// Extra dropdown options for built-in fields. Key is the field key, value is a list
    /// of additional suggested values to merge with the built-in defaults.
    /// </summary>
    public Dictionary<string, List<string>> BuiltInFieldExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Per-project field/category additions stored alongside existing project_tags data.
/// </summary>
public class ProjectStructureOverride
{
    public List<MasterCategoryDefinition> ExtraCategories { get; set; } = new();
    public List<MasterFieldDefinition> ExtraFields { get; set; } = new();

    /// <summary>
    /// Extra dropdown options for fields, scoped to this project only.
    /// Key is the field key, value is additional suggested values.
    /// </summary>
    public Dictionary<string, List<string>> FieldExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Audit trail entry for structure changes (master or project-specific).
/// </summary>
public class StructureChangeHistoryEntry
{
    /// <summary>Action type: field_added, category_added, dropdown_extended, field_removed, etc.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Scope: "master" or "project:{projectHash}".</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Encrypted username of the editor who made the change.</summary>
    public string EditedBy { get; set; } = string.Empty;

    /// <summary>ISO 8601 timestamp of when the change was made.</summary>
    public string EditedAt { get; set; } = string.Empty;

    /// <summary>Encrypted summary of what changed.</summary>
    public string DiffSummary { get; set; } = string.Empty;

    /// <summary>Encrypted JSON snapshot of the previous state (for rollback/audit).</summary>
    public string? Snapshot { get; set; }
}
