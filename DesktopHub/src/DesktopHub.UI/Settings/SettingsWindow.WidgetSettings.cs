using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopHub.Core.Models;
using DesktopHub.UI.Services;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

// Widget-specific settings: Quick Tasks, Doc Quick Open, Smart Project Search, Metrics, Updates
public partial class SettingsWindow
{
    // ===== Quick Tasks Settings =====

    private void LoadQuickTasksSettings()
    {
        if (_taskService == null) return;

        _isLoadingQTSettings = true;
        try
        {
            var config = _taskService.Config;
            QT_ShowCompletedToggle.IsChecked = config.ShowCompletedTasks;
            QT_CompactModeToggle.IsChecked = config.CompactMode;
            QT_AutoCarryOverToggle.IsChecked = config.AutoCarryOver;
            QT_CompletedOpacitySlider.Value = config.CompletedOpacity;

            // Default priority
            QT_PriorityLow.IsChecked = config.DefaultPriority == "low";
            QT_PriorityNormal.IsChecked = config.DefaultPriority == "normal";
            QT_PriorityHigh.IsChecked = config.DefaultPriority == "high";

            // Sort mode
            QT_SortManual.IsChecked = config.SortBy == "manual";
            QT_SortPriority.IsChecked = config.SortBy == "priority";
            QT_SortCreated.IsChecked = config.SortBy == "created";

            // Categories
            RenderCategoriesList();
        }
        finally
        {
            _isLoadingQTSettings = false;
        }
    }

    private void RenderCategoriesList()
    {
        if (_taskService == null || QT_CategoriesList == null) return;

        QT_CategoriesList.Children.Clear();
        foreach (var category in _taskService.Config.Categories)
        {
            var cat = category;
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new System.Windows.Controls.TextBlock
            {
                Text = cat,
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            label.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "TextBrush");
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var removeBtn = new System.Windows.Controls.Button
            {
                Content = "\u2715",
                Width = 26,
                Height = 26,
                FontSize = 11,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            removeBtn.SetResourceReference(System.Windows.Controls.Button.BackgroundProperty, "CardBorderBrush");
            removeBtn.SetResourceReference(System.Windows.Controls.Button.ForegroundProperty, "RedBrush");
            // Apply rounded template
            var btnTemplate = new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.Button));
            var btnBorder = new FrameworkElementFactory(typeof(Border));
            btnBorder.SetResourceReference(Border.BackgroundProperty, "CardBorderBrush");
            btnBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            var btnContent = new FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
            btnContent.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            btnContent.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            btnBorder.AppendChild(btnContent);
            btnTemplate.VisualTree = btnBorder;
            removeBtn.Template = btnTemplate;
            removeBtn.Click += async (s, ev) =>
            {
                _taskService.Config.Categories.Remove(cat);
                await _taskService.ApplyConfigAsync();
                RenderCategoriesList();
                StatusText.Text = $"Category '{cat}' removed";
            };
            Grid.SetColumn(removeBtn, 1);
            row.Children.Add(removeBtn);

            QT_CategoriesList.Children.Add(row);
        }
    }

    private async void QT_ShowCompletedToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        _taskService.Config.ShowCompletedTasks = QT_ShowCompletedToggle.IsChecked == true;
        await _taskService.ApplyConfigAsync();
        StatusText.Text = _taskService.Config.ShowCompletedTasks ? "Completed tasks visible" : "Completed tasks hidden";
    }

    private async void QT_CompactModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        _taskService.Config.CompactMode = QT_CompactModeToggle.IsChecked == true;
        await _taskService.ApplyConfigAsync();
        StatusText.Text = _taskService.Config.CompactMode ? "Compact mode enabled" : "Compact mode disabled";
    }

    private async void QT_AutoCarryOverToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        var enabled = QT_AutoCarryOverToggle.IsChecked == true;
        await _taskService.SetAutoCarryOverAsync(enabled);
        StatusText.Text = enabled ? "Auto carry-over enabled" : "Auto carry-over disabled — incomplete carry-overs removed";
    }

    private async void QT_CompletedOpacitySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        _taskService.Config.CompletedOpacity = QT_CompletedOpacitySlider.Value;
        await _taskService.ApplyConfigAsync();
    }

    private async void QT_DefaultPriority_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        if (QT_PriorityLow.IsChecked == true) _taskService.Config.DefaultPriority = "low";
        else if (QT_PriorityHigh.IsChecked == true) _taskService.Config.DefaultPriority = "high";
        else _taskService.Config.DefaultPriority = "normal";
        await _taskService.ApplyConfigAsync();
        StatusText.Text = $"Default priority: {_taskService.Config.DefaultPriority}";
    }

    private async void QT_SortMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        if (QT_SortPriority.IsChecked == true) _taskService.Config.SortBy = "priority";
        else if (QT_SortCreated.IsChecked == true) _taskService.Config.SortBy = "created";
        else _taskService.Config.SortBy = "manual";
        await _taskService.ApplyConfigAsync();
        StatusText.Text = $"Sort order: {_taskService.Config.SortBy}";
    }

    private async void QT_AddCategory_Click(object sender, RoutedEventArgs e)
    {
        await AddNewCategory();
    }

    private async void QT_NewCategoryInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await AddNewCategory();
            e.Handled = true;
        }
    }

    private async Task AddNewCategory()
    {
        if (_taskService == null) return;
        var name = QT_NewCategoryInput.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (_taskService.Config.Categories.Contains(name)) return;

        _taskService.Config.Categories.Add(name);
        await _taskService.ApplyConfigAsync();
        QT_NewCategoryInput.Text = "";
        RenderCategoriesList();
        StatusText.Text = $"Category '{name}' added";
    }

    // ========== Doc Quick Open Settings ==========

    private void LoadDocQuickOpenSettings()
    {
        if (_docService == null) return;
        _isLoadingDQSettings = true;
        try
        {
            var cfg = _docService.Config;
            DQ_ShowFileSizeToggle.IsChecked = cfg.ShowFileSize;
            DQ_ShowDateModifiedToggle.IsChecked = cfg.ShowDateModified;
            DQ_ShowFileExtToggle.IsChecked = cfg.ShowFileExtension;
            DQ_CompactModeToggle.IsChecked = cfg.CompactMode;
            DQ_MaxDepthSlider.Value = cfg.MaxDepth;
            DQ_MaxDepthValue.Text = cfg.MaxDepth.ToString();
            DQ_MaxFilesSlider.Value = cfg.MaxFiles;
            DQ_MaxFilesValue.Text = cfg.MaxFiles.ToString();
            DQ_ExtensionsInput.Text = string.Join(", ", cfg.Extensions);
            DQ_ExcludedFoldersInput.Text = string.Join(", ", cfg.ExcludedFolders);
            DQ_AutoOpenToggle.IsChecked = cfg.AutoOpenLastProject;
            DQ_RecentCountSlider.Value = cfg.RecentFilesCount;
            DQ_RecentCountValue.Text = cfg.RecentFilesCount.ToString();

            switch (cfg.SortBy)
            {
                case "date": DQ_SortDate.IsChecked = true; break;
                case "type": DQ_SortType.IsChecked = true; break;
                case "size": DQ_SortSize.IsChecked = true; break;
                default: DQ_SortName.IsChecked = true; break;
            }

            switch (cfg.GroupBy)
            {
                case "category": DQ_GroupCategory.IsChecked = true; break;
                case "extension": DQ_GroupExt.IsChecked = true; break;
                case "subfolder": DQ_GroupSubfolder.IsChecked = true; break;
                default: DQ_GroupNone.IsChecked = true; break;
            }
        }
        finally
        {
            _isLoadingDQSettings = false;
        }
    }

    private async void DQ_ShowFileSizeToggle_Changed(object sender, RoutedEventArgs e) { if (_isLoadingDQSettings || _docService == null) return; _docService.Config.ShowFileSize = DQ_ShowFileSizeToggle.IsChecked == true; await _docService.ApplyConfigAsync(); StatusText.Text = $"Show file size: {_docService.Config.ShowFileSize}"; }
    private async void DQ_ShowDateModifiedToggle_Changed(object sender, RoutedEventArgs e) { if (_isLoadingDQSettings || _docService == null) return; _docService.Config.ShowDateModified = DQ_ShowDateModifiedToggle.IsChecked == true; await _docService.ApplyConfigAsync(); StatusText.Text = $"Show date modified: {_docService.Config.ShowDateModified}"; }
    private async void DQ_ShowFileExtToggle_Changed(object sender, RoutedEventArgs e) { if (_isLoadingDQSettings || _docService == null) return; _docService.Config.ShowFileExtension = DQ_ShowFileExtToggle.IsChecked == true; await _docService.ApplyConfigAsync(); StatusText.Text = $"Show file extension: {_docService.Config.ShowFileExtension}"; }
    private async void DQ_CompactModeToggle_Changed(object sender, RoutedEventArgs e) { if (_isLoadingDQSettings || _docService == null) return; _docService.Config.CompactMode = DQ_CompactModeToggle.IsChecked == true; await _docService.ApplyConfigAsync(); StatusText.Text = $"Compact mode: {_docService.Config.CompactMode}"; }

    private async void DQ_MaxDepthSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_isLoadingDQSettings || _docService == null) return; var val = (int)DQ_MaxDepthSlider.Value; DQ_MaxDepthValue.Text = val.ToString(); _docService.Config.MaxDepth = val; await _docService.ApplyConfigAsync(); StatusText.Text = $"Max scan depth: {val}"; }
    private async void DQ_MaxFilesSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_isLoadingDQSettings || _docService == null) return; var val = (int)DQ_MaxFilesSlider.Value; DQ_MaxFilesValue.Text = val.ToString(); _docService.Config.MaxFiles = val; await _docService.ApplyConfigAsync(); StatusText.Text = $"Max files: {val}"; }

    private async void DQ_ExcludedFoldersInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        var text = DQ_ExcludedFoldersInput.Text;
        var folders = text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(x => x.Trim())
                         .Where(x => !string.IsNullOrEmpty(x))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .ToList();
        _docService.Config.ExcludedFolders = folders;
        DQ_ExcludedFoldersInput.Text = string.Join(", ", folders);
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Excluded folders updated ({folders.Count} folders)";
    }

    private async void DQ_ExtensionsInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        var text = DQ_ExtensionsInput.Text;
        var exts = text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(x => x.Trim().TrimStart('.').ToLowerInvariant())
                       .Where(x => !string.IsNullOrEmpty(x))
                       .Distinct()
                       .ToList();
        _docService.Config.Extensions = exts;
        DQ_ExtensionsInput.Text = string.Join(", ", exts);
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"File extensions updated ({exts.Count} types)";
    }

    private async void DQ_SortChanged(object sender, RoutedEventArgs e) { if (_isLoadingDQSettings || _docService == null) return; if (DQ_SortDate.IsChecked == true) _docService.Config.SortBy = "date"; else if (DQ_SortType.IsChecked == true) _docService.Config.SortBy = "type"; else if (DQ_SortSize.IsChecked == true) _docService.Config.SortBy = "size"; else _docService.Config.SortBy = "name"; await _docService.ApplyConfigAsync(); StatusText.Text = $"Sort order: {_docService.Config.SortBy}"; }
    private async void DQ_GroupChanged(object sender, RoutedEventArgs e) { if (_isLoadingDQSettings || _docService == null) return; if (DQ_GroupCategory.IsChecked == true) _docService.Config.GroupBy = "category"; else if (DQ_GroupExt.IsChecked == true) _docService.Config.GroupBy = "extension"; else if (DQ_GroupSubfolder.IsChecked == true) _docService.Config.GroupBy = "subfolder"; else _docService.Config.GroupBy = "none"; await _docService.ApplyConfigAsync(); StatusText.Text = $"Group by: {_docService.Config.GroupBy}"; }
    private async void DQ_AutoOpenToggle_Changed(object sender, RoutedEventArgs e) { if (_isLoadingDQSettings || _docService == null) return; _docService.Config.AutoOpenLastProject = DQ_AutoOpenToggle.IsChecked == true; await _docService.ApplyConfigAsync(); StatusText.Text = $"Remember last project: {_docService.Config.AutoOpenLastProject}"; }
    private async void DQ_RecentCountSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_isLoadingDQSettings || _docService == null) return; var val = (int)DQ_RecentCountSlider.Value; DQ_RecentCountValue.Text = val.ToString(); _docService.Config.RecentFilesCount = val; await _docService.ApplyConfigAsync(); StatusText.Text = $"Recent files count: {val}"; }

    // ========== Smart Project Search Settings ==========

    private void SmartProjectSearchAttachModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded || _isLoadingSPSettings) return;

        var attachEnabled = SmartProjectSearchAttachModeToggle.IsChecked == true;
        _settings.SetSmartProjectSearchAttachToSearchOverlayMode(attachEnabled);

        _isLoadingSPSettings = true;
        _widgetToggles.TryGetValue(WidgetIds.SmartProjectSearch, out var spEnabledToggle);
        if (attachEnabled)
        {
            var currentLauncherEnabled = _settings.GetSmartProjectSearchWidgetEnabled();
            _settings.SetSmartProjectSearchWidgetEnabledBeforeAttachMode(currentLauncherEnabled);
            _settings.SetSmartProjectSearchWidgetEnabled(false);
            if (spEnabledToggle != null) { spEnabledToggle.IsChecked = false; spEnabledToggle.IsEnabled = false; }
            StatusText.Text = "Smart Project Search attached mode enabled";
        }
        else
        {
            var restoreLauncherEnabled = _settings.GetSmartProjectSearchWidgetEnabledBeforeAttachMode();
            _settings.SetSmartProjectSearchWidgetEnabled(restoreLauncherEnabled);
            if (spEnabledToggle != null) { spEnabledToggle.IsEnabled = true; spEnabledToggle.IsChecked = restoreLauncherEnabled; }
            StatusText.Text = "Smart Project Search attached mode disabled";
        }
        _isLoadingSPSettings = false;

        _ = _settings.SaveAsync();
        _onSmartProjectSearchWidgetEnabledChanged?.Invoke();
    }

    private void SmartSearchLatestMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded || _isLoadingSPSettings) return;
        var mode = SmartSearchLatestSingleRadio.IsChecked == true ? "single" : "list";
        _settings.SetSmartProjectSearchLatestMode(mode);
        _ = _settings.SaveAsync();
        StatusText.Text = mode == "single"
            ? "Smart search latest mode: single newest result"
            : "Smart search latest mode: newest-first list";
    }

    private void SmartSearchFileTypesInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded || _isLoadingSPSettings) return;

        var values = SmartSearchFileTypesInput.Text
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim().TrimStart('.').ToLowerInvariant())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _settings.SetSmartProjectSearchFileTypes(values);
        SmartSearchFileTypesInput.Text = string.Join(", ", _settings.GetSmartProjectSearchFileTypes());
        _ = _settings.SaveAsync();
        StatusText.Text = $"Smart search file types updated ({_settings.GetSmartProjectSearchFileTypes().Count} entries)";
    }

    // ===== Metrics Viewer Settings =====

    private void MetricsRefreshIntervalSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_settings == null || !IsLoaded || _isUpdatingSliders) return;
        var seconds = (int)e.NewValue;
        _settings.SetMetricsRefreshIntervalSeconds(seconds);
        _ = _settings.SaveAsync();
        MetricsRefreshIntervalValue.Text = $"{seconds}s";
        StatusText.Text = $"Metrics refresh interval set to {seconds}s";
    }

    private void MetricsSnapGridToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = MetricsSnapGridToggle.IsChecked == true;
        _settings.SetMetricsSnapGridEnabled(enabled);
        _ = _settings.SaveAsync();
        StatusText.Text = enabled ? "Metrics snap grid enabled" : "Metrics snap grid disabled (free-floating)";
    }

    private void LoadMetricsSettings()
    {
        var interval = _settings.GetMetricsRefreshIntervalSeconds();
        _isUpdatingSliders = true;
        MetricsRefreshIntervalSlider.Value = interval;
        MetricsRefreshIntervalValue.Text = $"{interval}s";

        var rangeWeeks = _settings.GetMetricsRangeWeeks();
        MetricsRangeWeeksSlider.Value = rangeWeeks;
        MetricsRangeWeeksValue.Text = rangeWeeks == 1 ? "1 week" : $"{rangeWeeks} weeks";
        _isUpdatingSliders = false;

        MetricsSnapGridToggle.IsChecked = _settings.GetMetricsSnapGridEnabled();
        MetricsWeekStartDayCombo.SelectedIndex = _settings.GetMetricsWeekStartDay();
    }

    private void MetricsWeekStartDayCombo_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var dayIndex = MetricsWeekStartDayCombo.SelectedIndex;
        if (dayIndex < 0) return;
        _settings.SetMetricsWeekStartDay(dayIndex);
        _ = _settings.SaveAsync();
        var dayName = ((ComboBoxItem)MetricsWeekStartDayCombo.SelectedItem).Content.ToString();
        StatusText.Text = $"Metrics week starts on {dayName}";
        NotifyMetricsViewerDateRangeChanged();
    }

    private void MetricsRangeWeeksSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_settings == null || !IsLoaded || _isUpdatingSliders) return;
        var weeks = (int)e.NewValue;
        _settings.SetMetricsRangeWeeks(weeks);
        _ = _settings.SaveAsync();
        MetricsRangeWeeksValue.Text = weeks == 1 ? "1 week" : $"{weeks} weeks";
        StatusText.Text = $"Metrics range set to {weeks} week{(weeks == 1 ? "" : "s")}";
        NotifyMetricsViewerDateRangeChanged();
    }

    private void NotifyMetricsViewerDateRangeChanged()
    {
        // Find the MetricsViewerWidget and refresh its date range
        var app = System.Windows.Application.Current as App;
        if (app == null) return;
        foreach (System.Windows.Window w in app.Windows)
        {
            if (w.Content is MetricsViewerWidget mv)
            {
                mv.RefreshAdminDateRange();
                return;
            }
            // Also check if it's nested inside a content presenter
            var widget = FindChild<MetricsViewerWidget>(w);
            if (widget != null)
            {
                widget.RefreshAdminDateRange();
                return;
            }
        }
    }

    private static T? FindChild<T>(System.Windows.DependencyObject parent) where T : System.Windows.DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var nested = FindChild<T>(child);
            if (nested != null) return nested;
        }
        return null;
    }

    // ===== Cheat Sheet Snap Grid =====

    private void CheatSheetSnapGridToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = CheatSheetSnapGridToggle.IsChecked == true;
        _settings.SetCheatSheetSnapGridEnabled(enabled);
        _ = _settings.SaveAsync();
        StatusText.Text = enabled ? "Cheat sheet snap grid enabled" : "Cheat sheet snap grid disabled (free-floating)";
    }

    private void LoadCheatSheetSnapGridSetting()
    {
        CheatSheetSnapGridToggle.IsChecked = _settings.GetCheatSheetSnapGridEnabled();
        CheatSheetCrossDisciplineToggle.IsChecked = _settings.GetCheatSheetCrossDisciplineSearch();
    }

    private void CheatSheetCrossDisciplineToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = CheatSheetCrossDisciplineToggle.IsChecked == true;
        _settings.SetCheatSheetCrossDisciplineSearch(enabled);
        _ = _settings.SaveAsync();
        StatusText.Text = enabled ? "Cross-discipline search enabled" : "Cross-discipline search disabled";
    }

    // ===== General Tab - Update Settings =====

    private void AutoUpdateCheckToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = AutoUpdateCheckToggle.IsChecked == true;
        _settings.SetAutoUpdateCheckEnabled(enabled);
        _ = _settings.SaveAsync();
        _onUpdateSettingsChanged?.Invoke();
        StatusText.Text = enabled ? "Auto update check enabled" : "Auto update check disabled";
    }

    private void AutoUpdateInstallToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = AutoUpdateInstallToggle.IsChecked == true;
        _settings.SetAutoUpdateInstallEnabled(enabled);
        _ = _settings.SaveAsync();
        StatusText.Text = enabled ? "Auto install enabled" : "Auto install disabled";
    }

    private void UpdateFrequencyCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        if (UpdateFrequencyCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int minutes))
        {
            _settings.SetUpdateCheckFrequencyMinutes(minutes);
            _ = _settings.SaveAsync();
            _onUpdateSettingsChanged?.Invoke();
            StatusText.Text = $"Update check frequency set to {minutes / 60} hour(s)";
        }
    }

    private void TelemetryConsentToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = TelemetryConsentToggle.IsChecked == true;
        _settings.SetTelemetryConsentGiven(enabled);
        // User has explicitly answered via Settings -- keep the Asked flag true
        // so the first-run dialog never pops again.
        _settings.SetTelemetryConsentAsked(true);
        _ = _settings.SaveAsync();

        // Apply the change live to the running FirebaseService so the effect is
        // instant -- no restart needed. FirebaseLifecycleManager owns the
        // service; expose it via a lightweight accessor if needed.
        try
        {
            if (System.Windows.Application.Current is App app)
            {
                app.ApplyTelemetryConsent(enabled);
            }
        }
        catch { /* non-fatal -- the next launch will pick it up */ }

        StatusText.Text = enabled ? "Usage telemetry enabled" : "Usage telemetry disabled";
    }

    private void LoadUpdateFrequencyCombo()
    {
        var currentMinutes = _settings.GetUpdateCheckFrequencyMinutes();
        for (int i = 0; i < UpdateFrequencyCombo.Items.Count; i++)
        {
            if (UpdateFrequencyCombo.Items[i] is System.Windows.Controls.ComboBoxItem item && 
                item.Tag is string tagStr && int.TryParse(tagStr, out int minutes) && minutes == currentMinutes)
            {
                UpdateFrequencyCombo.SelectedIndex = i;
                return;
            }
        }
        // Default to "Every 6 hours" if no match
        UpdateFrequencyCombo.SelectedIndex = 1;
    }

    // ===== Frequent Projects Tab =====

    private async void LoadFrequentProjectsSettings()
    {
        _isLoadingFPSettings = true;
        try
        {
            FP_MaxShownSlider.Value = _settings.GetMaxFrequentProjectsShown();
            FP_MaxShownValue.Text = _settings.GetMaxFrequentProjectsShown().ToString();
            FP_MaxSavedSlider.Value = _settings.GetMaxFrequentProjectsSaved();
            FP_MaxSavedValue.Text = _settings.GetMaxFrequentProjectsSaved().ToString();
            FP_GridModeToggle.IsChecked = _settings.GetFrequentProjectsGridMode();

            if (_launchDataStore != null)
            {
                var topProjects = await _launchDataStore.GetTopProjectsAsync(100);
                var totalLaunches = topProjects.Sum(p => p.LaunchCount);
                FP_StatsText.Text = $"Tracking {topProjects.Count} project{(topProjects.Count == 1 ? "" : "s")} with {totalLaunches} total launch{(totalLaunches == 1 ? "" : "es")}";
            }
            else
            {
                FP_StatsText.Text = "Launch tracking not available";
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"LoadFrequentProjectsSettings error: {ex.Message}");
        }
        finally
        {
            _isLoadingFPSettings = false;
        }
    }

    private void FP_MaxShownSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_isLoadingFPSettings || _settings == null || !IsLoaded) return; var value = (int)e.NewValue; _settings.SetMaxFrequentProjectsShown(value); _ = _settings.SaveAsync(); if (FP_MaxShownValue != null) FP_MaxShownValue.Text = value.ToString(); StatusText.Text = $"Max projects shown: {value}"; }
    private void FP_MaxSavedSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (_isLoadingFPSettings || _settings == null || !IsLoaded) return; var value = (int)e.NewValue; _settings.SetMaxFrequentProjectsSaved(value); _ = _settings.SaveAsync(); if (FP_MaxSavedValue != null) FP_MaxSavedValue.Text = value.ToString(); StatusText.Text = $"Max projects tracked: {value}"; }

    private void FP_GridModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingFPSettings || _settings == null || !IsLoaded) return;
        var gridMode = FP_GridModeToggle.IsChecked == true;
        _settings.SetFrequentProjectsGridMode(gridMode);
        _ = _settings.SaveAsync();
        _onFrequentProjectsLayoutChanged?.Invoke();
        StatusText.Text = gridMode ? "Frequent Projects: grid mode" : "Frequent Projects: list mode";
    }

    private async void FP_ResetData_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to reset all project launch data?\n\nThis will clear all launch counts and cannot be undone.",
            "Reset Launch Data",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes && _launchDataStore != null)
        {
            try
            {
                await _launchDataStore.ClearAllAsync();
                FP_StatsText.Text = "Tracking 0 projects with 0 total launches";
                StatusText.Text = "All launch data has been reset";
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"FP_ResetData_Click error: {ex.Message}");
                StatusText.Text = $"Failed to reset data: {ex.Message}";
            }
        }
    }

    // ===== Quick Launch Tab =====

    private async void LoadQuickLaunchSettings()
    {
        _isLoadingQLSettings = true;
        try
        {
            QL_HorizontalModeToggle.IsChecked = _settings.GetQuickLaunchHorizontalMode();

            var config = await Infrastructure.Settings.QuickLaunchConfig.LoadAsync();
            var items = config.Items.OrderBy(i => i.SortOrder).ToList();
            QL_ItemCountText.Text = $"{items.Count} item{(items.Count == 1 ? "" : "s")} configured";

            QL_ItemsList.Children.Clear();
            foreach (var item in items)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var icon = new TextBlock
                {
                    Text = item.Icon,
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var info = new StackPanel();
                info.Children.Add(new TextBlock
                {
                    Text = item.Name,
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    Foreground = FindResource("TextBrush") as System.Windows.Media.Brush
                });
                info.Children.Add(new TextBlock
                {
                    Text = item.Path,
                    FontSize = 10,
                    Foreground = FindResource("TextSecondaryBrush") as System.Windows.Media.Brush,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                Grid.SetColumn(icon, 0);
                Grid.SetColumn(info, 1);
                row.Children.Add(icon);
                row.Children.Add(info);

                var border = new Border
                {
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 6, 8, 6),
                    Child = row
                };
                border.SetResourceReference(Border.BackgroundProperty, "HoverBrush");

                QL_ItemsList.Children.Add(border);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"LoadQuickLaunchSettings error: {ex.Message}");
        }
        finally
        {
            _isLoadingQLSettings = false;
        }
    }

    private void QL_HorizontalModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQLSettings || _settings == null || !IsLoaded) return;
        var horizontal = QL_HorizontalModeToggle.IsChecked == true;
        _settings.SetQuickLaunchHorizontalMode(horizontal);
        _ = _settings.SaveAsync();
        _onQuickLaunchLayoutChanged?.Invoke();
        StatusText.Text = horizontal ? "Quick Launch: horizontal mode" : "Quick Launch: vertical mode";
    }

    private async void QL_ClearAll_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to remove all Quick Launch items?\n\nThis cannot be undone.",
            "Clear All Items",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var config = await Infrastructure.Settings.QuickLaunchConfig.LoadAsync();
                config.Items.Clear();
                await config.SaveAsync();
                QL_ItemsList.Children.Clear();
                QL_ItemCountText.Text = "0 items configured";
                StatusText.Text = "All Quick Launch items cleared";
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"QL_ClearAll_Click error: {ex.Message}");
                StatusText.Text = $"Failed to clear items: {ex.Message}";
            }
        }
    }

    // ===== Tags Settings =====

    private void LoadTagSettings()
    {
        if (_settings == null) return;

        TagSearchEnabledToggle.IsChecked = _settings.GetTagSearchEnabled();
        TagCarouselAutoRefreshToggle.IsChecked = _settings.GetTagCarouselAutoRefresh();
        TagCarouselMaxChipsBox.Text = _settings.GetTagCarouselMaxChips().ToString();

        // Set display mode combo
        var mode = _settings.GetTagDisplayMode();
        for (int i = 0; i < TagDisplayModeCombo.Items.Count; i++)
        {
            if (TagDisplayModeCombo.Items[i] is System.Windows.Controls.ComboBoxItem item &&
                item.Tag?.ToString() == mode)
            {
                TagDisplayModeCombo.SelectedIndex = i;
                break;
            }
        }

        // Show/hide carousel-specific settings based on mode
        TagCarouselSettingsPanel.Visibility = mode == "carousel" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TagSearchEnabledToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = TagSearchEnabledToggle.IsChecked == true;
        _settings.SetTagSearchEnabled(enabled);
        _ = _settings.SaveAsync();
        TelemetryAccessor.TrackSettingChanged("tag_search_enabled", enabled.ToString());
        StatusText.Text = enabled ? "Tag search enabled" : "Tag search disabled";
    }

    private void TagDisplayModeCombo_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        if (TagDisplayModeCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item &&
            item.Tag is string mode)
        {
            _settings.SetTagDisplayMode(mode);
            _ = _settings.SaveAsync();
            TelemetryAccessor.TrackSettingChanged("tag_display_mode", mode);
            StatusText.Text = $"Tag display mode: {mode}";

            // Show/hide carousel-specific settings
            TagCarouselSettingsPanel.Visibility = mode == "carousel" ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    private void TagCarouselMaxChipsBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        if (int.TryParse(TagCarouselMaxChipsBox.Text, out var count))
        {
            count = Math.Clamp(count, 3, 20);
            _settings.SetTagCarouselMaxChips(count);
            TagCarouselMaxChipsBox.Text = count.ToString();
            _ = _settings.SaveAsync();
            StatusText.Text = $"Tag carousel max chips: {count}";
        }
        else
        {
            TagCarouselMaxChipsBox.Text = _settings.GetTagCarouselMaxChips().ToString();
        }
    }

    private void TagCarouselAutoRefreshToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = TagCarouselAutoRefreshToggle.IsChecked == true;
        _settings.SetTagCarouselAutoRefresh(enabled);
        _ = _settings.SaveAsync();
        StatusText.Text = enabled ? "Tag carousel auto-refresh enabled" : "Tag carousel auto-refresh disabled";
    }

    // ===== Search History Settings =====

    private void LoadSearchHistorySettings()
    {
        if (_settings == null) return;

        SearchHistoryMaxShownBox.Text = _settings.GetSearchHistoryMaxShown().ToString();
        SearchHistoryRetentionDaysBox.Text = _settings.GetSearchHistoryRetentionDays().ToString();
        SearchHistoryBackupPathBox.Text = _settings.GetSearchHistoryBackupPath();
    }

    private void SearchHistoryMaxShownBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        if (int.TryParse(SearchHistoryMaxShownBox.Text, out var count))
        {
            count = Math.Clamp(count, 1, 25);
            _settings.SetSearchHistoryMaxShown(count);
            SearchHistoryMaxShownBox.Text = count.ToString();
            _ = _settings.SaveAsync();
            StatusText.Text = $"History items shown: {count}";
        }
        else
        {
            SearchHistoryMaxShownBox.Text = _settings.GetSearchHistoryMaxShown().ToString();
        }
    }

    private void SearchHistoryRetentionDaysBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        if (int.TryParse(SearchHistoryRetentionDaysBox.Text, out var days))
        {
            days = Math.Clamp(days, 1, 365);
            _settings.SetSearchHistoryRetentionDays(days);
            SearchHistoryRetentionDaysBox.Text = days.ToString();
            _ = _settings.SaveAsync();
            StatusText.Text = $"History retention: {days} days";
        }
        else
        {
            SearchHistoryRetentionDaysBox.Text = _settings.GetSearchHistoryRetentionDays().ToString();
        }
    }

    private void SearchHistoryBackupPathBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        _settings.SetSearchHistoryBackupPath(SearchHistoryBackupPathBox.Text.Trim());
        _ = _settings.SaveAsync();
        StatusText.Text = string.IsNullOrWhiteSpace(SearchHistoryBackupPathBox.Text)
            ? "Search history backup path cleared"
            : $"Backup path: {SearchHistoryBackupPathBox.Text.Trim()}";
    }

    private void SearchHistoryBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Choose search history backup location",
            FileName = "search-history-backup.json",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() == true)
        {
            SearchHistoryBackupPathBox.Text = dialog.FileName;
            _settings?.SetSearchHistoryBackupPath(dialog.FileName);
            _ = _settings?.SaveAsync()!;
            StatusText.Text = $"Backup path: {dialog.FileName}";
        }
    }

    private async void SearchHistoryExportButton_Click(object sender, RoutedEventArgs e)
    {
        var backupPath = _settings?.GetSearchHistoryBackupPath();
        if (string.IsNullOrWhiteSpace(backupPath))
        {
            StatusText.Text = "Set a backup path first, then click Export.";
            return;
        }

        try
        {
            var store = new Infrastructure.Data.SearchHistoryStore();
            await store.LoadAsync();
            await store.ExportToFileAsync(backupPath);
            StatusText.Text = $"History exported to {backupPath}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Export failed: {ex.Message}";
        }
    }
}
