using System.Text.Json;

namespace DesktopHub.Infrastructure.Settings;

/// <summary>
/// Configuration for the Document Quick Open widget
/// </summary>
public class DocWidgetConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DesktopHub", "widgets");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "docquickopen.json");

    // === Display ===

    /// <summary>
    /// Whether the widget is enabled in the launcher
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Show file size in the list
    /// </summary>
    public bool ShowFileSize { get; set; } = true;

    /// <summary>
    /// Show last modified date in the list
    /// </summary>
    public bool ShowDateModified { get; set; } = true;

    /// <summary>
    /// Show file extension in the filename
    /// </summary>
    public bool ShowFileExtension { get; set; } = true;

    /// <summary>
    /// Compact row spacing
    /// </summary>
    public bool CompactMode { get; set; } = false;

    // === Scanning ===

    /// <summary>
    /// File extensions to include (without dot)
    /// </summary>
    public List<string> Extensions { get; set; } = new()
    {
        "pdf", "dwg", "rvt", "dwf", "dwfx", "dxf",
        "doc", "docx", "xlsx", "xls", "csv",
        "ppt", "pptx", "txt", "msg"
    };

    /// <summary>
    /// Maximum subfolder depth to recurse (1-5)
    /// </summary>
    public int MaxDepth { get; set; } = 0;

    /// <summary>
    /// Folder names to exclude from scanning (case-insensitive)
    /// </summary>
    public List<string> ExcludedFolders { get; set; } = new() { "Archive" };

    /// <summary>
    /// Maximum files to return per scan (50-500)
    /// </summary>
    public int MaxFiles { get; set; } = 200;

    // === Behavior ===

    /// <summary>
    /// Sort order: "name", "date", "type", "size"
    /// </summary>
    public string SortBy { get; set; } = "name";

    /// <summary>
    /// Group by: "none", "category", "extension", "subfolder"
    /// </summary>
    public string GroupBy { get; set; } = "category";

    /// <summary>
    /// Number of recent file opens to remember (0-20)
    /// </summary>
    public int RecentFilesCount { get; set; } = 10;

    /// <summary>
    /// Remember last selected project on restart
    /// </summary>
    public bool AutoOpenLastProject { get; set; } = false;

    /// <summary>
    /// Last selected project path (for auto-open)
    /// </summary>
    public string? LastProjectPath { get; set; }

    /// <summary>
    /// Last selected project display name
    /// </summary>
    public string? LastProjectName { get; set; }

    /// <summary>
    /// Last selected discipline (Electrical, Mechanical, Plumbing)
    /// </summary>
    public string LastDiscipline { get; set; } = "Mechanical";

    /// <summary>
    /// Recently opened file paths
    /// </summary>
    public List<string> RecentFiles { get; set; } = new();

    /// <summary>
    /// Pinned file paths
    /// </summary>
    public List<string> PinnedFiles { get; set; } = new();

    public static async Task<DocWidgetConfig> LoadAsync()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = await File.ReadAllTextAsync(ConfigPath);
                return JsonSerializer.Deserialize<DocWidgetConfig>(json) ?? new DocWidgetConfig();
            }
        }
        catch { }
        return new DocWidgetConfig();
    }

    public async Task SaveAsync()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(ConfigPath, json);
        }
        catch { }
    }
}
