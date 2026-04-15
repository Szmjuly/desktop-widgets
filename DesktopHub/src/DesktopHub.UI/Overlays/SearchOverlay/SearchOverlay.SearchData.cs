using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DesktopHub.Core.Models;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;

namespace DesktopHub.UI;

public partial class SearchOverlay
{
    private void LoadAllProjects()
    {
        try
        {
            DebugLogger.Log($"LoadAllProjects: Starting, total projects: {_allProjects.Count}");
            ShowLoading(true);

            // Get selected filters
            var selectedYear = YearFilter.SelectedItem?.ToString();
            var selectedLocation = DriveLocationFilter.SelectedItem?.ToString();

            // Start with all projects, but filter by enabled drives first
            _filteredProjects = _allProjects.Where(p =>
            {
                if (p.DriveLocation == "Q") return _settings.GetQDriveEnabled();
                if (p.DriveLocation == "P") return _settings.GetPDriveEnabled();
                return false; // Unknown drive, exclude
            }).ToList();

            DebugLogger.Log($"LoadAllProjects: After drive filter: {_filteredProjects.Count} projects (Q enabled: {_settings.GetQDriveEnabled()}, P enabled: {_settings.GetPDriveEnabled()})");

            // Apply year filter
            if (selectedYear != "All Years" && !string.IsNullOrEmpty(selectedYear))
            {
                _filteredProjects = _filteredProjects.Where(p => p.Year == selectedYear).ToList();
            }

            // Apply drive location filter — extract drive letter from label format "Label (X:)"
            if (!string.IsNullOrEmpty(selectedLocation) && selectedLocation != "All Locations")
            {
                var driveFilter = ExtractDriveLetterFromFilterLabel(selectedLocation);
                if (driveFilter != null)
                    _filteredProjects = _filteredProjects.Where(p => p.DriveLocation == driveFilter).ToList();
            }

            DebugLogger.Log($"LoadAllProjects: Final filtered count: {_filteredProjects.Count} projects for year {selectedYear}, location {selectedLocation}");
            ResultsList.ItemsSource = _filteredProjects;
            _isPathSearchResults = false;
            _activePathSearchRootDisplay = null;
            UpdateResultsHeader();
            UpdateHistoryVisibility();
            ShowLoading(false);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SearchOverlay: LoadAllProjects error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void UpdateResultsHeader()
    {
        var count = ResultsList.Items.Count;

        if (_isPathSearchResults)
        {
            if (!string.IsNullOrWhiteSpace(_activePathSearchRootDisplay))
            {
                ResultsHeaderText.Text = $"Path: {_activePathSearchRootDisplay} ({count})";
            }
            else
            {
                ResultsHeaderText.Text = $"Path Results ({count})";
            }
            return;
        }

        var selectedYear = YearFilter.SelectedItem?.ToString();

        if (selectedYear == "All Years" || string.IsNullOrEmpty(selectedYear))
        {
            ResultsHeaderText.Text = $"Projects ({count})";
        }
        else
        {
            ResultsHeaderText.Text = $"{selectedYear} Projects ({count})";
        }
    }

    private async Task PurgeNonExistentProjectsAsync()
    {
        try
        {
            var all = await _dataStore.GetAllProjectsAsync();
            var staleIds = all
                .Where(p => !Directory.Exists(p.Path))
                .Select(p => p.Id)
                .ToList();

            if (staleIds.Count > 0)
            {
                await _dataStore.DeleteProjectsAsync(staleIds);
                DebugLogger.Log($"PurgeNonExistentProjects: removed {staleIds.Count} record(s) with non-existent paths");
            }
            else
            {
                DebugLogger.Log("PurgeNonExistentProjects: no stale records found");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"PurgeNonExistentProjects error (non-fatal): {ex.Message}");
        }
    }

    private async Task LoadProjectsAsync()
    {
        try
        {
            ShowLoading(true);
            StatusText.Text = "Loading projects...";

            _allProjects = await _dataStore.GetAllProjectsAsync();

            // Populate year and drive location filters
            PopulateYearFilter();
            PopulateDriveLocationFilter();

            // Count only projects from enabled drives
            var enabledProjectsCount = _allProjects.Count(p =>
            {
                if (p.DriveLocation == "Q") return _settings.GetQDriveEnabled();
                if (p.DriveLocation == "P") return _settings.GetPDriveEnabled();
                return false;
            });

            StatusText.Text = $"{enabledProjectsCount} projects loaded";
            DebugLogger.Log($"LoadProjectsAsync: Total in DB: {_allProjects.Count}, From enabled drives: {enabledProjectsCount} (Q: {_settings.GetQDriveEnabled()}, P: {_settings.GetPDriveEnabled()})");
            ShowLoading(false);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading projects: {ex.Message}";
            ShowLoading(false);
        }
    }

    private void PopulateYearFilter()
    {
        var years = _allProjects
            .Select(p => p.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();

        years.Insert(0, "All Years");

        YearFilter.ItemsSource = years;
        YearFilter.SelectedIndex = 0;
    }

    private void PopulateDriveLocationFilter()
    {
        bool qEnabled = _settings.GetQDriveEnabled();
        bool pEnabled = _settings.GetPDriveEnabled();
        int enabledCount = (qEnabled ? 1 : 0) + (pEnabled ? 1 : 0);

        var qLabel = $"{_settings.GetDriveLabel("Q")} (Q:)";
        var pLabel = $"{_settings.GetDriveLabel("P")} (P:)";

        var locations = new List<string>();

        if (enabledCount <= 1)
        {
            // Single drive — show only that drive name, no dropdown interaction
            if (qEnabled) locations.Add(qLabel);
            else if (pEnabled) locations.Add(pLabel);
            else locations.Add("No Locations");

            DriveLocationFilter.ItemsSource = locations;
            DriveLocationFilter.SelectedIndex = 0;
            DriveLocationFilter.IsHitTestVisible = false;
            DriveLocationFilter.Cursor = System.Windows.Input.Cursors.Arrow;
        }
        else
        {
            // Multiple drives — show "All Locations" plus each drive
            locations.Add("All Locations");
            if (qEnabled) locations.Add(qLabel);
            if (pEnabled) locations.Add(pLabel);

            DriveLocationFilter.ItemsSource = locations;
            DriveLocationFilter.SelectedIndex = 0;
            DriveLocationFilter.IsHitTestVisible = true;
            DriveLocationFilter.Cursor = System.Windows.Input.Cursors.Hand;
        }
    }

    /// <summary>
    /// Extract drive letter from filter label format "Label (X:)" → "X". Returns null if not parseable.
    /// </summary>
    private static string? ExtractDriveLetterFromFilterLabel(string label)
    {
        var openParen = label.LastIndexOf('(');
        var closeParen = label.LastIndexOf(')');
        if (openParen >= 0 && closeParen > openParen)
        {
            var inner = label[(openParen + 1)..closeParen].TrimEnd(':');
            if (inner.Length > 0) return inner;
        }
        return null;
    }

    private async Task BackgroundScanAsync()
    {
        try
        {
            // Check if we need to scan (based on last scan time)
            var lastScan = await _dataStore.GetLastScanTimeAsync();
            var scanInterval = TimeSpan.FromMinutes(_settings.GetScanIntervalMinutes());

            if (lastScan == null || DateTime.UtcNow - lastScan.Value > scanInterval)
            {
                var allScannedProjects = new List<Project>();
                List<Project>? qScannedProjects = null;
                List<Project>? pScannedProjects = null;

                var qLabel = _settings.GetDriveLabel("Q");
                var pLabel = _settings.GetDriveLabel("P");

                // Scan Q: drive - only if enabled
                if (_settings.GetQDriveEnabled())
                {
                    await Dispatcher.InvokeAsync(() => StatusText.Text = $"Scanning Q: drive ({qLabel})...");
                    var qDrivePath = _settings.GetQDrivePath();
                    if (Directory.Exists(qDrivePath))
                    {
                        try
                        {
                            qScannedProjects = await _scanner.ScanProjectsAsync(qDrivePath, "Q", CancellationToken.None);
                            allScannedProjects.AddRange(qScannedProjects);
                            DebugLogger.Log($"Q: drive scan completed: {qScannedProjects.Count} projects found");
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"Q: drive scan error: {ex.Message}");
                        }
                    }
                }
                else
                {
                    DebugLogger.Log("Q: drive scanning disabled - skipping");
                }

                // Scan P: drive - only if enabled
                if (_settings.GetPDriveEnabled())
                {
                    await Dispatcher.InvokeAsync(() => StatusText.Text = $"Scanning P: drive ({pLabel})...");
                    var pDrivePath = _settings.GetPDrivePath();
                    if (Directory.Exists(pDrivePath))
                    {
                        try
                        {
                            pScannedProjects = await _scanner.ScanProjectsAsync(pDrivePath, "P", CancellationToken.None);
                            allScannedProjects.AddRange(pScannedProjects);
                            DebugLogger.Log($"P: drive scan completed: {pScannedProjects.Count} projects found");
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"P: drive scan error: {ex.Message}");
                        }
                    }
                }
                else
                {
                    DebugLogger.Log("P: drive scanning disabled - skipping");
                }

                // Update database with all scanned projects (Q and P drives are separate)
                await _dataStore.BatchUpsertProjectsAsync(allScannedProjects);

                // Remove stale records for each successfully-scanned drive.
                // When a project folder is renamed, the old path-based ID is left in the DB.
                // This cleanup deletes any DB record whose ID (path hash) was not found in the scan.
                if (qScannedProjects != null)
                {
                    var qIds = qScannedProjects.Select(p => p.Id);
                    await _dataStore.DeleteStaleProjectsForDriveAsync("Q", qIds);
                    DebugLogger.Log($"Q: stale record cleanup complete");
                }
                if (pScannedProjects != null)
                {
                    var pIds = pScannedProjects.Select(p => p.Id);
                    await _dataStore.DeleteStaleProjectsForDriveAsync("P", pIds);
                    DebugLogger.Log($"P: stale record cleanup complete");
                }

                await _dataStore.UpdateLastScanTimeAsync(DateTime.UtcNow);

                // Reload projects
                await Dispatcher.InvokeAsync(async () => await LoadProjectsAsync());
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                StatusText.Text = $"Scan error: {ex.Message}";
            });
        }
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Cancel previous search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        var query = SearchBox.Text;

        if (string.IsNullOrWhiteSpace(query))
        {
            // Show all projects when search is blank
            _isPathSearchResults = false;
            _activePathSearchRootDisplay = null;
            LoadAllProjects();
            StatusText.Text = $"{ResultsList.Items.Count} project{(ResultsList.Items.Count == 1 ? "" : "s")} loaded";
            UpdateHistoryVisibility();

            // Clear Doc Quick Open widget when search is cleared
            if (_docOverlay?.Widget != null)
            {
                try { await _docOverlay.Widget.SetProjectAsync("", null); }
                catch { }
            }

            // Clear Project Info widget when search is cleared
            if (_projectInfoOverlay?.Widget != null)
            {
                try { await _projectInfoOverlay.Widget.SetProjectAsync("", null); }
                catch { }
            }

            // Auto-collapse when search cleared (only if user hasn't manually toggled)
            if (!_userManuallySizedResults && !_isResultsCollapsed)
            {
                _isResultsCollapsed = true;
                ResultsContainer.Visibility = Visibility.Collapsed;
                CollapseIconRotation.Angle = -90;
                CollapseToggleBtn.ToolTip = "Expand project list";
                SetSmartProjectSearchAttachedPanelExpanded(false, true);
                UpdateOverlayHeightForCurrentState(true);
            }
            return;
        }

        // Update history visibility (hide horizontal pills when typing)
        UpdateHistoryVisibility();

        // Detect path-like input and perform directory listing/path-scoped search if enabled
        if (LooksLikePathInput(query))
        {
            if (_settings.GetPathSearchEnabled())
            {
                await PerformPathSearch(query, token);
            }
            else
            {
                _isPathSearchResults = false;
                _activePathSearchRootDisplay = null;
                ResultsList.ItemsSource = null;
                StatusText.Text = "Path detected — enable Path Search in General settings (supports C:\\Path :: terms)";
                UpdateResultsHeader();
                ShowLoading(false);
            }
            return;
        }

        try
        {
            // Debounce search (wait 250ms for slower PCs)
            await Task.Delay(250, token);

            if (token.IsCancellationRequested)
                return;

            ShowLoading(true);

            _isPathSearchResults = false;
            _activePathSearchRootDisplay = null;

            // Apply filters before searching
            var selectedYear = YearFilter.SelectedItem?.ToString();
            var selectedLocation = DriveLocationFilter.SelectedItem?.ToString();

            // Start with all projects, but filter by enabled drives first
            var projectsToSearch = _allProjects.Where(p =>
            {
                if (p.DriveLocation == "Q") return _settings.GetQDriveEnabled();
                if (p.DriveLocation == "P") return _settings.GetPDriveEnabled();
                return false; // Unknown drive, exclude
            }).ToList();

            // Apply year filter
            if (selectedYear != "All Years" && !string.IsNullOrEmpty(selectedYear))
            {
                projectsToSearch = projectsToSearch.Where(p => p.Year == selectedYear).ToList();
            }

            // Apply drive location filter — extract drive letter from label format "Label (X:)"
            if (!string.IsNullOrEmpty(selectedLocation) && selectedLocation != "All Locations")
            {
                var driveFilter = ExtractDriveLetterFromFilterLabel(selectedLocation);
                if (driveFilter != null)
                    projectsToSearch = projectsToSearch.Where(p => p.DriveLocation == driveFilter).ToList();
            }

            // Search filtered projects
            var results = await _searchService.SearchAsync(query, projectsToSearch);

            if (token.IsCancellationRequested)
                return;

            // Track search execution
            TelemetryAccessor.TrackSearch(
                TelemetryEventType.SearchExecuted,
                query,
                resultCount: results.Count,
                widgetName: "ProjectSearch",
                querySource: _lastQuerySource);

            // Update UI - batch operations to reduce overhead
            var projectViewModels = results.Select(r => new ProjectViewModel(
                r.Project, r.IsRelatedMatch, r.IsLooseTokenMatch, r.IsDuplicateNumber,
                hasTags: _tagService?.HasTags(r.Project.FullNumber) ?? false)).ToList();
            ResultsList.ItemsSource = projectViewModels;

            if (results.Any())
            {
                ResultsList.SelectedIndex = 0;
                StatusText.Text = $"{results.Count} result{(results.Count == 1 ? "" : "s")} found";

                // Auto-expand when search has results — always expand regardless of manual toggle
                // (user typed a query and got results; they always want to see them)
                if (_isResultsCollapsed)
                {
                    _isResultsCollapsed = false;
                    ResultsContainer.Visibility = Visibility.Visible;
                    CollapseIconRotation.Angle = 0;
                    CollapseToggleBtn.ToolTip = "Collapse project list";
                }

                UpdateOverlayHeightForCurrentState(true);

                // History tracking removed - only track on actual project launch
            }
            else
            {
                StatusText.Text = "No results found";
            }

            UpdateResultsHeader();
            UpdateHistoryVisibility();
            ShowLoading(false);
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled, ignore
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SearchBox_TextChanged: Search error: {ex.Message}\n{ex.StackTrace}");
            StatusText.Text = $"Search error: {ex.Message}";
            ShowLoading(false);
        }
    }
}
