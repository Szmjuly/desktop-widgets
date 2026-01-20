using System.Text.RegularExpressions;
using ProjectSearcher.Core.Abstractions;
using ProjectSearcher.Core.Models;

namespace ProjectSearcher.Infrastructure.Search;

/// <summary>
/// Service for searching and filtering projects with fuzzy matching
/// </summary>
public class SearchService : ISearchService
{
    public async Task<List<SearchResult>> SearchAsync(string query, List<Project> projects)
    {
        return await Task.Run(() =>
        {
            var filter = ParseQuery(query);
            var results = new List<SearchResult>();

            foreach (var project in projects)
            {
                // Apply structured filters first
                if (!PassesFilters(project, filter))
                    continue;

                // Calculate relevance score
                var score = CalculateRelevanceScore(project, filter);
                if (score > 0.0)
                {
                    results.Add(new SearchResult
                    {
                        Project = project,
                        Score = score,
                        MatchedFields = GetMatchedFields(project, filter)
                    });
                }
            }

            // Sort by score descending
            results = results.OrderByDescending(r => r.Score).Take(filter.MaxResults).ToList();
            return results;
        });
    }

    public SearchFilter ParseQuery(string query)
    {
        var filter = new SearchFilter();
        var segments = query.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var remainingSegments = new List<string>();

        foreach (var segment in segments)
        {
            var trimmed = segment.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            // Check for location filter
            var locMatch = Regex.Match(trimmed, @"^(location|loc):\s*(.+)$", RegexOptions.IgnoreCase);
            if (locMatch.Success)
            {
                var locations = locMatch.Groups[2].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                filter.Locations.AddRange(locations);
                continue;
            }

            // Check for status filter
            var statusMatch = Regex.Match(trimmed, @"^status:\s*(.+)$", RegexOptions.IgnoreCase);
            if (statusMatch.Success)
            {
                var statuses = statusMatch.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                filter.Statuses.AddRange(statuses);
                continue;
            }

            // Check for year filter
            var yearMatch = Regex.Match(trimmed, @"^year:\s*(.+)$", RegexOptions.IgnoreCase);
            if (yearMatch.Success)
            {
                var years = yearMatch.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                filter.Years.AddRange(years);
                continue;
            }

            // Check for tag filter
            var tagMatch = Regex.Match(trimmed, @"^tags?:\s*(.+)$", RegexOptions.IgnoreCase);
            if (tagMatch.Success)
            {
                var tags = tagMatch.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                filter.Tags.AddRange(tags);
                continue;
            }

            // Check for team filter
            var teamMatch = Regex.Match(trimmed, @"^team:\s*(.+)$", RegexOptions.IgnoreCase);
            if (teamMatch.Success)
            {
                var teams = teamMatch.Groups[1].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                filter.Teams.AddRange(teams);
                continue;
            }

            // Check for favorites filter
            if (Regex.IsMatch(trimmed, @"^(fav|favorite|favorites)$", RegexOptions.IgnoreCase))
            {
                filter.FavoritesOnly = true;
                continue;
            }

            // Not a prefix filter, add to general search text
            remainingSegments.Add(trimmed);
        }

        filter.SearchText = string.Join(" ", remainingSegments);
        return filter;
    }

    public double CalculateFuzzyScore(string source, string target)
    {
        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(target))
            return 0.0;

        source = source.ToLowerInvariant();
        target = target.ToLowerInvariant();

        // Exact match
        if (source == target)
            return 1.0;

        // Contains match
        if (target.Contains(source))
        {
            return 0.8 + (0.2 * (1.0 - (target.Length - source.Length) / (double)target.Length));
        }

        // Starts with match
        if (target.StartsWith(source))
        {
            return 0.7;
        }

        // Word-based matching: check if any word in target starts with or contains source
        var words = target.Split(new[] { ' ', '-', '_', '.' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var word in words)
        {
            if (word.StartsWith(source))
                return 0.75; // High score for word prefix match
            
            if (word.Contains(source))
                return 0.65; // Good score for word contains match
        }

        // Levenshtein distance - more lenient threshold for short queries
        var distance = CalculateLevenshteinDistance(source, target);
        var maxLength = Math.Max(source.Length, target.Length);
        var similarity = 1.0 - (distance / (double)maxLength);

        // Lower threshold for short search terms (3 chars or less)
        var threshold = source.Length <= 3 ? 0.3 : 0.5;
        return similarity > threshold ? similarity * 0.6 : 0.0;
    }

    private bool PassesFilters(Project project, SearchFilter filter)
    {
        // Location filter
        if (filter.Locations.Any())
        {
            var location = project.Metadata?.Location?.ToLowerInvariant();
            if (location == null || !filter.Locations.Any(l => location.Contains(l.ToLowerInvariant())))
                return false;
        }

        // Status filter
        if (filter.Statuses.Any())
        {
            var status = project.Metadata?.Status?.ToLowerInvariant();
            if (status == null || !filter.Statuses.Any(s => status.Equals(s, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        // Year filter
        if (filter.Years.Any())
        {
            if (!filter.Years.Any(y => project.Year.Contains(y)))
                return false;
        }

        // Tag filter
        if (filter.Tags.Any())
        {
            var tags = project.Metadata?.Tags ?? new List<string>();
            if (!filter.Tags.Any(t => tags.Any(pt => pt.Equals(t, StringComparison.OrdinalIgnoreCase))))
                return false;
        }

        // Team filter
        if (filter.Teams.Any())
        {
            var team = project.Metadata?.Team?.ToLowerInvariant();
            if (team == null || !filter.Teams.Any(t => team.Equals(t, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        // Favorites filter
        if (filter.FavoritesOnly == true)
        {
            if (project.Metadata?.IsFavorite != true)
                return false;
        }

        return true;
    }

    private double CalculateRelevanceScore(Project project, SearchFilter filter)
    {
        if (string.IsNullOrWhiteSpace(filter.SearchText))
        {
            // No search text, just return 1.0 if it passed filters
            return 1.0;
        }

        var searchText = filter.SearchText.ToLowerInvariant();
        var maxScore = 0.0;

        // Check full number
        var fullNumberScore = CalculateFuzzyScore(searchText, project.FullNumber);
        maxScore = Math.Max(maxScore, fullNumberScore * 1.2); // Boost number matches

        // Check short number
        var shortNumberScore = CalculateFuzzyScore(searchText, project.ShortNumber);
        maxScore = Math.Max(maxScore, shortNumberScore * 1.1);

        // Check name
        var nameScore = CalculateFuzzyScore(searchText, project.Name);
        maxScore = Math.Max(maxScore, nameScore);

        // Boost favorites
        if (project.Metadata?.IsFavorite == true)
        {
            maxScore *= 1.1;
        }

        return Math.Min(maxScore, 1.0); // Cap at 1.0
    }

    private List<string> GetMatchedFields(Project project, SearchFilter filter)
    {
        var fields = new List<string>();

        if (string.IsNullOrWhiteSpace(filter.SearchText))
            return fields;

        var searchText = filter.SearchText.ToLowerInvariant();

        if (project.FullNumber.ToLowerInvariant().Contains(searchText))
            fields.Add("FullNumber");

        if (project.ShortNumber.ToLowerInvariant().Contains(searchText))
            fields.Add("ShortNumber");

        if (project.Name.ToLowerInvariant().Contains(searchText))
            fields.Add("Name");

        return fields;
    }

    private static int CalculateLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return target?.Length ?? 0;

        if (string.IsNullOrEmpty(target))
            return source.Length;

        var sourceLength = source.Length;
        var targetLength = target.Length;
        var distance = new int[sourceLength + 1, targetLength + 1];

        for (var i = 0; i <= sourceLength; distance[i, 0] = i++) { }
        for (var j = 0; j <= targetLength; distance[0, j] = j++) { }

        for (var i = 1; i <= sourceLength; i++)
        {
            for (var j = 1; j <= targetLength; j++)
            {
                var cost = target[j - 1] == source[i - 1] ? 0 : 1;
                distance[i, j] = Math.Min(
                    Math.Min(distance[i - 1, j] + 1, distance[i, j - 1] + 1),
                    distance[i - 1, j - 1] + cost
                );
            }
        }

        return distance[sourceLength, targetLength];
    }
}
