using DesktopHub.Core.Models;

namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Service for managing the dynamic master field/category structure and project-specific overrides.
/// Provides merged field definitions (baseline + master + project) for the Project Info panel.
/// </summary>
public interface IMasterStructureService
{
    /// <summary>
    /// Initialize the service: load local cache, optionally sync from Firebase.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Get the raw master structure (without baseline merge).
    /// </summary>
    MasterStructure GetMasterStructure();

    /// <summary>
    /// Get merged field definitions: TagFieldRegistry baseline + master additions + project overrides.
    /// Pass null for projectNumber to get baseline + master only.
    /// </summary>
    List<MasterFieldDefinition> GetMergedFields(string? projectNumber = null);

    /// <summary>
    /// Get merged category definitions: built-in categories + master additions + project overrides.
    /// </summary>
    List<MasterCategoryDefinition> GetMergedCategories(string? projectNumber = null);

    /// <summary>
    /// Get extended dropdown values for a field: baseline suggested values + master extensions + project extensions.
    /// </summary>
    List<string> GetExtendedValues(string fieldKey, string? projectNumber = null);

    // --- Editor operations: master scope ---

    /// <summary>Add a new field definition to the master structure (affects all projects).</summary>
    Task AddMasterFieldAsync(MasterFieldDefinition field, string editedBy);

    /// <summary>Add a new category definition to the master structure (affects all projects).</summary>
    Task AddMasterCategoryAsync(MasterCategoryDefinition category, string editedBy);

    /// <summary>Add extra dropdown options to an existing field in the master structure.</summary>
    Task ExtendBuiltInFieldAsync(string fieldKey, List<string> newValues, string editedBy);

    // --- Editor operations: project scope ---

    /// <summary>Add a new field definition for a specific project only.</summary>
    Task AddProjectFieldAsync(string projectNumber, MasterFieldDefinition field, string editedBy);

    /// <summary>Add a new category definition for a specific project only.</summary>
    Task AddProjectCategoryAsync(string projectNumber, MasterCategoryDefinition category, string editedBy);

    /// <summary>Add extra dropdown options to a field for a specific project only.</summary>
    Task ExtendProjectFieldAsync(string projectNumber, string fieldKey, List<string> newValues, string editedBy);

    // --- Editor operations: removal ---

    /// <summary>Remove a dynamic (non-built-in) field from the master structure.</summary>
    Task RemoveMasterFieldAsync(string fieldKey, string editedBy);

    /// <summary>Remove a dynamic (non-built-in) category from the master structure.</summary>
    Task RemoveMasterCategoryAsync(string categoryName, string editedBy);

    /// <summary>Remove a project-specific field.</summary>
    Task RemoveProjectFieldAsync(string projectNumber, string fieldKey, string editedBy);

    /// <summary>Remove a project-specific category.</summary>
    Task RemoveProjectCategoryAsync(string projectNumber, string categoryName, string editedBy);

    /// <summary>
    /// Get the project-specific structure override (if any).
    /// </summary>
    Task<ProjectStructureOverride?> GetProjectOverrideAsync(string projectNumber);

    /// <summary>
    /// Sync master structure from Firebase (full refresh).
    /// </summary>
    Task SyncFromFirebaseAsync();

    /// <summary>
    /// Fired when the master structure or any project override is updated.
    /// UI should rebuild field rendering when this fires.
    /// </summary>
    event Action? StructureUpdated;
}
