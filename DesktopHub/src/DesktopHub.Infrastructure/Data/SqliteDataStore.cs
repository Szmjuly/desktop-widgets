using System.Text.Json;
using Microsoft.Data.Sqlite;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;

namespace DesktopHub.Infrastructure.Data;

/// <summary>
/// SQLite implementation of data store
/// </summary>
public class SqliteDataStore : IDataStore
{
    private readonly string _connectionString;
    private const string DatabaseFileName = "projects.db";

    public SqliteDataStore(string? dataDirectory = null)
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
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var createTablesSql = @"
            CREATE TABLE IF NOT EXISTS projects (
                id TEXT PRIMARY KEY,
                full_number TEXT NOT NULL,
                short_number TEXT NOT NULL,
                name TEXT NOT NULL,
                path TEXT NOT NULL,
                year TEXT NOT NULL,
                last_scanned TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS metadata (
                project_id TEXT PRIMARY KEY,
                location TEXT,
                status TEXT,
                tags TEXT,
                notes TEXT,
                is_favorite INTEGER NOT NULL DEFAULT 0,
                team TEXT,
                last_updated TEXT NOT NULL,
                FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS settings (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_projects_full_number ON projects(full_number);
            CREATE INDEX IF NOT EXISTS idx_projects_short_number ON projects(short_number);
            CREATE INDEX IF NOT EXISTS idx_projects_name ON projects(name);
            CREATE INDEX IF NOT EXISTS idx_projects_year ON projects(year);
            CREATE INDEX IF NOT EXISTS idx_metadata_location ON metadata(location);
            CREATE INDEX IF NOT EXISTS idx_metadata_status ON metadata(status);
        ";

        using var command = new SqliteCommand(createTablesSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        var projects = new List<Project>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT p.*, m.location, m.status, m.tags, m.notes, m.is_favorite, m.team, m.last_updated
            FROM projects p
            LEFT JOIN metadata m ON p.id = m.project_id
        ";

        using var command = new SqliteCommand(sql, connection);
        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var project = new Project
            {
                Id = reader.GetString(0),
                FullNumber = reader.GetString(1),
                ShortNumber = reader.GetString(2),
                Name = reader.GetString(3),
                Path = reader.GetString(4),
                Year = reader.GetString(5),
                LastScanned = DateTime.Parse(reader.GetString(6))
            };

            // Load metadata if exists
            if (!reader.IsDBNull(7))
            {
                project.Metadata = new ProjectMetadata
                {
                    ProjectId = project.Id,
                    Location = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Status = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Tags = reader.IsDBNull(9) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(reader.GetString(9)) ?? new List<string>(),
                    Notes = reader.IsDBNull(10) ? null : reader.GetString(10),
                    IsFavorite = reader.GetInt32(11) == 1,
                    Team = reader.IsDBNull(12) ? null : reader.GetString(12),
                    LastUpdated = reader.IsDBNull(13) ? DateTime.UtcNow : DateTime.Parse(reader.GetString(13))
                };
            }

            projects.Add(project);
        }

        return projects;
    }

    public async Task<Project?> GetProjectByIdAsync(string id)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            SELECT p.*, m.location, m.status, m.tags, m.notes, m.is_favorite, m.team, m.last_updated
            FROM projects p
            LEFT JOIN metadata m ON p.id = m.project_id
            WHERE p.id = @id
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var project = new Project
            {
                Id = reader.GetString(0),
                FullNumber = reader.GetString(1),
                ShortNumber = reader.GetString(2),
                Name = reader.GetString(3),
                Path = reader.GetString(4),
                Year = reader.GetString(5),
                LastScanned = DateTime.Parse(reader.GetString(6))
            };

            if (!reader.IsDBNull(7))
            {
                project.Metadata = new ProjectMetadata
                {
                    ProjectId = project.Id,
                    Location = reader.IsDBNull(7) ? null : reader.GetString(7),
                    Status = reader.IsDBNull(8) ? null : reader.GetString(8),
                    Tags = reader.IsDBNull(9) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(reader.GetString(9)) ?? new List<string>(),
                    Notes = reader.IsDBNull(10) ? null : reader.GetString(10),
                    IsFavorite = reader.GetInt32(11) == 1,
                    Team = reader.IsDBNull(12) ? null : reader.GetString(12),
                    LastUpdated = reader.IsDBNull(13) ? DateTime.UtcNow : DateTime.Parse(reader.GetString(13))
                };
            }

            return project;
        }

        return null;
    }

    public async Task UpsertProjectAsync(Project project)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO projects (id, full_number, short_number, name, path, year, last_scanned)
            VALUES (@id, @fullNumber, @shortNumber, @name, @path, @year, @lastScanned)
            ON CONFLICT(id) DO UPDATE SET
                full_number = @fullNumber,
                short_number = @shortNumber,
                name = @name,
                path = @path,
                year = @year,
                last_scanned = @lastScanned
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", project.Id);
        command.Parameters.AddWithValue("@fullNumber", project.FullNumber);
        command.Parameters.AddWithValue("@shortNumber", project.ShortNumber);
        command.Parameters.AddWithValue("@name", project.Name);
        command.Parameters.AddWithValue("@path", project.Path);
        command.Parameters.AddWithValue("@year", project.Year);
        command.Parameters.AddWithValue("@lastScanned", project.LastScanned.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task BatchUpsertProjectsAsync(List<Project> projects)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();

        try
        {
            var sql = @"
                INSERT INTO projects (id, full_number, short_number, name, path, year, last_scanned)
                VALUES (@id, @fullNumber, @shortNumber, @name, @path, @year, @lastScanned)
                ON CONFLICT(id) DO UPDATE SET
                    full_number = @fullNumber,
                    short_number = @shortNumber,
                    name = @name,
                    path = @path,
                    year = @year,
                    last_scanned = @lastScanned
            ";

            foreach (var project in projects)
            {
                using var command = new SqliteCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("@id", project.Id);
                command.Parameters.AddWithValue("@fullNumber", project.FullNumber);
                command.Parameters.AddWithValue("@shortNumber", project.ShortNumber);
                command.Parameters.AddWithValue("@name", project.Name);
                command.Parameters.AddWithValue("@path", project.Path);
                command.Parameters.AddWithValue("@year", project.Year);
                command.Parameters.AddWithValue("@lastScanned", project.LastScanned.ToString("O"));

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

    public async Task<ProjectMetadata?> GetMetadataAsync(string projectId)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT * FROM metadata WHERE project_id = @projectId";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@projectId", projectId);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ProjectMetadata
            {
                ProjectId = reader.GetString(0),
                Location = reader.IsDBNull(1) ? null : reader.GetString(1),
                Status = reader.IsDBNull(2) ? null : reader.GetString(2),
                Tags = reader.IsDBNull(3) ? new List<string>() : JsonSerializer.Deserialize<List<string>>(reader.GetString(3)) ?? new List<string>(),
                Notes = reader.IsDBNull(4) ? null : reader.GetString(4),
                IsFavorite = reader.GetInt32(5) == 1,
                Team = reader.IsDBNull(6) ? null : reader.GetString(6),
                LastUpdated = DateTime.Parse(reader.GetString(7))
            };
        }

        return null;
    }

    public async Task UpsertMetadataAsync(ProjectMetadata metadata)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO metadata (project_id, location, status, tags, notes, is_favorite, team, last_updated)
            VALUES (@projectId, @location, @status, @tags, @notes, @isFavorite, @team, @lastUpdated)
            ON CONFLICT(project_id) DO UPDATE SET
                location = @location,
                status = @status,
                tags = @tags,
                notes = @notes,
                is_favorite = @isFavorite,
                team = @team,
                last_updated = @lastUpdated
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@projectId", metadata.ProjectId);
        command.Parameters.AddWithValue("@location", (object?)metadata.Location ?? DBNull.Value);
        command.Parameters.AddWithValue("@status", (object?)metadata.Status ?? DBNull.Value);
        command.Parameters.AddWithValue("@tags", JsonSerializer.Serialize(metadata.Tags));
        command.Parameters.AddWithValue("@notes", (object?)metadata.Notes ?? DBNull.Value);
        command.Parameters.AddWithValue("@isFavorite", metadata.IsFavorite ? 1 : 0);
        command.Parameters.AddWithValue("@team", (object?)metadata.Team ?? DBNull.Value);
        command.Parameters.AddWithValue("@lastUpdated", metadata.LastUpdated.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteProjectsAsync(List<string> projectIds)
    {
        if (!projectIds.Any())
            return;

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var placeholders = string.Join(",", projectIds.Select((_, i) => $"@id{i}"));
        var sql = $"DELETE FROM projects WHERE id IN ({placeholders})";

        using var command = new SqliteCommand(sql, connection);
        for (int i = 0; i < projectIds.Count; i++)
        {
            command.Parameters.AddWithValue($"@id{i}", projectIds[i]);
        }

        await command.ExecuteNonQueryAsync();
    }

    public async Task<DateTime?> GetLastScanTimeAsync()
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = "SELECT value FROM settings WHERE key = 'last_scan_time'";
        using var command = new SqliteCommand(sql, connection);

        var result = await command.ExecuteScalarAsync();
        if (result != null && DateTime.TryParse(result.ToString(), out var scanTime))
        {
            return scanTime;
        }

        return null;
    }

    public async Task UpdateLastScanTimeAsync(DateTime scanTime)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var sql = @"
            INSERT INTO settings (key, value)
            VALUES ('last_scan_time', @value)
            ON CONFLICT(key) DO UPDATE SET value = @value
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@value", scanTime.ToString("O"));

        await command.ExecuteNonQueryAsync();
    }
}
