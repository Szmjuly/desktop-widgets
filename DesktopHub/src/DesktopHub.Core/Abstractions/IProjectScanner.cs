using DesktopHub.Core.Models;

namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Service for scanning Q: drive and discovering project folders
/// </summary>
public interface IProjectScanner
{
    /// <summary>
    /// Scan Q: drive for all project folders
    /// </summary>
    /// <param name="drivePath">Path to Q: drive (e.g., "Q:\\")</param>
    /// <param name="cancellationToken">Cancellation token for long-running scan</param>
    /// <returns>List of discovered projects</returns>
    Task<List<Project>> ScanProjectsAsync(string drivePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scan a specific year directory for projects
    /// </summary>
    /// <param name="yearDirectoryPath">Path to year directory (e.g., "Q:\\_Proj-24")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects in that year</returns>
    Task<List<Project>> ScanYearDirectoryAsync(string yearDirectoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a directory name matches a project folder pattern
    /// </summary>
    /// <param name="directoryName">Directory name to check</param>
    /// <returns>Parsed project if match, null otherwise</returns>
    Project? TryParseProjectFolder(string directoryName, string fullPath, string year);
}
