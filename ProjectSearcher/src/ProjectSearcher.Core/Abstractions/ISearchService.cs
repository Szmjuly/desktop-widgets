using ProjectSearcher.Core.Models;

namespace ProjectSearcher.Core.Abstractions;

/// <summary>
/// Service for searching and filtering projects
/// </summary>
public interface ISearchService
{
    /// <summary>
    /// Search projects with filters and fuzzy matching
    /// </summary>
    /// <param name="query">Raw search query (may contain prefix filters)</param>
    /// <param name="projects">List of projects to search</param>
    /// <returns>Ranked search results</returns>
    Task<List<SearchResult>> SearchAsync(string query, List<Project> projects);

    /// <summary>
    /// Parse search query into structured filters
    /// </summary>
    /// <param name="query">Raw search query</param>
    /// <returns>Parsed search filter</returns>
    SearchFilter ParseQuery(string query);

    /// <summary>
    /// Calculate fuzzy match score between two strings (0.0 to 1.0)
    /// </summary>
    /// <param name="source">Source string</param>
    /// <param name="target">Target string to match against</param>
    /// <returns>Match score (higher is better)</returns>
    double CalculateFuzzyScore(string source, string target);
}
