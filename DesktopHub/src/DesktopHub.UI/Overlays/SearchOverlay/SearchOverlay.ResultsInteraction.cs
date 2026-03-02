using System;
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

        public ProjectViewModel(Project project)
        {
            FullNumber = project.FullNumber;
            Name = project.Name;
            Path = project.Path;
            Location = project.Metadata?.Location;
            Status = project.Metadata?.Status;
            IsFavorite = project.Metadata?.IsFavorite ?? false;
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
                Process.Start("explorer.exe", itemPath);
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
                    widgetName: "PathSearch");
            }
            else
            {
                TelemetryAccessor.TrackSearch(
                    TelemetryEventType.SearchResultClicked,
                    SearchBox.Text,
                    resultIndex: ResultsList.SelectedIndex,
                    widgetName: "ProjectSearch");
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
                    }
                    catch { }
                }

                var menu = CreateDarkContextMenu();

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

    private static ContextMenu CreateDarkContextMenu()
    {
        var menuBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
        var menuBorder = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        var itemFg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0));
        var hoverBg = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0x4F, 0xC3, 0xF7));
        var transparentBrush = System.Windows.Media.Brushes.Transparent;

        // Build a MenuItem ControlTemplate that fully replaces WPF default chrome
        var itemTemplate = new ControlTemplate(typeof(MenuItem));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "Bd";
        borderFactory.SetValue(Border.BackgroundProperty, transparentBrush);
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 6, 10, 6));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFactory.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(contentFactory);
        itemTemplate.VisualTree = borderFactory;

        // Hover trigger
        var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "Bd"));
        itemTemplate.Triggers.Add(hoverTrigger);

        // MenuItem style using the custom template
        var itemStyle = new Style(typeof(MenuItem));
        itemStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty, itemFg));
        itemStyle.Setters.Add(new Setter(MenuItem.TemplateProperty, itemTemplate));
        itemStyle.Setters.Add(new Setter(MenuItem.CursorProperty, System.Windows.Input.Cursors.Hand));

        // ContextMenu with custom template to remove system chrome
        var contextMenuTemplate = new ControlTemplate(typeof(ContextMenu));
        var menuBorderFactory = new FrameworkElementFactory(typeof(Border));
        menuBorderFactory.SetValue(Border.BackgroundProperty, menuBg);
        menuBorderFactory.SetValue(Border.BorderBrushProperty, menuBorder);
        menuBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        menuBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        menuBorderFactory.SetValue(Border.PaddingProperty, new Thickness(2, 4, 2, 4));

        var shadowEffect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 12,
            ShadowDepth = 2,
            Opacity = 0.5,
            Color = System.Windows.Media.Color.FromRgb(0, 0, 0)
        };
        menuBorderFactory.SetValue(Border.EffectProperty, shadowEffect);

        var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        menuBorderFactory.AppendChild(itemsPresenter);
        contextMenuTemplate.VisualTree = menuBorderFactory;

        var menu = new ContextMenu
        {
            Template = contextMenuTemplate,
            HasDropShadow = false
        };
        menu.Resources[typeof(MenuItem)] = itemStyle;

        return menu;
    }
}
