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
    // Map of filter-dropdown display label → scan profile drive location code. Populated by
    // PopulateDriveLocationFilter(); lets SearchBox_TextChanged / LoadAllProjects resolve the
    // selected label back to its underlying profile without string-parsing.
    private readonly Dictionary<string, string> _driveLocationFilterMap = new();

    private void LoadAllProjects()
    {
        try
        {
            DebugLogger.Log($"LoadAllProjects: Starting, total projects: {_allProjects.Count}");
            ShowLoading(true);

            // Get selected filters
            var selectedYear = YearFilter.SelectedItem?.ToString();
            var selectedLocation = DriveLocationFilter.SelectedItem?.ToString();

            var enabledCodes = GetEnabledDriveCodes();

            // Start with all projects, but filter by enabled profiles first
            _filteredProjects = _allProjects.Where(p => enabledCodes.Contains(p.DriveLocation)).ToList();

            DebugLogger.Log($"LoadAllProjects: After profile filter: {_filteredProjects.Count} projects ({enabledCodes.Count} enabled profiles: {string.Join(",", enabledCodes)})");

            // Apply year filter
            if (selectedYear != "All Years" && !string.IsNullOrEmpty(selectedYear))
            {
                _filteredProjects = _filteredProjects.Where(p => p.Year == selectedYear).ToList();
            }

            // Apply drive location filter — look up the selected label in the filter map
            if (!string.IsNullOrEmpty(selectedLocation)
                && selectedLocation != "All Locations"
                && _driveLocationFilterMap.TryGetValue(selectedLocation, out var driveFilter))
            {
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

            // Count only projects from enabled profiles
            var enabledCodes = GetEnabledDriveCodes();
            var enabledProjectsCount = _allProjects.Count(p => enabledCodes.Contains(p.DriveLocation));

            StatusText.Text = $"{enabledProjectsCount} projects loaded";
            DebugLogger.Log($"LoadProjectsAsync: Total in DB: {_allProjects.Count}, From enabled profiles: {enabledProjectsCount} ({enabledCodes.Count} enabled)");
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
        _driveLocationFilterMap.Clear();

        var enabledProfiles = _settings.GetScanProfiles()
            .Where(p => p.Enabled)
            .OrderBy(p => p.SortOrder)
            .ToList();

        var locations = new List<string>();

        if (enabledProfiles.Count == 0)
        {
            locations.Add("No Locations");
            DriveLocationFilter.ItemsSource = locations;
            DriveLocationFilter.SelectedIndex = 0;
            DriveLocationFilter.IsHitTestVisible = false;
            DriveLocationFilter.Cursor = System.Windows.Input.Cursors.Arrow;
            return;
        }

        if (enabledProfiles.Count == 1)
        {
            var only = enabledProfiles[0];
            var label = BuildFilterLabel(only);
            locations.Add(label);
            _driveLocationFilterMap[label] = ProfileDriveCode(only);

            DriveLocationFilter.ItemsSource = locations;
            DriveLocationFilter.SelectedIndex = 0;
            DriveLocationFilter.IsHitTestVisible = false;
            DriveLocationFilter.Cursor = System.Windows.Input.Cursors.Arrow;
            return;
        }

        locations.Add("All Locations");
        foreach (var profile in enabledProfiles)
        {
            var label = BuildFilterLabel(profile);
            // Disambiguate if two profiles would share a label
            var candidate = label;
            var suffix = 2;
            while (_driveLocationFilterMap.ContainsKey(candidate))
            {
                candidate = $"{label} #{suffix++}";
            }
            locations.Add(candidate);
            _driveLocationFilterMap[candidate] = ProfileDriveCode(profile);
        }

        DriveLocationFilter.ItemsSource = locations;
        DriveLocationFilter.SelectedIndex = 0;
        DriveLocationFilter.IsHitTestVisible = true;
        DriveLocationFilter.Cursor = System.Windows.Input.Cursors.Hand;
    }

    private static string BuildFilterLabel(ScanProfile profile)
    {
        // For legacy drive-mapped profiles, show "<Name> (<Letter>:)" to match what CES users
        // have always seen in the dropdown. For net-new profiles, just show the name.
        if (!string.IsNullOrEmpty(profile.LegacyDriveCode))
        {
            return $"{profile.Name} ({profile.LegacyDriveCode}:)";
        }
        return string.IsNullOrWhiteSpace(profile.Name) ? "(Unnamed Profile)" : profile.Name;
    }

    private static string ProfileDriveCode(ScanProfile profile)
    {
        if (!string.IsNullOrEmpty(profile.LegacyDriveCode)) return profile.LegacyDriveCode;
        return profile.Id.Length >= 8 ? profile.Id.Substring(0, 8) : profile.Id;
    }

    private HashSet<string> GetEnabledDriveCodes()
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var profile in _settings.GetScanProfiles())
        {
            if (!profile.Enabled) continue;
            set.Add(ProfileDriveCode(profile));
        }
        return set;
    }

    private bool IsDriveEnabled(string driveLocation) => GetEnabledDriveCodes().Contains(driveLocation);

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
                // Track per-profile scanned results so we can do stale-record cleanup scoped to
                // each profile's drive-location code.
                var perProfileResults = new Dictionary<string, List<Project>>(StringComparer.Ordinal);

                var profiles = _settings.GetScanProfiles()
                    .Where(p => p.Enabled)
                    .OrderBy(p => p.SortOrder)
                    .ToList();

                foreach (var profile in profiles)
                {
                    // FileBrowser profiles don't produce projects — their files are surfaced
                    // through the path-search path instead. Skip them in the project scan loop.
                    if (profile.Mode != ScanProfileMode.ProjectMode) continue;
                    if (string.IsNullOrWhiteSpace(profile.RootPath))
                    {
                        DebugLogger.Log($"BackgroundScan: Skipping profile '{profile.Name}' (root path not configured)");
                        continue;
                    }
                    if (!Directory.Exists(profile.RootPath))
                    {
                        DebugLogger.Log($"BackgroundScan: Skipping profile '{profile.Name}' (path does not exist: {profile.RootPath})");
                        continue;
                    }

                    await Dispatcher.InvokeAsync(() => StatusText.Text = $"Scanning {profile.Name}...");
                    try
                    {
                        var scanned = await _scanner.ScanProjectsAsync(profile, CancellationToken.None);
                        allScannedProjects.AddRange(scanned);
                        perProfileResults[ProfileDriveCode(profile)] = scanned;
                        DebugLogger.Log($"Profile '{profile.Name}' scan completed: {scanned.Count} projects found");
                    }
                    catch (Exception ex)
                    {
                        DebugLogger.Log($"Profile '{profile.Name}' scan error: {ex.Message}");
                    }
                }

                // Update database with all scanned projects
                await _dataStore.BatchUpsertProjectsAsync(allScannedProjects);

                // Remove stale records for each successfully-scanned profile. When a project
                // folder is renamed, the old path-based ID is left in the DB; this cleanup
                // deletes any DB record whose ID (path hash) was not found in the scan.
                foreach (var (driveCode, scanned) in perProfileResults)
                {
                    var ids = scanned.Select(p => p.Id);
                    await _dataStore.DeleteStaleProjectsForDriveAsync(driveCode, ids);
                    DebugLogger.Log($"Profile '{driveCode}': stale record cleanup complete");
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

            var enabledCodes = GetEnabledDriveCodes();
            // Start with all projects, but filter by enabled profiles first
            var projectsToSearch = _allProjects.Where(p => enabledCodes.Contains(p.DriveLocation)).ToList();

            // Apply year filter
            if (selectedYear != "All Years" && !string.IsNullOrEmpty(selectedYear))
            {
                projectsToSearch = projectsToSearch.Where(p => p.Year == selectedYear).ToList();
            }

            // Apply drive location filter via the label→code map
            if (!string.IsNullOrEmpty(selectedLocation)
                && selectedLocation != "All Locations"
                && _driveLocationFilterMap.TryGetValue(selectedLocation, out var driveFilter))
            {
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
