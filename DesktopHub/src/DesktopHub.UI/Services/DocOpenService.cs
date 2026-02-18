using System.IO;
using System.Text.RegularExpressions;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Settings;

namespace DesktopHub.UI.Services;

/// <summary>
/// Orchestrates project scanning, discipline switching, and file management
/// for the Doc Quick Open widget. Linked to SearchOverlay project selection.
/// </summary>
public class DocOpenService
{
    private static readonly HashSet<string> SearchStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "or", "the", "from", "for", "to", "of", "in", "on", "at", "by", "with"
    };

    private static readonly Dictionary<string, string[]> SmartTokenAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fault"] = new[] { "fault", "short", "short-circuit", "short circuit", "sc" },
        ["current"] = new[] { "current", "amp", "amps", "amperage", "kaic", "aic" },
        ["letter"] = new[] { "letter", "ltr", "memo", "correspondence" },
        ["fpl"] = new[] { "fpl", "fp&l", "florida power", "florida power and light", "utility" },
        ["utility"] = new[] { "utility", "fpl", "power" },
        ["service"] = new[] { "service", "svc" },
    };

    private readonly IDocumentScanner _scanner;
    private DocWidgetConfig _config;
    private ProjectFileInfo? _projectInfo;
    private Discipline _selectedDiscipline = Discipline.Mechanical;
    private List<DocumentItem> _currentFiles = new();
    private string? _searchQuery;
    private CancellationTokenSource? _scanCts;

    /// <summary>Fired when project info or file list changes</summary>
    public event EventHandler? ProjectChanged;

    /// <summary>Fired when config changes (from settings)</summary>
    public event EventHandler? ConfigChanged;

    /// <summary>Fired when scanning starts/ends</summary>
    public event EventHandler<bool>? ScanningChanged;

    public DocWidgetConfig Config => _config;
    public ProjectFileInfo? ProjectInfo => _projectInfo;
    public Discipline SelectedDiscipline => _selectedDiscipline;
    public List<DocumentItem> CurrentFiles => _currentFiles;
    public bool IsScanning { get; private set; }

    public DocOpenService(IDocumentScanner scanner)
    {
        _scanner = scanner;
        _config = new DocWidgetConfig();
    }

    public async Task InitializeAsync()
    {
        _config = await DocWidgetConfig.LoadAsync();

        // Restore last selected discipline
        if (Enum.TryParse<Discipline>(_config.LastDiscipline, true, out var disc))
            _selectedDiscipline = disc;
    }

    /// <summary>
    /// Called when user selects a project in SearchOverlay (single click)
    /// </summary>
    public async Task SetProjectAsync(string? projectPath, string? projectName = null)
    {
        if (string.IsNullOrEmpty(projectPath))
        {
            _projectInfo = null;
            _currentFiles.Clear();
            ProjectChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Don't rescan if same project
        if (_projectInfo != null &&
            string.Equals(_projectInfo.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase))
            return;

        // Save as last project
        _config.LastProjectPath = projectPath;
        _config.LastProjectName = projectName;
        await _config.SaveAsync();

        await ScanProjectAsync(projectPath, projectName);
    }

    /// <summary>
    /// Scan the project folder to detect type and files
    /// </summary>
    private async Task ScanProjectAsync(string projectPath, string? projectName)
    {
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        IsScanning = true;
        ScanningChanged?.Invoke(this, true);

        try
        {
            _projectInfo = await _scanner.ScanProjectAsync(
                projectPath, projectName,
                _config.MaxDepth, _config.ExcludedFolders, _config.MaxFiles, _config.Extensions, token);
            RefreshCurrentFiles();
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsScanning = false;
            ScanningChanged?.Invoke(this, false);
        }
    }

    /// <summary>
    /// Switch the active discipline (CAD projects)
    /// </summary>
    public async Task SetDisciplineAsync(Discipline discipline)
    {
        _selectedDiscipline = discipline;
        _config.LastDiscipline = discipline.ToString();
        await _config.SaveAsync();
        RefreshCurrentFiles();
    }

    /// <summary>
    /// Apply search filter
    /// </summary>
    public void SetSearchQuery(string? query)
    {
        _searchQuery = query;
        RefreshCurrentFiles();
    }

    /// <summary>
    /// Rebuild _currentFiles from project info + selected discipline + search query
    /// </summary>
    private void RefreshCurrentFiles()
    {
        _currentFiles.Clear();

        if (_projectInfo == null)
        {
            ProjectChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        IEnumerable<DocumentItem> files;

        if (_projectInfo.Type == ProjectType.Cad || 
            (_projectInfo.IsHybrid && _projectInfo.DisciplineFiles.ContainsKey(_selectedDiscipline)))
        {
            // Show files from selected discipline
            files = _projectInfo.DisciplineFiles.GetValueOrDefault(_selectedDiscipline)
                    ?? Enumerable.Empty<DocumentItem>();
        }
        else if (_projectInfo.Type == ProjectType.Revit && _projectInfo.Revit != null)
        {
            files = _projectInfo.Revit.RvtFiles;
        }
        else
        {
            files = Enumerable.Empty<DocumentItem>();
        }

        var defaultFiles = files;

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            var searchablePool = BuildSearchPool(defaultFiles);
            files = ApplySmartSearch(searchablePool, _searchQuery);
        }

        // Sort
        if (string.IsNullOrWhiteSpace(_searchQuery))
        {
            files = _config.SortBy switch
            {
                "date" => files.OrderByDescending(d => d.LastModified),
                "size" => files.OrderByDescending(d => d.SizeBytes),
                "type" => files.OrderBy(d => d.Extension).ThenBy(d => d.FileName),
                _ => files.OrderBy(d => d.FileName)
            };
        }

        _currentFiles = files.ToList();
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    private IEnumerable<DocumentItem> BuildSearchPool(IEnumerable<DocumentItem> defaultFiles)
    {
        if (_projectInfo == null)
            return defaultFiles;

        var aggregate = _projectInfo.AllFiles ?? new List<DocumentItem>();
        var revitFiles = _projectInfo.Revit?.RvtFiles ?? Enumerable.Empty<DocumentItem>();

        return aggregate
            .Concat(defaultFiles)
            .Concat(revitFiles)
            .GroupBy(d => d.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());
    }

    private IEnumerable<DocumentItem> ApplySmartSearch(IEnumerable<DocumentItem> files, string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
            return files;

        if (TryParseRegexPattern(trimmed, out var regexPattern))
        {
            return ApplyRegexSearch(files, regexPattern);
        }

        return ApplyTokenSearch(files, trimmed);
    }

    private static bool TryParseRegexPattern(string query, out string pattern)
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

    private static IEnumerable<DocumentItem> ApplyRegexSearch(IEnumerable<DocumentItem> files, string pattern)
    {
        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            return files.Where(d => regex.IsMatch(BuildSearchText(d)));
        }
        catch
        {
            // Invalid regex: return no results instead of throwing.
            return Enumerable.Empty<DocumentItem>();
        }
    }

    private static IEnumerable<DocumentItem> ApplyTokenSearch(IEnumerable<DocumentItem> files, string query)
    {
        var terms = ExtractSearchTerms(query);
        if (terms.Count == 0)
            return files;

        var normalizedPhrase = query.ToLowerInvariant();
        var minimumHits = terms.Count <= 2 ? terms.Count : terms.Count - 1;
        var scored = new List<(DocumentItem Doc, double Score, int Hits)>();

        foreach (var doc in files)
        {
            var score = 0.0;
            var hits = 0;

            foreach (var term in terms)
            {
                var termScore = ScoreTerm(doc, term);
                if (termScore > 0)
                {
                    hits++;
                    score += termScore;
                }
            }

            var allText = BuildSearchText(doc).ToLowerInvariant();
            var hasPhrase = allText.Contains(normalizedPhrase, StringComparison.OrdinalIgnoreCase);
            if (hasPhrase)
            {
                score += 4.0;
            }

            if (hits >= minimumHits || hasPhrase)
            {
                scored.Add((doc, score, hits));
            }
        }

        return scored
            .OrderByDescending(s => s.Score)
            .ThenByDescending(s => s.Hits)
            .ThenBy(s => s.Doc.FileName)
            .Select(s => s.Doc);
    }

    private static List<string> ExtractSearchTerms(string query)
    {
        var terms = new List<string>();
        foreach (Match match in Regex.Matches(query, "\"([^\"]+)\"|(\\S+)"))
        {
            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            var normalized = value.Trim().ToLowerInvariant();
            if (normalized.Length == 0 || SearchStopWords.Contains(normalized))
                continue;

            terms.Add(normalized);
        }

        return terms;
    }

    private static double ScoreTerm(DocumentItem doc, string term)
    {
        var fileName = doc.FileName.ToLowerInvariant();
        var relativePath = doc.RelativePath.ToLowerInvariant();
        var subfolder = doc.Subfolder.ToLowerInvariant();
        var category = doc.Category.ToLowerInvariant();
        var best = 0.0;

        foreach (var alias in ExpandTerm(term))
        {
            if (alias.Length == 0)
                continue;

            if (fileName.StartsWith(alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 4.0);
            if (fileName.Contains(alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 3.2);
            if (relativePath.Contains(alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 2.4);
            if (subfolder.Contains(alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 1.5);
            if (category.Contains(alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 1.2);
            if (best == 0.0 && alias.Length >= 3 && IsSubsequence(alias, fileName)) best = Math.Max(best, 0.6);
        }

        return best;
    }

    private static IEnumerable<string> ExpandTerm(string term)
    {
        if (SmartTokenAliases.TryGetValue(term, out var aliases) && aliases.Length > 0)
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

    private static string BuildSearchText(DocumentItem doc)
        => $"{doc.FileName} {doc.RelativePath} {doc.Subfolder} {doc.Category}";

    private static bool IsSubsequence(string needle, string haystack)
    {
        var i = 0;
        var j = 0;

        while (i < needle.Length && j < haystack.Length)
        {
            if (needle[i] == haystack[j]) i++;
            j++;
        }

        return i == needle.Length;
    }

    /// <summary>
    /// Re-scan the current project
    /// </summary>
    public async Task RescanAsync()
    {
        if (_projectInfo != null)
        {
            await ScanProjectAsync(_projectInfo.ProjectPath, _projectInfo.ProjectName);
        }
    }

    /// <summary>
    /// Record a file open in recent history
    /// </summary>
    public async Task RecordFileOpenAsync(string filePath)
    {
        _config.RecentFiles.Remove(filePath);
        _config.RecentFiles.Insert(0, filePath);

        while (_config.RecentFiles.Count > _config.RecentFilesCount && _config.RecentFiles.Count > 0)
            _config.RecentFiles.RemoveAt(_config.RecentFiles.Count - 1);

        await _config.SaveAsync();
    }

    /// <summary>
    /// Get recent files that still exist
    /// </summary>
    public List<string> GetRecentFiles()
    {
        return _config.RecentFiles.Where(File.Exists).Take(_config.RecentFilesCount).ToList();
    }

    /// <summary>
    /// Apply config changes from settings, save, rescan, and notify
    /// </summary>
    public async Task ApplyConfigAsync()
    {
        await _config.SaveAsync();
        if (_projectInfo != null)
        {
            await RescanAsync();
        }
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }
}
