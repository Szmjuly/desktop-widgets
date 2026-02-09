using System.Text.Json;

namespace DesktopHub.Infrastructure.Settings;

/// <summary>
/// Configuration for the Quick Tasks widget.
/// Stored in %APPDATA%\DesktopHub\widgets\quicktasks.json
/// </summary>
public class TaskWidgetConfig
{
    /// <summary>
    /// Maximum number of tasks visible before scrolling
    /// </summary>
    public int MaxVisibleTasks { get; set; } = 8;

    /// <summary>
    /// Default priority for new tasks: "low", "normal", "high"
    /// </summary>
    public string DefaultPriority { get; set; } = "normal";

    /// <summary>
    /// Whether to show completed tasks in the list
    /// </summary>
    public bool ShowCompletedTasks { get; set; } = true;

    /// <summary>
    /// Automatically carry over incomplete tasks to the next day
    /// </summary>
    public bool AutoCarryOver { get; set; } = false;

    /// <summary>
    /// User-defined categories for tasks
    /// </summary>
    public List<string> Categories { get; set; } = new()
    {
        "General",
        "RFI",
        "Submittal",
        "Coordination",
        "Field"
    };

    /// <summary>
    /// Accent color for priority indicators and highlights
    /// </summary>
    public string AccentColor { get; set; } = "#4FC3F7";

    /// <summary>
    /// Opacity for completed tasks (0.0 to 1.0)
    /// </summary>
    public double CompletedOpacity { get; set; } = 0.5;

    /// <summary>
    /// Number of past days to show in the day browser
    /// </summary>
    public int DaysToShow { get; set; } = 10;

    /// <summary>
    /// Sort mode: "priority", "created", "manual"
    /// </summary>
    public string SortBy { get; set; } = "manual";

    /// <summary>
    /// Compact mode for tighter spacing
    /// </summary>
    public bool CompactMode { get; set; } = false;

    // --- File I/O ---

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    private static string GetConfigPath()
    {
        var configDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopHub",
            "widgets"
        );
        Directory.CreateDirectory(configDir);
        return Path.Combine(configDir, "quicktasks.json");
    }

    /// <summary>
    /// Load config from disk, or create default if missing
    /// </summary>
    public static async Task<TaskWidgetConfig> LoadAsync()
    {
        var path = GetConfigPath();
        if (File.Exists(path))
        {
            try
            {
                var json = await File.ReadAllTextAsync(path);
                return JsonSerializer.Deserialize<TaskWidgetConfig>(json, _jsonOptions) ?? new TaskWidgetConfig();
            }
            catch
            {
                // Corrupted file — return default and overwrite
                var config = new TaskWidgetConfig();
                await config.SaveAsync();
                return config;
            }
        }

        // First run — create default config
        var defaultConfig = new TaskWidgetConfig();
        await defaultConfig.SaveAsync();
        return defaultConfig;
    }

    /// <summary>
    /// Save config to disk
    /// </summary>
    public async Task SaveAsync()
    {
        var path = GetConfigPath();
        var json = JsonSerializer.Serialize(this, _jsonOptions);
        await File.WriteAllTextAsync(path, json);
    }
}
