namespace DesktopHub.Core.Models;

/// <summary>
/// Represents a search result with relevance scoring
/// </summary>
public class SearchResult
{
    /// <summary>
    /// The matched project
    /// </summary>
    public Project Project { get; set; } = null!;

    /// <summary>
    /// Relevance score (0.0 to 1.0, higher is better match)
    /// </summary>
    public double Score { get; set; }

    /// <summary>
    /// Which fields matched the search query
    /// </summary>
    public List<string> MatchedFields { get; set; } = new();

    /// <summary>
    /// Highlighted portions of the match (for UI display)
    /// </summary>
    public Dictionary<string, string> Highlights { get; set; } = new();
}
