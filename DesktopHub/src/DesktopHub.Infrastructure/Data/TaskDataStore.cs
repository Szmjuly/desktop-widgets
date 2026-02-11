using Microsoft.Data.Sqlite;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;

namespace DesktopHub.Infrastructure.Data;

/// <summary>
/// SQLite implementation of task data store.
/// Database lives in %USERPROFILE%\Documents\DesktopHub\QuickTasks\
/// </summary>
public class TaskDataStore : ITaskDataStore
{
    private readonly string _connectionString;
    private const string DatabaseFileName = "quicktasks.db";

    public TaskDataStore(string? dataDirectory = null)
    {
        dataDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DesktopHub",
            "QuickTasks"
        );

        Directory.CreateDirectory(dataDirectory);
        var dbPath = Path.Combine(dataDirectory, DatabaseFileName);
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var createTablesSql = @"
            CREATE TABLE IF NOT EXISTS tasks (
                id TEXT PRIMARY KEY,
                date TEXT NOT NULL,
                title TEXT NOT NULL,
                is_completed INTEGER NOT NULL DEFAULT 0,
                priority TEXT NOT NULL DEFAULT 'normal',
                sort_order INTEGER NOT NULL DEFAULT 0,
                created_at TEXT NOT NULL,
                completed_at TEXT,
                category TEXT,
                notes TEXT,
                carried_from_task_id TEXT,
                carried_from_date TEXT,
                is_carried_over INTEGER NOT NULL DEFAULT 0
            );

            CREATE INDEX IF NOT EXISTS idx_tasks_date ON tasks(date);
            CREATE INDEX IF NOT EXISTS idx_tasks_date_sort ON tasks(date, sort_order);
            CREATE INDEX IF NOT EXISTS idx_tasks_title ON tasks(title);
        ";

        using var command = new SqliteCommand(createTablesSql, connection);
        await command.ExecuteNonQueryAsync();

        // Migrate existing databases: add carry-over columns if missing
        await MigrateCarryOverColumnsAsync(connection);
    }

    public async Task<List<TaskItem>> GetTasksByDateAsync(string date)
    {
        var tasks = new List<TaskItem>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT * FROM tasks WHERE date = @date ORDER BY sort_order, created_at";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@date", date);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tasks.Add(ReadTaskFromReader(reader));
        }

        return tasks;
    }

    public async Task<List<TaskItem>> GetTasksByDateRangeAsync(string startDate, string endDate)
    {
        var tasks = new List<TaskItem>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT * FROM tasks WHERE date >= @startDate AND date <= @endDate ORDER BY date DESC, sort_order, created_at";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@startDate", startDate);
        command.Parameters.AddWithValue("@endDate", endDate);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tasks.Add(ReadTaskFromReader(reader));
        }

        return tasks;
    }

    public async Task<List<string>> GetRecentTaskDatesAsync(int count)
    {
        var dates = new List<string>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT DISTINCT date FROM tasks ORDER BY date DESC LIMIT @count";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@count", count);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            dates.Add(reader.GetString(0));
        }

        return dates;
    }

    public async Task UpsertTaskAsync(TaskItem task)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO tasks (id, date, title, is_completed, priority, sort_order, created_at, completed_at, category, notes, carried_from_task_id, carried_from_date, is_carried_over)
            VALUES (@id, @date, @title, @isCompleted, @priority, @sortOrder, @createdAt, @completedAt, @category, @notes, @carriedFromTaskId, @carriedFromDate, @isCarriedOver)
            ON CONFLICT(id) DO UPDATE SET
                date = @date,
                title = @title,
                is_completed = @isCompleted,
                priority = @priority,
                sort_order = @sortOrder,
                completed_at = @completedAt,
                category = @category,
                notes = @notes,
                carried_from_task_id = @carriedFromTaskId,
                carried_from_date = @carriedFromDate,
                is_carried_over = @isCarriedOver
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", task.Id);
        command.Parameters.AddWithValue("@date", task.Date);
        command.Parameters.AddWithValue("@title", task.Title);
        command.Parameters.AddWithValue("@isCompleted", task.IsCompleted ? 1 : 0);
        command.Parameters.AddWithValue("@priority", task.Priority);
        command.Parameters.AddWithValue("@sortOrder", task.SortOrder);
        command.Parameters.AddWithValue("@createdAt", task.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("@completedAt", task.CompletedAt?.ToString("O") ?? (object)DBNull.Value);
        command.Parameters.AddWithValue("@category", (object?)task.Category ?? DBNull.Value);
        command.Parameters.AddWithValue("@notes", (object?)task.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("@carriedFromTaskId", (object?)task.CarriedFromTaskId ?? DBNull.Value);
        command.Parameters.AddWithValue("@carriedFromDate", (object?)task.CarriedFromDate ?? DBNull.Value);
        command.Parameters.AddWithValue("@isCarriedOver", task.IsCarriedOver ? 1 : 0);

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteTaskAsync(string taskId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "DELETE FROM tasks WHERE id = @id";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", taskId);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<TaskItem>> SearchTasksAsync(string query, int limit = 50)
    {
        var tasks = new List<TaskItem>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT * FROM tasks WHERE title LIKE @query OR category LIKE @query OR notes LIKE @query ORDER BY date DESC, sort_order LIMIT @limit";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@query", $"%{query}%");
        command.Parameters.AddWithValue("@limit", limit);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tasks.Add(ReadTaskFromReader(reader));
        }

        return tasks;
    }

    public async Task<(int active, int completed)> GetTaskCountsAsync(string date)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT 
                SUM(CASE WHEN is_completed = 0 THEN 1 ELSE 0 END) as active,
                SUM(CASE WHEN is_completed = 1 THEN 1 ELSE 0 END) as completed
            FROM tasks WHERE date = @date
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@date", date);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var active = reader.IsDBNull(0) ? 0 : reader.GetInt32(0);
            var completed = reader.IsDBNull(1) ? 0 : reader.GetInt32(1);
            return (active, completed);
        }

        return (0, 0);
    }

    public async Task<List<TaskItem>> GetIncompleteTasksAsync(string date)
    {
        var tasks = new List<TaskItem>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT * FROM tasks WHERE date = @date AND is_completed = 0 ORDER BY sort_order";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@date", date);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tasks.Add(ReadTaskFromReader(reader));
        }

        return tasks;
    }

    public async Task<int> GetNextSortOrderAsync(string date)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT COALESCE(MAX(sort_order), -1) + 1 FROM tasks WHERE date = @date";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@date", date);

        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }

    public async Task<TaskItem?> GetTaskByIdAsync(string taskId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT * FROM tasks WHERE id = @id";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", taskId);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadTaskFromReader(reader);
        }
        return null;
    }

    public async Task<List<TaskItem>> GetAllIncompleteOriginalTasksBeforeDateAsync(string beforeDate)
    {
        var tasks = new List<TaskItem>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"SELECT * FROM tasks 
                    WHERE date < @beforeDate 
                      AND is_completed = 0 
                      AND (carried_from_task_id IS NULL OR carried_from_task_id = '')
                    ORDER BY date DESC, sort_order";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@beforeDate", beforeDate);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tasks.Add(ReadTaskFromReader(reader));
        }

        return tasks;
    }

    public async Task<List<TaskItem>> GetCarriedOverCopiesOnDateAsync(string date)
    {
        var tasks = new List<TaskItem>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"SELECT * FROM tasks 
                    WHERE date = @date 
                      AND carried_from_task_id IS NOT NULL 
                      AND carried_from_task_id != ''
                    ORDER BY sort_order";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@date", date);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tasks.Add(ReadTaskFromReader(reader));
        }

        return tasks;
    }

    public async Task<List<string>> DeleteIncompleteCarriedOverCopiesAsync()
    {
        var originalTaskIds = new List<string>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // First, get the original task IDs for copies we're about to delete
        var selectSql = @"SELECT carried_from_task_id FROM tasks 
                          WHERE carried_from_task_id IS NOT NULL 
                            AND carried_from_task_id != '' 
                            AND is_completed = 0";
        using (var selectCmd = new SqliteCommand(selectSql, connection))
        using (var reader = await selectCmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                originalTaskIds.Add(reader.GetString(0));
            }
        }

        // Delete incomplete carry-over copies
        var deleteSql = @"DELETE FROM tasks 
                          WHERE carried_from_task_id IS NOT NULL 
                            AND carried_from_task_id != '' 
                            AND is_completed = 0";
        using (var deleteCmd = new SqliteCommand(deleteSql, connection))
        {
            await deleteCmd.ExecuteNonQueryAsync();
        }

        // Un-mark originals as carried over
        foreach (var origId in originalTaskIds)
        {
            // Only un-mark if there are no other carry-over copies remaining for this original
            var checkSql = @"SELECT COUNT(*) FROM tasks 
                             WHERE carried_from_task_id = @origId";
            using var checkCmd = new SqliteCommand(checkSql, connection);
            checkCmd.Parameters.AddWithValue("@origId", origId);
            var remaining = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());

            if (remaining == 0)
            {
                var updateSql = "UPDATE tasks SET is_carried_over = 0 WHERE id = @id";
                using var updateCmd = new SqliteCommand(updateSql, connection);
                updateCmd.Parameters.AddWithValue("@id", origId);
                await updateCmd.ExecuteNonQueryAsync();
            }
        }

        return originalTaskIds;
    }

    private async Task MigrateCarryOverColumnsAsync(SqliteConnection connection)
    {
        // Check if columns exist by querying table_info
        var pragmaSql = "PRAGMA table_info(tasks)";
        using var pragmaCmd = new SqliteCommand(pragmaSql, connection);
        using var reader = await pragmaCmd.ExecuteReaderAsync();

        var columns = new HashSet<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1)); // column name is at index 1
        }
        reader.Close();

        if (!columns.Contains("carried_from_task_id"))
        {
            using var cmd = new SqliteCommand("ALTER TABLE tasks ADD COLUMN carried_from_task_id TEXT", connection);
            await cmd.ExecuteNonQueryAsync();
        }
        if (!columns.Contains("carried_from_date"))
        {
            using var cmd = new SqliteCommand("ALTER TABLE tasks ADD COLUMN carried_from_date TEXT", connection);
            await cmd.ExecuteNonQueryAsync();
        }
        if (!columns.Contains("is_carried_over"))
        {
            using var cmd = new SqliteCommand("ALTER TABLE tasks ADD COLUMN is_carried_over INTEGER NOT NULL DEFAULT 0", connection);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static TaskItem ReadTaskFromReader(SqliteDataReader reader)
    {
        var task = new TaskItem
        {
            Id = reader.GetString(reader.GetOrdinal("id")),
            Date = reader.GetString(reader.GetOrdinal("date")),
            Title = reader.GetString(reader.GetOrdinal("title")),
            IsCompleted = reader.GetInt32(reader.GetOrdinal("is_completed")) == 1,
            Priority = reader.GetString(reader.GetOrdinal("priority")),
            SortOrder = reader.GetInt32(reader.GetOrdinal("sort_order")),
            CreatedAt = DateTime.Parse(reader.GetString(reader.GetOrdinal("created_at"))),
            CompletedAt = reader.IsDBNull(reader.GetOrdinal("completed_at"))
                ? null
                : DateTime.Parse(reader.GetString(reader.GetOrdinal("completed_at"))),
            Category = reader.IsDBNull(reader.GetOrdinal("category"))
                ? null
                : reader.GetString(reader.GetOrdinal("category")),
            Notes = reader.IsDBNull(reader.GetOrdinal("notes"))
                ? null
                : reader.GetString(reader.GetOrdinal("notes"))
        };

        // Read carry-over fields (may not exist in older DBs before migration runs)
        try
        {
            var carriedFromIdOrd = reader.GetOrdinal("carried_from_task_id");
            task.CarriedFromTaskId = reader.IsDBNull(carriedFromIdOrd) ? null : reader.GetString(carriedFromIdOrd);

            var carriedFromDateOrd = reader.GetOrdinal("carried_from_date");
            task.CarriedFromDate = reader.IsDBNull(carriedFromDateOrd) ? null : reader.GetString(carriedFromDateOrd);

            var isCarriedOverOrd = reader.GetOrdinal("is_carried_over");
            task.IsCarriedOver = reader.GetInt32(isCarriedOverOrd) == 1;
        }
        catch { }

        return task;
    }
}
