using System.IO;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Settings;

namespace DesktopHub.UI.Services;

/// <summary>
/// Orchestrates project scanning, discipline switching, and file management
/// for the Doc Quick Open widget. Linked to SearchOverlay project selection.
/// </summary>
public class DocOpenService
{
    private readonly IDocumentScanner _scanner;
    private DocWidgetConfig _config;
    private ProjectFileInfo? _projectInfo;
    private Discipline _selectedDiscipline = Discipline.Mechanical;
    private List<DocumentItem> _currentFiles = new();
    private string? _searchQuery;
    private CancellationTokenSource? _scanCts;

    /// <summary>Fired when project info or file list changes</summary>
    public event EventHandler? ProjectChanged;

    /// <summary>Fired when config changes (from settings)</summary>
    public event EventHandler? ConfigChanged;

    /// <summary>Fired when scanning starts/ends</summary>
    public event EventHandler<bool>? ScanningChanged;

    public DocWidgetConfig Config => _config;
    public ProjectFileInfo? ProjectInfo => _projectInfo;
    public Discipline SelectedDiscipline => _selectedDiscipline;
    public List<DocumentItem> CurrentFiles => _currentFiles;
    public bool IsScanning { get; private set; }

    public DocOpenService(IDocumentScanner scanner)
    {
        _scanner = scanner;
        _config = new DocWidgetConfig();
    }

    public async Task InitializeAsync()
    {
        _config = await DocWidgetConfig.LoadAsync();

        // Restore last selected discipline
        if (Enum.TryParse<Discipline>(_config.LastDiscipline, true, out var disc))
            _selectedDiscipline = disc;
    }

    /// <summary>
    /// Called when user selects a project in SearchOverlay (single click)
    /// </summary>
    public async Task SetProjectAsync(string? projectPath, string? projectName = null)
    {
        if (string.IsNullOrEmpty(projectPath))
        {
            _projectInfo = null;
            _currentFiles.Clear();
            ProjectChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Don't rescan if same project
        if (_projectInfo != null &&
            string.Equals(_projectInfo.ProjectPath, projectPath, StringComparison.OrdinalIgnoreCase))
            return;

        // Save as last project
        _config.LastProjectPath = projectPath;
        _config.LastProjectName = projectName;
        await _config.SaveAsync();

        await ScanProjectAsync(projectPath, projectName);
    }

    /// <summary>
    /// Scan the project folder to detect type and files
    /// </summary>
    private async Task ScanProjectAsync(string projectPath, string? projectName)
    {
        _scanCts?.Cancel();
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        IsScanning = true;
        ScanningChanged?.Invoke(this, true);

        try
        {
            _projectInfo = await _scanner.ScanProjectAsync(
                projectPath, projectName,
                _config.MaxDepth, _config.ExcludedFolders, _config.MaxFiles, token);
            RefreshCurrentFiles();
        }
        catch (OperationCanceledException) { }
        finally
        {
            IsScanning = false;
            ScanningChanged?.Invoke(this, false);
        }
    }

    /// <summary>
    /// Switch the active discipline (CAD projects)
    /// </summary>
    public async Task SetDisciplineAsync(Discipline discipline)
    {
        _selectedDiscipline = discipline;
        _config.LastDiscipline = discipline.ToString();
        await _config.SaveAsync();
        RefreshCurrentFiles();
    }

    /// <summary>
    /// Apply search filter
    /// </summary>
    public void SetSearchQuery(string? query)
    {
        _searchQuery = query;
        RefreshCurrentFiles();
    }

    /// <summary>
    /// Rebuild _currentFiles from project info + selected discipline + search query
    /// </summary>
    private void RefreshCurrentFiles()
    {
        _currentFiles.Clear();

        if (_projectInfo == null)
        {
            ProjectChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        IEnumerable<DocumentItem> files;

        if (_projectInfo.Type == ProjectType.Cad || 
            (_projectInfo.IsHybrid && _projectInfo.DisciplineFiles.ContainsKey(_selectedDiscipline)))
        {
            // Show files from selected discipline
            files = _projectInfo.DisciplineFiles.GetValueOrDefault(_selectedDiscipline)
                    ?? Enumerable.Empty<DocumentItem>();
        }
        else if (_projectInfo.Type == ProjectType.Revit && _projectInfo.Revit != null)
        {
            files = _projectInfo.Revit.RvtFiles;
        }
        else
        {
            files = Enumerable.Empty<DocumentItem>();
        }

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(_searchQuery))
        {
            var terms = _searchQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            files = files.Where(d =>
                terms.All(t =>
                    d.FileName.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                    d.RelativePath.Contains(t, StringComparison.OrdinalIgnoreCase)));
        }

        // Sort
        files = _config.SortBy switch
        {
            "date" => files.OrderByDescending(d => d.LastModified),
            "size" => files.OrderByDescending(d => d.SizeBytes),
            "type" => files.OrderBy(d => d.Extension).ThenBy(d => d.FileName),
            _ => files.OrderBy(d => d.FileName)
        };

        _currentFiles = files.ToList();
        ProjectChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Re-scan the current project
    /// </summary>
    public async Task RescanAsync()
    {
        if (_projectInfo != null)
        {
            await ScanProjectAsync(_projectInfo.ProjectPath, _projectInfo.ProjectName);
        }
    }

    /// <summary>
    /// Record a file open in recent history
    /// </summary>
    public async Task RecordFileOpenAsync(string filePath)
    {
        _config.RecentFiles.Remove(filePath);
        _config.RecentFiles.Insert(0, filePath);

        while (_config.RecentFiles.Count > _config.RecentFilesCount && _config.RecentFiles.Count > 0)
            _config.RecentFiles.RemoveAt(_config.RecentFiles.Count - 1);

        await _config.SaveAsync();
    }

    /// <summary>
    /// Get recent files that still exist
    /// </summary>
    public List<string> GetRecentFiles()
    {
        return _config.RecentFiles.Where(File.Exists).Take(_config.RecentFilesCount).ToList();
    }

    /// <summary>
    /// Apply config changes from settings, save, rescan, and notify
    /// </summary>
    public async Task ApplyConfigAsync()
    {
        await _config.SaveAsync();
        if (_projectInfo != null)
        {
            await RescanAsync();
        }
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }
}
