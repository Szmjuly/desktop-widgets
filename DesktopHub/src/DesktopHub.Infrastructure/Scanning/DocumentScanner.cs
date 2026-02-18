using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;

namespace DesktopHub.Infrastructure.Scanning;

/// <summary>
/// Scans project folders for drawings/documents, detects CAD vs Revit project type
/// </summary>
public class DocumentScanner : IDocumentScanner
{
    // Discipline folder names (case-insensitive)
    private static readonly Dictionary<string, Discipline> DisciplineFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Electrical", Discipline.Electrical },
        { "Mechanical", Discipline.Mechanical },
        { "Plumbing", Discipline.Plumbing },
    };

    // Map extensions to categories (fallback)
    private static readonly Dictionary<string, string> ExtensionCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        { "dwg", "Drawing" }, { "rvt", "Drawing" }, { "dwf", "Drawing" },
        { "dwfx", "Drawing" }, { "dxf", "Drawing" }, { "dgn", "Drawing" },
        { "pdf", "Document" }, { "doc", "Document" }, { "docx", "Document" },
        { "xlsx", "Spreadsheet" }, { "xls", "Spreadsheet" }, { "csv", "Spreadsheet" },
        { "txt", "Document" }, { "jpg", "Photo" }, { "png", "Photo" },
    };

    // ---- Legacy generic scan (kept for backward compat) ----
    public async Task<List<DocumentItem>> ScanAsync(
        string projectPath,
        IReadOnlyList<string> extensions,
        int maxDepth = 3,
        int maxFiles = 200,
        CancellationToken cancellationToken = default)
    {
        var results = new List<DocumentItem>();
        if (!Directory.Exists(projectPath)) return results;

        var extensionSet = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        await Task.Run(() =>
            ScanDirectory(projectPath, projectPath, extensionSet, maxDepth, 0, maxFiles, results, cancellationToken),
            cancellationToken);
        return results;
    }

    // ---- New project-aware scan ----
    public async Task<ProjectFileInfo> ScanProjectAsync(
        string projectPath,
        string? projectName = null,
        int maxDepth = 0,
        IReadOnlyList<string>? excludedFolders = null,
        int maxFiles = 200,
        IReadOnlyList<string>? includeExtensions = null,
        CancellationToken cancellationToken = default)
    {
        var info = new ProjectFileInfo
        {
            ProjectPath = projectPath,
            ProjectName = projectName ?? Path.GetFileName(projectPath)
        };
        var includeSet = BuildIncludeExtensionSet(includeExtensions);

        if (!Directory.Exists(projectPath))
            return info;

        await Task.Run(() =>
        {
            // 1. Check for discipline folders (CAD)
            foreach (var (folderName, discipline) in DisciplineFolders)
            {
                var discPath = Path.Combine(projectPath, folderName);
                if (Directory.Exists(discPath))
                {
                    info.AvailableDisciplines.Add(discipline);
                    var dwgFiles = ScanDisciplineFolder(discPath, projectPath, maxDepth, excludedFolders, maxFiles, cancellationToken);
                    info.DisciplineFiles[discipline] = dwgFiles;
                }
            }

            // 1.5 Build a broad searchable file set across the full project directory.
            // Keep this independent from discipline view so users can find letters/docs anywhere.
            var fullProjectDepth = Math.Max(maxDepth, 2);
            info.AllFiles = ScanProjectFiles(projectPath, includeSet, fullProjectDepth, excludedFolders, maxFiles, cancellationToken);

            // 2. Check for "Revit File" folder
            var revitFolderPath = FindRevitFolder(projectPath);
            if (revitFolderPath != null)
            {
                info.Revit = ParseRevitFolder(revitFolderPath, projectPath, cancellationToken);
            }

            // 3. Determine project type
            bool hasDisciplines = info.AvailableDisciplines.Count > 0;
            bool hasRevit = info.Revit != null;

            if (hasDisciplines && hasRevit)
            {
                info.IsHybrid = true;
                info.Type = ProjectType.Revit; // Default to Revit view when both exist
            }
            else if (hasRevit)
            {
                info.Type = ProjectType.Revit;
            }
            else if (hasDisciplines)
            {
                info.Type = ProjectType.Cad;
            }
            else
            {
                info.Type = ProjectType.Unknown;
            }
        }, cancellationToken);

        return info;
    }

    /// <summary>
    /// Scan a discipline folder (Electrical/Mechanical/Plumbing) for .dwg files
    /// </summary>
    private List<DocumentItem> ScanDisciplineFolder(
        string folderPath,
        string projectRoot,
        int maxDepth,
        IReadOnlyList<string>? excludedFolders,
        int maxFiles,
        CancellationToken ct)
    {
        var results = new List<DocumentItem>();
        var excludeSet = excludedFolders != null && excludedFolders.Count > 0
            ? new HashSet<string>(excludedFolders, StringComparer.OrdinalIgnoreCase)
            : null;
        try
        {
            ScanForExtensions(folderPath, projectRoot, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "dwg", "dxf" },
                maxDepth, 0, maxFiles, results, ct, excludeSet);
        }
        catch { }
        return results;
    }

    private List<DocumentItem> ScanProjectFiles(
        string projectPath,
        HashSet<string> includeSet,
        int maxDepth,
        IReadOnlyList<string>? excludedFolders,
        int maxFiles,
        CancellationToken ct)
    {
        var results = new List<DocumentItem>();
        var excludeSet = excludedFolders != null && excludedFolders.Count > 0
            ? new HashSet<string>(excludedFolders, StringComparer.OrdinalIgnoreCase)
            : null;

        try
        {
            ScanForExtensions(projectPath, projectPath, includeSet,
                maxDepth, 0, maxFiles, results, ct, excludeSet);
        }
        catch { }

        return results;
    }

    private static HashSet<string> BuildIncludeExtensionSet(IReadOnlyList<string>? includeExtensions)
    {
        if (includeExtensions != null && includeExtensions.Count > 0)
        {
            return includeExtensions
                .Select(e => e.Trim().TrimStart('.'))
                .Where(e => !string.IsNullOrWhiteSpace(e))
                .Select(e => e.ToLowerInvariant())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pdf", "dwg", "rvt", "dwf", "dwfx", "dxf", "dgn",
            "doc", "docx", "xlsx", "xls", "csv", "ppt", "pptx",
            "txt", "msg", "eml", "rtf"
        };
    }

    /// <summary>
    /// Find the "Revit File" (or "Revit Files") folder under a project path
    /// </summary>
    private static string? FindRevitFolder(string projectPath)
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(projectPath))
            {
                var name = Path.GetFileName(dir);
                if (name.Equals("Revit File", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Revit Files", StringComparison.OrdinalIgnoreCase))
                {
                    return dir;
                }
            }
        }
        catch { }
        return null;
    }

    /// <summary>
    /// Parse a Revit File folder for metadata (version, cloud status) and .rvt files
    /// </summary>
    private RevitInfo ParseRevitFolder(string revitFolderPath, string projectRoot, CancellationToken ct)
    {
        var info = new RevitInfo { RevitFolderPath = revitFolderPath };

        try
        {
            // Scan subfolders for metadata markers
            foreach (var dir in Directory.EnumerateDirectories(revitFolderPath))
            {
                if (ct.IsCancellationRequested) break;
                var name = Path.GetFileName(dir).Trim();
                var nameUpper = name.ToUpperInvariant();

                // Revit version detection
                // Patterns: "REVIT 2024", "REVIT VERSION - REVIT 2026"
                if (nameUpper.StartsWith("REVIT VERSION"))
                {
                    // "REVIT VERSION - REVIT 2026" -> extract version
                    var parts = name.Split('-', StringSplitOptions.TrimEntries);
                    if (parts.Length >= 2)
                    {
                        var versionPart = parts[^1]; // last segment
                        var vNum = ExtractVersionNumber(versionPart);
                        if (!string.IsNullOrEmpty(vNum))
                            info.RevitVersion = vNum;
                    }
                }
                else if (nameUpper.StartsWith("REVIT 20"))
                {
                    // "REVIT 2024"
                    var vNum = ExtractVersionNumber(name);
                    if (!string.IsNullOrEmpty(vNum) && string.IsNullOrEmpty(info.RevitVersion))
                        info.RevitVersion = vNum;
                }

                // Cloud detection
                // Patterns: "Cloud Job - Yes", "CLOUD MODEL", "CLOUD MODEL - NO", "CLOUD PROJECT - NO/YES"
                if (nameUpper.StartsWith("CLOUD"))
                {
                    info.CloudStatusExplicit = true;
                    if (nameUpper.Contains("YES") || 
                        (!nameUpper.Contains("NO") && !nameUpper.Contains("-")))
                    {
                        // "Cloud Job - Yes", "CLOUD MODEL" (no dash = assume yes)
                        info.IsCloudProject = true;
                    }
                    else
                    {
                        // "CLOUD MODEL - NO", "CLOUD PROJECT - NO"
                        info.IsCloudProject = false;
                    }
                }

                // CRevit detection
                if (nameUpper.StartsWith("CREVIT"))
                {
                    info.IsCRevit = nameUpper.Contains("YES") || !nameUpper.Contains("NO");
                }
            }

            // Scan for .rvt files in the Revit File folder (not deep — just top level)
            foreach (var file in Directory.EnumerateFiles(revitFolderPath))
            {
                if (ct.IsCancellationRequested) break;
                var ext = Path.GetExtension(file).TrimStart('.').ToLowerInvariant();
                if (ext == "rvt")
                {
                    try
                    {
                        var fi = new FileInfo(file);
                        // Skip backup files (contain .0001. etc.)
                        if (fi.Name.Contains(".0001.") || fi.Name.Contains(".0002.") ||
                            fi.Name.Contains("_backup"))
                            continue;

                        info.RvtFiles.Add(new DocumentItem
                        {
                            Path = file,
                            FileName = fi.Name,
                            Extension = ext,
                            SizeBytes = fi.Length,
                            LastModified = fi.LastWriteTime,
                            Category = "Revit Model",
                            RelativePath = Path.GetRelativePath(projectRoot, file),
                            Subfolder = "Revit File"
                        });
                    }
                    catch { }
                }
            }

            // If no explicit cloud status and no .rvt files, likely cloud
            if (!info.CloudStatusExplicit && info.RvtFiles.Count == 0)
            {
                info.IsCloudProject = true;
            }
        }
        catch { }

        return info;
    }

    /// <summary>
    /// Extract a 4-digit year version number from a string like "REVIT 2024"
    /// </summary>
    private static string ExtractVersionNumber(string text)
    {
        // Find "20XX" pattern
        for (int i = 0; i <= text.Length - 4; i++)
        {
            if (text[i] == '2' && text[i + 1] == '0' && char.IsDigit(text[i + 2]) && char.IsDigit(text[i + 3]))
            {
                return text.Substring(i, 4);
            }
        }
        return string.Empty;
    }

    // ---- Shared scanning helpers ----

    private void ScanForExtensions(
        string rootPath, string projectRoot,
        HashSet<string> extensions, int maxDepth, int currentDepth, int maxFiles,
        List<DocumentItem> results, CancellationToken ct, HashSet<string>? excludedFolders = null)
    {
        if (ct.IsCancellationRequested || results.Count >= maxFiles) return;

        try
        {
            foreach (var filePath in Directory.EnumerateFiles(rootPath))
            {
                if (ct.IsCancellationRequested || results.Count >= maxFiles) return;
                try
                {
                    var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
                    if (!extensions.Contains(ext)) continue;

                    var fi = new FileInfo(filePath);
                    results.Add(new DocumentItem
                    {
                        Path = filePath,
                        FileName = fi.Name,
                        Extension = ext,
                        SizeBytes = fi.Length,
                        LastModified = fi.LastWriteTime,
                        Category = ExtensionCategories.GetValueOrDefault(ext, "Other"),
                        RelativePath = Path.GetRelativePath(projectRoot, filePath),
                        Subfolder = Path.GetFileName(rootPath)
                    });
                }
                catch (UnauthorizedAccessException) { }
                catch (IOException) { }
            }

            if (currentDepth < maxDepth)
            {
                foreach (var dir in Directory.EnumerateDirectories(rootPath))
                {
                    if (ct.IsCancellationRequested || results.Count >= maxFiles) return;
                    var dirName = Path.GetFileName(dir);
                    if (dirName.StartsWith('.') || dirName.StartsWith('_')) continue;
                    if (excludedFolders != null && excludedFolders.Contains(dirName)) continue;
                    ScanForExtensions(dir, projectRoot, extensions, maxDepth, currentDepth + 1, maxFiles, results, ct, excludedFolders);
                }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (IOException) { }
    }

    // ---- Legacy helpers (kept for ScanAsync backward compat) ----

    private void ScanDirectory(
        string rootPath, string currentPath, HashSet<string> extensions,
        int maxDepth, int currentDepth, int maxFiles,
        List<DocumentItem> results, CancellationToken ct)
    {
        ScanForExtensions(currentPath, rootPath, extensions, maxDepth, currentDepth, maxFiles, results, ct);
    }
}
