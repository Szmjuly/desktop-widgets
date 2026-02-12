namespace DesktopHub.Core.Models;

/// <summary>
/// Tracks how many times a project has been launched from the search overlay
/// </summary>
public class ProjectLaunchRecord
{
    /// <summary>
    /// Project path (primary key)
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Full project number (e.g., "2024638.001")
    /// </summary>
    public string FullNumber { get; set; } = string.Empty;

    /// <summary>
    /// Project name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Total number of times this project was launched
    /// </summary>
    public int LaunchCount { get; set; }

    /// <summary>
    /// When the project was last launched
    /// </summary>
    public DateTime LastLaunched { get; set; }
}
