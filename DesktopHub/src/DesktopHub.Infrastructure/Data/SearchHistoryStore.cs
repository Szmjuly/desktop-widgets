using System.Text.Json;

namespace DesktopHub.Infrastructure.Data;

/// <summary>
/// Persists search history entries to a JSON file at %AppData%\DesktopHub\search-history.json.
/// Thread-safe; entries include timestamps for retention pruning and export.
/// </summary>
public class SearchHistoryStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private List<SearchHistoryEntry> _entries = new();

    public SearchHistoryStore()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopHub"
        );
        Directory.CreateDirectory(appDataPath);
        _filePath = Path.Combine(appDataPath, "search-history.json");
    }

    public async Task LoadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath);
                _entries = JsonSerializer.Deserialize<List<SearchHistoryEntry>>(json) ?? new();
            }
        }
        catch
        {
            _entries = new();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync()
    {
        await _lock.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(_entries, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_filePath, json);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Add a query to the front of history. Deduplicates (moves existing to front).
    /// Caps total entries at <paramref name="maxEntries"/>.
    /// </summary>
    public async Task AddEntryAsync(string query, int maxEntries = 25)
    {
        await _lock.WaitAsync();
        try
        {
            _entries.RemoveAll(e => e.Query.Equals(query, StringComparison.OrdinalIgnoreCase));
            _entries.Insert(0, new SearchHistoryEntry { Query = query, Timestamp = DateTime.UtcNow });
            if (_entries.Count > maxEntries)
                _entries = _entries.Take(maxEntries).ToList();
        }
        finally
        {
            _lock.Release();
        }
        await SaveAsync();
    }

    /// <summary>
    /// Get up to <paramref name="maxCount"/> most recent queries.
    /// </summary>
    public List<string> GetEntries(int maxCount = 25)
    {
        _lock.Wait();
        try
        {
            return _entries.Take(maxCount).Select(e => e.Query).ToList();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Remove entries older than the given number of days.
    /// </summary>
    public async Task PruneOlderThanAsync(int days)
    {
        var cutoff = DateTime.UtcNow.AddDays(-days);
        await _lock.WaitAsync();
        try
        {
            _entries.RemoveAll(e => e.Timestamp < cutoff);
        }
        finally
        {
            _lock.Release();
        }
        await SaveAsync();
    }

    /// <summary>
    /// Export the current history to a file at the given path.
    /// Creates the directory if needed.
    /// </summary>
    public async Task ExportToFileAsync(string exportPath)
    {
        await _lock.WaitAsync();
        List<SearchHistoryEntry> snapshot;
        try
        {
            snapshot = new List<SearchHistoryEntry>(_entries);
        }
        finally
        {
            _lock.Release();
        }

        var dir = Path.GetDirectoryName(exportPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(exportPath, json);
    }

    public class SearchHistoryEntry
    {
        public string Query { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
