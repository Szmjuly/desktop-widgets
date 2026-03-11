using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopHub.Core.Models;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;

namespace DesktopHub.UI;

public partial class SearchOverlay
{
    // ViewModel for binding
    private class ProjectViewModel
    {
        public string FullNumber { get; }
        public string Name { get; }
        public string Path { get; }
        public string? Location { get; }
        public string? Status { get; }
        public bool IsFavorite { get; }
        public bool IsRelatedMatch { get; }
        public bool IsLooseTokenMatch { get; }
        public bool IsDuplicateNumber { get; }
        public bool HasTags { get; }
        public string MatchTag { get; }

        public ProjectViewModel(Project project, bool isRelatedMatch = false, bool isLooseTokenMatch = false, bool isDuplicateNumber = false, bool hasTags = false)
        {
            FullNumber = project.FullNumber;
            Name = project.Name;
            Path = project.Path;
            Location = project.Metadata?.Location;
            Status = project.Metadata?.Status;
            IsFavorite = project.Metadata?.IsFavorite ?? false;
            IsRelatedMatch = isRelatedMatch;
            IsLooseTokenMatch = isLooseTokenMatch;
            IsDuplicateNumber = isDuplicateNumber;
            HasTags = hasTags;
            MatchTag = isDuplicateNumber ? "Duplicate Number?" :
                       isRelatedMatch ? "Related Project" :
                       isLooseTokenMatch ? "Similar Match" : "";
        }
    }

    private List<Project> GetFilteredProjectsByYear()
    {
        var selectedYear = YearFilter.SelectedItem as string;
        if (string.IsNullOrEmpty(selectedYear) || selectedYear == "All Years")
        {
            return _allProjects;
        }

        return _allProjects.Where(p => p.Year == selectedYear).ToList();
    }

    private void AddToSearchHistory(string query)
    {
        // Remove if already exists
        _searchHistory.Remove(query);

        // Add to front
        _searchHistory.Insert(0, query);

        // Keep only last 25 to prevent excessive memory usage
        if (_searchHistory.Count > 25)
        {
            _searchHistory = _searchHistory.Take(25).ToList();
        }
    }

    private void YearFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedYear = (sender as System.Windows.Controls.ComboBox)?.SelectedItem?.ToString();
        TelemetryAccessor.TrackFilterChanged("year", selectedYear);

        // Reload projects with new year filter
        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            // Re-trigger search with new filter
            SearchBox_TextChanged(SearchBox, new TextChangedEventArgs(e.RoutedEvent, UndoAction.None));
        }
        else
        {
            // Reload all projects with new year filter
            LoadAllProjects();
        }
    }

    private void DriveLocationFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var selectedDrive = (sender as System.Windows.Controls.ComboBox)?.SelectedItem?.ToString();
        TelemetryAccessor.TrackFilterChanged("drive_location", selectedDrive);

        // Reload projects with new drive location filter
        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            // Re-trigger search with new filter
            SearchBox_TextChanged(SearchBox, new TextChangedEventArgs(e.RoutedEvent, UndoAction.None));
        }
        else
        {
            // Reload all projects with new drive location filter
            LoadAllProjects();
        }
    }

    private void WidgetLauncherToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_widgetLauncher != null)
        {
            if (_widgetLauncher.Visibility == Visibility.Visible)
            {
                _widgetLauncher.Visibility = Visibility.Hidden;
                DebugLogger.Log("Widget launcher hidden via toggle button");
            }
            else
            {
                _widgetLauncher.Visibility = Visibility.Visible;
                DebugLogger.Log("Widget launcher shown via toggle button");
            }

            // Save visibility state if in Living Widgets Mode
            var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
            if (isLivingWidgetsMode)
            {
                _settings.SetWidgetLauncherVisible(_widgetLauncher.Visibility == Visibility.Visible);
                _ = _settings.SaveAsync();
                DebugLogger.Log($"Saved widget launcher visibility state: {_widgetLauncher.Visibility == Visibility.Visible}");
            }
        }
    }

    private void HistoryItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.DataContext is string query)
        {
            _lastQuerySource = Core.Models.QuerySources.History;
            SearchBox.Text = query;
        }
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        DebugLogger.LogHeader("SearchBox GotFocus");
        DebugLogger.LogVariable("Window.IsActive", this.IsActive);
        DebugLogger.LogVariable("Window.IsFocused", this.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocusWithin", SearchBox.IsKeyboardFocusWithin);
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        DebugLogger.LogHeader("SearchBox LostFocus");
        DebugLogger.LogVariable("Window.IsActive", this.IsActive);
        DebugLogger.LogVariable("Window.IsFocused", this.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
    }

    private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        DebugLogger.LogHeader("SearchBox PreviewKeyDown");
        DebugLogger.LogVariable("Key", e.Key);
        DebugLogger.LogVariable("SystemKey", e.SystemKey);
        DebugLogger.LogVariable("KeyStates", e.KeyStates);
        DebugLogger.LogVariable("IsDown", e.IsDown);
        DebugLogger.LogVariable("IsUp", e.IsUp);
        DebugLogger.LogVariable("IsRepeat", e.IsRepeat);
        DebugLogger.LogVariable("Keyboard.Modifiers", Keyboard.Modifiers);
        DebugLogger.LogVariable("SearchBox.Text (before)", SearchBox.Text);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);

        // Detect paste (Ctrl+V) to track query source
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            _lastQuerySource = Core.Models.QuerySources.Pasted;
        }
        else if (e.Key != Key.LeftCtrl && e.Key != Key.RightCtrl &&
                 e.Key != Key.LeftShift && e.Key != Key.RightShift &&
                 e.Key != Key.LeftAlt && e.Key != Key.RightAlt &&
                 e.Key != Key.Enter && e.Key != Key.Escape &&
                 e.Key != Key.Tab && e.Key != Key.Up && e.Key != Key.Down)
        {
            _lastQuerySource = Core.Models.QuerySources.Typed;
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        DebugLogger.LogHeader("Window KeyDown");
        DebugLogger.LogVariable("Key", e.Key);
        DebugLogger.LogVariable("Handled", e.Handled);
        DebugLogger.LogVariable("Source", e.Source?.GetType().Name ?? "<null>");

        // Check if close shortcut was pressed
        var (closeModifiers, closeKey) = _settings.GetCloseShortcut();
        var currentModifiers = 0;
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
            currentModifiers |= (int)GlobalHotkey.MOD_CONTROL;
        if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
            currentModifiers |= (int)GlobalHotkey.MOD_ALT;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
            currentModifiers |= (int)GlobalHotkey.MOD_SHIFT;
        if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0)
            currentModifiers |= (int)GlobalHotkey.MOD_WIN;

        var currentKey = KeyInterop.VirtualKeyFromKey(e.Key);

        if (currentModifiers == closeModifiers && currentKey == closeKey)
        {
            DebugLogger.Log("Window_KeyDown: Close shortcut pressed -> Hiding overlay");
            HideOverlay();
            e.Handled = true;
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                DebugLogger.Log("Window_KeyDown: Enter pressed -> Opening selected project");
                OpenSelectedProject();
                e.Handled = true;
                break;

            case Key.Down:
                if (ResultsList.Items.Count > 0)
                {
                    var newIndex = Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1);
                    DebugLogger.Log($"Window_KeyDown: Down pressed -> Selecting item {newIndex}");
                    ResultsList.SelectedIndex = newIndex;
                    ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                }
                e.Handled = true;
                break;

            case Key.Up:
                if (ResultsList.Items.Count > 0)
                {
                    var newIndex = Math.Max(ResultsList.SelectedIndex - 1, 0);
                    DebugLogger.Log($"Window_KeyDown: Up pressed -> Selecting item {newIndex}");
                    ResultsList.SelectedIndex = newIndex;
                    ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                }
                e.Handled = true;
                break;

            case Key.C when Keyboard.Modifiers == ModifierKeys.Control:
                DebugLogger.Log("Window_KeyDown: Ctrl+C pressed -> Copying path");
                CopySelectedProjectPath();
                e.Handled = true;
                break;
        }
    }

    private async void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultsList.SelectedItem is not ProjectViewModel vm)
            return;

        // Cancel any previous in-flight selection scans (handles fast clicking)
        _selectionCts?.Cancel();
        _selectionCts = new CancellationTokenSource();
        var token = _selectionCts.Token;

        var path = vm.Path;
        var displayName = $"{vm.FullNumber} {vm.Name}";

        // Run both widget scans in parallel instead of sequentially
        var tasks = new List<Task>();

        if (_docOverlay?.Widget != null)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    await _docOverlay.Widget.SetProjectAsync(path, displayName);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    DebugLogger.Log($"ResultsList_SelectionChanged: Error feeding project to doc widget: {ex.Message}");
                }
            }, token));
        }

        if (_smartProjectSearchService != null)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    await _smartProjectSearchService.SetProjectAsync(path, displayName);
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    DebugLogger.Log($"ResultsList_SelectionChanged: Error feeding project to smart search service: {ex.Message}");
                }
            }, token));
        }

        if (_projectInfoOverlay?.Widget != null)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    await Dispatcher.InvokeAsync(async () =>
                        await _projectInfoOverlay.Widget.SetProjectAsync(vm.FullNumber, displayName));
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    DebugLogger.Log($"ResultsList_SelectionChanged: Error feeding project to project info widget: {ex.Message}");
                }
            }, token));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException) { }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenSelectedProject();
    }

    private async void OpenSelectedProject()
    {
        string? itemPath = null;
        string? itemNumber = null;
        string? itemName = null;
        bool isDirectoryResult = true;

        if (ResultsList.SelectedItem is ProjectViewModel vm)
        {
            itemPath = vm.Path;
            itemNumber = vm.FullNumber;
            itemName = vm.Name;
        }
        else if (ResultsList.SelectedItem is PathSearchResultViewModel psvm)
        {
            itemPath = psvm.Path;
            isDirectoryResult = psvm.IsDirectory;
        }

        if (itemPath == null) return;

        try
        {
            // Track search query when project is actually opened
            // Skip tag carousel/tag search queries — the tag carousel already
            // serves as "history" for those, so adding them here causes doubling.
            var query = SearchBox.Text;
            if (!string.IsNullOrWhiteSpace(query) &&
                _lastQuerySource != Core.Models.QuerySources.TagCarousel &&
                _lastQuerySource != Core.Models.QuerySources.TagSearch)
            {
                AddToSearchHistory(query);
            }

            if (isDirectoryResult || Directory.Exists(itemPath))
            {
                // Quote the path to prevent explorer.exe from splitting on commas
                // e.g. "P250870.00 - 2340 Gordon Drive, Naples FL" without quotes
                // would be split at the comma and open the wrong directory
                Process.Start("explorer.exe", $"\"{itemPath}\"");
            }
            else
            {
                Process.Start(new ProcessStartInfo(itemPath) { UseShellExecute = true });
            }

            // Track project launch telemetry
            TelemetryAccessor.TrackProjectLaunch(
                _isPathSearchResults ? "PathSearch" : "ProjectSearch",
                itemNumber,
                projectType: null);

            if (_isPathSearchResults)
            {
                TelemetryAccessor.TrackSearch(
                    TelemetryEventType.PathResultClicked,
                    SearchBox.Text,
                    resultIndex: ResultsList.SelectedIndex,
                    widgetName: "PathSearch",
                    querySource: _lastQuerySource);
            }
            else
            {
                TelemetryAccessor.TrackSearch(
                    TelemetryEventType.SearchResultClicked,
                    SearchBox.Text,
                    resultIndex: ResultsList.SelectedIndex,
                    widgetName: "ProjectSearch",
                    querySource: _lastQuerySource);
            }

            // Record the launch for frequency tracking (only for project results)
            if (_launchDataStore != null && itemNumber != null && itemName != null)
            {
                try
                {
                    await _launchDataStore.RecordLaunchAsync(itemPath, itemNumber, itemName);
                    DebugLogger.Log($"OpenSelectedProject: Recorded launch for {itemNumber}");

                    // Refresh the frequent projects widget if it's open
                    if (_frequentProjectsOverlay?.Widget != null)
                    {
                        await _frequentProjectsOverlay.Widget.RefreshAsync();
                    }
                }
                catch (Exception trackEx)
                {
                    DebugLogger.Log($"OpenSelectedProject: Failed to record launch: {trackEx.Message}");
                }
            }

            // Only hide overlay if NOT in Living Widgets Mode (live widget mode keeps it open)
            var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
            if (!isLivingWidgetsMode)
            {
                HideOverlay();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to open: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );
        }
    }

    private void CopySelectedProjectPath()
    {
        string? path = null;
        if (ResultsList.SelectedItem is ProjectViewModel vm)
            path = vm.Path;
        else if (ResultsList.SelectedItem is PathSearchResultViewModel psvm)
            path = psvm.Path;

        if (path != null)
        {
            try
            {
                System.Windows.Clipboard.SetText(path);
                StatusText.Text = "Path copied to clipboard";
                TelemetryAccessor.TrackClipboardCopy("project_path", "SearchOverlay");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to copy: {ex.Message}";
            }
        }
    }

    private void CopyPathBorder_Click(object sender, MouseButtonEventArgs e)
    {
        string? path = null;
        if (sender is Border border && border.DataContext is ProjectViewModel vm)
            path = vm.Path;
        else if (sender is Border border2 && border2.DataContext is PathSearchResultViewModel psvm)
            path = psvm.Path;

        if (path != null)
        {
            e.Handled = true;
            try
            {
                System.Windows.Clipboard.SetText(path);
                StatusText.Text = "Path copied to clipboard";
                TelemetryAccessor.TrackClipboardCopy("project_path", "SearchOverlay");
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to copy: {ex.Message}";
            }
        }
    }

    private void CopyPathBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = Helpers.ThemeHelper.HoverStrong;
            border.Opacity = 1.0;
        }
    }

    private void CopyPathBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = System.Windows.Media.Brushes.Transparent;
            border.Opacity = 0.6;
        }
    }

    private void ResultsList_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Find the ListBoxItem that was right-clicked
        // Note: OriginalSource can be a Run (ContentElement), not a Visual,
        // so we must use LogicalTreeHelper for non-Visual elements first.
        var element = e.OriginalSource as DependencyObject;
        while (element != null && element is not ListBoxItem)
        {
            element = element is Visual
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        if (element is ListBoxItem item)
        {
            string? itemPath = null;
            string? itemProjectNumber = null;
            string? itemProjectName = null;
            bool isProjectResult = false;
            if (item.DataContext is ProjectViewModel vm)
            {
                ResultsList.SelectedItem = vm;
                itemPath = vm.Path;
                itemProjectNumber = vm.FullNumber;
                itemProjectName = vm.Name;
                isProjectResult = true;
            }
            else if (item.DataContext is PathSearchResultViewModel psvm)
            {
                ResultsList.SelectedItem = psvm;
                itemPath = psvm.Path;
                itemProjectNumber = psvm.FullNumber;
                itemProjectName = psvm.Name;
            }

            if (itemPath != null)
            {
                var capturedPath = itemPath;
                var capturedProjectNumber = itemProjectNumber?.Trim() ?? string.Empty;
                var capturedProjectName = itemProjectName?.Trim() ?? string.Empty;
                var capturedNumberAndName = string.Join(" ", new[] { capturedProjectNumber, capturedProjectName }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));
                var isDirectoryPath = Directory.Exists(capturedPath);
                var isFilePath = !isDirectoryPath && File.Exists(capturedPath);
                var fileExtension = Path.GetExtension(capturedPath)?.TrimStart('.').ToUpperInvariant();
                var openFileHeader = string.IsNullOrWhiteSpace(fileExtension) ? "Open File" : $"Open {fileExtension}";

                void CopyText(string text, string statusMessage)
                {
                    try
                    {
                        System.Windows.Clipboard.SetText(text);
                        StatusText.Text = statusMessage;
                        TelemetryAccessor.TrackClipboardCopy(statusMessage, "SearchOverlay");
                    }
                    catch { }
                }

                var menu = DarkContextMenuFactory.Create();

                if (isProjectResult)
                {
                    var copyNumberItem = new MenuItem { Header = "Copy Project Number", IsEnabled = !string.IsNullOrWhiteSpace(capturedProjectNumber) };
                    copyNumberItem.Click += (s, args) => CopyText(capturedProjectNumber, "Project number copied to clipboard");
                    menu.Items.Add(copyNumberItem);

                    var copyNameItem = new MenuItem { Header = "Copy Project Name", IsEnabled = !string.IsNullOrWhiteSpace(capturedProjectName) };
                    copyNameItem.Click += (s, args) => CopyText(capturedProjectName, "Project name copied to clipboard");
                    menu.Items.Add(copyNameItem);

                    var copyNumberAndNameItem = new MenuItem { Header = "Copy Number + Name", IsEnabled = !string.IsNullOrWhiteSpace(capturedNumberAndName) };
                    copyNumberAndNameItem.Click += (s, args) => CopyText(capturedNumberAndName, "Project number + name copied to clipboard");
                    menu.Items.Add(copyNumberAndNameItem);

                    menu.Items.Add(new Separator());

                    var openItem = new MenuItem { Header = "Open Folder" };
                    openItem.Click += (s, args) =>
                    {
                        try { Process.Start("explorer.exe", capturedPath); }
                        catch { }
                    };
                    menu.Items.Add(openItem);

                    var copyItem = new MenuItem { Header = "Copy Path" };
                    copyItem.Click += (s, args) => CopyText(capturedPath, "Path copied to clipboard");
                    menu.Items.Add(copyItem);

                    // --- Production (UNC) path items ---
                    var uncPath = UncPathHelper.ResolveToUnc(capturedPath);
                    if (uncPath != null)
                    {
                        menu.Items.Add(new Separator());

                        var openProdItem = new MenuItem { Header = "Open Production Path" };
                        openProdItem.Click += (s, args) =>
                        {
                            try { Process.Start("explorer.exe", uncPath); }
                            catch { }
                        };
                        menu.Items.Add(openProdItem);

                        var copyProdItem = new MenuItem { Header = "Copy Production Path" };
                        copyProdItem.Click += (s, args) => CopyText(uncPath, "Production path copied to clipboard");
                        menu.Items.Add(copyProdItem);
                    }

                    // --- Tags section ---
                    if (_tagService != null && !string.IsNullOrEmpty(capturedProjectNumber))
                    {
                        menu.Items.Add(new Separator());
                        AddTagsMenuItems(menu, capturedProjectNumber);
                    }
                }
                else if (isFilePath)
                {
                    var openFileItem = new MenuItem { Header = openFileHeader };
                    openFileItem.Click += (s, args) =>
                    {
                        try { Process.Start(new ProcessStartInfo(capturedPath) { UseShellExecute = true }); }
                        catch { }
                    };
                    menu.Items.Add(openFileItem);

                    var copyItem = new MenuItem { Header = "Copy Path" };
                    copyItem.Click += (s, args) => CopyText(capturedPath, "Path copied to clipboard");
                    menu.Items.Add(copyItem);

                    var openParentItem = new MenuItem { Header = "Open Parent Directory" };
                    openParentItem.Click += (s, args) =>
                    {
                        try { Process.Start("explorer.exe", $"/select,\"{capturedPath}\""); }
                        catch { }
                    };
                    menu.Items.Add(openParentItem);
                }
                else
                {
                    var openItem = new MenuItem { Header = "Open Folder" };
                    openItem.Click += (s, args) =>
                    {
                        try { Process.Start("explorer.exe", capturedPath); }
                        catch { }
                    };
                    menu.Items.Add(openItem);

                    var copyItem = new MenuItem { Header = "Copy Path" };
                    copyItem.Click += (s, args) => CopyText(capturedPath, "Path copied to clipboard");
                    menu.Items.Add(copyItem);
                }

                ResultsList.ContextMenu = menu;
                menu.IsOpen = true;
                e.Handled = true;
            }
        }
    }

    /// <summary>
    /// Add tag-related menu items directly to the context menu.
    /// Always shows "Edit Project Tags..." as a direct-click item.
    /// If the project has existing tags, shows them as read-only items above the edit option.
    /// </summary>
    private void AddTagsMenuItems(ItemsControl menu, string projectNumber)
    {
        if (_tagService == null) return;

        var capturedNumber = projectNumber;

        // Always show direct-click edit item
        var editItem = new MenuItem { Header = "Edit Project Tags..." };
        editItem.Click += (s, args) => OpenTagEditor(capturedNumber);
        menu.Items.Add(editItem);

        // Open in Project Info widget
        var widgetItem = new MenuItem { Header = "Open in Project Info Widget" };
        widgetItem.Click += (s, args) =>
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(async () =>
            {
                try
                {
                    if (_projectInfoOverlay == null)
                        OnProjectInfoRequested(this, EventArgs.Empty);

                    if (_projectInfoOverlay?.Widget != null)
                    {
                        if (_projectInfoOverlay.Visibility != Visibility.Visible)
                        {
                            _projectInfoOverlay.Visibility = Visibility.Visible;
                            _projectInfoOverlay.Tag = "WasVisible";
                        }
                        await _projectInfoOverlay.Widget.SetProjectAsync(capturedNumber, capturedNumber);
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"Tags: Open in widget error: {ex.Message}");
                }
            }));
        };
        menu.Items.Add(widgetItem);
    }

    private void OpenTagEditor(string projectNumber)
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(async () =>
        {
            try
            {
                await ShowTagEditDialog(projectNumber);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"Tags: Edit dialog error: {ex.Message}");
            }
        }));
    }

    /// <summary>
    /// Get non-empty tag fields as (DisplayName, Value) pairs for display.
    /// </summary>
    private static List<(string Label, string Value)> GetNonEmptyTagDisplay(ProjectTags tags)
    {
        var result = new List<(string, string)>();
        void Add(string label, string? value) { if (!string.IsNullOrWhiteSpace(value)) result.Add((label, value)); }

        Add("Voltage", tags.Voltage);
        Add("Phase", tags.Phase);
        Add("Service Amps", tags.AmperageService);
        Add("Generator Amps", tags.AmperageGenerator);
        Add("Generator", tags.GeneratorBrand);
        Add("Generator Load", tags.GeneratorLoadKw != null ? $"{tags.GeneratorLoadKw} kW" : null);
        Add("HVAC Type", tags.HvacType);
        Add("HVAC Brand", tags.HvacBrand);
        Add("Tonnage", tags.HvacTonnage);
        Add("HVAC Load", tags.HvacLoadKw != null ? $"{tags.HvacLoadKw} kW" : null);
        Add("Sq Ft", tags.SquareFootage);
        Add("Build Type", tags.BuildType);
        Add("City", tags.LocationCity);
        Add("State", tags.LocationState);
        Add("Municipality", tags.LocationMunicipality);
        Add("Address", tags.LocationAddress);
        Add("Stamping Engineer", tags.StampingEngineer);
        if (tags.Engineers.Count > 0) result.Add(("Engineers", string.Join(", ", tags.Engineers)));
        if (tags.CodeReferences.Count > 0) result.Add(("Codes", string.Join(", ", tags.CodeReferences)));

        foreach (var (key, value) in tags.Custom)
            Add(key, value);

        return result;
    }

    /// <summary>
    /// Show a modal tag-editing dialog for a project, matching the dark app theme.
    /// Shows existing tags as visual chips, allows adding from available tags dropdown,
    /// and supports adding custom tags.
    /// </summary>
    private async Task ShowTagEditDialog(string projectNumber)
    {
        if (_tagService == null) return;

        var existingTags = await _tagService.GetTagsAsync(projectNumber) ?? new ProjectTags();

        // Collect existing project-info-derived tags (read-only display)
        var infoTags = GetNonEmptyTagDisplay(existingTags);

        // Collect existing custom tags (editable)
        var customTags = new List<KeyValuePair<string, string>>(existingTags.Custom);

        // Collect all available custom tag keys: shared registry + local cache + defaults
        var allCustomKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // From shared tag registry (synced from Firebase, encrypted)
        if (_tagRegistryService != null)
        {
            foreach (var rk in _tagRegistryService.GetAllKeys())
                allCustomKeys.Add(rk);
        }
        // From local cache (covers keys not yet registered)
        foreach (var kvp in _tagService.GetAllCachedTags())
        {
            foreach (var ck in kvp.Value.Custom.Keys)
                allCustomKeys.Add(ck);
        }
        // Add default custom tag suggestions
        foreach (var dt in new[] { "Permit Submitted", "Permit Approved", "As-Built", "Revision Required", "Load Calc Done", "Panel Schedule Done", "Short Circuit Done", "Arc Flash Done", "Energy Calc Done", "Coordination Study" })
            allCustomKeys.Add(dt);

        // --- Build dialog ---
        var accentColor = Helpers.ThemeHelper.AccentColor;
        var accentBrush = Helpers.ThemeHelper.Accent;
        var darkBg = Helpers.ThemeHelper.WindowBackground;
        var cardBg = Helpers.ThemeHelper.SurfaceSolid;
        var inputBg = Helpers.ThemeHelper.Card;
        var inputBorder = Helpers.ThemeHelper.Border;
        var dimText = Helpers.ThemeHelper.TextSecondary;
        var whiteBrush = Helpers.ThemeHelper.TextPrimary;

        var dialog = new Window
        {
            Title = $"Tags — {projectNumber}",
            Width = 420,
            Height = 520,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Owner = Window.GetWindow(this)
        };

        // Root border matching app overlay style — use Dialogs transparency from settings
        var dialogAlpha = (byte)(_settings.GetWidgetTransparency(Core.Models.WidgetIds.Dialogs) * 255);
        var rootBorder = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(dialogAlpha, 
                Helpers.ThemeHelper.GetColor("WindowBackgroundColor").R,
                Helpers.ThemeHelper.GetColor("WindowBackgroundColor").G,
                Helpers.ThemeHelper.GetColor("WindowBackgroundColor").B)),
            CornerRadius = new CornerRadius(12),
            BorderBrush = Helpers.ThemeHelper.Border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16)
        };

        var mainStack = new StackPanel();

        // --- Title bar ---
        var titleBar = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var titleText = new TextBlock
        {
            Text = $"\U0001F3F7\uFE0F Tags — {projectNumber}",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = whiteBrush,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(titleText, 0);
        titleBar.Children.Add(titleText);

        var closeBtn = new System.Windows.Controls.Button
        {
            Content = "\u2715",
            FontSize = 11,
            Width = 28, Height = 28,
            Cursor = System.Windows.Input.Cursors.Hand,
            Foreground = dimText
        };
        closeBtn.Template = CreateDarkButtonTemplate(
            System.Windows.Media.Color.FromArgb(0x00, 0, 0, 0),
            Helpers.ThemeHelper.GetColor("RedBackgroundColor"),
            cornerRadius: 6);
        closeBtn.Click += (s, a) => dialog.Close();
        Grid.SetColumn(closeBtn, 1);
        titleBar.Children.Add(closeBtn);

        // Enable drag on title bar
        titleBar.MouseLeftButtonDown += (s, a) => { try { dialog.DragMove(); } catch { } };

        mainStack.Children.Add(titleBar);

        // --- Project Info Tags (auto-generated, read-only) ---
        if (infoTags.Count > 0)
        {
            var infoLabel = new TextBlock
            {
                Text = "Project Info Tags",
                FontSize = 11,
                Foreground = dimText,
                Margin = new Thickness(0, 0, 0, 6)
            };
            mainStack.Children.Add(infoLabel);

            var infoChipsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            foreach (var (label, value) in infoTags)
            {
                var chip = CreateTagChip($"{label}: {value}", accentColor, isRemovable: false);
                infoChipsPanel.Children.Add(chip);
            }
            mainStack.Children.Add(infoChipsPanel);
        }

        // --- Custom Tags (editable) ---
        var customLabel = new TextBlock
        {
            Text = "Custom Tags",
            FontSize = 11,
            Foreground = dimText,
            Margin = new Thickness(0, 0, 0, 6)
        };
        mainStack.Children.Add(customLabel);

        var customChipsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 8) };

        // Build initial custom tag chips
        void RebuildCustomChips()
        {
            customChipsPanel.Children.Clear();
            foreach (var kvp in customTags)
            {
                var chipText = string.IsNullOrEmpty(kvp.Value) ? kvp.Key : $"{kvp.Key}: {kvp.Value}";
                var capturedKey = kvp.Key;
                var chip = CreateTagChip(chipText, Helpers.ThemeHelper.GreenColor, isRemovable: true, onRemove: () =>
                {
                    customTags.RemoveAll(t => t.Key.Equals(capturedKey, StringComparison.OrdinalIgnoreCase));
                    RebuildCustomChips();
                });
                customChipsPanel.Children.Add(chip);
            }
            if (customTags.Count == 0)
            {
                customChipsPanel.Children.Add(new TextBlock
                {
                    Text = "No custom tags",
                    FontSize = 10,
                    Foreground = Helpers.ThemeHelper.TextTertiary,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(4, 2, 0, 2)
                });
            }
        }
        RebuildCustomChips();
        mainStack.Children.Add(customChipsPanel);

        // --- Add from available tags ---
        var addSectionLabel = new TextBlock
        {
            Text = "Add from available tags",
            FontSize = 11,
            Foreground = dimText,
            Margin = new Thickness(0, 4, 0, 4)
        };
        mainStack.Children.Add(addSectionLabel);

        var addRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var availableCombo = new System.Windows.Controls.ComboBox
        {
            IsEditable = true,
            FontSize = 11,
            Height = 28,
            Margin = new Thickness(0, 0, 6, 0)
        };
        availableCombo.Template = CreateDarkComboBoxTemplate(inputBg, inputBorder);
        availableCombo.Foreground = whiteBrush;
        availableCombo.ItemContainerStyle = CreateDarkComboItemStyle();
        // Populate with available tags not already added
        foreach (var key in allCustomKeys.OrderBy(k => k))
        {
            if (!customTags.Any(t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
                availableCombo.Items.Add(key);
        }
        Grid.SetColumn(availableCombo, 0);
        addRow.Children.Add(availableCombo);

        var addBtn = CreateActionButton("+", Helpers.ThemeHelper.AccentColor);
        Grid.SetColumn(addBtn, 1);
        addBtn.Click += (s, a) =>
        {
            var tagName = availableCombo.Text?.Trim();
            if (string.IsNullOrEmpty(tagName)) return;
            if (customTags.Any(t => t.Key.Equals(tagName, StringComparison.OrdinalIgnoreCase))) return;
            customTags.Add(new KeyValuePair<string, string>(tagName, ""));
            availableCombo.Items.Remove(tagName);
            availableCombo.Text = "";
            RebuildCustomChips();
        };
        addRow.Children.Add(addBtn);
        mainStack.Children.Add(addRow);

        // --- Add new custom tag ---
        var newTagLabel = new TextBlock
        {
            Text = "New custom tag",
            FontSize = 11,
            Foreground = dimText,
            Margin = new Thickness(0, 4, 0, 4)
        };
        mainStack.Children.Add(newTagLabel);

        var newTagRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
        newTagRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        newTagRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        newTagRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var newKeyBox = new System.Windows.Controls.TextBox
        {
            Background = inputBg,
            Foreground = whiteBrush,
            BorderBrush = inputBorder,
            FontSize = 11,
            Height = 28,
            Padding = new Thickness(6, 3, 6, 3),
            CaretBrush = whiteBrush,
            Margin = new Thickness(0, 0, 4, 0)
        };
        // Watermark via Tag
        newKeyBox.Tag = "Tag name";
        newKeyBox.Foreground = dimText;
        newKeyBox.Text = "Tag name";
        newKeyBox.GotFocus += (s, a) => { if (newKeyBox.Text == "Tag name") { newKeyBox.Text = ""; newKeyBox.Foreground = whiteBrush; } };
        newKeyBox.LostFocus += (s, a) => { if (string.IsNullOrWhiteSpace(newKeyBox.Text)) { newKeyBox.Text = "Tag name"; newKeyBox.Foreground = dimText; } };
        Grid.SetColumn(newKeyBox, 0);
        newTagRow.Children.Add(newKeyBox);

        var newValBox = new System.Windows.Controls.TextBox
        {
            Background = inputBg,
            Foreground = whiteBrush,
            BorderBrush = inputBorder,
            FontSize = 11,
            Height = 28,
            Padding = new Thickness(6, 3, 6, 3),
            CaretBrush = whiteBrush,
            Margin = new Thickness(0, 0, 6, 0)
        };
        newValBox.Tag = "Value (optional)";
        newValBox.Foreground = dimText;
        newValBox.Text = "Value (optional)";
        newValBox.GotFocus += (s, a) => { if (newValBox.Text == "Value (optional)") { newValBox.Text = ""; newValBox.Foreground = whiteBrush; } };
        newValBox.LostFocus += (s, a) => { if (string.IsNullOrWhiteSpace(newValBox.Text)) { newValBox.Text = "Value (optional)"; newValBox.Foreground = dimText; } };
        Grid.SetColumn(newValBox, 1);
        newTagRow.Children.Add(newValBox);

        var newAddBtn = CreateActionButton("+", Helpers.ThemeHelper.GreenColor);
        Grid.SetColumn(newAddBtn, 2);
        newAddBtn.Click += (s, a) =>
        {
            var k = newKeyBox.Text?.Trim();
            if (string.IsNullOrEmpty(k) || k == "Tag name") return;
            if (customTags.Any(t => t.Key.Equals(k, StringComparison.OrdinalIgnoreCase))) return;
            var v = (newValBox.Text?.Trim() == "Value (optional)") ? "" : (newValBox.Text?.Trim() ?? "");
            customTags.Add(new KeyValuePair<string, string>(k, v));
            newKeyBox.Text = "Tag name"; newKeyBox.Foreground = dimText;
            newValBox.Text = "Value (optional)"; newValBox.Foreground = dimText;
            RebuildCustomChips();
        };
        newTagRow.Children.Add(newAddBtn);
        mainStack.Children.Add(newTagRow);

        // --- Save / Close buttons ---
        var buttonPanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };

        var saveButton = CreateActionButton("Save", Helpers.ThemeHelper.AccentColor, width: 80);
        var cancelButton = CreateActionButton("Close", Helpers.ThemeHelper.GetColor("ToggleOffColor"), width: 80);
        cancelButton.Margin = new Thickness(8, 0, 0, 0);
        cancelButton.Click += (s, a) => dialog.Close();

        var capturedProjectNumber = projectNumber;
        saveButton.Click += async (s, args) =>
        {
            // Preserve existing structured field values
            var newTags = existingTags;
            newTags.Custom.Clear();
            foreach (var kvp in customTags)
            {
                if (!string.IsNullOrEmpty(kvp.Key))
                    newTags.Custom[kvp.Key] = kvp.Value;
            }

            var isNew = !_tagService!.HasTags(capturedProjectNumber);
            await _tagService.SaveTagsAsync(capturedProjectNumber, newTags);

            // Register any new custom tag keys to the shared registry
            if (_tagRegistryService != null && newTags.Custom.Count > 0)
                await _tagRegistryService.RegisterKeysAsync(newTags.Custom.Keys);

            // Refresh local vocabulary with updated cache
            _vocabService?.RefreshFromCache(_tagService.GetAllCachedTags());

            TelemetryAccessor.TrackTag(
                isNew ? TelemetryEventType.TagCreated : TelemetryEventType.TagUpdated,
                projectNumber: capturedProjectNumber,
                tagCount: customTags.Count);

            RefreshTagCarousel();
            StatusText.Text = $"Tags saved for {capturedProjectNumber}";
            dialog.Close();
        };

        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(cancelButton);
        mainStack.Children.Add(buttonPanel);

        rootBorder.Child = mainStack;
        dialog.Content = rootBorder;
        dialog.ShowDialog();
    }

    /// <summary>
    /// Create a rounded tag chip with optional remove button.
    /// </summary>
    private static Border CreateTagChip(string text, System.Windows.Media.Color color, bool isRemovable = false, Action? onRemove = null)
    {
        var chipBg = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, color.R, color.G, color.B));
        var chipFg = new SolidColorBrush(color);

        var chipStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        chipStack.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = chipFg,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = 320
        });

        if (isRemovable && onRemove != null)
        {
            var removeBtn = new TextBlock
            {
                Text = " \u2715",
                FontSize = 9,
                Foreground = Helpers.ThemeHelper.Red,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(2, 0, 0, 0)
            };
            removeBtn.MouseLeftButtonDown += (s, e) => { onRemove(); e.Handled = true; };
            chipStack.Children.Add(removeBtn);
        }

        return new Border
        {
            Background = chipBg,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 3, 8, 3),
            Margin = new Thickness(0, 0, 4, 4),
            Child = chipStack
        };
    }

    /// <summary>
    /// Create a styled dark-themed action button with rounded template.
    /// </summary>
    private static System.Windows.Controls.Button CreateActionButton(string content, System.Windows.Media.Color bgColor, double width = 28)
    {
        var hoverColor = System.Windows.Media.Color.FromArgb(255,
            (byte)Math.Min(255, bgColor.R + 30),
            (byte)Math.Min(255, bgColor.G + 30),
            (byte)Math.Min(255, bgColor.B + 30));
        var btn = new System.Windows.Controls.Button
        {
            Content = content,
            Width = width,
            Height = 28,
            FontSize = 11,
            Foreground = Helpers.ThemeHelper.TextOnAccent,
            Cursor = System.Windows.Input.Cursors.Hand,
            Padding = new Thickness(4, 2, 4, 2)
        };
        btn.Template = CreateDarkButtonTemplate(bgColor, hoverColor, cornerRadius: 6);
        return btn;
    }

    /// <summary>
    /// Create a ControlTemplate for a dark-themed button with hover effect.
    /// </summary>
    private static ControlTemplate CreateDarkButtonTemplate(
        System.Windows.Media.Color bgColor, System.Windows.Media.Color hoverColor, double cornerRadius = 6)
    {
        var template = new ControlTemplate(typeof(System.Windows.Controls.Button));
        var borderFactory = new FrameworkElementFactory(typeof(Border), "Bd");
        borderFactory.SetValue(Border.BackgroundProperty, new SolidColorBrush(bgColor));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(4, 2, 4, 2));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
        borderFactory.AppendChild(contentFactory);

        template.VisualTree = borderFactory;

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(hoverColor), "Bd"));
        template.Triggers.Add(hoverTrigger);

        return template;
    }

    /// <summary>
    /// Create a dark-themed ComboBox ControlTemplate matching the app-wide DarkCombo style.
    /// Matches ProjectInfoWidget.xaml and Settings dialog patterns: CornerRadius 4, arrow pill,
    /// accent border on hover/checked, dark popup with rounded corners.
    /// </summary>
    private static ControlTemplate CreateDarkComboBoxTemplate(System.Windows.Media.Brush inputBg, System.Windows.Media.Brush inputBorder)
    {
        var accentBrush = Helpers.ThemeHelper.Accent;
        var hoverBg = Helpers.ThemeHelper.HoverMedium;

        var template = new ControlTemplate(typeof(System.Windows.Controls.ComboBox));
        var gridFactory = new FrameworkElementFactory(typeof(Grid));

        // --- ToggleButton with inner template ---
        var toggleTemplate = new ControlTemplate(typeof(System.Windows.Controls.Primitives.ToggleButton));
        var toggleBorder = new FrameworkElementFactory(typeof(Border), "Bd");
        toggleBorder.SetValue(Border.BackgroundProperty, inputBg);
        toggleBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        toggleBorder.SetValue(Border.BorderBrushProperty, inputBorder);
        toggleBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        toggleBorder.SetValue(Border.PaddingProperty, new Thickness(6, 0, 6, 0));

        var toggleGrid = new FrameworkElementFactory(typeof(Grid));
        var col1 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col1.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
        var col2 = new FrameworkElementFactory(typeof(ColumnDefinition));
        col2.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
        toggleGrid.AppendChild(col1);
        toggleGrid.AppendChild(col2);

        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(Grid.ColumnProperty, 0);
        toggleGrid.AppendChild(contentPresenter);

        // Arrow inside a pill border (matches DarkCombo style)
        var arrowPill = new FrameworkElementFactory(typeof(Border));
        arrowPill.SetValue(Border.BackgroundProperty, Helpers.ThemeHelper.HoverStrong);
        arrowPill.SetValue(Border.CornerRadiusProperty, new CornerRadius(2));
        arrowPill.SetValue(Border.PaddingProperty, new Thickness(4, 1, 4, 1));
        arrowPill.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 0, 0));
        arrowPill.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        arrowPill.SetValue(Grid.ColumnProperty, 1);

        var arrowText = new FrameworkElementFactory(typeof(TextBlock));
        arrowText.SetValue(TextBlock.TextProperty, "\u25BE");
        arrowText.SetValue(TextBlock.ForegroundProperty, Helpers.ThemeHelper.TextSecondary);
        arrowText.SetValue(TextBlock.FontSizeProperty, 9.0);
        arrowText.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        arrowText.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
        arrowPill.AppendChild(arrowText);
        toggleGrid.AppendChild(arrowPill);

        toggleBorder.AppendChild(toggleGrid);
        toggleTemplate.VisualTree = toggleBorder;

        // Hover: accent border + slightly lighter background
        var toggleHover = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        toggleHover.Setters.Add(new Setter(Border.BorderBrushProperty, accentBrush, "Bd"));
        toggleHover.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "Bd"));
        toggleTemplate.Triggers.Add(toggleHover);

        // Checked (dropdown open): accent border
        var toggleChecked = new Trigger { Property = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Value = true };
        toggleChecked.Setters.Add(new Setter(Border.BorderBrushProperty, accentBrush, "Bd"));
        toggleTemplate.Triggers.Add(toggleChecked);

        var toggleFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.ToggleButton));
        toggleFactory.SetBinding(System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty,
            new System.Windows.Data.Binding("IsDropDownOpen")
            {
                Mode = System.Windows.Data.BindingMode.TwoWay,
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });
        toggleFactory.SetValue(System.Windows.Controls.Primitives.ToggleButton.FocusableProperty, false);
        toggleFactory.SetValue(System.Windows.Controls.Primitives.ToggleButton.ClickModeProperty, ClickMode.Press);
        toggleFactory.SetValue(System.Windows.Controls.Primitives.ToggleButton.TemplateProperty, toggleTemplate);
        gridFactory.AppendChild(toggleFactory);

        // --- Editable TextBox ---
        var textBox = new FrameworkElementFactory(typeof(System.Windows.Controls.TextBox), "PART_EditableTextBox");
        textBox.SetValue(System.Windows.Controls.TextBox.IsReadOnlyProperty, false);
        textBox.SetValue(UIElement.VisibilityProperty, Visibility.Visible);
        textBox.SetValue(System.Windows.Controls.Control.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        textBox.SetValue(System.Windows.Controls.Control.ForegroundProperty, new SolidColorBrush(Colors.White));
        textBox.SetValue(System.Windows.Controls.Control.FontSizeProperty, 11.0);
        textBox.SetValue(System.Windows.Controls.Control.BorderThicknessProperty, new Thickness(0));
        textBox.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 28, 0));
        textBox.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        textBox.SetValue(System.Windows.Controls.TextBox.CaretBrushProperty, new SolidColorBrush(Colors.White));
        gridFactory.AppendChild(textBox);

        // --- Popup dropdown ---
        var popup = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Popup), "Popup");
        popup.SetValue(System.Windows.Controls.Primitives.Popup.PlacementProperty, System.Windows.Controls.Primitives.PlacementMode.Bottom);
        popup.SetBinding(System.Windows.Controls.Primitives.Popup.IsOpenProperty,
            new System.Windows.Data.Binding("IsDropDownOpen")
            { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
        popup.SetValue(System.Windows.Controls.Primitives.Popup.AllowsTransparencyProperty, true);
        popup.SetValue(System.Windows.Controls.Primitives.Popup.FocusableProperty, false);

        var popupBorder = new FrameworkElementFactory(typeof(Border));
        popupBorder.SetValue(Border.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x22, 0x22, 0x28)));
        popupBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        popupBorder.SetValue(Border.BorderBrushProperty, inputBorder);
        popupBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        popupBorder.SetValue(Border.PaddingProperty, new Thickness(3));
        popupBorder.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 0));
        popupBorder.SetValue(Border.MaxHeightProperty, 200.0);

        var scrollViewer = new FrameworkElementFactory(typeof(ScrollViewer));
        scrollViewer.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        var itemsHost = new FrameworkElementFactory(typeof(StackPanel));
        itemsHost.SetValue(StackPanel.IsItemsHostProperty, true);
        scrollViewer.AppendChild(itemsHost);
        popupBorder.AppendChild(scrollViewer);
        popup.AppendChild(popupBorder);
        gridFactory.AppendChild(popup);

        // --- Disabled state ---
        var disabledTrigger = new Trigger { Property = UIElement.IsEnabledProperty, Value = false };
        disabledTrigger.Setters.Add(new Setter(UIElement.OpacityProperty, 0.5));
        template.Triggers.Add(disabledTrigger);

        template.VisualTree = gridFactory;
        return template;
    }

    /// <summary>
    /// Create an ItemContainerStyle for ComboBoxItems matching the app-wide DarkComboItem style.
    /// </summary>
    private static Style CreateDarkComboItemStyle()
    {
        var style = new Style(typeof(ComboBoxItem));
        style.Setters.Add(new Setter(ComboBoxItem.ForegroundProperty, new SolidColorBrush(Colors.White)));
        style.Setters.Add(new Setter(ComboBoxItem.BackgroundProperty, System.Windows.Media.Brushes.Transparent));
        style.Setters.Add(new Setter(ComboBoxItem.PaddingProperty, new Thickness(10, 6, 10, 6)));
        style.Setters.Add(new Setter(ComboBoxItem.CursorProperty, System.Windows.Input.Cursors.Hand));

        var itemTemplate = new ControlTemplate(typeof(ComboBoxItem));
        var borderFactory = new FrameworkElementFactory(typeof(Border), "Bd");
        borderFactory.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(3));
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 6, 10, 6));

        var cpFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        borderFactory.AppendChild(cpFactory);
        itemTemplate.VisualTree = borderFactory;

        var highlightTrigger = new Trigger { Property = ComboBoxItem.IsHighlightedProperty, Value = true };
        highlightTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 122, 204)), "Bd"));
        itemTemplate.Triggers.Add(highlightTrigger);

        var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x28, 255, 255, 255)), "Bd"));
        itemTemplate.Triggers.Add(hoverTrigger);

        style.Setters.Add(new Setter(ComboBoxItem.TemplateProperty, itemTemplate));
        return style;
    }

    /// <summary>
    /// Get the text value from a ComboBox or TextBox control.
    /// </summary>
    private static string GetControlText(System.Windows.Controls.Control control)
    {
        return control switch
        {
            System.Windows.Controls.ComboBox combo => combo.Text ?? "",
            System.Windows.Controls.TextBox tb => tb.Text ?? "",
            _ => ""
        };
    }

    /// <summary>
    /// Set a tag field on a ProjectTags instance by canonical key.
    /// </summary>
    private static void SetTagField(ProjectTags tags, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "voltage": tags.Voltage = value; break;
            case "phase": tags.Phase = value; break;
            case "amperage_service": tags.AmperageService = value; break;
            case "amperage_generator": tags.AmperageGenerator = value; break;
            case "generator_brand": tags.GeneratorBrand = value; break;
            case "generator_load_kw": tags.GeneratorLoadKw = value; break;
            case "hvac_type": tags.HvacType = value; break;
            case "hvac_brand": tags.HvacBrand = value; break;
            case "hvac_tonnage": tags.HvacTonnage = value; break;
            case "hvac_load_kw": tags.HvacLoadKw = value; break;
            case "square_footage": tags.SquareFootage = value; break;
            case "build_type": tags.BuildType = value; break;
            case "location_city": tags.LocationCity = value; break;
            case "location_state": tags.LocationState = value; break;
            case "location_municipality": tags.LocationMunicipality = value; break;
            case "location_address": tags.LocationAddress = value; break;
            case "stamping_engineer": tags.StampingEngineer = value; break;
            case "engineers":
                tags.Engineers = value.Split(',', StringSplitOptions.TrimEntries).Where(s => s.Length > 0).ToList();
                break;
            case "code_refs":
                tags.CodeReferences = value.Split(',', StringSplitOptions.TrimEntries).Where(s => s.Length > 0).ToList();
                break;
            default:
                tags.Custom[key] = value;
                break;
        }
    }

}
