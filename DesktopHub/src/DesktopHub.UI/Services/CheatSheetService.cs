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
    /// Find rows in a table where any cell contains the search text.
    /// Returns matching row indices. Strips common unit suffixes so e.g. "10 kVA" matches cell "10".
    /// </summary>
    public List<int> FindInTable(CheatSheet sheet, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText) || sheet.Rows.Count == 0)
            return Enumerable.Range(0, sheet.Rows.Count).ToList();

        var normalized = NormalizeQueryForSearch(searchText);

        var indices = new List<int>();
        for (var i = 0; i < sheet.Rows.Count; i++)
        {
            if (sheet.Rows[i].Any(cell =>
                cell.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                cell.Contains(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                indices.Add(i);
            }
        }
        return indices;
    }

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

