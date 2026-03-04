using System.Text.RegularExpressions;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;

namespace DesktopHub.Infrastructure.Search;

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
            const double minScoreThreshold = 0.3; // Skip very low relevance results

            foreach (var project in projects)
            {
                // Apply structured filters first
                if (!PassesFilters(project, filter))
                    continue;

                // Calculate relevance score
                var score = CalculateRelevanceScore(project, filter, out var isLoose);
                if (score > minScoreThreshold)
                {
                    results.Add(new SearchResult
                    {
                        Project = project,
                        Score = score,
                        MatchedFields = GetMatchedFields(project, filter),
                        IsLooseTokenMatch = isLoose
                    });
                }
            }

            // --- Project-family expansion ---
            // When projects share a base number with direct matches, pull in siblings.
            // >= 2 direct matches: confirmed family → "Related Project" tag, score 0.9x
            // == 1 direct match:  possible duplicate → "Duplicate Number?" tag, score 0.5x
            if (!string.IsNullOrWhiteSpace(filter.SearchText) && results.Count > 0)
            {
                // Count direct (non-loose) matches per base number
                var baseNumberCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var baseNumberBestScore = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in results)
                {
                    if (r.IsLooseTokenMatch) continue; // Don't count loose matches for family expansion
                    var baseNum = ExtractBaseProjectNumber(r.Project.FullNumber);
                    if (string.IsNullOrEmpty(baseNum)) continue;

                    baseNumberCounts.TryGetValue(baseNum, out var count);
                    baseNumberCounts[baseNum] = count + 1;

                    baseNumberBestScore.TryGetValue(baseNum, out var best);
                    if (r.Score > best) baseNumberBestScore[baseNum] = r.Score;
                }

                var existingIds = new HashSet<string>(results.Select(r => r.Project.Id));

                foreach (var project in projects)
                {
                    if (existingIds.Contains(project.Id))
                        continue;

                    if (!PassesFilters(project, filter))
                        continue;

                    var baseNum = ExtractBaseProjectNumber(project.FullNumber);
                    if (string.IsNullOrEmpty(baseNum) || !baseNumberCounts.ContainsKey(baseNum))
                        continue;

                    var directCount = baseNumberCounts[baseNum];
                    var bestScore = baseNumberBestScore.GetValueOrDefault(baseNum, 0.7);

                    if (directCount >= 2)
                    {
                        // Confirmed family — score at 0.9x, ranks right after direct siblings
                        results.Add(new SearchResult
                        {
                            Project = project,
                            Score = bestScore * 0.9,
                            MatchedFields = new List<string> { "RelatedProject" },
                            IsRelatedMatch = true
                        });
                    }
                    else
                    {
                        // Single direct match — possible duplicate/mismatch, lower score
                        results.Add(new SearchResult
                        {
                            Project = project,
                            Score = bestScore * 0.5,
                            MatchedFields = new List<string> { "DuplicateNumber" },
                            IsRelatedMatch = true,
                            IsDuplicateNumber = true
                        });
                    }
                    existingIds.Add(project.Id);
                }
            }

            // Sort by score descending, then by full number for stable ordering within families
            results = results
                .OrderByDescending(r => r.Score)
                .ThenBy(r => r.Project.FullNumber)
                .Take(filter.MaxResults)
                .ToList();
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

        // Contains match (fast string search)
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

        // Skip expensive Levenshtein for very different length strings
        var lengthDiff = Math.Abs(source.Length - target.Length);
        if (lengthDiff > source.Length)
            return 0.0;

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

        // Drive location filter
        if (!string.IsNullOrEmpty(filter.DriveLocation) && filter.DriveLocation != "All")
        {
            if (!project.DriveLocation.Equals(filter.DriveLocation, StringComparison.OrdinalIgnoreCase))
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

    private double CalculateRelevanceScore(Project project, SearchFilter filter, out bool isLooseTokenMatch)
    {
        isLooseTokenMatch = false;

        if (string.IsNullOrWhiteSpace(filter.SearchText))
        {
            // No search text, just return 1.0 if it passed filters
            return 1.0;
        }

        var searchText = filter.SearchText.ToLowerInvariant();

        // Tokenize the query for multi-word matching
        var tokens = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        double score;
        if (tokens.Length <= 1)
        {
            // Single token — use original single-string scoring
            score = CalculateSingleTokenScore(searchText, project);
        }
        else
        {
            // Multi-token — score as whole phrase first, then fall back to per-token AND matching
            var phraseScore = CalculateSingleTokenScore(searchText, project);
            var tokenScore = CalculateMultiTokenScore(tokens, project, out isLooseTokenMatch);

            // If phrase match is strong, it's not a loose match regardless of token order
            if (phraseScore >= tokenScore)
                isLooseTokenMatch = false;

            score = Math.Max(phraseScore, tokenScore);
        }

        // Boost favorites
        if (project.Metadata?.IsFavorite == true)
        {
            score *= 1.1;
        }

        return Math.Min(score, 1.0); // Cap at 1.0
    }

    private double CalculateSingleTokenScore(string searchText, Project project)
    {
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

        // Also check against the folder name (full display) for broader matching
        var displayScore = CalculateFuzzyScore(searchText, project.Display);
        maxScore = Math.Max(maxScore, displayScore * 0.9);

        return maxScore;
    }

    /// <summary>
    /// Multi-token AND matching: ALL tokens must match somewhere in the project fields.
    /// Returns 0 if any token has no match. Otherwise returns average of per-token best scores.
    /// Penalizes results where tokens appear out of order (e.g. "West Boca" for query "Boca West").
    /// </summary>
    private double CalculateMultiTokenScore(string[] tokens, Project project, out bool isLooseTokenMatch)
    {
        isLooseTokenMatch = false;

        var fields = new[]
        {
            (project.FullNumber, 1.2),
            (project.ShortNumber, 1.1),
            (project.Name, 1.0),
            (project.Display, 0.9)
        };

        double totalScore = 0;
        foreach (var token in tokens)
        {
            double bestTokenScore = 0;
            foreach (var (fieldValue, boost) in fields)
            {
                var s = CalculateFuzzyScore(token, fieldValue) * boost;
                bestTokenScore = Math.Max(bestTokenScore, s);
            }

            if (bestTokenScore <= 0.0)
                return 0.0; // AND semantics: if any token fails, whole query fails

            totalScore += bestTokenScore;
        }

        var avgScore = totalScore / tokens.Length;

        // Check token order: if tokens appear out of order in ALL fields, it's a loose match
        // e.g. query "Boca West" matching "West Boca Outpatient" — tokens reversed
        var inOrderInAnyField = false;
        foreach (var (fieldValue, _) in fields)
        {
            if (TokensAppearInOrder(tokens, fieldValue))
            {
                inOrderInAnyField = true;
                break;
            }
        }

        if (!inOrderInAnyField)
        {
            isLooseTokenMatch = true;
            avgScore *= 0.55; // Significant penalty for reversed/scrambled token order
        }

        return avgScore;
    }

    /// <summary>
    /// Check if tokens appear in the same order (not necessarily contiguous) within the target string.
    /// </summary>
    private static bool TokensAppearInOrder(string[] tokens, string target)
    {
        var lower = target.ToLowerInvariant();
        int searchFrom = 0;
        foreach (var token in tokens)
        {
            var idx = lower.IndexOf(token, searchFrom, StringComparison.Ordinal);
            if (idx < 0) return false;
            searchFrom = idx + token.Length;
        }
        return true;
    }

    private List<string> GetMatchedFields(Project project, SearchFilter filter)
    {
        var fields = new List<string>();

        if (string.IsNullOrWhiteSpace(filter.SearchText))
            return fields;

        var searchText = filter.SearchText.ToLowerInvariant();
        var tokens = searchText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        // A field matches if it contains the full phrase OR all individual tokens
        var fullNum = project.FullNumber.ToLowerInvariant();
        var shortNum = project.ShortNumber.ToLowerInvariant();
        var name = project.Name.ToLowerInvariant();

        if (fullNum.Contains(searchText) || tokens.All(t => fullNum.Contains(t)))
            fields.Add("FullNumber");

        if (shortNum.Contains(searchText) || tokens.All(t => shortNum.Contains(t)))
            fields.Add("ShortNumber");

        if (name.Contains(searchText) || tokens.All(t => name.Contains(t)))
            fields.Add("Name");

        return fields;
    }

    /// <summary>
    /// Extract the base project number from a full number for family grouping.
    /// Examples:
    ///   "2024337.02" → "2024337"
    ///   "2024337.02-B2" → "2024337"
    ///   "P260261.00" → "260261"
    ///   "2024638.001" → "2024638" (old format, first 4 digits are year → base is digits 5+)
    /// </summary>
    internal static string? ExtractBaseProjectNumber(string fullNumber)
    {
        if (string.IsNullOrWhiteSpace(fullNumber))
            return null;

        var num = fullNumber;

        // Strip leading 'P' prefix if present (new Q-drive format)
        if (num.StartsWith("P", StringComparison.OrdinalIgnoreCase))
            num = num.Substring(1);

        // Take everything before the first '.' (the base integer portion)
        var dotIndex = num.IndexOf('.');
        if (dotIndex > 0)
            num = num.Substring(0, dotIndex);
        else
        {
            // No dot — take digits up to first non-digit
            var end = 0;
            while (end < num.Length && char.IsDigit(num[end]))
                end++;
            if (end == 0)
                return null;
            num = num.Substring(0, end);
        }

        return num.Length >= 4 ? num : null; // Must be at least 4 digits to be meaningful
    }

    private static int CalculateLevenshteinDistance(string source, string target)
    {
        if (string.IsNullOrEmpty(source))
            return target?.Length ?? 0;

        if (string.IsNullOrEmpty(target))
            return source.Length;

        var sourceLength = source.Length;
        var targetLength = target.Length;
        
        // Optimize memory: use single array instead of 2D matrix
        var previousRow = new int[targetLength + 1];
        var currentRow = new int[targetLength + 1];

        for (var j = 0; j <= targetLength; j++)
            previousRow[j] = j;

        for (var i = 1; i <= sourceLength; i++)
        {
            currentRow[0] = i;
            
            for (var j = 1; j <= targetLength; j++)
            {
                var cost = target[j - 1] == source[i - 1] ? 0 : 1;
                currentRow[j] = Math.Min(
                    Math.Min(previousRow[j] + 1, currentRow[j - 1] + 1),
                    previousRow[j - 1] + cost
                );
            }
            
            // Swap rows
            var temp = previousRow;
            previousRow = currentRow;
            currentRow = temp;
        }

        return previousRow[targetLength];
    }
}
