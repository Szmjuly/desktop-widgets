using DesktopHub.Core.Models;

namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Persistence for project launch frequency tracking
/// </summary>
public interface IProjectLaunchDataStore
{
    /// <summary>
    /// Initialize the data store (create tables, etc.)
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Record a project launch (increment count, update last-launched timestamp)
    /// </summary>
    Task RecordLaunchAsync(string path, string fullNumber, string name);

    /// <summary>
    /// Get the top N most frequently launched projects
    /// </summary>
    Task<List<ProjectLaunchRecord>> GetTopProjectsAsync(int count = 5);

    /// <summary>
    /// Get all launch records
    /// </summary>
    Task<List<ProjectLaunchRecord>> GetAllAsync();

    /// <summary>
    /// Clear all launch history
    /// </summary>
    Task ClearAllAsync();
}
