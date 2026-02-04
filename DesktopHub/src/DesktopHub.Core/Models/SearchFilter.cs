namespace DesktopHub.Core.Models;

/// <summary>
/// Parsed search filters from user input
/// </summary>
public class SearchFilter
{
    /// <summary>
    /// General search text (fuzzy matched against name/number)
    /// </summary>
    public string? SearchText { get; set; }

    /// <summary>
    /// Filter by locations (e.g., ["Miami", "Boca"])
    /// </summary>
    public List<string> Locations { get; set; } = new();

    /// <summary>
    /// Filter by statuses (e.g., ["Active", "Completed"])
    /// </summary>
    public List<string> Statuses { get; set; } = new();

    /// <summary>
    /// Filter by years (e.g., ["2024", "2023"])
    /// </summary>
    public List<string> Years { get; set; } = new();

    /// <summary>
    /// Filter by drive location (e.g., "Q", "P", "All")
    /// </summary>
    public string? DriveLocation { get; set; }

    /// <summary>
    /// Filter by tags (e.g., ["residential", "commercial"])
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Filter by teams (e.g., ["Design", "Engineering"])
    /// </summary>
    public List<string> Teams { get; set; } = new();

    /// <summary>
    /// Show only favorites
    /// </summary>
    public bool? FavoritesOnly { get; set; }

    /// <summary>
    /// Maximum number of results to return
    /// </summary>
    public int MaxResults { get; set; } = 10;
}
