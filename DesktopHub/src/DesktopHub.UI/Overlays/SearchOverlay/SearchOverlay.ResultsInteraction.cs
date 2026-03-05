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
            var query = SearchBox.Text;
            if (!string.IsNullOrWhiteSpace(query))
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
            border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
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

                    // --- Tags submenu ---
                    if (_tagService != null && !string.IsNullOrEmpty(capturedProjectNumber))
                    {
                        menu.Items.Add(new Separator());
                        var tagsSubmenu = BuildTagsSubmenu(capturedProjectNumber);
                        menu.Items.Add(tagsSubmenu);
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
    /// Build a "Tags" submenu showing existing tags and an "Edit Tags..." option.
    /// </summary>
    private MenuItem BuildTagsSubmenu(string projectNumber)
    {
        var tagsMenu = new MenuItem { Header = "Tags" };

        if (_tagService == null)
        {
            tagsMenu.IsEnabled = false;
            return tagsMenu;
        }

        var tags = _tagService.GetAllCachedTags()
            .FirstOrDefault(kvp => kvp.Key.Equals(projectNumber, StringComparison.OrdinalIgnoreCase));

        if (tags.Value != null)
        {
            // Show existing tags as read-only items
            var existingTags = GetNonEmptyTagDisplay(tags.Value);
            if (existingTags.Count > 0)
            {
                foreach (var (label, value) in existingTags)
                {
                    var tagItem = new MenuItem
                    {
                        Header = $"{label}: {value}",
                        IsEnabled = false,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 180))
                    };
                    tagsMenu.Items.Add(tagItem);
                }
                tagsMenu.Items.Add(new Separator());
            }
        }

        // "Edit Tags..." opens the edit dialog
        var editItem = new MenuItem { Header = "Edit Tags..." };
        var capturedNumber = projectNumber;
        editItem.Click += async (s, args) =>
        {
            await ShowTagEditDialog(capturedNumber);
        };
        tagsMenu.Items.Add(editItem);

        return tagsMenu;
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
    /// Show a modal dialog for editing tags on a project.
    /// </summary>
    private async Task ShowTagEditDialog(string projectNumber)
    {
        if (_tagService == null) return;

        var existingTags = await _tagService.GetTagsAsync(projectNumber) ?? new ProjectTags();

        var dialog = new Window
        {
            Title = $"Edit Tags — {projectNumber}",
            Width = 480,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)),
            Foreground = new SolidColorBrush(Colors.White),
            ResizeMode = ResizeMode.CanResizeWithGrip,
            WindowStyle = WindowStyle.ToolWindow
        };

        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Margin = new Thickness(12)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var fields = TagFieldRegistry.Fields;
        var textBoxes = new Dictionary<string, System.Windows.Controls.TextBox>();
        var row = 0;

        // Group by category
        var categories = fields.GroupBy(f => f.Category ?? "Other").ToList();
        foreach (var category in categories)
        {
            // Category header
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var header = new TextBlock
            {
                Text = category.Key,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 180, 255)),
                Margin = new Thickness(0, row == 0 ? 0 : 12, 0, 4)
            };
            Grid.SetRow(header, row);
            Grid.SetColumnSpan(header, 2);
            grid.Children.Add(header);
            row++;

            foreach (var field in category)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var label = new TextBlock
                {
                    Text = field.DisplayName,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200)),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 2, 8, 2)
                };
                Grid.SetRow(label, row);
                Grid.SetColumn(label, 0);
                grid.Children.Add(label);

                var currentValue = _tagService.GetTagValue(projectNumber, field.Key) ?? "";
                var textBox = new System.Windows.Controls.TextBox
                {
                    Text = currentValue,
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 50)),
                    Foreground = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
                    Padding = new Thickness(4, 2, 4, 2),
                    Margin = new Thickness(0, 2, 0, 2),
                    ToolTip = field.SuggestedValues.Length > 0
                        ? $"Suggested: {string.Join(", ", field.SuggestedValues)}"
                        : null
                };
                Grid.SetRow(textBox, row);
                Grid.SetColumn(textBox, 1);
                grid.Children.Add(textBox);

                textBoxes[field.Key] = textBox;
                row++;
            }
        }

        // Custom tags section
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var customHeader = new TextBlock
        {
            Text = "Custom Tags (key=value, one per line)",
            FontWeight = FontWeights.Bold,
            FontSize = 14,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 180, 255)),
            Margin = new Thickness(0, 12, 0, 4)
        };
        Grid.SetRow(customHeader, row);
        Grid.SetColumnSpan(customHeader, 2);
        grid.Children.Add(customHeader);
        row++;

        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(80) });
        var customText = new System.Windows.Controls.TextBox
        {
            Text = string.Join("\n", existingTags.Custom.Select(kv => $"{kv.Key}={kv.Value}")),
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 50)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80)),
            Padding = new Thickness(4),
            Margin = new Thickness(0, 2, 0, 2),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        Grid.SetRow(customText, row);
        Grid.SetColumnSpan(customText, 2);
        grid.Children.Add(customText);
        row++;

        // Save / Cancel buttons
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var buttonPanel = new StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };

        var saveButton = new System.Windows.Controls.Button
        {
            Content = "Save",
            Width = 80,
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 8, 0),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(40, 120, 200)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 140, 220))
        };

        var cancelButton = new System.Windows.Controls.Button
        {
            Content = "Cancel",
            Width = 80,
            Padding = new Thickness(8, 4, 8, 4),
            Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60)),
            Foreground = new SolidColorBrush(Colors.White),
            BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80))
        };

        cancelButton.Click += (s, args) => dialog.Close();

        var capturedProjectNumber = projectNumber;
        saveButton.Click += async (s, args) =>
        {
            var newTags = new ProjectTags();
            foreach (var (key, tb) in textBoxes)
            {
                var val = tb.Text.Trim();
                if (string.IsNullOrEmpty(val)) continue;
                SetTagField(newTags, key, val);
            }

            // Parse custom tags
            foreach (var line in customText.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var eqIdx = line.IndexOf('=');
                if (eqIdx > 0)
                {
                    var k = line[..eqIdx].Trim();
                    var v = line[(eqIdx + 1)..].Trim();
                    if (!string.IsNullOrEmpty(k) && !string.IsNullOrEmpty(v))
                        newTags.Custom[k] = v;
                }
            }

            await _tagService!.SaveTagsAsync(capturedProjectNumber, newTags);
            StatusText.Text = $"Tags saved for {capturedProjectNumber}";
            dialog.Close();
        };

        buttonPanel.Children.Add(saveButton);
        buttonPanel.Children.Add(cancelButton);
        Grid.SetRow(buttonPanel, row);
        Grid.SetColumnSpan(buttonPanel, 2);
        grid.Children.Add(buttonPanel);

        scrollViewer.Content = grid;
        dialog.Content = scrollViewer;
        dialog.ShowDialog();
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
