using DesktopHub.Core.Models;

namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Scans a project folder for drawings and documents
/// </summary>
public interface IDocumentScanner
{
    /// <summary>
    /// Scan a project folder for files matching the configured extensions and depth
    /// </summary>
    Task<List<DocumentItem>> ScanAsync(
        string projectPath,
        IReadOnlyList<string> extensions,
        int maxDepth = 3,
        int maxFiles = 200,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Detect project type (CAD vs Revit) and scan discipline folders / Revit File folder
    /// </summary>
    Task<ProjectFileInfo> ScanProjectAsync(
        string projectPath,
        string? projectName = null,
        int maxDepth = 0,
        IReadOnlyList<string>? excludedFolders = null,
        int maxFiles = 200,
        CancellationToken cancellationToken = default);
}
