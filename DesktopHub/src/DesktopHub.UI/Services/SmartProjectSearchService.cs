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
    private List<DocumentItem> _results = new();
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _projectSwitchCts;
    private CancellationTokenSource? _queryCts;
    private int _refreshVersion;
    private const int MaxDisplayResultsDefault = 200;
    private const int MaxDisplayResultsShortQuery = 50;
    private List<IndexedDocument>? _cachedSearchPool;
    private string? _cachedPathOverride;
    private readonly Dictionary<string, ProjectFileInfo> _scanCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Queue<string> _scanCacheOrder = new();
    private const int MaxScanCacheEntries = 12;

    public event EventHandler? StateChanged;
    public event EventHandler<bool>? ScanningChanged;

    public IReadOnlyList<DocumentItem> Results => _results;
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
        // Cancel any previous project-switch flow (handles fast clicking)
        _projectSwitchCts?.Cancel();
        _projectSwitchCts = new CancellationTokenSource();
        var switchToken = _projectSwitchCts.Token;

        Interlocked.Increment(ref _refreshVersion);

        if (string.IsNullOrWhiteSpace(projectPath))
        {
            DebugLogger.Log("SmartSearch: SetProjectAsync - cleared (null/empty path)");
            _projectPath = null;
            _projectName = null;
            _projectInfo = null;
            ActiveProjectLabel = "No project selected";
            StatusText = "Select a project to begin searching.";
            _results = new List<DocumentItem>();
            RaiseStateChanged();
            return;
        }

        var normalizedPath = projectPath.Trim();
        var isSameProject = string.Equals(_projectPath, normalizedPath, StringComparison.OrdinalIgnoreCase)
                            && _projectInfo != null
                            && _cachedSearchPool != null;

        DebugLogger.Log($"SmartSearch: SetProjectAsync path='{normalizedPath}' isSameProject={isSameProject} hasInfo={_projectInfo != null} hasPool={_cachedSearchPool != null}");

        _projectPath = normalizedPath;
        _projectName = string.IsNullOrWhiteSpace(projectName) ? Path.GetFileName(normalizedPath) : projectName;
        ActiveProjectLabel = _projectName ?? normalizedPath;

        if (!isSameProject)
        {
            _cachedSearchPool = null;
            await ScanSelectedProjectAsync();
        }

        switchToken.ThrowIfCancellationRequested();
        await RefreshResultsAsync();
    }

    public async Task SetQueryAsync(string? query)
    {
        // Cancel any previous in-flight query search
        _queryCts?.Cancel();
        _queryCts = new CancellationTokenSource();

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

        _cachedSearchPool = null;
        _projectInfo = null;

        var intendedPath = _projectPath;
        var intendedName = _projectName;

        IsScanning = true;
        ScanningChanged?.Invoke(this, true);

        try
        {
            var extensions = ExpandConfiguredFileTypesToExtensions(_settings.GetSmartProjectSearchFileTypes());
            DebugLogger.Log($"SmartSearch: Scan starting for '{intendedPath}' extensions=[{string.Join(",", extensions)}]");
            var cacheKey = BuildScanCacheKey(intendedPath, extensions);

            ProjectFileInfo scannedInfo;
            if (_scanCache.TryGetValue(cacheKey, out var cachedProjectInfo))
            {
                scannedInfo = cachedProjectInfo;
                DebugLogger.Log($"SmartSearch: Scan cache HIT — AllFiles={scannedInfo.AllFiles?.Count ?? 0}");
            }
            else
            {
                scannedInfo = await _scanner.ScanProjectAsync(
                    intendedPath,
                    intendedName,
                    maxDepth: 6,
                    excludedFolders: Array.Empty<string>(),
                    maxFiles: 10000,
                    includeExtensions: extensions,
                    cancellationToken: token);
                StoreScanCache(cacheKey, scannedInfo);
                DebugLogger.Log($"SmartSearch: Scan complete — AllFiles={scannedInfo.AllFiles?.Count ?? 0} DisciplineFiles={scannedInfo.DisciplineFiles.Values.Sum(v => v.Count)} RvtFiles={scannedInfo.Revit?.RvtFiles?.Count ?? 0}");
            }

            token.ThrowIfCancellationRequested();

            if (!string.Equals(_projectPath, intendedPath, StringComparison.OrdinalIgnoreCase))
            {
                DebugLogger.Log($"SmartSearch: Scan DISCARDED — project changed from '{intendedPath}' to '{_projectPath}'");
                return;
            }

            _projectInfo = scannedInfo;
            _cachedSearchPool = null;
            var pool = GetOrBuildSearchPool(_projectInfo);
            DebugLogger.Log($"SmartSearch: Search pool built — {pool.Count} unique indexed documents");
            StatusText = pool.Count == 0
                ? "No searchable files found in selected project."
                : $"Indexed {pool.Count} files in {intendedName}.";
        }
        catch (OperationCanceledException)
        {
            DebugLogger.Log("SmartSearch: Scan canceled");
            StatusText = "Scan canceled.";
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SmartSearch: Scan FAILED — {ex.Message}");
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
            DebugLogger.Log("SmartSearch: RefreshResults — no project loaded, clearing results");
            _results = new List<DocumentItem>();
            RaiseStateChanged();
            return;
        }

        var pathOverride = TryExtractPathOverride(_query, out var overridePath, out var overrideFilter);
        var effectiveQuery = pathOverride ? overrideFilter : (_query ?? string.Empty);

        DebugLogger.Log($"SmartSearch: RefreshResults query='{_query}' effectiveQuery='{effectiveQuery}' pathOverride={pathOverride}");

        var sourceInfo = _projectInfo;
        if (pathOverride && !string.IsNullOrWhiteSpace(overridePath))
        {
            // Check Directory.Exists off UI thread
            var pathExists = await Task.Run(() => Directory.Exists(overridePath));
            if (refreshVersion != Volatile.Read(ref _refreshVersion))
                return;

            if (pathExists)
            {
                var includeExtensions = ExpandConfiguredFileTypesToExtensions(_settings.GetSmartProjectSearchFileTypes());
                var cacheKey = BuildScanCacheKey(overridePath, includeExtensions);
                if (!_scanCache.TryGetValue(cacheKey, out var cachedPathInfo))
                {
                    var scanCt = _scanCts?.Token ?? CancellationToken.None;
                    cachedPathInfo = await _scanner.ScanProjectAsync(
                        overridePath,
                        Path.GetFileName(overridePath),
                        maxDepth: 6,
                        excludedFolders: Array.Empty<string>(),
                        maxFiles: 10000,
                        includeExtensions: includeExtensions,
                        cancellationToken: scanCt);
                    StoreScanCache(cacheKey, cachedPathInfo);
                }

                sourceInfo = cachedPathInfo;
            }
        }

        if (refreshVersion != Volatile.Read(ref _refreshVersion))
            return;

        // Capture values for background work
        var capturedSourceInfo = sourceInfo;
        var capturedPathOverride = pathOverride;
        var capturedOverridePath = overridePath;
        var latestMode = _settings.GetSmartProjectSearchLatestMode();
        var configuredFileTypes = _settings.GetSmartProjectSearchFileTypes();
        var capturedQuery = effectiveQuery;
        var capturedProjectPath = sourceInfo.ProjectPath;

        // Capture query CTS so background work can bail out early
        var queryToken = _queryCts?.Token ?? CancellationToken.None;

        List<DocumentItem> newResults;
        try
        {
            // Move pool building AND search ranking off UI thread
            newResults = await Task.Run(() =>
            {
                queryToken.ThrowIfCancellationRequested();

                var pool = capturedPathOverride
                    ? BuildSearchPoolRaw(capturedSourceInfo)
                    : GetOrBuildSearchPool(capturedSourceInfo);

                DebugLogger.Log($"SmartSearch: Pool size={pool.Count} latestMode='{latestMode}'");

                queryToken.ThrowIfCancellationRequested();

                var ranked = ApplySearch(pool, capturedQuery, latestMode, configuredFileTypes);
                var cap = capturedQuery.Length <= 2 ? MaxDisplayResultsShortQuery : MaxDisplayResultsDefault;
                return ranked.Select(idx => idx.Doc).Take(cap).ToList();
            }, queryToken);
        }
        catch (OperationCanceledException)
        {
            DebugLogger.Log($"SmartSearch: Query canceled for '{capturedQuery}'");
            return;
        }

        if (refreshVersion != Volatile.Read(ref _refreshVersion))
            return;

        _cachedPathOverride = capturedPathOverride ? capturedOverridePath : null;
        _results = newResults;

        DebugLogger.Log($"SmartSearch: Results={_results.Count} for query='{capturedQuery}' projectPath='{capturedProjectPath}'");
        for (var i = 0; i < Math.Min(_results.Count, 5); i++)
            DebugLogger.Log($"SmartSearch:   [{i + 1}] {_results[i].RelativePath}");

        StatusText = BuildStatusText(capturedProjectPath, effectiveQuery, _results.Count, latestMode);
        RaiseStateChanged();
    }

    private List<IndexedDocument> GetOrBuildSearchPool(ProjectFileInfo info)
    {
        if (_cachedSearchPool != null && _cachedPathOverride == null)
            return _cachedSearchPool;

        var pool = BuildSearchPoolRaw(info);
        if (_cachedPathOverride == null)
            _cachedSearchPool = pool;
        return pool;
    }

    private static List<IndexedDocument> BuildSearchPoolRaw(ProjectFileInfo info)
    {
        var disciplineFiles = info.DisciplineFiles.Values.SelectMany(v => v);
        var revitFiles = info.Revit?.RvtFiles ?? Enumerable.Empty<DocumentItem>();
        var allFiles = info.AllFiles ?? new List<DocumentItem>();

        var allCount = allFiles.Count;
        var discCount = disciplineFiles.Count();
        var rvtCount = revitFiles.Count();

        var pool = allFiles
            .Concat(info.DisciplineFiles.Values.SelectMany(v => v))
            .Concat(info.Revit?.RvtFiles ?? Enumerable.Empty<DocumentItem>())
            .GroupBy(d => d.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => new IndexedDocument(g.First()))
            .ToList();

        DebugLogger.Log($"SmartSearch: BuildSearchPoolRaw — allFiles={allCount} disciplineFiles={discCount} rvtFiles={rvtCount} → pool={pool.Count} unique");
        return pool;
    }

    private static IEnumerable<IndexedDocument> ApplySearch(List<IndexedDocument> pool, string? query, string latestMode, IReadOnlyList<string> configuredFileTypes)
    {
        if (pool.Count == 0)
            return Enumerable.Empty<IndexedDocument>();

        var trimmed = (query ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return Enumerable.Empty<IndexedDocument>();
        }

        var parse = ParseQuery(trimmed, configuredFileTypes);

        var scored = new List<(IndexedDocument Idx, double Score)>();
        Regex? regex = null;

        if (parse.RegexPattern != null)
        {
            try
            {
                regex = new Regex(parse.RegexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            catch
            {
                return Enumerable.Empty<IndexedDocument>();
            }
        }

        var extFiltered = 0;
        var exclFiltered = 0;
        var clauseFiltered = 0;

        foreach (var idx in pool)
        {
            if (parse.AllowedExtensions.Count > 0 && !parse.AllowedExtensions.Contains(idx.Doc.Extension, StringComparer.OrdinalIgnoreCase))
            {
                extFiltered++;
                continue;
            }

            if (parse.ExclusionTerms.Count > 0)
            {
                var excluded = false;
                foreach (var excl in parse.ExclusionTerms)
                {
                    if (idx.SearchText.Contains(excl, StringComparison.OrdinalIgnoreCase))
                    {
                        excluded = true;
                        break;
                    }
                }
                if (excluded) { exclFiltered++; continue; }
            }

            double score = 0;

            if (regex != null)
            {
                if (!regex.IsMatch(idx.SearchText))
                    continue;

                score = 2.0;
            }
            else
            {
                var clauseMatchedAll = true;
                foreach (var clause in parse.TextClauses)
                {
                    var (matched, clauseScore) = EvaluateClause(idx, clause);
                    if (!matched)
                    {
                        clauseMatchedAll = false;
                        break;
                    }

                    score += clauseScore;
                }

                if (!clauseMatchedAll)
                {
                    clauseFiltered++;
                    continue;
                }
            }

            if (parse.LatestRequested)
            {
                var ageDays = Math.Max(0.0, (DateTime.Now - idx.Doc.LastModified).TotalDays);
                var freshness = Math.Max(0.0, 30.0 - ageDays) / 30.0;
                score += freshness * 5.0;
            }

            scored.Add((idx, score));
        }

        DebugLogger.Log($"SmartSearch: ApplySearch — pool={pool.Count} extFiltered={extFiltered} exclFiltered={exclFiltered} clauseFiltered={clauseFiltered} scored={scored.Count}");

        if (parse.LatestRequested && string.Equals(latestMode, "single", StringComparison.OrdinalIgnoreCase))
        {
            return scored
                .OrderByDescending(s => s.Idx.Doc.LastModified)
                .ThenByDescending(s => s.Score)
                .Take(1)
                .Select(s => s.Idx);
        }

        return parse.LatestRequested
            ? scored
                .OrderByDescending(s => s.Idx.Doc.LastModified)
                .ThenByDescending(s => s.Score)
                .ThenBy(s => s.Idx.Doc.FileName)
                .Select(s => s.Idx)
            : scored
                .OrderByDescending(s => s.Score)
                .ThenByDescending(s => s.Idx.Doc.LastModified)
                .ThenBy(s => s.Idx.Doc.FileName)
                .Select(s => s.Idx);
    }

    private static (bool Matched, double Score) EvaluateClause(IndexedDocument idx, ParsedClause clause)
    {
        var best = 0.0;
        var matched = false;

        foreach (var option in clause.Options)
        {
            var score = ScoreAlternative(idx, option);
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

    private static double ScoreAlternative(IndexedDocument idx, string alternative)
    {
        var normalizedOption = alternative.Trim();
        if (normalizedOption.Length == 0)
            return 0;

        var terms = ExtractSearchTerms(normalizedOption);
        if (terms.Count == 0)
        {
            return idx.SearchText.Contains(normalizedOption, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        }

        var hits = 0;
        var score = 0.0;

        foreach (var term in terms)
        {
            var termScore = ScoreTerm(idx, term);
            if (termScore > 0)
            {
                hits++;
                score += termScore;
            }
        }

        var phraseMatch = idx.SearchText.Contains(normalizedOption, StringComparison.OrdinalIgnoreCase);
        if (phraseMatch)
            score += 4.0;

        var minimumHits = terms.Count <= 2 ? terms.Count : terms.Count - 1;
        return hits >= minimumHits || phraseMatch ? score : 0;
    }

    private static double ScoreTerm(IndexedDocument idx, string term)
    {
        var best = 0.0;

        foreach (var alias in ExpandTerm(term))
        {
            if (alias.Length == 0)
                continue;

            if (idx.FileNameLower.StartsWith(alias, StringComparison.Ordinal)) best = Math.Max(best, 4.0);
            if (idx.FileNameLower.Contains(alias, StringComparison.Ordinal)) best = Math.Max(best, 3.2);
            if (idx.RelativePathLower.Contains(alias, StringComparison.Ordinal)) best = Math.Max(best, 2.4);
            if (idx.SubfolderLower.Contains(alias, StringComparison.Ordinal)) best = Math.Max(best, 1.5);
            if (idx.CategoryLower.Contains(alias, StringComparison.Ordinal)) best = Math.Max(best, 1.2);
            if (best == 0.0 && alias.Length >= 3 && IsSubsequence(alias, idx.FileNameLower)) best = Math.Max(best, 0.6);

            if (best == 0.0 && alias.Length >= 4)
            {
                best = Math.Max(best, FuzzyMatchTokens(alias, idx.FileNameLower));
            }
        }

        return best;
    }

    private static double FuzzyMatchTokens(string term, string text)
    {
        var maxAllowedDistance = term.Length <= 5 ? 1 : 2;
        var tokens = text.Split(new[] { ' ', '_', '-', '.' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var token in tokens)
        {
            if (Math.Abs(token.Length - term.Length) > maxAllowedDistance)
                continue;

            var dist = LevenshteinDistance(term, token);
            if (dist <= maxAllowedDistance)
            {
                return 0.3 * (1.0 - (double)dist / term.Length);
            }
        }

        return 0.0;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var n = s.Length;
        var m = t.Length;

        if (n == 0) return m;
        if (m == 0) return n;

        var prev = new int[m + 1];
        var curr = new int[m + 1];

        for (var j = 0; j <= m; j++)
            prev[j] = j;

        for (var i = 1; i <= n; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= m; j++)
            {
                var cost = s[i - 1] == t[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }

            (prev, curr) = (curr, prev);
        }

        return prev[m];
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
        DebugLogger.Log($"SmartSearch: ParseQuery input='{query}'");
        var configuredTypeExtensions = ExpandConfiguredFileTypesToExtensions(configuredFileTypes)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var latestRequested = Regex.IsMatch(query, @"\blatest\b", RegexOptions.IgnoreCase);
        var withoutLatest = Regex.Replace(query, @"\blatest\b", " ", RegexOptions.IgnoreCase).Trim();

        var exclusionTerms = ExtractExclusionTerms(withoutLatest);
        var withoutExclusions = RemoveExclusionTerms(withoutLatest).Trim();

        var parts = withoutExclusions
            .Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        if (parts.Count == 0 && withoutExclusions.Length > 0)
            parts.Add(withoutExclusions);

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

            var positiveOptions = new List<string>();
            foreach (var opt in options)
            {
                if (opt.StartsWith('-') && opt.Length > 1)
                {
                    exclusionTerms.Add(opt[1..].ToLowerInvariant());
                }
                else
                {
                    positiveOptions.Add(opt);
                }
            }

            if (positiveOptions.Count > 0)
                textClauses.Add(new ParsedClause(isOr, positiveOptions));
        }

        var result = new ParsedQuery(latestRequested, regexPattern, typeExtensions, textClauses, exclusionTerms);
        DebugLogger.Log($"SmartSearch: ParseQuery result — latest={result.LatestRequested} regex='{result.RegexPattern ?? ""}' extensions=[{string.Join(",", result.AllowedExtensions)}] exclusions=[{string.Join(",", result.ExclusionTerms)}] clauses={result.TextClauses.Count}");
        for (var ci = 0; ci < result.TextClauses.Count; ci++)
        {
            var c = result.TextClauses[ci];
            DebugLogger.Log($"SmartSearch:   clause[{ci}] isOr={c.IsOr} options=[{string.Join(" | ", c.Options)}]");
        }
        return result;
    }

    private static List<string> ExtractExclusionTerms(string query)
    {
        var excluded = new List<string>();
        foreach (Match match in Regex.Matches(query, @"(?:^|\s)-([a-z0-9._-]+)", RegexOptions.IgnoreCase))
        {
            var value = match.Groups[1].Value.Trim().ToLowerInvariant();
            if (value.Length > 0)
                excluded.Add(value);
        }

        return excluded
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string RemoveExclusionTerms(string query)
        => Regex.Replace(query, @"(?:^|\s)-[a-z0-9._-]+", " ", RegexOptions.IgnoreCase);

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

    private static string BuildScanCacheKey(string path, IReadOnlyCollection<string> extensions)
    {
        var extKey = string.Join(",", extensions
            .Select(e => (e ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant())
            .Where(e => e.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(e => e, StringComparer.Ordinal));

        return $"{path.Trim().ToLowerInvariant()}|{extKey}";
    }

    private void InvalidateScanCacheForPath(string path)
    {
        var prefix = path.Trim().ToLowerInvariant();
        var toRemove = _scanCache.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var k in toRemove)
            _scanCache.Remove(k);
    }

    private void StoreScanCache(string key, ProjectFileInfo info)
    {
        if (_scanCache.ContainsKey(key))
        {
            _scanCache[key] = info;
            return;
        }

        _scanCache[key] = info;
        _scanCacheOrder.Enqueue(key);

        while (_scanCacheOrder.Count > MaxScanCacheEntries)
        {
            var oldest = _scanCacheOrder.Dequeue();
            _scanCache.Remove(oldest);
        }
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
        // Don't check Directory.Exists here — it blocks the UI thread.
        // The caller validates the path off-thread instead.
        if (pathCandidate.Length < 2 || (pathCandidate.Length >= 2 && pathCandidate[1] != ':'))
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
        List<ParsedClause> TextClauses,
        List<string> ExclusionTerms);

    private sealed record ParsedClause(bool IsOr, List<string> Options);

    internal sealed class IndexedDocument
    {
        public DocumentItem Doc { get; }
        public string FileNameLower { get; }
        public string RelativePathLower { get; }
        public string SubfolderLower { get; }
        public string CategoryLower { get; }
        public string SearchText { get; }

        public IndexedDocument(DocumentItem doc)
        {
            Doc = doc;
            FileNameLower = doc.FileName.ToLowerInvariant();
            RelativePathLower = doc.RelativePath.ToLowerInvariant();
            SubfolderLower = doc.Subfolder.ToLowerInvariant();
            CategoryLower = doc.Category.ToLowerInvariant();
            SearchText = $"{doc.FileName} {doc.RelativePath} {doc.Subfolder} {doc.Category}";
        }
    }
}
