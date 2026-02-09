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
                notes TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_tasks_date ON tasks(date);
            CREATE INDEX IF NOT EXISTS idx_tasks_date_sort ON tasks(date, sort_order);
            CREATE INDEX IF NOT EXISTS idx_tasks_title ON tasks(title);
        ";

        using var command = new SqliteCommand(createTablesSql, connection);
        await command.ExecuteNonQueryAsync();
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
            INSERT INTO tasks (id, date, title, is_completed, priority, sort_order, created_at, completed_at, category, notes)
            VALUES (@id, @date, @title, @isCompleted, @priority, @sortOrder, @createdAt, @completedAt, @category, @notes)
            ON CONFLICT(id) DO UPDATE SET
                date = @date,
                title = @title,
                is_completed = @isCompleted,
                priority = @priority,
                sort_order = @sortOrder,
                completed_at = @completedAt,
                category = @category,
                notes = @notes
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

    private static TaskItem ReadTaskFromReader(SqliteDataReader reader)
    {
        return new TaskItem
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
    }
}
