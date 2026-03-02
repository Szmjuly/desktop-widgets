using System.Text.Json;
using Microsoft.Data.Sqlite;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Logging;

namespace DesktopHub.Infrastructure.Data;

/// <summary>
/// SQLite database for telemetry event storage and aggregation.
/// Separate database file from the main projects.db to keep metrics isolated.
/// </summary>
public class MetricsDatabase
{
    private readonly string _connectionString;
    private const string DatabaseFileName = "metrics.db";

    public MetricsDatabase(string? dataDirectory = null)
    {
        dataDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopHub"
        );

        Directory.CreateDirectory(dataDirectory);
        var dbPath = Path.Combine(dataDirectory, DatabaseFileName);
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task InitializeAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                PRAGMA journal_mode=WAL;

                CREATE TABLE IF NOT EXISTS telemetry_events (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    category TEXT NOT NULL,
                    event_type TEXT NOT NULL,
                    timestamp TEXT NOT NULL,
                    session_id TEXT,
                    data_json TEXT,
                    widget_name TEXT,
                    query_text TEXT,
                    project_number TEXT,
                    project_type TEXT,
                    discipline TEXT,
                    file_extension TEXT,
                    result_count INTEGER,
                    result_index INTEGER,
                    duration_ms INTEGER,
                    char_count INTEGER,
                    synced INTEGER NOT NULL DEFAULT 0
                );

                CREATE INDEX IF NOT EXISTS idx_events_category ON telemetry_events(category);
                CREATE INDEX IF NOT EXISTS idx_events_type ON telemetry_events(event_type);
                CREATE INDEX IF NOT EXISTS idx_events_timestamp ON telemetry_events(timestamp);
                CREATE INDEX IF NOT EXISTS idx_events_session ON telemetry_events(session_id);
                CREATE INDEX IF NOT EXISTS idx_events_synced ON telemetry_events(synced);
                CREATE INDEX IF NOT EXISTS idx_events_widget ON telemetry_events(widget_name);
                CREATE INDEX IF NOT EXISTS idx_events_category_timestamp ON telemetry_events(category, timestamp);
            ";

            using var command0 = new SqliteCommand(sql, connection);
            await command0.ExecuteNonQueryAsync();

            // Migration: add query_source column if it doesn't exist
            var migrationSql = @"
                SELECT COUNT(*) FROM pragma_table_info('telemetry_events') WHERE name='query_source';
            ";
            using var migCmd = new SqliteCommand(migrationSql, connection);
            var colExists = Convert.ToInt32(await migCmd.ExecuteScalarAsync());
            if (colExists == 0)
            {
                using var alterCmd = new SqliteCommand("ALTER TABLE telemetry_events ADD COLUMN query_source TEXT", connection);
                await alterCmd.ExecuteNonQueryAsync();
                InfraLogger.Log("MetricsDatabase: Migrated - added query_source column");
            }

            InfraLogger.Log("MetricsDatabase: Initialized successfully");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MetricsDatabase: Initialization failed: {ex.Message}");
            throw;
        }
    }

    public async Task InsertEventAsync(TelemetryEvent evt)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                INSERT INTO telemetry_events 
                    (category, event_type, timestamp, session_id, data_json,
                     widget_name, query_text, project_number, project_type,
                     discipline, file_extension, result_count, result_index,
                     duration_ms, char_count, query_source, synced)
                VALUES 
                    (@category, @eventType, @timestamp, @sessionId, @dataJson,
                     @widgetName, @queryText, @projectNumber, @projectType,
                     @discipline, @fileExtension, @resultCount, @resultIndex,
                     @durationMs, @charCount, @querySource, 0)
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@category", evt.Category);
            command.Parameters.AddWithValue("@eventType", evt.EventType);
            command.Parameters.AddWithValue("@timestamp", evt.Timestamp.ToString("O"));
            command.Parameters.AddWithValue("@sessionId", (object?)evt.SessionId ?? DBNull.Value);
            command.Parameters.AddWithValue("@dataJson", (object?)evt.DataJson ?? DBNull.Value);
            command.Parameters.AddWithValue("@widgetName", (object?)evt.WidgetName ?? DBNull.Value);
            command.Parameters.AddWithValue("@queryText", (object?)evt.QueryText ?? DBNull.Value);
            command.Parameters.AddWithValue("@projectNumber", (object?)evt.ProjectNumber ?? DBNull.Value);
            command.Parameters.AddWithValue("@projectType", (object?)evt.ProjectType ?? DBNull.Value);
            command.Parameters.AddWithValue("@discipline", (object?)evt.Discipline ?? DBNull.Value);
            command.Parameters.AddWithValue("@fileExtension", (object?)evt.FileExtension ?? DBNull.Value);
            command.Parameters.AddWithValue("@resultCount", (object?)evt.ResultCount ?? DBNull.Value);
            command.Parameters.AddWithValue("@resultIndex", (object?)evt.ResultIndex ?? DBNull.Value);
            command.Parameters.AddWithValue("@durationMs", (object?)evt.DurationMs ?? DBNull.Value);
            command.Parameters.AddWithValue("@charCount", (object?)evt.CharCount ?? DBNull.Value);
            command.Parameters.AddWithValue("@querySource", (object?)evt.QuerySource ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MetricsDatabase: Failed to insert event: {ex.Message}");
        }
    }

    public async Task InsertEventsBatchAsync(List<TelemetryEvent> events)
    {
        if (events.Count == 0) return;

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                var sql = @"
                    INSERT INTO telemetry_events 
                        (category, event_type, timestamp, session_id, data_json,
                         widget_name, query_text, project_number, project_type,
                         discipline, file_extension, result_count, result_index,
                         duration_ms, char_count, query_source, synced)
                    VALUES 
                        (@category, @eventType, @timestamp, @sessionId, @dataJson,
                         @widgetName, @queryText, @projectNumber, @projectType,
                         @discipline, @fileExtension, @resultCount, @resultIndex,
                         @durationMs, @charCount, @querySource, 0)
                ";

                foreach (var evt in events)
                {
                    using var command = new SqliteCommand(sql, connection, transaction);
                    command.Parameters.AddWithValue("@category", evt.Category);
                    command.Parameters.AddWithValue("@eventType", evt.EventType);
                    command.Parameters.AddWithValue("@timestamp", evt.Timestamp.ToString("O"));
                    command.Parameters.AddWithValue("@sessionId", (object?)evt.SessionId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@dataJson", (object?)evt.DataJson ?? DBNull.Value);
                    command.Parameters.AddWithValue("@widgetName", (object?)evt.WidgetName ?? DBNull.Value);
                    command.Parameters.AddWithValue("@queryText", (object?)evt.QueryText ?? DBNull.Value);
                    command.Parameters.AddWithValue("@projectNumber", (object?)evt.ProjectNumber ?? DBNull.Value);
                    command.Parameters.AddWithValue("@projectType", (object?)evt.ProjectType ?? DBNull.Value);
                    command.Parameters.AddWithValue("@discipline", (object?)evt.Discipline ?? DBNull.Value);
                    command.Parameters.AddWithValue("@fileExtension", (object?)evt.FileExtension ?? DBNull.Value);
                    command.Parameters.AddWithValue("@resultCount", (object?)evt.ResultCount ?? DBNull.Value);
                    command.Parameters.AddWithValue("@resultIndex", (object?)evt.ResultIndex ?? DBNull.Value);
                    command.Parameters.AddWithValue("@durationMs", (object?)evt.DurationMs ?? DBNull.Value);
                    command.Parameters.AddWithValue("@charCount", (object?)evt.CharCount ?? DBNull.Value);
                    command.Parameters.AddWithValue("@querySource", (object?)evt.QuerySource ?? DBNull.Value);

                    await command.ExecuteNonQueryAsync();
                }

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MetricsDatabase: Failed to batch insert events: {ex.Message}");
        }
    }

    public async Task<List<TelemetryEvent>> GetEventsAsync(
        string? category = null,
        string? eventType = null,
        DateTime? from = null,
        DateTime? to = null,
        int limit = 1000)
    {
        var results = new List<TelemetryEvent>();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var conditions = new List<string>();
            var parameters = new List<SqliteParameter>();

            if (category != null)
            {
                conditions.Add("category = @category");
                parameters.Add(new SqliteParameter("@category", category));
            }
            if (eventType != null)
            {
                conditions.Add("event_type = @eventType");
                parameters.Add(new SqliteParameter("@eventType", eventType));
            }
            if (from != null)
            {
                conditions.Add("timestamp >= @from");
                parameters.Add(new SqliteParameter("@from", from.Value.ToString("O")));
            }
            if (to != null)
            {
                conditions.Add("timestamp <= @to");
                parameters.Add(new SqliteParameter("@to", to.Value.ToString("O")));
            }

            var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            var sql = $"SELECT * FROM telemetry_events {where} ORDER BY timestamp DESC LIMIT @limit";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@limit", limit);
            foreach (var p in parameters)
                command.Parameters.Add(p);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(ReadEvent(reader));
            }
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MetricsDatabase: Failed to query events: {ex.Message}");
        }

        return results;
    }

    public async Task<Dictionary<string, int>> GetEventCountsByTypeAsync(DateTime from, DateTime to)
    {
        var counts = new Dictionary<string, int>();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT event_type, COUNT(*) as cnt
                FROM telemetry_events
                WHERE timestamp >= @from AND timestamp <= @to
                GROUP BY event_type
                ORDER BY cnt DESC
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@from", from.ToString("O"));
            command.Parameters.AddWithValue("@to", to.ToString("O"));

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                counts[reader.GetString(0)] = reader.GetInt32(1);
            }
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MetricsDatabase: Failed to get event counts: {ex.Message}");
        }

        return counts;
    }

    public async Task<DailyMetricsSummary?> GetDailySummaryAsync(DateTime date)
    {
        try
        {
            var dayStart = date.Date.ToString("O");
            var dayEnd = date.Date.AddDays(1).ToString("O");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Get all counts in one query
            var sql = @"
                SELECT event_type, COUNT(*) as cnt, 
                       SUM(COALESCE(duration_ms, 0)) as total_duration
                FROM telemetry_events
                WHERE timestamp >= @from AND timestamp < @to
                GROUP BY event_type
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@from", dayStart);
            command.Parameters.AddWithValue("@to", dayEnd);

            var summary = new DailyMetricsSummary { Date = date.Date.ToString("yyyy-MM-dd") };
            var eventCounts = new Dictionary<string, int>();
            var eventDurations = new Dictionary<string, long>();

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var type = reader.GetString(0);
                var count = reader.GetInt32(1);
                var duration = reader.GetInt64(2);
                eventCounts[type] = count;
                eventDurations[type] = duration;
            }

            summary.SessionCount = eventCounts.GetValueOrDefault(TelemetryEventType.SessionStart, 0);
            summary.TotalSessionDurationMs = eventDurations.GetValueOrDefault(TelemetryEventType.SessionEnd, 0);
            summary.TotalSearches = eventCounts.GetValueOrDefault(TelemetryEventType.SearchExecuted, 0);
            summary.TotalSmartSearches = eventCounts.GetValueOrDefault(TelemetryEventType.SmartSearchExecuted, 0);
            summary.TotalDocSearches = eventCounts.GetValueOrDefault(TelemetryEventType.DocSearchExecuted, 0);
            summary.TotalPathSearches = eventCounts.GetValueOrDefault(TelemetryEventType.PathSearchExecuted, 0);
            summary.TotalProjectLaunches = eventCounts.GetValueOrDefault(TelemetryEventType.SearchProjectLaunched, 0)
                + eventCounts.GetValueOrDefault(TelemetryEventType.FrequentProjectOpened, 0);
            summary.TotalQuickLaunchUses = eventCounts.GetValueOrDefault(TelemetryEventType.QuickLaunchItemLaunched, 0);
            summary.TotalQuickLaunchAdds = eventCounts.GetValueOrDefault(TelemetryEventType.QuickLaunchItemAdded, 0);
            summary.TotalQuickLaunchRemoves = eventCounts.GetValueOrDefault(TelemetryEventType.QuickLaunchItemRemoved, 0);
            summary.TotalTasksCreated = eventCounts.GetValueOrDefault(TelemetryEventType.TaskCreated, 0);
            summary.TotalTasksCompleted = eventCounts.GetValueOrDefault(TelemetryEventType.TaskCompleted, 0);
            summary.TotalDocOpens = eventCounts.GetValueOrDefault(TelemetryEventType.DocOpened, 0);
            summary.TotalTimerUses = eventCounts.GetValueOrDefault(TelemetryEventType.TimerStarted, 0);
            summary.TotalCheatSheetViews = eventCounts.GetValueOrDefault(TelemetryEventType.CheatSheetViewed, 0);

            // Widget usage counts
            summary.WidgetUsageCounts = await GetWidgetUsageCountsAsync(connection, dayStart, dayEnd);

            // Project type frequency
            summary.ProjectTypeFrequency = await GetDimensionFrequencyAsync(connection, "project_type", dayStart, dayEnd);

            // Discipline frequency
            summary.DisciplineFrequency = await GetDimensionFrequencyAsync(connection, "discipline", dayStart, dayEnd);

            // Top search queries
            summary.TopSearchQueries = await GetTopSearchQueriesAsync(connection, dayStart, dayEnd, 20);

            // Device/user identity
            summary.DeviceName = Environment.MachineName;
            summary.UserName = Environment.UserName;

            return summary;
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MetricsDatabase: Failed to get daily summary: {ex.Message}");
            return null;
        }
    }

    public async Task<List<TelemetryEvent>> GetRecentSearchEventsAsync(int count = 100)
    {
        return await GetEventsAsync(
            category: TelemetryCategory.Search,
            limit: count);
    }

    public async Task<List<string>> GetUnsyncedDatesAsync()
    {
        var dates = new List<string>();

        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT DISTINCT substr(timestamp, 1, 10) as event_date
                FROM telemetry_events
                WHERE synced = 0
                ORDER BY event_date
            ";

            using var command = new SqliteCommand(sql, connection);
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                dates.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MetricsDatabase: Failed to get unsynced dates: {ex.Message}");
        }

        return dates;
    }

    public async Task MarkEventsSyncedAsync(DateTime from, DateTime to)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                UPDATE telemetry_events 
                SET synced = 1 
                WHERE timestamp >= @from AND timestamp < @to
            ";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@from", from.ToString("O"));
            command.Parameters.AddWithValue("@to", to.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MetricsDatabase: Failed to mark events synced: {ex.Message}");
        }
    }

    public async Task PurgeOldEventsAsync(int retainDays = 90)
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var cutoff = DateTime.UtcNow.AddDays(-retainDays).ToString("O");
            var sql = "DELETE FROM telemetry_events WHERE timestamp < @cutoff AND synced = 1";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@cutoff", cutoff);
            var deleted = await command.ExecuteNonQueryAsync();

            if (deleted > 0)
                InfraLogger.Log($"MetricsDatabase: Purged {deleted} old events");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"MetricsDatabase: Failed to purge old events: {ex.Message}");
        }
    }

    public async Task<long> GetTotalEventCountAsync()
    {
        try
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = new SqliteCommand("SELECT COUNT(*) FROM telemetry_events", connection);
            return Convert.ToInt64(await command.ExecuteScalarAsync());
        }
        catch
        {
            return 0;
        }
    }

    private async Task<Dictionary<string, int>> GetWidgetUsageCountsAsync(
        SqliteConnection connection, string dayStart, string dayEnd)
    {
        var counts = new Dictionary<string, int>();

        var sql = @"
            SELECT widget_name, COUNT(*) as cnt
            FROM telemetry_events
            WHERE timestamp >= @from AND timestamp < @to
              AND widget_name IS NOT NULL
            GROUP BY widget_name
            ORDER BY cnt DESC
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@from", dayStart);
        command.Parameters.AddWithValue("@to", dayEnd);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            counts[reader.GetString(0)] = reader.GetInt32(1);
        }

        return counts;
    }

    private async Task<Dictionary<string, int>> GetDimensionFrequencyAsync(
        SqliteConnection connection, string column, string dayStart, string dayEnd)
    {
        var freq = new Dictionary<string, int>();

        var sql = $@"
            SELECT {column}, COUNT(*) as cnt
            FROM telemetry_events
            WHERE timestamp >= @from AND timestamp < @to
              AND {column} IS NOT NULL
            GROUP BY {column}
            ORDER BY cnt DESC
            LIMIT 50
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@from", dayStart);
        command.Parameters.AddWithValue("@to", dayEnd);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            freq[reader.GetString(0)] = reader.GetInt32(1);
        }

        return freq;
    }

    private async Task<List<SearchQueryCount>> GetTopSearchQueriesAsync(
        SqliteConnection connection, string dayStart, string dayEnd, int limit)
    {
        var queries = new List<SearchQueryCount>();

        var sql = @"
            SELECT query_text, COUNT(*) as cnt
            FROM telemetry_events
            WHERE timestamp >= @from AND timestamp < @to
              AND query_text IS NOT NULL AND query_text != ''
              AND category = 'search'
            GROUP BY query_text
            ORDER BY cnt DESC
            LIMIT @limit
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@from", dayStart);
        command.Parameters.AddWithValue("@to", dayEnd);
        command.Parameters.AddWithValue("@limit", limit);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            queries.Add(new SearchQueryCount
            {
                Query = reader.GetString(0),
                Count = reader.GetInt32(1)
            });
        }

        // Enrich with source breakdown per query
        foreach (var q in queries)
        {
            var srcSql = @"
                SELECT COALESCE(query_source, 'unknown') as src, COUNT(*) as cnt
                FROM telemetry_events
                WHERE timestamp >= @from AND timestamp < @to
                  AND query_text = @query AND category = 'search'
                GROUP BY src ORDER BY cnt DESC
            ";
            using var srcCmd = new SqliteCommand(srcSql, connection);
            srcCmd.Parameters.AddWithValue("@from", dayStart);
            srcCmd.Parameters.AddWithValue("@to", dayEnd);
            srcCmd.Parameters.AddWithValue("@query", q.Query);
            using var srcReader = await srcCmd.ExecuteReaderAsync();
            while (await srcReader.ReadAsync())
            {
                var src = srcReader.GetString(0);
                var cnt = srcReader.GetInt32(1);
                q.SourceBreakdown[src] = cnt;
                if (string.IsNullOrEmpty(q.PrimarySource))
                    q.PrimarySource = src;
            }
        }

        return queries;
    }

    public async Task<HourlyBreakdown> GetHourlyBreakdownAsync(DateTime date)
    {
        var breakdown = new HourlyBreakdown();
        try
        {
            var dayStart = date.Date.ToString("O");
            var dayEnd = date.Date.AddDays(1).ToString("O");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT CAST(substr(timestamp, 12, 2) AS INTEGER) as hour, COUNT(*) as cnt
                FROM telemetry_events
                WHERE timestamp >= @from AND timestamp < @to
                GROUP BY hour ORDER BY hour
            ";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@from", dayStart);
            cmd.Parameters.AddWithValue("@to", dayEnd);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var hour = reader.GetInt32(0);
                var cnt = reader.GetInt32(1);
                if (hour >= 0 && hour < 24) breakdown.EventCounts[hour] = cnt;
            }

            int peakH = 0, peakC = 0;
            for (int i = 0; i < 24; i++)
            {
                if (breakdown.EventCounts[i] > peakC) { peakC = breakdown.EventCounts[i]; peakH = i; }
            }
            breakdown.PeakHour = peakH;
            breakdown.PeakCount = peakC;
        }
        catch (Exception ex) { InfraLogger.Log($"MetricsDatabase: GetHourlyBreakdownAsync failed: {ex.Message}"); }
        return breakdown;
    }

    public async Task<List<SessionDetail>> GetSessionDetailsAsync(DateTime date)
    {
        var sessions = new List<SessionDetail>();
        try
        {
            var dayStart = date.Date.ToString("O");
            var dayEnd = date.Date.AddDays(1).ToString("O");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT session_id,
                       MIN(timestamp) as first_event,
                       MAX(timestamp) as last_event,
                       COUNT(*) as event_count,
                       SUM(COALESCE(duration_ms, 0)) as total_dur
                FROM telemetry_events
                WHERE timestamp >= @from AND timestamp < @to
                  AND session_id IS NOT NULL
                GROUP BY session_id
                ORDER BY first_event
            ";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@from", dayStart);
            cmd.Parameters.AddWithValue("@to", dayEnd);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var sid = reader.GetString(0);
                var start = DateTime.Parse(reader.GetString(1));
                var end = DateTime.Parse(reader.GetString(2));
                sessions.Add(new SessionDetail
                {
                    SessionId = sid,
                    StartTime = start,
                    EndTime = end,
                    EventCount = reader.GetInt32(3),
                    DurationMs = reader.GetInt64(4)
                });
            }

            // Enrich with action breakdown per session
            foreach (var sess in sessions)
            {
                var abSql = @"
                    SELECT category, COUNT(*) as cnt
                    FROM telemetry_events
                    WHERE session_id = @sid AND timestamp >= @from AND timestamp < @to
                    GROUP BY category ORDER BY cnt DESC
                ";
                using var abCmd = new SqliteCommand(abSql, connection);
                abCmd.Parameters.AddWithValue("@sid", sess.SessionId);
                abCmd.Parameters.AddWithValue("@from", dayStart);
                abCmd.Parameters.AddWithValue("@to", dayEnd);
                using var abReader = await abCmd.ExecuteReaderAsync();
                while (await abReader.ReadAsync())
                    sess.ActionBreakdown[abReader.GetString(0)] = abReader.GetInt32(1);
            }
        }
        catch (Exception ex) { InfraLogger.Log($"MetricsDatabase: GetSessionDetailsAsync failed: {ex.Message}"); }
        return sessions;
    }

    public async Task<List<TopProjectInfo>> GetTopProjectsAsync(DateTime from, DateTime to, int limit = 10)
    {
        var projects = new List<TopProjectInfo>();
        try
        {
            var fromStr = from.Date.ToString("O");
            var toStr = to.Date.AddDays(1).ToString("O");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sql = @"
                SELECT project_number, project_type,
                       SUM(CASE WHEN event_type IN ('search_project_launched','frequent_project_opened') THEN 1 ELSE 0 END) as launches,
                       SUM(CASE WHEN category = 'search' THEN 1 ELSE 0 END) as searches,
                       SUM(CASE WHEN event_type = 'doc_opened' THEN 1 ELSE 0 END) as doc_opens
                FROM telemetry_events
                WHERE timestamp >= @from AND timestamp < @to
                  AND project_number IS NOT NULL AND project_number != ''
                GROUP BY project_number
                ORDER BY (launches + searches + doc_opens) DESC
                LIMIT @limit
            ";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@from", fromStr);
            cmd.Parameters.AddWithValue("@to", toStr);
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                projects.Add(new TopProjectInfo
                {
                    ProjectNumber = reader.GetString(0),
                    ProjectType = reader.IsDBNull(1) ? null : reader.GetString(1),
                    LaunchCount = reader.GetInt32(2),
                    SearchCount = reader.GetInt32(3),
                    DocOpenCount = reader.GetInt32(4)
                });
            }
        }
        catch (Exception ex) { InfraLogger.Log($"MetricsDatabase: GetTopProjectsAsync failed: {ex.Message}"); }
        return projects;
    }

    public async Task<List<DailyMetricsSummary>> GetMultiDaySummariesAsync(DateTime from, DateTime to)
    {
        var results = new List<DailyMetricsSummary>();
        try
        {
            for (var date = from.Date; date <= to.Date; date = date.AddDays(1))
            {
                var summary = await GetDailySummaryAsync(date);
                if (summary != null) results.Add(summary);
            }
        }
        catch (Exception ex) { InfraLogger.Log($"MetricsDatabase: GetMultiDaySummariesAsync failed: {ex.Message}"); }
        return results;
    }

    public async Task<List<FeatureTransition>> GetFeatureTransitionsAsync(DateTime from, DateTime to, int limit = 50)
    {
        var transitions = new List<FeatureTransition>();
        try
        {
            var fromStr = from.Date.ToString("O");
            var toStr = to.Date.AddDays(1).ToString("O");

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Get sequential category pairs within same session
            var sql = @"
                WITH ordered AS (
                    SELECT session_id, category,
                           LAG(category) OVER (PARTITION BY session_id ORDER BY timestamp) as prev_category
                    FROM telemetry_events
                    WHERE timestamp >= @from AND timestamp < @to
                      AND session_id IS NOT NULL
                )
                SELECT prev_category, category, COUNT(*) as cnt
                FROM ordered
                WHERE prev_category IS NOT NULL AND prev_category != category
                GROUP BY prev_category, category
                ORDER BY cnt DESC
                LIMIT @limit
            ";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@from", fromStr);
            cmd.Parameters.AddWithValue("@to", toStr);
            cmd.Parameters.AddWithValue("@limit", limit);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                transitions.Add(new FeatureTransition
                {
                    From = reader.GetString(0),
                    To = reader.GetString(1),
                    Count = reader.GetInt32(2)
                });
            }
        }
        catch (Exception ex) { InfraLogger.Log($"MetricsDatabase: GetFeatureTransitionsAsync failed: {ex.Message}"); }
        return transitions;
    }

    private static TelemetryEvent ReadEvent(SqliteDataReader reader)
    {
        return new TelemetryEvent
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            Category = reader.GetString(reader.GetOrdinal("category")),
            EventType = reader.GetString(reader.GetOrdinal("event_type")),
            Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("timestamp"))),
            SessionId = reader.IsDBNull(reader.GetOrdinal("session_id")) ? null : reader.GetString(reader.GetOrdinal("session_id")),
            DataJson = reader.IsDBNull(reader.GetOrdinal("data_json")) ? null : reader.GetString(reader.GetOrdinal("data_json")),
            WidgetName = reader.IsDBNull(reader.GetOrdinal("widget_name")) ? null : reader.GetString(reader.GetOrdinal("widget_name")),
            QueryText = reader.IsDBNull(reader.GetOrdinal("query_text")) ? null : reader.GetString(reader.GetOrdinal("query_text")),
            ProjectNumber = reader.IsDBNull(reader.GetOrdinal("project_number")) ? null : reader.GetString(reader.GetOrdinal("project_number")),
            ProjectType = reader.IsDBNull(reader.GetOrdinal("project_type")) ? null : reader.GetString(reader.GetOrdinal("project_type")),
            Discipline = reader.IsDBNull(reader.GetOrdinal("discipline")) ? null : reader.GetString(reader.GetOrdinal("discipline")),
            FileExtension = reader.IsDBNull(reader.GetOrdinal("file_extension")) ? null : reader.GetString(reader.GetOrdinal("file_extension")),
            ResultCount = reader.IsDBNull(reader.GetOrdinal("result_count")) ? null : reader.GetInt32(reader.GetOrdinal("result_count")),
            ResultIndex = reader.IsDBNull(reader.GetOrdinal("result_index")) ? null : reader.GetInt32(reader.GetOrdinal("result_index")),
            DurationMs = reader.IsDBNull(reader.GetOrdinal("duration_ms")) ? null : reader.GetInt64(reader.GetOrdinal("duration_ms")),
            CharCount = reader.IsDBNull(reader.GetOrdinal("char_count")) ? null : reader.GetInt32(reader.GetOrdinal("char_count")),
            QuerySource = reader.IsDBNull(reader.GetOrdinal("query_source")) ? null : reader.GetString(reader.GetOrdinal("query_source")),
            Synced = reader.GetInt32(reader.GetOrdinal("synced")) == 1
        };
    }
}
