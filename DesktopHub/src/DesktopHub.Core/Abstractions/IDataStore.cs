using DesktopHub.Core.Models;

namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Data persistence layer for projects and metadata
/// </summary>
public interface IDataStore
{
    /// <summary>
    /// Initialize the data store (create tables, etc.)
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Get all projects from the database
    /// </summary>
    Task<List<Project>> GetAllProjectsAsync();

    /// <summary>
    /// Get a single project by ID
    /// </summary>
    Task<Project?> GetProjectByIdAsync(string id);

    /// <summary>
    /// Upsert (insert or update) a project
    /// </summary>
    Task UpsertProjectAsync(Project project);

    /// <summary>
    /// Batch upsert multiple projects (for performance)
    /// </summary>
    Task BatchUpsertProjectsAsync(List<Project> projects);

    /// <summary>
    /// Get metadata for a project
    /// </summary>
    Task<ProjectMetadata?> GetMetadataAsync(string projectId);

    /// <summary>
    /// Update metadata for a project
    /// </summary>
    Task UpsertMetadataAsync(ProjectMetadata metadata);

    /// <summary>
    /// Delete projects that no longer exist on filesystem
    /// </summary>
    Task DeleteProjectsAsync(List<string> projectIds);

    /// <summary>
    /// Delete stale project records for a given drive whose IDs are not in the provided set.
    /// Called after a scan completes to remove orphaned entries from renamed/deleted folders.
    /// </summary>
    Task DeleteStaleProjectsForDriveAsync(string driveLocation, IEnumerable<string> currentProjectIds);

    /// <summary>
    /// Get last scan timestamp
    /// </summary>
    Task<DateTime?> GetLastScanTimeAsync();

    /// <summary>
    /// Update last scan timestamp
    /// </summary>
    Task UpdateLastScanTimeAsync(DateTime scanTime);
}
