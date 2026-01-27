namespace DesktopHub.Core.Models;

/// <summary>
/// User-added metadata for a project
/// </summary>
public class ProjectMetadata
{
    /// <summary>
    /// Project ID this metadata belongs to
    /// </summary>
    public string ProjectId { get; set; } = string.Empty;

    /// <summary>
    /// Project location (e.g., "Miami", "Boca Raton", "Palm Beach")
    /// </summary>
    public string? Location { get; set; }

    /// <summary>
    /// Project status (e.g., "Active", "Completed", "On Hold")
    /// </summary>
    public string? Status { get; set; }

    /// <summary>
    /// User-defined tags (comma-separated or list)
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// User notes about the project
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Whether this project is marked as favorite
    /// </summary>
    public bool IsFavorite { get; set; }

    /// <summary>
    /// Team or department associated with project
    /// </summary>
    public string? Team { get; set; }

    /// <summary>
    /// When this metadata was last updated
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
