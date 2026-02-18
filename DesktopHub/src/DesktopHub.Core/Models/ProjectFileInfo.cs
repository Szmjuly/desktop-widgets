namespace DesktopHub.Core.Models;

/// <summary>
/// Detected project type: CAD-based with discipline folders, or Revit-based
/// </summary>
public enum ProjectType
{
    Unknown,
    Cad,
    Revit
}

/// <summary>
/// MEP discipline (subfolder) within a CAD project
/// </summary>
public enum Discipline
{
    Electrical,
    Mechanical,
    Plumbing
}

/// <summary>
/// Metadata about a Revit project's "Revit File" folder
/// </summary>
public class RevitInfo
{
    /// <summary>
    /// Full path to the "Revit File" folder
    /// </summary>
    public string RevitFolderPath { get; set; } = string.Empty;

    /// <summary>
    /// Detected Revit version (e.g., "2024", "2026"), empty if unknown
    /// </summary>
    public string RevitVersion { get; set; } = string.Empty;

    /// <summary>
    /// Whether the project is a cloud model (BIM 360 / ACC)
    /// </summary>
    public bool IsCloudProject { get; set; }

    /// <summary>
    /// Whether a CRevit indicator was found
    /// </summary>
    public bool IsCRevit { get; set; }

    /// <summary>
    /// Local .rvt files found (empty for cloud-only projects)
    /// </summary>
    public List<DocumentItem> RvtFiles { get; set; } = new();

    /// <summary>
    /// True when cloud status is explicitly known (vs just guessed)
    /// </summary>
    public bool CloudStatusExplicit { get; set; }
}

/// <summary>
/// Full scan result for a project — what type it is, what disciplines/files exist
/// </summary>
public class ProjectFileInfo
{
    /// <summary>
    /// The project path that was scanned
    /// </summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the project
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Detected project type
    /// </summary>
    public ProjectType Type { get; set; } = ProjectType.Unknown;

    /// <summary>
    /// Which discipline folders were found (CAD projects)
    /// </summary>
    public List<Discipline> AvailableDisciplines { get; set; } = new();

    /// <summary>
    /// .dwg files keyed by discipline (CAD projects)
    /// </summary>
    public Dictionary<Discipline, List<DocumentItem>> DisciplineFiles { get; set; } = new();

    /// <summary>
    /// Revit project metadata (null when not a Revit project)
    /// </summary>
    public RevitInfo? Revit { get; set; }

    /// <summary>
    /// Aggregated searchable files across the full project path.
    /// Used for smart/regex file search beyond discipline-only views.
    /// </summary>
    public List<DocumentItem> AllFiles { get; set; } = new();

    /// <summary>
    /// True if both CAD discipline folders AND Revit File folder exist (hybrid project)
    /// </summary>
    public bool IsHybrid { get; set; }
}
