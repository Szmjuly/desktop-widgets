using System.Text.Json;

namespace DesktopHub.Infrastructure.Settings;

/// <summary>
/// Configuration for the Quick Launch widget — user-defined folders/apps to launch
/// </summary>
public class QuickLaunchConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DesktopHub", "widgets");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "quicklaunch.json");

    /// <summary>
    /// User-configured launch items
    /// </summary>
    public List<QuickLaunchItem> Items { get; set; } = new();

    public static async Task<QuickLaunchConfig> LoadAsync()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = await File.ReadAllTextAsync(ConfigPath);
                return JsonSerializer.Deserialize<QuickLaunchConfig>(json) ?? new QuickLaunchConfig();
            }
        }
        catch { }
        return new QuickLaunchConfig();
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

/// <summary>
/// A single quick-launch item (folder, file, or URL)
/// </summary>
public class QuickLaunchItem
{
    /// <summary>
    /// Unique identifier
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Display name (user-configurable)
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Path to the folder, file, or URL
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Icon emoji or text to display (user-configurable)
    /// </summary>
    public string Icon { get; set; } = "📁";

    /// <summary>
    /// Sort order
    /// </summary>
    public int SortOrder { get; set; }
}
