using Microsoft.Data.Sqlite;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;

namespace DesktopHub.Infrastructure.Data;

/// <summary>
/// SQLite persistence for project launch frequency tracking.
/// Uses the same database as the main projects store.
/// </summary>
public class ProjectLaunchDataStore : IProjectLaunchDataStore
{
    private readonly string _connectionString;

    public ProjectLaunchDataStore(string? dataDirectory = null)
    {
        dataDirectory ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopHub"
        );

        Directory.CreateDirectory(dataDirectory);
        var dbPath = Path.Combine(dataDirectory, "projects.db");
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task InitializeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            CREATE TABLE IF NOT EXISTS project_launches (
                path TEXT PRIMARY KEY,
                full_number TEXT NOT NULL,
                name TEXT NOT NULL,
                launch_count INTEGER NOT NULL DEFAULT 0,
                last_launched TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_launches_count ON project_launches(launch_count DESC);
        ";

        using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task RecordLaunchAsync(string path, string fullNumber, string name)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO project_launches (path, full_number, name, launch_count, last_launched)
            VALUES (@path, @fullNumber, @name, 1, @now)
            ON CONFLICT(path) DO UPDATE SET
                full_number = @fullNumber,
                name = @name,
                launch_count = launch_count + 1,
                last_launched = @now
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@path", path);
        command.Parameters.AddWithValue("@fullNumber", fullNumber);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@now", DateTime.Now.ToString("O"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<ProjectLaunchRecord>> GetTopProjectsAsync(int count = 5)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT path, full_number, name, launch_count, last_launched FROM project_launches ORDER BY launch_count DESC, last_launched DESC LIMIT @count";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@count", count);

        var results = new List<ProjectLaunchRecord>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadRecord(reader));
        }
        return results;
    }

    public async Task<List<ProjectLaunchRecord>> GetAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT path, full_number, name, launch_count, last_launched FROM project_launches ORDER BY launch_count DESC";

        using var command = new SqliteCommand(sql, connection);
        var results = new List<ProjectLaunchRecord>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadRecord(reader));
        }
        return results;
    }

    public async Task ClearAllAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var command = new SqliteCommand("DELETE FROM project_launches", connection);
        await command.ExecuteNonQueryAsync();
    }

    private static ProjectLaunchRecord ReadRecord(SqliteDataReader reader)
    {
        return new ProjectLaunchRecord
        {
            Path = reader.GetString(reader.GetOrdinal("path")),
            FullNumber = reader.GetString(reader.GetOrdinal("full_number")),
            Name = reader.GetString(reader.GetOrdinal("name")),
            LaunchCount = reader.GetInt32(reader.GetOrdinal("launch_count")),
            LastLaunched = DateTime.Parse(reader.GetString(reader.GetOrdinal("last_launched")))
        };
    }
}
