using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using DesktopHub.Core.Models;

namespace DesktopHub.UI.Services;

/// <summary>
/// Manages cheat sheet data: loading, saving, searching, and inputâ†’output lookups.
/// </summary>
public class CheatSheetService
{
    private readonly string _dataFilePath;
    private CheatSheetDataStore _store = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public CheatSheetService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopHub"
        );
        Directory.CreateDirectory(appDataPath);
        _dataFilePath = Path.Combine(appDataPath, "cheatsheets.json");
    }

    public CheatSheetDataStore Store => _store;

    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(_dataFilePath))
            {
                var json = await File.ReadAllTextAsync(_dataFilePath);
                _store = JsonSerializer.Deserialize<CheatSheetDataStore>(json, JsonOptions) ?? new CheatSheetDataStore();

                // Auto-merge any new default sheets/code books that don't exist in saved data
                MergeDefaults();
                await SaveAsync();
            }
            else
            {
                _store = CreateDefaultData();
                await SaveAsync();
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"CheatSheetService.LoadAsync: Error: {ex.Message}");
            _store = CreateDefaultData();
        }
    }

    /// <summary>
    /// Merges new default code books, jurisdictions, and sheets into the loaded store
    /// without overwriting any user-modified data. Only adds entries whose Id is missing.
    /// </summary>
    private void MergeDefaults()
    {
        var defaults = CreateDefaultData();
        var merged = false;

        var existingBookIds = new HashSet<string>(_store.CodeBooks.Select(b => b.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var book in defaults.CodeBooks)
        {
            if (!existingBookIds.Contains(book.Id))
            {
                _store.CodeBooks.Add(book);
                merged = true;
                DebugLogger.Log($"CheatSheetService: Merged new code book '{book.Name} {book.Edition}'");
            }
        }

        var existingJurisdictionIds = new HashSet<string>(_store.Jurisdictions.Select(j => j.JurisdictionId), StringComparer.OrdinalIgnoreCase);
        foreach (var jurisdiction in defaults.Jurisdictions)
        {
            if (!existingJurisdictionIds.Contains(jurisdiction.JurisdictionId))
            {
                _store.Jurisdictions.Add(jurisdiction);
                merged = true;
                DebugLogger.Log($"CheatSheetService: Merged new jurisdiction '{jurisdiction.Name}'");
            }
        }

        var existingSheetIds = new HashSet<string>(_store.Sheets.Select(s => s.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var sheet in defaults.Sheets)
        {
            if (!existingSheetIds.Contains(sheet.Id))
            {
                _store.Sheets.Add(sheet);
                merged = true;
                DebugLogger.Log($"CheatSheetService: Merged new sheet '{sheet.Title}' ({sheet.Discipline})");
            }
        }

        if (merged)
            DebugLogger.Log("CheatSheetService: Default data merge complete â€” new entries added");
    }

    public async Task SaveAsync()
    {
        try
        {
            var json = JsonSerializer.Serialize(_store, JsonOptions);
            await File.WriteAllTextAsync(_dataFilePath, json);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"CheatSheetService.SaveAsync: Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Get all code books for a discipline.
    /// </summary>
    public List<CodeBook> GetCodeBooks(Discipline discipline)
        => _store.CodeBooks.Where(cb => cb.Discipline == discipline).ToList();

    /// <summary>
    /// Get all jurisdictions that adopt a specific code book.
    /// </summary>
    public List<JurisdictionCodeAdoption> GetJurisdictions(string? codeBookId = null)
        => codeBookId == null
            ? _store.Jurisdictions.ToList()
            : _store.Jurisdictions.Where(j => j.AdoptedCodeBookId == codeBookId).ToList();

    /// <summary>
    /// Get all sheets for a discipline, optionally filtered by code book.
    /// </summary>
    public List<CheatSheet> GetSheets(Discipline discipline, string? codeBookId = null)
    {
        var sheets = _store.Sheets.Where(s => s.Discipline == discipline);
        if (!string.IsNullOrEmpty(codeBookId))
            sheets = sheets.Where(s => s.CodeBookId == codeBookId);
        return sheets.ToList();
    }

    /// <summary>
    /// Smart search across sheets for a discipline. Matches title, subtitle, description, tags.
    /// </summary>
    public List<CheatSheet> Search(Discipline discipline, string query, string? codeBookId = null)
    {
        var sheets = GetSheets(discipline, codeBookId);
        if (string.IsNullOrWhiteSpace(query))
            return sheets;

        var tokens = query.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        var results = new List<(CheatSheet sheet, int score)>();

        foreach (var sheet in sheets)
        {
            var score = ScoreSheet(sheet, tokens);
            if (score > 0)
                results.Add((sheet, score));
        }

        return results.OrderByDescending(r => r.score).Select(r => r.sheet).ToList();
    }

    /// <summary>
    /// Perform a table lookup: given input column values, find matching rows and return output column values.
    /// </summary>
    public List<Dictionary<string, string>> Lookup(CheatSheet sheet, Dictionary<string, string> inputs)
    {
        if (sheet.Columns.Count == 0 || sheet.Rows.Count == 0)
            return new List<Dictionary<string, string>>();

        var results = new List<Dictionary<string, string>>();

        foreach (var row in sheet.Rows)
        {
            if (row.Count != sheet.Columns.Count)
                continue;

            var match = true;
            foreach (var input in inputs)
            {
                var colIdx = sheet.Columns.FindIndex(c =>
                    c.Header.Equals(input.Key, StringComparison.OrdinalIgnoreCase));
                if (colIdx < 0 || colIdx >= row.Count)
                {
                    match = false;
                    break;
                }

                if (!MatchesValue(row[colIdx], input.Value))
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                var result = new Dictionary<string, string>();
                for (var i = 0; i < sheet.Columns.Count; i++)
                {
                    if (i < row.Count)
                        result[sheet.Columns[i].Header] = row[i];
                }
                results.Add(result);
            }
        }

        return results;
    }

    /// <summary>
    /// Find rows in a table where cells match the search text.
    /// Supports token-based matching, unit/filler word awareness, numeric matching,
    /// and graceful partial-typing behaviour (e.g. "1 H" still matches while the user types "1 HP").
    /// </summary>
    public List<int> FindInTable(CheatSheet sheet, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText) || sheet.Rows.Count == 0)
            return Enumerable.Range(0, sheet.Rows.Count).ToList();

        var raw = searchText.Trim();
        var normalized = NormalizeQueryForSearch(raw);
        var tokens = raw.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

        // Phase 0: Column-targeted search for queries like "1 HP", "1HP", "16A"
        var targeted = TryColumnTargetedSearch(sheet, raw, tokens);
        if (targeted != null && targeted.Count > 0)
            return targeted;

        // Phase 1: full-string contains (fast path)
        var indices = new List<int>();
        for (var i = 0; i < sheet.Rows.Count; i++)
        {
            if (sheet.Rows[i].Any(cell =>
                cell.Contains(raw, StringComparison.OrdinalIgnoreCase) ||
                (!normalized.Equals(raw, StringComparison.OrdinalIgnoreCase) &&
                 cell.Contains(normalized, StringComparison.OrdinalIgnoreCase))))
            {
                indices.Add(i);
            }
        }
        if (indices.Count > 0)
            return indices;

        // Phase 2: token-based matching
        var valueTokens = ExtractValueTokens(tokens, sheet);

        if (valueTokens.Count == 0)
            return Enumerable.Range(0, sheet.Rows.Count).ToList(); // all tokens were unit/filler → show all

        for (var i = 0; i < sheet.Rows.Count; i++)
        {
            var row = sheet.Rows[i];
            if (valueTokens.All(vt => row.Any(cell => CellMatchesFind(cell, vt))))
                indices.Add(i);
        }
        if (indices.Count > 0)
            return indices;

        // Phase 3: single-value numeric match as last resort
        if (valueTokens.Count == 1 && TryParseFlexibleNumber(valueTokens[0], out _))
        {
            for (var i = 0; i < sheet.Rows.Count; i++)
            {
                if (sheet.Rows[i].Any(cell =>
                    TryParseFlexibleNumber(cell, out var cn) &&
                    TryParseFlexibleNumber(valueTokens[0], out var vn) &&
                    Math.Abs(cn - vn) < 0.001))
                {
                    indices.Add(i);
                }
            }
        }

        return indices;
    }

    /// <summary>
    /// Splits query tokens into value tokens by filtering out unit words, filler words,
    /// column header words, and partial unit prefixes (for the last token while the user is still typing).
    /// </summary>
    private static List<string> ExtractValueTokens(string[] tokens, CheatSheet sheet)
    {
        // Collect column header words and units for this sheet
        var headerWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in sheet.Columns)
        {
            foreach (var word in col.Header.Split(new[] { ' ', '(', ')' }, StringSplitOptions.RemoveEmptyEntries))
                headerWords.Add(word);
            if (col.Unit != null)
                headerWords.Add(col.Unit);
        }

        var result = new List<string>();
        for (var t = 0; t < tokens.Length; t++)
        {
            var token = tokens[t];
            var isLast = t == tokens.Length - 1;

            // Skip recognized unit/filler words
            if (UnitAndFillerWords.Contains(token) || headerWords.Contains(token))
                continue;

            // Normalize individual token (e.g. "208V" → "208")
            var nt = NormalizeQueryForSearch(token);
            if (!nt.Equals(token, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(nt);
                continue;
            }

            // Last token: skip if it is a prefix of a known unit/header word (user still typing)
            if (isLast && token.Length <= 4 && tokens.Length > 1)
            {
                var isPartial = UnitAndFillerWords.Any(u =>
                    u.Length > token.Length && u.StartsWith(token, StringComparison.OrdinalIgnoreCase)) ||
                    headerWords.Any(h =>
                    h.Length > token.Length && h.StartsWith(token, StringComparison.OrdinalIgnoreCase));
                if (isPartial)
                    continue;
            }

            result.Add(token);
        }
        return result;
    }

    /// <summary>
    /// Checks if a cell value matches a search token via contains or numeric equality.
    /// </summary>
    private static bool CellMatchesFind(string cell, string token)
    {
        if (cell.Contains(token, StringComparison.OrdinalIgnoreCase))
            return true;

        if (TryParseFlexibleNumber(cell, out var cellNum) && TryParseFlexibleNumber(token, out var tokenNum))
            return Math.Abs(cellNum - tokenNum) < 0.001;

        return false;
    }

    /// <summary>
    /// Column-targeted search: when the query includes a column identifier (header or unit),
    /// do exact/numeric matching in that column instead of broad substring search.
    /// E.g. "1 HP" → exact match "1" in HP column; "1HP" → strip "HP" suffix, match in HP column.
    /// </summary>
    private List<int>? TryColumnTargetedSearch(CheatSheet sheet, string raw, string[] tokens)
    {
        // Multi-token: check if any token exactly matches a column header or unit
        if (tokens.Length >= 2)
        {
            var matched = new HashSet<int>();
            for (var t = 0; t < tokens.Length; t++)
            {
                var token = tokens[t];
                foreach (var col in sheet.Columns)
                {
                    if (!col.Header.Equals(token, StringComparison.OrdinalIgnoreCase) &&
                        !(col.Unit != null && col.Unit.Equals(token, StringComparison.OrdinalIgnoreCase)))
                        continue;

                    var colIdx = sheet.Columns.IndexOf(col);
                    // Build value string from the other tokens, stripping units/filler
                    var valueStr = string.Join(" ", tokens
                        .Where((_, i) => i != t)
                        .Select(NormalizeQueryForSearch)
                        .Where(tk => !string.IsNullOrWhiteSpace(tk) && !UnitAndFillerWords.Contains(tk)));

                    if (string.IsNullOrWhiteSpace(valueStr)) continue;

                    for (var i = 0; i < sheet.Rows.Count; i++)
                    {
                        var row = sheet.Rows[i];
                        if (colIdx < row.Count && CellMatchesExact(row[colIdx], valueStr))
                            matched.Add(i);
                    }
                }
            }
            if (matched.Count > 0)
                return matched.OrderBy(x => x).ToList();
        }

        // Single-token with column unit/header suffix: "1HP" → ("1", "HP"), "16A" → ("16", "A")
        if (tokens.Length == 1)
        {
            var matched = new HashSet<int>();
            foreach (var col in sheet.Columns)
            {
                var suffixes = new List<string>();
                if (col.Unit != null) suffixes.Add(col.Unit);
                if (col.Header.Length < raw.Length) suffixes.Add(col.Header);

                foreach (var suffix in suffixes)
                {
                    if (suffix.Length >= raw.Length) continue;
                    if (!raw.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;

                    var valPart = raw[..^suffix.Length].Trim();
                    if (valPart.Length == 0) continue;

                    var colIdx = sheet.Columns.IndexOf(col);
                    for (var i = 0; i < sheet.Rows.Count; i++)
                    {
                        var row = sheet.Rows[i];
                        if (colIdx < row.Count && CellMatchesExact(row[colIdx], valPart))
                            matched.Add(i);
                    }
                }
            }
            if (matched.Count > 0)
                return matched.OrderBy(x => x).ToList();
        }

        return null;
    }

    /// <summary>
    /// Strict cell match: exact string equality or numeric equality (no substring).
    /// </summary>
    private static bool CellMatchesExact(string cell, string value)
    {
        if (cell.Equals(value, StringComparison.OrdinalIgnoreCase))
            return true;
        if (TryParseFlexibleNumber(cell, out var cellNum) && TryParseFlexibleNumber(value, out var valueNum))
            return Math.Abs(cellNum - valueNum) < 0.001;
        return false;
    }

    private static readonly HashSet<string> UnitAndFillerWords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Electrical
        "hp", "horsepower", "amp", "amps", "amperage", "ampere", "amperes",
        "volt", "volts", "voltage", "kv", "kva", "kw", "kwh", "mva", "mw",
        "hz", "pf", "va", "awg", "kcmil", "watt", "watts",
        // Plumbing
        "dfu", "wsfu", "gpm", "psi", "pipe", "drain", "fixture",
        // Mechanical
        "btuh", "btu", "cfm", "ton", "tons",
        // Generic units
        "in", "ft", "mm", "lbs",
        // Filler / prepositions
        "at", "for", "the", "of", "a", "an"
    };

    /// <summary>
    /// Strips common electrical unit suffixes from a query string, returning just the numeric/bare portion.
    /// E.g. "10 kVA" -> "10", "100A" -> "100", "10KVA" -> "10".
    /// </summary>
    private static string NormalizeQueryForSearch(string query)
    {
        var q = query.Trim();
        // Remove common unit suffixes (case-insensitive)
        var units = new[] { "kcmil", "kva", "kw", "kwh", "hp", " a", "amp", "amps", "volt", "volts",
                            "v", "w", "hz", "pf", "va", "mva", "mw", "awg" };
        foreach (var unit in units)
        {
            if (q.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
            {
                var stripped = q[..^unit.Length].TrimEnd();
                if (stripped.Length > 0)
                    return stripped;
            }
        }
        return q;
    }

    private static bool MatchesValue(string cellValue, string inputValue)
    {
        if (string.IsNullOrWhiteSpace(inputValue))
            return true;

        // Strip unit suffixes from the input value before comparing
        var normalizedInput = NormalizeQueryForSearch(inputValue.Trim());

        // Exact match (raw or normalized)
        if (cellValue.Equals(inputValue, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!normalizedInput.Equals(inputValue, StringComparison.OrdinalIgnoreCase) &&
            cellValue.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase))
            return true;

        // Numeric match (supports fractions like "1/3" and mixed like "1-1/2")
        if (TryParseFlexibleNumber(cellValue, out var cellNum) && TryParseFlexibleNumber(normalizedInput, out var inputNum))
            return Math.Abs(cellNum - inputNum) < 0.001;

        return false;
    }

    private static bool TryParseFlexibleNumber(string raw, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();

        if (double.TryParse(s, out value))
            return true;

        // Mixed number like 1-1/2
        var dashParts = s.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (dashParts.Length == 2 &&
            double.TryParse(dashParts[0], out var whole) &&
            TryParseFraction(dashParts[1], out var frac))
        {
            value = whole + frac;
            return true;
        }

        // Fraction like 1/3
        return TryParseFraction(s, out value);
    }

    private static bool TryParseFraction(string raw, out double value)
    {
        value = 0;
        var parts = raw.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        if (!double.TryParse(parts[0], out var num) || !double.TryParse(parts[1], out var den))
            return false;
        if (Math.Abs(den) < 0.0000001)
            return false;

        value = num / den;
        return true;
    }

    private static int ScoreSheet(CheatSheet sheet, string[] tokens)
    {
        var score = 0;
        var searchable = $"{sheet.Title} {sheet.Subtitle} {sheet.Description} {string.Join(" ", sheet.Tags)}";

        foreach (var token in tokens)
        {
            if (sheet.Title.Contains(token, StringComparison.OrdinalIgnoreCase))
                score += 10;
            else if (sheet.Tags.Any(t => t.Contains(token, StringComparison.OrdinalIgnoreCase)))
                score += 7;
            else if ((sheet.Subtitle ?? "").Contains(token, StringComparison.OrdinalIgnoreCase))
                score += 5;
            else if ((sheet.Description ?? "").Contains(token, StringComparison.OrdinalIgnoreCase))
                score += 3;
            else if (searchable.Contains(token, StringComparison.OrdinalIgnoreCase))
                score += 1;
            else
                return 0; // All tokens must match something
        }

        return score;
    }

    private static CheatSheetDataStore CreateDefaultData()
    {
        var store = new CheatSheetDataStore();

        CheatSheetSharedDefaults.AddTo(store);
        CheatSheetElectricalDefaults.AddTo(store);
        CheatSheetPlumbingDefaults.AddTo(store);
        CheatSheetMechanicalDefaults.AddTo(store);

        return store;
    }

    /// <summary>
    /// Search across ALL disciplines for a query. Returns results grouped by discipline.
    /// Used for cross-discipline search when current discipline has no matches.
    /// </summary>
    public Dictionary<Discipline, List<CheatSheet>> SearchAllDisciplines(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return new Dictionary<Discipline, List<CheatSheet>>();

        var results = new Dictionary<Discipline, List<CheatSheet>>();
        foreach (Discipline discipline in Enum.GetValues(typeof(Discipline)))
        {
            var matches = Search(discipline, query);
            if (matches.Count > 0)
                results[discipline] = matches;
        }
        return results;
    }
}

