using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace DesktopHub.UI;

public partial class SearchOverlay
{
    private static bool LooksLikePathInput(string query)
    {
        var trimmed = query.TrimStart();
        return trimmed.StartsWith("\\\\") ||
               Regex.IsMatch(trimmed, @"^[a-zA-Z]:[\\/]");
    }

    private static bool TryExtractPathAndFilter(string query, out string path, out string filter)
    {
        path = string.Empty;
        filter = string.Empty;

        var trimmed = query.Trim();
        if (trimmed.Length == 0 || !LooksLikePathInput(trimmed))
            return false;

        var delimiterIndex = trimmed.IndexOf("::", StringComparison.Ordinal);
        if (delimiterIndex >= 0)
        {
            path = trimmed[..delimiterIndex].Trim().Trim('"');
            filter = trimmed[(delimiterIndex + 2)..].Trim();
            return path.Length > 0;
        }

        if (trimmed.StartsWith('"'))
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                path = trimmed[1..closingQuote].Trim();
                filter = trimmed[(closingQuote + 1)..].Trim();
                return path.Length > 0;
            }
        }

        var tokens = Regex.Split(trimmed, @"\s+")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();
        if (tokens.Length == 0)
            return false;

        var candidate = tokens[0];
        string? bestPath = System.IO.Directory.Exists(candidate) ? candidate : null;
        var bestTokenCount = bestPath != null ? 1 : 0;

        for (var i = 1; i < tokens.Length; i++)
        {
            candidate += " " + tokens[i];
            if (System.IO.Directory.Exists(candidate))
            {
                bestPath = candidate;
                bestTokenCount = i + 1;
            }
        }

        if (bestPath != null)
        {
            path = bestPath;
            filter = string.Join(" ", tokens.Skip(bestTokenCount));
            return true;
        }

        path = trimmed.Trim('"');
        filter = string.Empty;
        return true;
    }

    private static bool IsHiddenOrSystem(string path)
    {
        try
        {
            var attr = System.IO.File.GetAttributes(path);
            return (attr & System.IO.FileAttributes.Hidden) != 0 ||
                   (attr & System.IO.FileAttributes.System) != 0;
        }
        catch
        {
            return true;
        }
    }

    private static IEnumerable<(string Path, bool IsDirectory)> EnumeratePathEntries(
        string rootPath,
        bool includeDirectories,
        bool includeFiles,
        CancellationToken token)
    {
        var stack = new Stack<string>();
        stack.Push(rootPath);

        while (stack.Count > 0)
        {
            if (token.IsCancellationRequested)
                yield break;

            var current = stack.Pop();
            IEnumerable<string> subDirectories;
            try
            {
                subDirectories = System.IO.Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var dir in subDirectories)
            {
                if (token.IsCancellationRequested)
                    yield break;

                if (includeDirectories)
                    yield return (dir, true);

                stack.Push(dir);
            }

            if (!includeFiles)
                continue;

            IEnumerable<string> files;
            try
            {
                files = System.IO.Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                if (token.IsCancellationRequested)
                    yield break;
                yield return (file, false);
            }
        }
    }

    private static bool IsRegexQuery(string query, out string pattern)
    {
        pattern = string.Empty;

        if (query.StartsWith("re:", StringComparison.OrdinalIgnoreCase))
        {
            pattern = query[3..].Trim();
            return pattern.Length > 0;
        }

        if (query.Length >= 3 && query[0] == '/' && query.LastIndexOf('/') > 0)
        {
            var lastSlash = query.LastIndexOf('/');
            pattern = query[1..lastSlash].Trim();
            return pattern.Length > 0;
        }

        return false;
    }

    private static List<string> ExtractPathSearchTerms(string query)
    {
        var terms = new List<string>();
        foreach (Match match in Regex.Matches(query, "\"([^\"]+)\"|(\\S+)"))
        {
            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            var normalized = value.Trim().ToLowerInvariant();
            if (normalized.Length == 0 || PathSearchStopWords.Contains(normalized))
                continue;

            terms.Add(normalized);
        }

        return terms;
    }

    private static IEnumerable<string> ExpandPathSearchAliases(string term)
    {
        if (PathSearchAliases.TryGetValue(term, out var aliases) && aliases.Length > 0)
        {
            return aliases
                .Append(term)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(a => a.ToLowerInvariant());
        }

        if (term.EndsWith('s') && term.Length > 3)
        {
            return new[] { term, term[..^1] };
        }

        return new[] { term };
    }

    private static double ScorePathEntry(string fullPath, string normalizedQuery, IReadOnlyList<string> terms, Regex? regex)
    {
        if (regex != null)
            return regex.IsMatch(fullPath) ? 10.0 : 0.0;

        var normalizedPath = fullPath.ToLowerInvariant();
        var fileName = System.IO.Path.GetFileName(fullPath).ToLowerInvariant();

        if (terms.Count == 0)
        {
            return normalizedPath.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        }

        var minimumHits = terms.Count <= 2 ? terms.Count : terms.Count - 1;
        var hits = 0;
        var score = 0.0;

        foreach (var term in terms)
        {
            var best = 0.0;
            foreach (var alias in ExpandPathSearchAliases(term))
            {
                if (fileName.StartsWith(alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 4.0);
                if (fileName.Contains(alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 3.0);
                if (normalizedPath.Contains(alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 1.5);
            }

            if (best > 0)
            {
                hits++;
                score += best;
            }
        }

        var hasPhrase = normalizedPath.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
        if (hasPhrase)
            score += 4.0;

        if (hits < minimumHits && !hasPhrase)
            return 0.0;

        return score;
    }

    private static string? BuildPathMatchStatus(string rootPath, string fullPath)
    {
        try
        {
            var relative = System.IO.Path.GetRelativePath(rootPath, fullPath);
            var parent = System.IO.Path.GetDirectoryName(relative);
            if (string.IsNullOrWhiteSpace(parent) || parent == ".")
                return null;
            return parent;
        }
        catch
        {
            return null;
        }
    }

    // Path search result that reuses the same DataTemplate bindings as ProjectViewModel
    private class PathSearchResultViewModel
    {
        public string FullNumber { get; }
        public string Name { get; }
        public string Path { get; }
        public string? Location { get; }
        public string? Status { get; }
        public bool IsDirectory { get; }
        public bool IsFavorite { get; } = false;

        public PathSearchResultViewModel(string fullPath, bool isDirectory, string? status = null)
        {
            Path = fullPath;
            IsDirectory = isDirectory;
            Name = System.IO.Path.GetFileName(fullPath);
            if (string.IsNullOrEmpty(Name))
                Name = fullPath; // root paths like C:\
            FullNumber = isDirectory ? "📁" : GetFileIcon(fullPath);
            Location = isDirectory ? "Directory" : GetFileSize(fullPath);
            Status = status;
        }

        private static string GetFileIcon(string path)
        {
            var ext = System.IO.Path.GetExtension(path)?.ToLowerInvariant();
            return ext switch
            {
                ".exe" or ".msi" => "⚙️",
                ".pdf" => "📄",
                ".doc" or ".docx" => "📝",
                ".xls" or ".xlsx" => "📊",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".svg" => "🖼️",
                ".zip" or ".rar" or ".7z" => "📦",
                ".txt" or ".log" or ".csv" => "📃",
                ".dwg" or ".dxf" => "📐",
                _ => "📄"
            };
        }

        private static string? GetFileSize(string path)
        {
            try
            {
                var info = new System.IO.FileInfo(path);
                if (!info.Exists) return null;
                var bytes = info.Length;
                if (bytes < 1024) return $"{bytes} B";
                if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
                if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
                return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
            }
            catch { return null; }
        }
    }

    private async Task PerformPathSearch(string query, CancellationToken token)
    {
        try
        {
            await Task.Delay(200, token);
            if (token.IsCancellationRequested) return;

            ShowLoading(true);

            if (!TryExtractPathAndFilter(query, out var path, out var scopedQuery))
            {
                _isPathSearchResults = false;
                _activePathSearchRootDisplay = null;
                ResultsList.ItemsSource = null;
                StatusText.Text = "Path format not recognized. Use C:\\Folder or C:\\Folder :: search terms";
                UpdateResultsHeader();
                ShowLoading(false);
                return;
            }

            _isPathSearchResults = true;
            _activePathSearchRootDisplay = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
            if (string.IsNullOrWhiteSpace(_activePathSearchRootDisplay))
                _activePathSearchRootDisplay = path;

            if (!System.IO.Directory.Exists(path))
            {
                ResultsList.ItemsSource = null;
                StatusText.Text = "Directory not found";
                UpdateResultsHeader();
                ShowLoading(false);
                return;
            }

            var showDirs = _settings.GetPathSearchShowSubDirs();
            var showFiles = _settings.GetPathSearchShowSubFiles();
            var showHidden = _settings.GetPathSearchShowHidden();
            var results = new List<PathSearchResultViewModel>();

            if (!showDirs && !showFiles)
            {
                ResultsList.ItemsSource = null;
                StatusText.Text = "Enable folder and/or file path results in General settings";
                UpdateResultsHeader();
                ShowLoading(false);
                return;
            }

            var scopedQueryNormalized = scopedQuery.Trim();
            var isRecursiveScopedSearch = scopedQueryNormalized.Length > 0;

            await Task.Run(() =>
            {
                try
                {
                    if (!isRecursiveScopedSearch)
                    {
                        if (showDirs)
                        {
                            foreach (var dir in System.IO.Directory.GetDirectories(path))
                            {
                                if (token.IsCancellationRequested) return;
                                if (!showHidden && IsHiddenOrSystem(dir))
                                    continue;
                                results.Add(new PathSearchResultViewModel(dir, true));
                            }
                        }

                        if (showFiles)
                        {
                            foreach (var file in System.IO.Directory.GetFiles(path))
                            {
                                if (token.IsCancellationRequested) return;
                                if (!showHidden && IsHiddenOrSystem(file))
                                    continue;
                                results.Add(new PathSearchResultViewModel(file, false));
                            }
                        }
                        return;
                    }

                    var regexPattern = string.Empty;
                    var isRegexQuery = IsRegexQuery(scopedQueryNormalized, out regexPattern);
                    Regex? regex = null;

                    if (isRegexQuery)
                    {
                        try
                        {
                            regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                        }
                        catch
                        {
                            return;
                        }
                    }

                    var terms = ExtractPathSearchTerms(scopedQueryNormalized);
                    var ranked = new List<(PathSearchResultViewModel ViewModel, double Score)>();
                    const int maxMatches = 350;

                    foreach (var (entryPath, isDir) in EnumeratePathEntries(path, showDirs, showFiles, token))
                    {
                        if (token.IsCancellationRequested) return;
                        if (!showHidden && IsHiddenOrSystem(entryPath))
                            continue;

                        var score = ScorePathEntry(entryPath, scopedQueryNormalized, terms, regex);
                        if (score <= 0)
                            continue;

                        var status = BuildPathMatchStatus(path, entryPath);
                        ranked.Add((new PathSearchResultViewModel(entryPath, isDir, status), score));

                        if (ranked.Count >= maxMatches)
                            break;
                    }

                    foreach (var match in ranked
                        .OrderByDescending(r => r.Score)
                        .ThenBy(r => r.ViewModel.Name)
                        .Select(r => r.ViewModel))
                    {
                        results.Add(match);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Silently skip inaccessible directories
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"PerformPathSearch: Error enumerating {path}: {ex.Message}");
                }
            }, token);

            if (token.IsCancellationRequested) return;

            ResultsList.ItemsSource = results;

            if (results.Count > 0)
            {
                ResultsList.SelectedIndex = 0;

                if (isRecursiveScopedSearch)
                {
                    var modeLabel = IsRegexQuery(scopedQueryNormalized, out _) ? "regex" : "smart";
                    StatusText.Text = $"{results.Count} match{(results.Count == 1 ? "" : "es")} in {modeLabel} search";
                }
                else
                {
                    var dirCount = results.Count(r => r.IsDirectory);
                    var fileCount = results.Count - dirCount;
                    var parts = new List<string>();
                    if (dirCount > 0) parts.Add($"{dirCount} folder{(dirCount == 1 ? "" : "s")}");
                    if (fileCount > 0) parts.Add($"{fileCount} file{(fileCount == 1 ? "" : "s")}");
                    StatusText.Text = $"Path: {string.Join(", ", parts)}";
                }

                if (!_userManuallySizedResults && _isResultsCollapsed)
                {
                    _isResultsCollapsed = false;
                    ResultsContainer.Visibility = Visibility.Visible;
                    CollapseIconRotation.Angle = 0;
                    this.Height = 500;
                }
            }
            else
            {
                if (isRecursiveScopedSearch)
                {
                    StatusText.Text = "No path matches found";
                }
                else
                {
                    StatusText.Text = "Directory is empty";
                }
            }

            UpdateResultsHeader();
            ShowLoading(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            DebugLogger.Log($"PerformPathSearch: Error: {ex.Message}");
            StatusText.Text = $"Path search error: {ex.Message}";
            ShowLoading(false);
        }
    }
}
