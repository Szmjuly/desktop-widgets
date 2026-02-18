using System.IO;
using System.Text.RegularExpressions;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;

namespace DesktopHub.UI.Services;

public sealed class SmartProjectSearchService
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
        ["revision"] = new[] { "revision", "rev", "issuance", "issued" }
    };

    private static readonly Dictionary<string, string[]> FileTypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["word"] = new[] { "doc", "docx" },
        ["doc"] = new[] { "doc" },
        ["docx"] = new[] { "docx" },
        ["pdf"] = new[] { "pdf" },
        ["txt"] = new[] { "txt" },
        ["dwg"] = new[] { "dwg" },
        ["rvt"] = new[] { "rvt" },
        ["excel"] = new[] { "xls", "xlsx", "csv" },
        ["xls"] = new[] { "xls" },
        ["xlsx"] = new[] { "xlsx" },
        ["csv"] = new[] { "csv" },
        ["png"] = new[] { "png" },
        ["jpeg"] = new[] { "jpg", "jpeg" },
        ["jpg"] = new[] { "jpg", "jpeg" },
        ["msg"] = new[] { "msg" }
    };

    private readonly IDocumentScanner _scanner;
    private readonly ISettingsService _settings;
    private ProjectFileInfo? _projectInfo;
    private string? _projectName;
    private string? _projectPath;
    private string? _query;
    private List<SmartProjectSearchResult> _results = new();
    private CancellationTokenSource? _scanCts;
    private int _refreshVersion;

    public event EventHandler? StateChanged;
    public event EventHandler<bool>? ScanningChanged;

    public IReadOnlyList<SmartProjectSearchResult> Results => _results;
    public bool IsScanning { get; private set; }
    public string StatusText { get; private set; } = "Select a project to begin searching.";
    public string ActiveProjectLabel { get; private set; } = "No project selected";

    public SmartProjectSearchService(IDocumentScanner scanner, ISettingsService settings)
    {
        _scanner = scanner;
        _settings = settings;
    }

    public async Task SetProjectAsync(string? projectPath, string? projectName = null)
    {
        Interlocked.Increment(ref _refreshVersion);

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            _projectPath = null;
            _projectName = null;
            _projectInfo = null;
            ActiveProjectLabel = "No project selected";
            StatusText = "Select a project to begin searching.";
            _results = new List<SmartProjectSearchResult>();
            RaiseStateChanged();
            return;
        }

        var normalizedPath = projectPath.Trim();
        var isSameProject = _projectInfo != null && string.Equals(_projectPath, normalizedPath, StringComparison.OrdinalIgnoreCase);

        _projectPath = normalizedPath;
        _projectName = string.IsNullOrWhiteSpace(projectName) ? Path.GetFileName(normalizedPath) : projectName;
        ActiveProjectLabel = _projectName ?? normalizedPath;

        if (!isSameProject)
        {
            await ScanSelectedProjectAsync();
        }

        await RefreshResultsAsync();
    }

    public async Task SetQueryAsync(string? query)
    {
        _query = query;
        await RefreshResultsAsync();
    }

    public Task RequeryAsync() => RefreshResultsAsync();

    private async Task ScanSelectedProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(_projectPath))
            return;

        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        IsScanning = true;
        ScanningChanged?.Invoke(this, true);

        try
        {
            var extensions = ExpandConfiguredFileTypesToExtensions(_settings.GetSmartProjectSearchFileTypes());
            _projectInfo = await _scanner.ScanProjectAsync(
                _projectPath,
                _projectName,
                maxDepth: 4,
                excludedFolders: Array.Empty<string>(),
                maxFiles: 5000,
                includeExtensions: extensions,
                cancellationToken: token);

            var poolCount = BuildSearchPool(_projectInfo).Count;
            StatusText = poolCount == 0
                ? "No searchable files found in selected project."
                : $"Indexed {poolCount} files in {_projectName}.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan canceled.";
        }
        catch (Exception ex)
        {
            StatusText = $"Scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
            ScanningChanged?.Invoke(this, false);
        }
    }

    private async Task RefreshResultsAsync()
    {
        var refreshVersion = Interlocked.Increment(ref _refreshVersion);

        if (_projectInfo == null)
        {
            _results = new List<SmartProjectSearchResult>();
            RaiseStateChanged();
            return;
        }

        var pathOverride = TryExtractPathOverride(_query, out var overridePath, out var overrideFilter);
        var effectiveQuery = pathOverride ? overrideFilter : (_query ?? string.Empty);

        var sourceInfo = _projectInfo;
        if (pathOverride && !string.IsNullOrWhiteSpace(overridePath) && Directory.Exists(overridePath))
        {
            var includeExtensions = ExpandConfiguredFileTypesToExtensions(_settings.GetSmartProjectSearchFileTypes());
            sourceInfo = await _scanner.ScanProjectAsync(
                overridePath,
                Path.GetFileName(overridePath),
                maxDepth: 4,
                excludedFolders: Array.Empty<string>(),
                maxFiles: 5000,
                includeExtensions: includeExtensions,
                cancellationToken: CancellationToken.None);
        }

        if (refreshVersion != Volatile.Read(ref _refreshVersion))
            return;

        var pool = BuildSearchPool(sourceInfo);
        var latestMode = _settings.GetSmartProjectSearchLatestMode();
        var ranked = ApplySearch(pool, effectiveQuery, latestMode, _settings.GetSmartProjectSearchFileTypes()).ToList();

        var newResults = ranked.Select(doc => new SmartProjectSearchResult
        {
            Path = doc.Path,
            FileName = doc.FileName,
            Extension = doc.Extension,
            RelativePath = doc.RelativePath,
            LastModified = doc.LastModified,
            SizeBytes = doc.SizeBytes,
            SizeDisplay = doc.SizeDisplay,
            Category = doc.Category
        }).ToList();

        if (refreshVersion != Volatile.Read(ref _refreshVersion))
            return;

        _results = newResults;

        StatusText = BuildStatusText(sourceInfo.ProjectPath, effectiveQuery, _results.Count, latestMode);
        RaiseStateChanged();
    }

    private static List<DocumentItem> BuildSearchPool(ProjectFileInfo info)
    {
        var disciplineFiles = info.DisciplineFiles.Values.SelectMany(v => v);
        var revitFiles = info.Revit?.RvtFiles ?? Enumerable.Empty<DocumentItem>();
        var allFiles = info.AllFiles ?? new List<DocumentItem>();

        return allFiles
            .Concat(disciplineFiles)
            .Concat(revitFiles)
            .GroupBy(d => d.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static IEnumerable<DocumentItem> ApplySearch(List<DocumentItem> pool, string? query, string latestMode, IReadOnlyList<string> configuredFileTypes)
    {
        if (pool.Count == 0)
            return Enumerable.Empty<DocumentItem>();

        var trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return Enumerable.Empty<DocumentItem>();
        }

        var parse = ParseQuery(trimmed, configuredFileTypes);

        var scored = new List<(DocumentItem Doc, double Score)>();
        Regex? regex = null;

        if (parse.RegexPattern != null)
        {
            try
            {
                regex = new Regex(parse.RegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch
            {
                return Enumerable.Empty<DocumentItem>();
            }
        }

        foreach (var doc in pool)
        {
            if (parse.AllowedExtensions.Count > 0 && !parse.AllowedExtensions.Contains(doc.Extension, StringComparer.OrdinalIgnoreCase))
                continue;

            var text = BuildSearchText(doc);
            double score = 0;

            if (regex != null)
            {
                if (!regex.IsMatch(text))
                    continue;

                score = 2.0;
            }
            else
            {
                var clauseMatchedAll = true;
                foreach (var clause in parse.TextClauses)
                {
                    var (matched, clauseScore) = EvaluateClause(doc, text, clause);
                    if (!matched)
                    {
                        clauseMatchedAll = false;
                        break;
                    }

                    score += clauseScore;
                }

                if (!clauseMatchedAll)
                    continue;
            }

            if (parse.LatestRequested)
            {
                var ageDays = Math.Max(0.0, (DateTime.Now - doc.LastModified).TotalDays);
                var freshness = Math.Max(0.0, 30.0 - ageDays) / 30.0;
                score += freshness * 5.0;
            }

            scored.Add((doc, score));
        }

        if (parse.LatestRequested && string.Equals(latestMode, "single", StringComparison.OrdinalIgnoreCase))
        {
            return scored
                .OrderByDescending(s => s.Doc.LastModified)
                .ThenByDescending(s => s.Score)
                .Take(1)
                .Select(s => s.Doc);
        }

        return parse.LatestRequested
            ? scored
                .OrderByDescending(s => s.Doc.LastModified)
                .ThenByDescending(s => s.Score)
                .ThenBy(s => s.Doc.FileName)
                .Select(s => s.Doc)
            : scored
                .OrderByDescending(s => s.Score)
                .ThenByDescending(s => s.Doc.LastModified)
                .ThenBy(s => s.Doc.FileName)
                .Select(s => s.Doc);
    }

    private static (bool Matched, double Score) EvaluateClause(DocumentItem doc, string text, ParsedClause clause)
    {
        var best = 0.0;
        var matched = false;

        foreach (var option in clause.Options)
        {
            var score = ScoreAlternative(doc, text, option);
            if (score > 0)
            {
                matched = true;
                best = Math.Max(best, score);

                if (clause.IsOr)
                    break;
            }
        }

        return (matched, best);
    }

    private static double ScoreAlternative(DocumentItem doc, string text, string alternative)
    {
        var normalizedOption = alternative.Trim();
        if (normalizedOption.Length == 0)
            return 0;

        var terms = ExtractSearchTerms(normalizedOption);
        if (terms.Count == 0)
        {
            return text.Contains(normalizedOption, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        }

        var hits = 0;
        var score = 0.0;

        foreach (var term in terms)
        {
            var termScore = ScoreTerm(doc, term);
            if (termScore > 0)
            {
                hits++;
                score += termScore;
            }
        }

        var phraseMatch = text.Contains(normalizedOption, StringComparison.OrdinalIgnoreCase);
        if (phraseMatch)
            score += 4.0;

        var minimumHits = terms.Count <= 2 ? terms.Count : terms.Count - 1;
        return hits >= minimumHits || phraseMatch ? score : 0;
    }

    private static string BuildSearchText(DocumentItem doc)
        => $"{doc.FileName} {doc.RelativePath} {doc.Subfolder} {doc.Category}";

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
            return new[] { term, term[..^1] };

        return new[] { term };
    }

    private static bool IsSubsequence(string needle, string haystack)
    {
        var i = 0;
        var j = 0;

        while (i < needle.Length && j < haystack.Length)
        {
            if (needle[i] == haystack[j])
                i++;

            j++;
        }

        return i == needle.Length;
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

    private static ParsedQuery ParseQuery(string query, IReadOnlyList<string> configuredFileTypes)
    {
        var configuredTypeExtensions = ExpandConfiguredFileTypesToExtensions(configuredFileTypes)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var latestRequested = Regex.IsMatch(query, @"\blatest\b", RegexOptions.IgnoreCase);
        var withoutLatest = Regex.Replace(query, @"\blatest\b", " ", RegexOptions.IgnoreCase).Trim();

        var parts = withoutLatest
            .Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (parts.Count == 0 && withoutLatest.Length > 0)
            parts.Add(withoutLatest);

        string? regexPattern = null;
        var textClauses = new List<ParsedClause>();
        var typeExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in parts)
        {
            if (regexPattern == null && TryParseRegexPattern(part, out var parsedPattern))
            {
                regexPattern = parsedPattern;
                continue;
            }

            var (isOr, options) = ParseClauseOptions(part);
            if (options.Count == 0)
                continue;

            var allTypeTokens = true;
            var resolvedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var option in options)
            {
                if (!TryResolveTypeOption(option, configuredTypeExtensions, out var expanded))
                {
                    allTypeTokens = false;
                    break;
                }

                foreach (var ext in expanded)
                    resolvedExtensions.Add(ext);
            }

            if (allTypeTokens && resolvedExtensions.Count > 0)
            {
                foreach (var ext in resolvedExtensions)
                    typeExtensions.Add(ext);

                continue;
            }

            textClauses.Add(new ParsedClause(isOr, options));
        }

        return new ParsedQuery(latestRequested, regexPattern, typeExtensions, textClauses);
    }

    private static (bool IsOr, List<string> Options) ParseClauseOptions(string clause)
    {
        if (string.IsNullOrWhiteSpace(clause))
            return (false, new List<string>());

        if (clause.Contains('|'))
        {
            var options = clause
                .Split('|', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            return (true, options);
        }

        var orSplit = Regex.Split(clause, @"\s+or\s+", RegexOptions.IgnoreCase)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        if (orSplit.Count > 1)
            return (true, orSplit);

        return (false, new List<string> { clause.Trim() });
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

    private static bool TryResolveTypeOption(string option, HashSet<string> configuredTypeExtensions, out List<string> extensions)
    {
        extensions = new List<string>();

        var token = option.Trim().TrimStart('.').ToLowerInvariant();
        if (token.Length == 0)
            return false;

        if (FileTypeAliases.TryGetValue(token, out var alias))
        {
            var expanded = alias
                .Select(v => v.ToLowerInvariant())
                .Where(configuredTypeExtensions.Contains)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (expanded.Count == 0)
                return false;

            extensions.AddRange(expanded);
            return true;
        }

        if (configuredTypeExtensions.Contains(token))
        {
            extensions.Add(token);
            return true;
        }

        return false;
    }

    private static List<string> ExpandConfiguredFileTypesToExtensions(IReadOnlyList<string> configuredFileTypes)
    {
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var type in configuredFileTypes)
        {
            var token = (type ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
            if (token.Length == 0)
                continue;

            if (FileTypeAliases.TryGetValue(token, out var aliases) && aliases.Length > 0)
            {
                foreach (var aliasExt in aliases)
                    expanded.Add(aliasExt.ToLowerInvariant());
                continue;
            }

            if (Regex.IsMatch(token, "^[a-z0-9]{2,8}$", RegexOptions.IgnoreCase))
            {
                expanded.Add(token);
            }
        }

        if (expanded.Count == 0)
            expanded.UnionWith(new[] { "doc", "docx", "pdf", "txt", "dwg", "rvt", "xls", "xlsx", "csv", "png", "jpg", "jpeg", "msg" });

        return expanded.ToList();
    }

    private static bool TryExtractPathOverride(string? query, out string? path, out string filter)
    {
        path = null;
        filter = query ?? string.Empty;
        var trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            return false;

        var splitIndex = trimmed.IndexOf("::", StringComparison.Ordinal);
        if (splitIndex <= 0)
            return false;

        var pathCandidate = trimmed[..splitIndex].Trim().Trim('"');
        if (!Directory.Exists(pathCandidate))
            return false;

        path = pathCandidate;
        filter = splitIndex + 2 < trimmed.Length
            ? trimmed[(splitIndex + 2)..].Trim()
            : string.Empty;
        return true;
    }

    private static string BuildStatusText(string sourcePath, string query, int count, string latestMode)
    {
        if (string.IsNullOrWhiteSpace(query))
            return $"Type to search within {sourcePath}";

        if (Regex.IsMatch(query, @"\blatest\b", RegexOptions.IgnoreCase))
        {
            return string.Equals(latestMode, "single", StringComparison.OrdinalIgnoreCase)
                ? $"Latest mode (single): {count} match"
                : $"Latest mode (list): {count} matches";
        }

        return $"{count} smart matches";
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);

    private sealed record ParsedQuery(
        bool LatestRequested,
        string? RegexPattern,
        HashSet<string> AllowedExtensions,
        List<ParsedClause> TextClauses);

    private sealed record ParsedClause(bool IsOr, List<string> Options);
}

public sealed class SmartProjectSearchResult
{
    public string Path { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string Extension { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string SizeDisplay { get; set; } = string.Empty;
    public DateTime LastModified { get; set; }
    public string Category { get; set; } = string.Empty;
}
