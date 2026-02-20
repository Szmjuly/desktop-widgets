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

            // Apply drive location filter
            if (!string.IsNullOrEmpty(selectedLocation) && selectedLocation != "All Locations")
            {
                var driveFilter = selectedLocation.Contains("Florida") ? "Q" : "P";
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

        var locations = new List<string>();

        if (enabledCount <= 1)
        {
            // Single drive — show only that drive name, no dropdown interaction
            if (qEnabled) locations.Add("Florida (Q:)");
            else if (pEnabled) locations.Add("Connecticut (P:)");
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
            if (qEnabled) locations.Add("Florida (Q:)");
            if (pEnabled) locations.Add("Connecticut (P:)");

            DriveLocationFilter.ItemsSource = locations;
            DriveLocationFilter.SelectedIndex = 0;
            DriveLocationFilter.IsHitTestVisible = true;
            DriveLocationFilter.Cursor = System.Windows.Input.Cursors.Hand;
        }
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

                // Scan Q: drive (Florida) - only if enabled
                if (_settings.GetQDriveEnabled())
                {
                    await Dispatcher.InvokeAsync(() => StatusText.Text = "Scanning Q: drive (Florida)...");
                    var qDrivePath = _settings.GetQDrivePath();
                    if (Directory.Exists(qDrivePath))
                    {
                        try
                        {
                            var qProjects = await _scanner.ScanProjectsAsync(qDrivePath, "Q", CancellationToken.None);
                            allScannedProjects.AddRange(qProjects);
                            DebugLogger.Log($"Q: drive scan completed: {qProjects.Count} projects found");
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

                // Scan P: drive (Connecticut) - only if enabled
                if (_settings.GetPDriveEnabled())
                {
                    await Dispatcher.InvokeAsync(() => StatusText.Text = "Scanning P: drive (Connecticut)...");
                    var pDrivePath = _settings.GetPDrivePath();
                    if (Directory.Exists(pDrivePath))
                    {
                        try
                        {
                            var pProjects = await _scanner.ScanProjectsAsync(pDrivePath, "P", CancellationToken.None);
                            allScannedProjects.AddRange(pProjects);
                            DebugLogger.Log($"P: drive scan completed: {pProjects.Count} projects found");
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
            UpdateHistoryVisibility();

            // Clear Doc Quick Open widget when search is cleared
            if (_docOverlay?.Widget != null)
            {
                try { await _docOverlay.Widget.SetProjectAsync("", null); }
                catch { }
            }

            // Auto-collapse when search cleared (only if user hasn't manually toggled)
            if (!_userManuallySizedResults && !_isResultsCollapsed)
            {
                _isResultsCollapsed = true;
                ResultsContainer.Visibility = Visibility.Collapsed;
                CollapseIconRotation.Angle = -90;
                this.Height = 140;
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

            // Apply drive location filter
            if (!string.IsNullOrEmpty(selectedLocation) && selectedLocation != "All Locations")
            {
                var driveFilter = selectedLocation.Contains("Florida") ? "Q" : "P";
                projectsToSearch = projectsToSearch.Where(p => p.DriveLocation == driveFilter).ToList();
            }

            // Search filtered projects
            var results = await _searchService.SearchAsync(query, projectsToSearch);

            if (token.IsCancellationRequested)
                return;

            // Update UI - batch operations to reduce overhead
            var projectViewModels = results.Select(r => new ProjectViewModel(r.Project)).ToList();
            ResultsList.ItemsSource = projectViewModels;

            if (results.Any())
            {
                ResultsList.SelectedIndex = 0;
                StatusText.Text = $"{results.Count} result{(results.Count == 1 ? "" : "s")} found";

                // Auto-expand when search has results (only if user hasn't manually toggled)
                if (!_userManuallySizedResults && _isResultsCollapsed)
                {
                    _isResultsCollapsed = false;
                    ResultsContainer.Visibility = Visibility.Visible;
                    CollapseIconRotation.Angle = 0;
                    this.Height = 500;
                }

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
