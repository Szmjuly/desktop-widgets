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

    /// <summary>
    /// True if this result was included because a sibling project (same base number) matched the query.
    /// For example, "2024337.02-B2 - Employee Lounge Addition" is a related match when searching "Boca West"
    /// because "2024337.00 Boca West Renovations" directly matched.
    /// </summary>
    public bool IsRelatedMatch { get; set; }

    /// <summary>
    /// True if all query tokens matched but NOT in the same order as the query.
    /// For example, "West Boca Outpatient" is a loose match for query "Boca West".
    /// </summary>
    public bool IsLooseTokenMatch { get; set; }

    /// <summary>
    /// True if this result shares a base project number with a direct match but only 1 direct match
    /// exists for that base number. Indicates a possible folder naming issue / duplicate number.
    /// For example, "P260261 - 516 Clematis" shares base 260261 with "P260261.00 - Boca West Country Club"
    /// but they appear to be unrelated projects using the same number.
    /// </summary>
    public bool IsDuplicateNumber { get; set; }
}
