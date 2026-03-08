namespace DesktopHub.Core.Models;

/// <summary>
/// Structured tag data for a project, stored in Firebase RTDB under an HMAC-hashed key.
/// </summary>
public class ProjectTags
{
    // --- Electrical ---
    public string? Voltage { get; set; }
    public string? Phase { get; set; }
    public string? AmperageService { get; set; }
    public string? AmperageGenerator { get; set; }
    public string? GeneratorBrand { get; set; }
    public string? GeneratorLoadKw { get; set; }

    // --- HVAC / Mechanical ---
    public string? HvacType { get; set; }
    public string? HvacBrand { get; set; }
    public string? HvacTonnage { get; set; }
    public string? HvacLoadKw { get; set; }

    // --- Building ---
    public string? SquareFootage { get; set; }
    public string? BuildType { get; set; }

    // --- Location ---
    public string? LocationCity { get; set; }
    public string? LocationState { get; set; }
    public string? LocationMunicipality { get; set; }
    public string? LocationAddress { get; set; }

    // --- People ---
    public string? StampingEngineer { get; set; }
    public List<string> Engineers { get; set; } = new();

    // --- Code ---
    public List<string> CodeReferences { get; set; } = new();

    // --- Free-form custom tags (key → value) ---
    public Dictionary<string, string> Custom { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // --- Audit ---
    public string? UpdatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// How a tag field should be rendered in the Project Info panel.
/// </summary>
public enum TagInputMode
{
    /// <summary>Editable dropdown: preset list + user can type a custom value.</summary>
    Dropdown,
    /// <summary>Free-text input (no preset list, e.g. address, square footage).</summary>
    FreeText,
    /// <summary>Multi-select: user can pick multiple values (e.g. engineers, code refs).</summary>
    MultiSelect
}

/// <summary>
/// Defines a known tag field with its canonical key, display name, aliases, and suggested values.
/// </summary>
public class TagFieldDefinition
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string[] Aliases { get; init; } = Array.Empty<string>();
    public string[] SuggestedValues { get; init; } = Array.Empty<string>();
    public string? Category { get; init; }
    public TagInputMode InputMode { get; init; } = TagInputMode.Dropdown;
}
