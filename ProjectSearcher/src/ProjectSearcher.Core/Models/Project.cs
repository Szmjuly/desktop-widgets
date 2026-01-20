namespace ProjectSearcher.Core.Models;

/// <summary>
/// Represents a project folder scanned from Q: drive
/// </summary>
public class Project
{
    /// <summary>
    /// Unique identifier (generated from path hash or database ID)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Full project number (e.g., "2024638.001" or "P250784.00")
    /// </summary>
    public string FullNumber { get; set; } = string.Empty;

    /// <summary>
    /// Short project number (last 4-6 digits for quick reference)
    /// </summary>
    public string ShortNumber { get; set; } = string.Empty;

    /// <summary>
    /// Project name extracted from folder name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Full filesystem path to project folder
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Project year (e.g., "2024")
    /// </summary>
    public string Year { get; set; } = string.Empty;

    /// <summary>
    /// Display string for UI (e.g., "2024638.001 - Project Name")
    /// </summary>
    public string Display => $"{FullNumber} - {Name}";

    /// <summary>
    /// When this project was last scanned from filesystem
    /// </summary>
    public DateTime LastScanned { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User-added metadata (tags, status, location, etc.)
    /// </summary>
    public ProjectMetadata? Metadata { get; set; }
}
