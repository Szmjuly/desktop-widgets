using DesktopHub.Core.Models;

namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Service for scanning Q: and P: drives and discovering project folders
/// </summary>
public interface IProjectScanner
{
    /// <summary>
    /// Scan drive for all project folders
    /// </summary>
    /// <param name="drivePath">Path to drive (e.g., "Q:\\" or "P:\\")</param>
    /// <param name="cancellationToken">Cancellation token for long-running scan</param>
    /// <returns>List of discovered projects</returns>
    Task<List<Project>> ScanProjectsAsync(string drivePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scan drive for all project folders with explicit drive location
    /// </summary>
    /// <param name="drivePath">Path to drive (e.g., "Q:\\" or "P:\\")</param>
    /// <param name="driveLocation">Drive location identifier (e.g., "Q" or "P")</param>
    /// <param name="cancellationToken">Cancellation token for long-running scan</param>
    /// <returns>List of discovered projects</returns>
    Task<List<Project>> ScanProjectsAsync(string drivePath, string driveLocation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scan a specific year directory for projects
    /// </summary>
    /// <param name="yearDirectoryPath">Path to year directory (e.g., "Q:\\_Proj-24")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects in that year</returns>
    Task<List<Project>> ScanYearDirectoryAsync(string yearDirectoryPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Scan a specific year directory for projects with explicit drive location
    /// </summary>
    /// <param name="yearDirectoryPath">Path to year directory (e.g., "Q:\\_Proj-24")</param>
    /// <param name="driveLocation">Drive location identifier (e.g., "Q" or "P")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of projects in that year</returns>
    Task<List<Project>> ScanYearDirectoryAsync(string yearDirectoryPath, string driveLocation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if a directory name matches a project folder pattern
    /// </summary>
    /// <param name="directoryName">Directory name to check</param>
    /// <returns>Parsed project if match, null otherwise</returns>
    Project? TryParseProjectFolder(string directoryName, string fullPath, string year);

    /// <summary>
    /// Check if a directory name matches a project folder pattern with explicit drive location
    /// </summary>
    /// <param name="directoryName">Directory name to check</param>
    /// <param name="fullPath">Full path to the directory</param>
    /// <param name="year">Year directory</param>
    /// <param name="driveLocation">Drive location identifier (e.g., "Q" or "P")</param>
    /// <returns>Parsed project if match, null otherwise</returns>
    Project? TryParseProjectFolder(string directoryName, string fullPath, string year, string driveLocation);
}
