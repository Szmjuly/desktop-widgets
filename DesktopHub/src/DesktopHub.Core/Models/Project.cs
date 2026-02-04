namespace DesktopHub.Core.Models;

/// <summary>
/// Represents a project folder scanned from Q: or P: drive
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
    /// Full filesystem path to project folder (primary location)
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Project year (e.g., "2024")
    /// </summary>
    public string Year { get; set; } = string.Empty;

    /// <summary>
    /// Drive location: "Q" (Florida) or "P" (Connecticut)
    /// </summary>
    public string DriveLocation { get; set; } = "Q";

    /// <summary>
    /// Alternative path if project exists on another drive
    /// </summary>
    public string? AlternatePath { get; set; }

    /// <summary>
    /// Alternative drive location if project exists on another drive
    /// </summary>
    public string? AlternateDriveLocation { get; set; }

    /// <summary>
    /// Display string for UI (e.g., "2024638.001 - Project Name")
    /// </summary>
    public string Display => $"{FullNumber} - {Name}";

    /// <summary>
    /// Display name for drive location
    /// </summary>
    public string DriveLocationDisplay => DriveLocation == "Q" ? "Florida" : "Connecticut";

    /// <summary>
    /// Display name for alternate drive location
    /// </summary>
    public string? AlternateDriveLocationDisplay => AlternateDriveLocation == "Q" ? "Florida" : 
                                                     AlternateDriveLocation == "P" ? "Connecticut" : null;

    /// <summary>
    /// Whether this project exists on multiple drives
    /// </summary>
    public bool HasMultipleLocations => !string.IsNullOrEmpty(AlternatePath);

    /// <summary>
    /// When this project was last scanned from filesystem
    /// </summary>
    public DateTime LastScanned { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// User-added metadata (tags, status, location, etc.)
    /// </summary>
    public ProjectMetadata? Metadata { get; set; }
}
