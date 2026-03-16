using System;
using System.Windows;
using System.Windows.Forms;
using DesktopHub.Core.Models;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;

namespace DesktopHub.UI;

public partial class SearchOverlay
{
    private void CreateTimerOverlay(double? left = null, double? top = null)
    {
        _timerOverlay = new TimerOverlay(_timerService, _settings);
        RegisterWidgetWindow(_timerOverlay);
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        _timerOverlay.Topmost = !isLivingWidgetsMode;

        if (left.HasValue && top.HasValue)
        {
            _timerOverlay.Left = left.Value;
            _timerOverlay.Top = top.Value;
        }
        else if (isLivingWidgetsMode)
        {
            var (savedLeft, savedTop) = _settings.GetTimerWidgetPosition();
            _timerOverlay.Left = savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
            _timerOverlay.Top = savedTop ?? this.Top;
        }
        else
        {
            // Non-live mode: use auto-grid layout
            var estimatedSize = new System.Windows.Size(_timerOverlay.Width > 0 ? _timerOverlay.Width : 200, _timerOverlay.Height > 0 ? _timerOverlay.Height : 120);
            var pos = ComputeNonLivePosition(_timerOverlay, WidgetIds.Timer, estimatedSize);
            if (pos == null) { UnregisterWidgetWindow(_timerOverlay); _timerOverlay = null; return; }
            _timerOverlay.Left = pos.Value.left;
            _timerOverlay.Top = pos.Value.top;
        }

        if (isLivingWidgetsMode)
            _timerOverlay.EnableDragging();

        _timerOverlay.Show();
        _timerOverlay.UpdateTransparency();
        _timerOverlay.Tag = "WasVisible";
        TelemetryAccessor.TrackWidgetVisibility(WidgetIds.Timer, true);

        if (isLivingWidgetsMode)
        {
            ApplyLiveLayoutForWindow(_timerOverlay);
            RefreshAttachmentMappings();
            TrackVisibleWindowBounds();
        }

        if (isLivingWidgetsMode && _desktopFollower != null)
        {
            _desktopFollower.TrackWindow(_timerOverlay);
        }

        // Register with update indicator manager
        var timerRef = _timerOverlay;
        _updateIndicatorManager?.RegisterWidget("TimerOverlay", 3, _timerOverlay,
            visible => Dispatcher.Invoke(() => timerRef.SetUpdateIndicatorVisible(visible)));

        DebugLogger.Log($"CreateTimerOverlay: Timer overlay created at ({_timerOverlay.Left}, {_timerOverlay.Top}), Topmost={_timerOverlay.Topmost}");
    }

    private void OnSearchWidgetRequested(object? sender, EventArgs e)
    {
        try
        {
            if (this.Visibility == Visibility.Visible && this.IsVisible)
            {
                HideOverlay();
                DebugLogger.Log("OnSearchWidgetRequested: Search overlay hidden");
            }
            else
            {
                ShowOverlay();
                DebugLogger.Log("OnSearchWidgetRequested: Search overlay shown");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnSearchWidgetRequested: Error: {ex}");
        }
    }

    private void OnTimerWidgetRequested(object? sender, EventArgs e)
    {
        try
        {
            if (_timerOverlay == null)
            {
                CreateTimerOverlay();
            }
            else
            {
                if (_timerOverlay.Visibility == Visibility.Visible)
                {
                    _timerOverlay.Visibility = Visibility.Hidden;
                    _timerOverlay.Tag = null;
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.Timer, false);
                    DebugLogger.Log("OnTimerWidgetRequested: Timer overlay hidden");
                }
                else
                {
                    _timerOverlay.Visibility = Visibility.Visible;
                    _timerOverlay.Tag = "WasVisible";
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.Timer, true);
                    DebugLogger.Log("OnTimerWidgetRequested: Timer overlay shown");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnTimerWidgetRequested: Error with timer overlay: {ex}");
            System.Windows.MessageBox.Show($"Error with timer overlay: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateQuickTasksOverlay(double? left = null, double? top = null)
    {
        _quickTasksOverlay = new QuickTasksOverlay(_taskService!, _settings);
        ApplyResponsiveWidgetWidth(_quickTasksOverlay);
        RegisterWidgetWindow(_quickTasksOverlay);
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        _quickTasksOverlay.Topmost = !isLivingWidgetsMode;

        if (left.HasValue && top.HasValue)
        {
            _quickTasksOverlay.Left = left.Value;
            _quickTasksOverlay.Top = top.Value;
        }
        else if (isLivingWidgetsMode)
        {
            var (savedLeft, savedTop) = _settings.GetQuickTasksWidgetPosition();
            _quickTasksOverlay.Left = savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
            _quickTasksOverlay.Top = savedTop ?? this.Top;
        }
        else
        {
            var estimatedSize = new System.Windows.Size(_quickTasksOverlay.Width > 0 ? _quickTasksOverlay.Width : GetResponsiveColumnWidgetWidth(), _quickTasksOverlay.Height > 0 ? _quickTasksOverlay.Height : 300);
            var pos = ComputeNonLivePosition(_quickTasksOverlay, WidgetIds.QuickTasks, estimatedSize);
            if (pos == null) { UnregisterWidgetWindow(_quickTasksOverlay); _quickTasksOverlay = null; return; }
            _quickTasksOverlay.Left = pos.Value.left;
            _quickTasksOverlay.Top = pos.Value.top;
        }

        if (isLivingWidgetsMode)
            _quickTasksOverlay.EnableDragging();

        _quickTasksOverlay.Show();
        _quickTasksOverlay.UpdateTransparency();
        _quickTasksOverlay.Tag = "WasVisible";
        TelemetryAccessor.TrackWidgetVisibility(WidgetIds.QuickTasks, true);

        if (isLivingWidgetsMode)
        {
            ApplyLiveLayoutForWindow(_quickTasksOverlay);
            RefreshAttachmentMappings();
            TrackVisibleWindowBounds();
        }

        if (isLivingWidgetsMode && _desktopFollower != null)
        {
            _desktopFollower.TrackWindow(_quickTasksOverlay);
        }

        // Register with update indicator manager
        var qtRef = _quickTasksOverlay;
        _updateIndicatorManager?.RegisterWidget("QuickTasksOverlay", 4, _quickTasksOverlay,
            visible => Dispatcher.Invoke(() => qtRef.SetUpdateIndicatorVisible(visible)));

        DebugLogger.Log($"CreateQuickTasksOverlay: Quick Tasks overlay created at ({_quickTasksOverlay.Left}, {_quickTasksOverlay.Top}), Topmost={_quickTasksOverlay.Topmost}");
    }

    private void OnQuickTasksWidgetRequested(object? sender, EventArgs e)
    {
        try
        {
            if (_quickTasksOverlay == null)
            {
                CreateQuickTasksOverlay();
            }
            else
            {
                if (_quickTasksOverlay.Visibility == Visibility.Visible)
                {
                    _quickTasksOverlay.Visibility = Visibility.Hidden;
                    _quickTasksOverlay.Tag = null;
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.QuickTasks, false);
                    DebugLogger.Log("OnQuickTasksWidgetRequested: Quick Tasks overlay hidden");
                }
                else
                {
                    _quickTasksOverlay.Visibility = Visibility.Visible;
                    _quickTasksOverlay.Tag = "WasVisible";
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.QuickTasks, true);
                    DebugLogger.Log("OnQuickTasksWidgetRequested: Quick Tasks overlay shown");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnQuickTasksWidgetRequested: Error with quick tasks overlay: {ex}");
            System.Windows.MessageBox.Show($"Error with quick tasks overlay: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateDocOverlay(double? left = null, double? top = null)
    {
        _docOverlay = new DocQuickOpenOverlay(_docService!, _settings);
        ApplyResponsiveWidgetWidth(_docOverlay);
        RegisterWidgetWindow(_docOverlay);
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        _docOverlay.Topmost = !isLivingWidgetsMode;

        if (left.HasValue && top.HasValue)
        {
            _docOverlay.Left = left.Value;
            _docOverlay.Top = top.Value;
        }
        else if (isLivingWidgetsMode)
        {
            var (savedLeft, savedTop) = _settings.GetDocWidgetPosition();
            _docOverlay.Left = savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
            _docOverlay.Top = savedTop ?? (this.Top + 100);
        }
        else
        {
            var estimatedSize = new System.Windows.Size(_docOverlay.Width > 0 ? _docOverlay.Width : GetResponsiveColumnWidgetWidth(), _docOverlay.Height > 0 ? _docOverlay.Height : 300);
            var pos = ComputeNonLivePosition(_docOverlay, WidgetIds.DocQuickOpen, estimatedSize);
            if (pos == null) { UnregisterWidgetWindow(_docOverlay); _docOverlay = null; return; }
            _docOverlay.Left = pos.Value.left;
            _docOverlay.Top = pos.Value.top;
        }

        if (isLivingWidgetsMode)
            _docOverlay.EnableDragging();

        _docOverlay.Show();
        _docOverlay.UpdateTransparency();
        _docOverlay.Tag = "WasVisible";
        TelemetryAccessor.TrackWidgetVisibility(WidgetIds.DocQuickOpen, true);
        UpdateDynamicOverlayMaxHeight(_docOverlay);

        if (isLivingWidgetsMode)
        {
            ApplyLiveLayoutForWindow(_docOverlay);
            RefreshAttachmentMappings();
            TrackVisibleWindowBounds();
        }

        if (isLivingWidgetsMode && _desktopFollower != null)
        {
            _desktopFollower.TrackWindow(_docOverlay);
        }

        // Register with update indicator manager
        var docRef = _docOverlay;
        _updateIndicatorManager?.RegisterWidget("DocQuickOpenOverlay", 5, _docOverlay,
            visible => Dispatcher.Invoke(() => docRef.SetUpdateIndicatorVisible(visible)));

        DebugLogger.Log($"CreateDocOverlay: Doc overlay created at ({_docOverlay.Left}, {_docOverlay.Top}), Topmost={_docOverlay.Topmost}");
    }

    private void OnDocQuickOpenRequested(object? sender, EventArgs e)
    {
        try
        {
            if (_docOverlay == null)
            {
                CreateDocOverlay();
            }
            else
            {
                if (_docOverlay.Visibility == Visibility.Visible)
                {
                    _docOverlay.Visibility = Visibility.Hidden;
                    _docOverlay.Tag = null;
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.DocQuickOpen, false);
                    DebugLogger.Log("OnDocQuickOpenRequested: Doc overlay hidden");
                }
                else
                {
                    _docOverlay.Visibility = Visibility.Visible;
                    _docOverlay.Tag = "WasVisible";
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.DocQuickOpen, true);
                    DebugLogger.Log("OnDocQuickOpenRequested: Doc overlay shown");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnDocQuickOpenRequested: Error with doc overlay: {ex}");
            System.Windows.MessageBox.Show($"Error with doc overlay: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateFrequentProjectsOverlay(double? left = null, double? top = null)
    {
        _frequentProjectsOverlay = new FrequentProjectsOverlay(_launchDataStore!, _settings);
        ApplyResponsiveWidgetWidth(_frequentProjectsOverlay);
        RegisterWidgetWindow(_frequentProjectsOverlay);
        _frequentProjectsOverlay.OnProjectSelectedForSearch += (path) =>
        {
            Dispatcher.Invoke(() =>
            {
                _lastQuerySource = Core.Models.QuerySources.FrequentProject;
                SearchBox.Text = path;
                SearchBox.Focus();
                SearchBox.CaretIndex = path.Length;
                DebugLogger.Log($"FrequentProjectsOverlay: Loaded project path into search field: {path}");
            });
        };
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        _frequentProjectsOverlay.Topmost = !isLivingWidgetsMode;

        if (left.HasValue && top.HasValue)
        {
            _frequentProjectsOverlay.Left = left.Value;
            _frequentProjectsOverlay.Top = top.Value;
        }
        else if (isLivingWidgetsMode)
        {
            var (savedLeft, savedTop) = _settings.GetFrequentProjectsWidgetPosition();
            _frequentProjectsOverlay.Left = savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
            _frequentProjectsOverlay.Top = savedTop ?? (this.Top + 200);
        }
        else
        {
            var estimatedSize = new System.Windows.Size(_frequentProjectsOverlay.Width > 0 ? _frequentProjectsOverlay.Width : GetResponsiveColumnWidgetWidth(), _frequentProjectsOverlay.Height > 0 ? _frequentProjectsOverlay.Height : 250);
            var pos = ComputeNonLivePosition(_frequentProjectsOverlay, WidgetIds.FrequentProjects, estimatedSize);
            if (pos == null) { UnregisterWidgetWindow(_frequentProjectsOverlay); _frequentProjectsOverlay = null; return; }
            _frequentProjectsOverlay.Left = pos.Value.left;
            _frequentProjectsOverlay.Top = pos.Value.top;
        }

        if (isLivingWidgetsMode)
            _frequentProjectsOverlay.EnableDragging();

        _frequentProjectsOverlay.Show();
        _frequentProjectsOverlay.UpdateTransparency();
        _frequentProjectsOverlay.Tag = "WasVisible";
        TelemetryAccessor.TrackWidgetVisibility(WidgetIds.FrequentProjects, true);

        if (isLivingWidgetsMode)
        {
            ApplyLiveLayoutForWindow(_frequentProjectsOverlay);
            RefreshAttachmentMappings();
            TrackVisibleWindowBounds();
        }

        if (isLivingWidgetsMode && _desktopFollower != null)
        {
            _desktopFollower.TrackWindow(_frequentProjectsOverlay);
        }

        var fpRef = _frequentProjectsOverlay;
        _updateIndicatorManager?.RegisterWidget("FrequentProjectsOverlay", 6, _frequentProjectsOverlay,
            visible => Dispatcher.Invoke(() => fpRef.SetUpdateIndicatorVisible(visible)));

        DebugLogger.Log($"CreateFrequentProjectsOverlay: Created at ({_frequentProjectsOverlay.Left}, {_frequentProjectsOverlay.Top})");
    }

    private void OnFrequentProjectsRequested(object? sender, EventArgs e)
    {
        try
        {
            if (_frequentProjectsOverlay == null)
            {
                CreateFrequentProjectsOverlay();
            }
            else
            {
                if (_frequentProjectsOverlay.Visibility == Visibility.Visible)
                {
                    _frequentProjectsOverlay.Visibility = Visibility.Hidden;
                    _frequentProjectsOverlay.Tag = null;
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.FrequentProjects, false);
                    DebugLogger.Log("OnFrequentProjectsRequested: Frequent projects overlay hidden");
                }
                else
                {
                    _frequentProjectsOverlay.Visibility = Visibility.Visible;
                    _frequentProjectsOverlay.Tag = "WasVisible";
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.FrequentProjects, true);
                    DebugLogger.Log("OnFrequentProjectsRequested: Frequent projects overlay shown");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnFrequentProjectsRequested: Error: {ex}");
            System.Windows.MessageBox.Show($"Error with frequent projects overlay: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateQuickLaunchOverlay(double? left = null, double? top = null)
    {
        _quickLaunchOverlay = new QuickLaunchOverlay(_settings);
        ApplyResponsiveWidgetWidth(_quickLaunchOverlay);
        RegisterWidgetWindow(_quickLaunchOverlay);
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        _quickLaunchOverlay.Topmost = !isLivingWidgetsMode;

        if (left.HasValue && top.HasValue)
        {
            _quickLaunchOverlay.Left = left.Value;
            _quickLaunchOverlay.Top = top.Value;
        }
        else if (isLivingWidgetsMode)
        {
            var (savedLeft, savedTop) = _settings.GetQuickLaunchWidgetPosition();
            _quickLaunchOverlay.Left = savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
            _quickLaunchOverlay.Top = savedTop ?? (this.Top + 300);
        }
        else
        {
            var estimatedSize = new System.Windows.Size(_quickLaunchOverlay.Width > 0 ? _quickLaunchOverlay.Width : GetResponsiveColumnWidgetWidth(), _quickLaunchOverlay.Height > 0 ? _quickLaunchOverlay.Height : 200);
            var pos = ComputeNonLivePosition(_quickLaunchOverlay, WidgetIds.QuickLaunch, estimatedSize);
            if (pos == null) { UnregisterWidgetWindow(_quickLaunchOverlay); _quickLaunchOverlay = null; return; }
            _quickLaunchOverlay.Left = pos.Value.left;
            _quickLaunchOverlay.Top = pos.Value.top;
        }

        if (isLivingWidgetsMode)
            _quickLaunchOverlay.EnableDragging();

        _quickLaunchOverlay.Show();
        _quickLaunchOverlay.UpdateTransparency();
        _quickLaunchOverlay.Tag = "WasVisible";
        TelemetryAccessor.TrackWidgetVisibility(WidgetIds.QuickLaunch, true);

        if (isLivingWidgetsMode)
        {
            ApplyLiveLayoutForWindow(_quickLaunchOverlay);
            RefreshAttachmentMappings();
            TrackVisibleWindowBounds();
        }

        if (isLivingWidgetsMode && _desktopFollower != null)
        {
            _desktopFollower.TrackWindow(_quickLaunchOverlay);
        }

        var qlRef = _quickLaunchOverlay;
        _updateIndicatorManager?.RegisterWidget("QuickLaunchOverlay", 7, _quickLaunchOverlay,
            visible => Dispatcher.Invoke(() => qlRef.SetUpdateIndicatorVisible(visible)));

        DebugLogger.Log($"CreateQuickLaunchOverlay: Created at ({_quickLaunchOverlay.Left}, {_quickLaunchOverlay.Top})");
    }

    private void OnQuickLaunchRequested(object? sender, EventArgs e)
    {
        try
        {
            if (_quickLaunchOverlay == null)
            {
                CreateQuickLaunchOverlay();
            }
            else
            {
                if (_quickLaunchOverlay.Visibility == Visibility.Visible)
                {
                    _quickLaunchOverlay.Visibility = Visibility.Hidden;
                    _quickLaunchOverlay.Tag = null;
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.QuickLaunch, false);
                    DebugLogger.Log("OnQuickLaunchRequested: Quick launch overlay hidden");
                }
                else
                {
                    _quickLaunchOverlay.Visibility = Visibility.Visible;
                    _quickLaunchOverlay.Tag = "WasVisible";
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.QuickLaunch, true);
                    DebugLogger.Log("OnQuickLaunchRequested: Quick launch overlay shown");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnQuickLaunchRequested: Error: {ex}");
            System.Windows.MessageBox.Show($"Error with quick launch overlay: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void CreateSmartProjectSearchOverlay(double? left = null, double? top = null)
    {
        if (_settings.GetSmartProjectSearchAttachToSearchOverlayMode())
        {
            DebugLogger.Log("CreateSmartProjectSearchOverlay: Skipped because attach mode is enabled");
            return;
        }

        if (_smartProjectSearchService == null)
            return;

        _smartProjectSearchOverlay = new SmartProjectSearchOverlay(_smartProjectSearchService, _settings);
        ApplyResponsiveWidgetWidth(_smartProjectSearchOverlay);
        RegisterWidgetWindow(_smartProjectSearchOverlay);
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        _smartProjectSearchOverlay.Topmost = !isLivingWidgetsMode;

        if (left.HasValue && top.HasValue)
        {
            _smartProjectSearchOverlay.Left = left.Value;
            _smartProjectSearchOverlay.Top = top.Value;
        }
        else if (isLivingWidgetsMode)
        {
            var (savedLeft, savedTop) = _settings.GetSmartProjectSearchWidgetPosition();
            _smartProjectSearchOverlay.Left = savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
            _smartProjectSearchOverlay.Top = savedTop ?? (this.Top + 380);
        }
        else
        {
            var estimatedSize = new System.Windows.Size(_smartProjectSearchOverlay.Width > 0 ? _smartProjectSearchOverlay.Width : GetResponsiveColumnWidgetWidth(), _smartProjectSearchOverlay.Height > 0 ? _smartProjectSearchOverlay.Height : 300);
            var pos = ComputeNonLivePosition(_smartProjectSearchOverlay, WidgetIds.SmartProjectSearch, estimatedSize);
            if (pos == null) { UnregisterWidgetWindow(_smartProjectSearchOverlay); _smartProjectSearchOverlay = null; return; }
            _smartProjectSearchOverlay.Left = pos.Value.left;
            _smartProjectSearchOverlay.Top = pos.Value.top;
        }

        if (isLivingWidgetsMode)
            _smartProjectSearchOverlay.EnableDragging();

        _smartProjectSearchOverlay.Show();
        _smartProjectSearchOverlay.UpdateTransparency();
        _smartProjectSearchOverlay.Tag = "WasVisible";
        TelemetryAccessor.TrackWidgetVisibility(WidgetIds.SmartProjectSearch, true);

        if (isLivingWidgetsMode)
        {
            ApplyLiveLayoutForWindow(_smartProjectSearchOverlay);
            RefreshAttachmentMappings();
            TrackVisibleWindowBounds();
        }

        if (isLivingWidgetsMode && _desktopFollower != null)
        {
            _desktopFollower.TrackWindow(_smartProjectSearchOverlay);
        }

        var smartRef = _smartProjectSearchOverlay;
        _updateIndicatorManager?.RegisterWidget("SmartProjectSearchOverlay", 8, _smartProjectSearchOverlay,
            visible => Dispatcher.Invoke(() => smartRef.SetUpdateIndicatorVisible(visible)));

        if (ResultsList.SelectedItem is ProjectViewModel vm)
        {
            try
            {
                await _smartProjectSearchOverlay.Widget.SetProjectAsync(vm.Path, $"{vm.FullNumber} {vm.Name}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"CreateSmartProjectSearchOverlay: Failed to prime selected project: {ex.Message}");
            }
        }

        DebugLogger.Log($"CreateSmartProjectSearchOverlay: Created at ({_smartProjectSearchOverlay.Left}, {_smartProjectSearchOverlay.Top})");
    }

    private void OnSmartProjectSearchRequested(object? sender, EventArgs e)
    {
        try
        {
            if (_settings.GetSmartProjectSearchAttachToSearchOverlayMode())
            {
                DebugLogger.Log("OnSmartProjectSearchRequested: Ignored because attach mode is enabled");
                return;
            }

            if (_smartProjectSearchOverlay == null)
            {
                CreateSmartProjectSearchOverlay();
            }
            else
            {
                if (_smartProjectSearchOverlay.Visibility == Visibility.Visible)
                {
                    _smartProjectSearchOverlay.Visibility = Visibility.Hidden;
                    _smartProjectSearchOverlay.Tag = null;
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.SmartProjectSearch, false);
                    DebugLogger.Log("OnSmartProjectSearchRequested: Smart search overlay hidden");
                }
                else
                {
                    _smartProjectSearchOverlay.Visibility = Visibility.Visible;
                    _smartProjectSearchOverlay.Tag = "WasVisible";
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.SmartProjectSearch, true);
                    DebugLogger.Log("OnSmartProjectSearchRequested: Smart search overlay shown");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnSmartProjectSearchRequested: Error: {ex}");
            System.Windows.MessageBox.Show($"Error with smart project search overlay: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateCheatSheetOverlay(double? left = null, double? top = null)
    {
        if (_cheatSheetService == null)
            return;

        var fbSvc = ((App)System.Windows.Application.Current).FirebaseManager?.FirebaseService;
        _cheatSheetOverlay = new CheatSheetOverlay(_cheatSheetService, _settings, _cheatSheetDataService, fbSvc);
        ApplyResponsiveWidgetWidth(_cheatSheetOverlay);
        RegisterWidgetWindow(_cheatSheetOverlay);
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        _cheatSheetOverlay.Topmost = !isLivingWidgetsMode;

        if (left.HasValue && top.HasValue)
        {
            _cheatSheetOverlay.Left = left.Value;
            _cheatSheetOverlay.Top = top.Value;
        }
        else if (isLivingWidgetsMode)
        {
            var (savedLeft, savedTop) = _settings.GetCheatSheetWidgetPosition();
            _cheatSheetOverlay.Left = savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
            _cheatSheetOverlay.Top = savedTop ?? (this.Top + 460);
        }
        else
        {
            var estimatedSize = new System.Windows.Size(_cheatSheetOverlay.Width > 0 ? _cheatSheetOverlay.Width : GetResponsiveColumnWidgetWidth(), _cheatSheetOverlay.Height > 0 ? _cheatSheetOverlay.Height : 400);
            var pos = ComputeNonLivePosition(_cheatSheetOverlay, WidgetIds.CheatSheet, estimatedSize);
            if (pos == null) { UnregisterWidgetWindow(_cheatSheetOverlay); _cheatSheetOverlay = null; return; }
            _cheatSheetOverlay.Left = pos.Value.left;
            _cheatSheetOverlay.Top = pos.Value.top;
        }

        if (isLivingWidgetsMode)
            _cheatSheetOverlay.EnableDragging();

        _cheatSheetOverlay.Show();
        _cheatSheetOverlay.UpdateTransparency();
        _cheatSheetOverlay.Tag = "WasVisible";
        TelemetryAccessor.TrackWidgetVisibility(WidgetIds.CheatSheet, true);

        if (isLivingWidgetsMode)
        {
            ApplyLiveLayoutForWindow(_cheatSheetOverlay);
            RefreshAttachmentMappings();
            TrackVisibleWindowBounds();
        }

        if (isLivingWidgetsMode && _desktopFollower != null)
        {
            _desktopFollower.TrackWindow(_cheatSheetOverlay);
        }

        var csRef = _cheatSheetOverlay;
        _updateIndicatorManager?.RegisterWidget("CheatSheetOverlay", 9, _cheatSheetOverlay,
            visible => Dispatcher.Invoke(() => csRef.SetUpdateIndicatorVisible(visible)));

        DebugLogger.Log($"CreateCheatSheetOverlay: Created at ({_cheatSheetOverlay.Left}, {_cheatSheetOverlay.Top})");
    }

    private void OnCheatSheetRequested(object? sender, EventArgs e)
    {
        try
        {
            if (_cheatSheetOverlay == null)
            {
                CreateCheatSheetOverlay();
            }
            else
            {
                if (_cheatSheetOverlay.Visibility == Visibility.Visible)
                {
                    _cheatSheetOverlay.Visibility = Visibility.Hidden;
                    _cheatSheetOverlay.Tag = null;
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.CheatSheet, false);
                    DebugLogger.Log("OnCheatSheetRequested: Cheat sheet overlay hidden");
                }
                else
                {
                    _cheatSheetOverlay.Visibility = Visibility.Visible;
                    _cheatSheetOverlay.Tag = "WasVisible";
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.CheatSheet, true);
                    DebugLogger.Log("OnCheatSheetRequested: Cheat sheet overlay shown");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnCheatSheetRequested: Error: {ex}");
            System.Windows.MessageBox.Show($"Error with cheat sheet overlay: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateMetricsViewerOverlay(double? left = null, double? top = null)
    {
        _metricsViewerOverlay = new MetricsViewerOverlay(_settings);
        RegisterWidgetWindow(_metricsViewerOverlay);
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        _metricsViewerOverlay.Topmost = !isLivingWidgetsMode;

        if (left.HasValue && top.HasValue)
        {
            _metricsViewerOverlay.Left = left.Value;
            _metricsViewerOverlay.Top = top.Value;
        }
        else if (isLivingWidgetsMode)
        {
            _metricsViewerOverlay.Left = this.Left + this.Width + GetConfiguredWidgetGap();
            _metricsViewerOverlay.Top = this.Top + 100;
        }
        else
        {
            var estimatedSize = new System.Windows.Size(_metricsViewerOverlay.Width > 0 ? _metricsViewerOverlay.Width : 500, _metricsViewerOverlay.Height > 0 ? _metricsViewerOverlay.Height : 400);
            var pos = ComputeNonLivePosition(_metricsViewerOverlay, WidgetIds.MetricsViewer, estimatedSize);
            if (pos == null) { UnregisterWidgetWindow(_metricsViewerOverlay); _metricsViewerOverlay = null; return; }
            _metricsViewerOverlay.Left = pos.Value.left;
            _metricsViewerOverlay.Top = pos.Value.top;
        }

        if (isLivingWidgetsMode)
            _metricsViewerOverlay.EnableDragging();

        _metricsViewerOverlay.Show();
        _metricsViewerOverlay.UpdateTransparency();
        _metricsViewerOverlay.Tag = "WasVisible";
        TelemetryAccessor.TrackWidgetVisibility(WidgetIds.MetricsViewer, true);

        if (isLivingWidgetsMode)
        {
            ApplyLiveLayoutForWindow(_metricsViewerOverlay);
            RefreshAttachmentMappings();
            TrackVisibleWindowBounds();
        }

        if (isLivingWidgetsMode && _desktopFollower != null)
        {
            _desktopFollower.TrackWindow(_metricsViewerOverlay);
        }

        var mvRef = _metricsViewerOverlay;
        _updateIndicatorManager?.RegisterWidget("MetricsViewerOverlay", 10, _metricsViewerOverlay,
            visible => Dispatcher.Invoke(() => mvRef.SetUpdateIndicatorVisible(visible)));

        DebugLogger.Log($"CreateMetricsViewerOverlay: Created at ({_metricsViewerOverlay.Left}, {_metricsViewerOverlay.Top})");
    }

    private void OnMetricsViewerRequested(object? sender, EventArgs e)
    {
        try
        {
            if (_metricsViewerOverlay == null)
            {
                CreateMetricsViewerOverlay();
            }
            else
            {
                if (_metricsViewerOverlay.Visibility == Visibility.Visible)
                {
                    _metricsViewerOverlay.Visibility = Visibility.Hidden;
                    _metricsViewerOverlay.Tag = null;
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.MetricsViewer, false);
                    DebugLogger.Log("OnMetricsViewerRequested: Metrics viewer hidden");
                }
                else
                {
                    _metricsViewerOverlay.Visibility = Visibility.Visible;
                    _metricsViewerOverlay.Tag = "WasVisible";
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.MetricsViewer, true);
                    DebugLogger.Log("OnMetricsViewerRequested: Metrics viewer shown");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnMetricsViewerRequested: Error: {ex}");
            System.Windows.MessageBox.Show($"Error with metrics viewer: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ===== Project Info Overlay =====

    private void CreateProjectInfoOverlay(double? left = null, double? top = null)
    {
        if (_tagService == null) return;

        _projectInfoOverlay = new ProjectInfoOverlay(_tagService, _vocabService, _settings);
        ApplyResponsiveWidgetWidth(_projectInfoOverlay);
        RegisterWidgetWindow(_projectInfoOverlay);
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        _projectInfoOverlay.Topmost = !isLivingWidgetsMode;

        if (left.HasValue && top.HasValue)
        {
            _projectInfoOverlay.Left = left.Value;
            _projectInfoOverlay.Top = top.Value;
        }
        else if (isLivingWidgetsMode)
        {
            _projectInfoOverlay.Left = this.Left + this.Width + GetConfiguredWidgetGap();
            _projectInfoOverlay.Top = this.Top + 200;
        }
        else
        {
            var estimatedSize = new System.Windows.Size(_projectInfoOverlay.Width > 0 ? _projectInfoOverlay.Width : GetResponsiveColumnWidgetWidth(), _projectInfoOverlay.Height > 0 ? _projectInfoOverlay.Height : 350);
            var pos = ComputeNonLivePosition(_projectInfoOverlay, WidgetIds.ProjectInfo, estimatedSize);
            if (pos == null) { UnregisterWidgetWindow(_projectInfoOverlay); _projectInfoOverlay = null; return; }
            _projectInfoOverlay.Left = pos.Value.left;
            _projectInfoOverlay.Top = pos.Value.top;
        }

        if (isLivingWidgetsMode)
            _projectInfoOverlay.EnableDragging();

        _projectInfoOverlay.Show();
        _projectInfoOverlay.UpdateTransparency();
        _projectInfoOverlay.Tag = "WasVisible";
        TelemetryAccessor.TrackWidgetVisibility(WidgetIds.ProjectInfo, true);
        UpdateDynamicOverlayMaxHeight(_projectInfoOverlay);

        // Auto-feed currently selected project
        FeedSelectedProjectToProjectInfo();

        if (isLivingWidgetsMode)
        {
            ApplyLiveLayoutForWindow(_projectInfoOverlay);
            RefreshAttachmentMappings();
            TrackVisibleWindowBounds();
        }

        if (isLivingWidgetsMode && _desktopFollower != null)
        {
            _desktopFollower.TrackWindow(_projectInfoOverlay);
        }

        var piRef = _projectInfoOverlay;
        _updateIndicatorManager?.RegisterWidget("ProjectInfoOverlay", 6, _projectInfoOverlay,
            visible => Dispatcher.Invoke(() => piRef.SetUpdateIndicatorVisible(visible)));

        DebugLogger.Log($"CreateProjectInfoOverlay: Created at ({_projectInfoOverlay.Left}, {_projectInfoOverlay.Top}), Topmost={_projectInfoOverlay.Topmost}");
    }

    private void OnProjectInfoRequested(object? sender, EventArgs e)
    {
        try
        {
            if (_projectInfoOverlay == null)
            {
                CreateProjectInfoOverlay();
            }
            else
            {
                if (_projectInfoOverlay.Visibility == Visibility.Visible)
                {
                    _projectInfoOverlay.HideAndLock();
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.ProjectInfo, false);
                    DebugLogger.Log("OnProjectInfoRequested: Project info hidden");
                }
                else
                {
                    _projectInfoOverlay.Visibility = Visibility.Visible;
                    _projectInfoOverlay.Tag = "WasVisible";
                    TelemetryAccessor.TrackWidgetVisibility(WidgetIds.ProjectInfo, true);
                    FeedSelectedProjectToProjectInfo();
                    DebugLogger.Log("OnProjectInfoRequested: Project info shown");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnProjectInfoRequested: Error: {ex}");
            System.Windows.MessageBox.Show($"Error with project info: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Feed the currently selected project from ResultsList to the Project Info widget.
    /// </summary>
    private void FeedSelectedProjectToProjectInfo()
    {
        if (_projectInfoOverlay?.Widget == null) return;

        if (ResultsList.SelectedItem is ProjectViewModel vm && !string.IsNullOrWhiteSpace(vm.FullNumber))
        {
            var displayName = $"{vm.FullNumber} {vm.Name}";
            _ = _projectInfoOverlay.Widget.SetProjectAsync(vm.FullNumber, displayName);
            DebugLogger.Log($"FeedSelectedProjectToProjectInfo: Fed {vm.FullNumber} to Project Info");
        }
    }

    private void PositionTimerOverlayOnSameScreen()
    {
        if (_timerOverlay == null)
            return;

        try
        {
            // Get the screen working area in DIPs for the screen containing the search overlay
            var searchRect = new Rect(this.Left, this.Top, this.Width, this.Height);
            var workArea = ScreenHelper.GetWorkingAreaFromDipRect(searchRect, this);

            // Position timer in bottom-right corner of the same screen
            _timerOverlay.Left = workArea.Right - _timerOverlay.Width - 20;
            _timerOverlay.Top = workArea.Bottom - _timerOverlay.Height - 20;

            DebugLogger.Log($"PositionTimerOverlayOnSameScreen: Timer positioned at ({_timerOverlay.Left}, {_timerOverlay.Top})");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"PositionTimerOverlayOnSameScreen: Error positioning timer: {ex.Message}");
        }
    }

    private void EnableWindowDragging()
    {
        // Remove handlers first to prevent duplicates when switching modes
        this.MouseLeftButtonDown -= Window_MouseLeftButtonDown;
        this.MouseLeftButtonUp -= Window_MouseLeftButtonUp;
        this.MouseMove -= Window_MouseMove;

        // Add mouse event handlers for dragging when Living Widgets Mode is enabled
        this.MouseLeftButtonDown += Window_MouseLeftButtonDown;
        this.MouseLeftButtonUp += Window_MouseLeftButtonUp;
        this.MouseMove += Window_MouseMove;
    }

    private void DisableWindowDragging()
    {
        // Remove mouse event handlers when Living Widgets Mode is disabled
        this.MouseLeftButtonDown -= Window_MouseLeftButtonDown;
        this.MouseLeftButtonUp -= Window_MouseLeftButtonUp;
        this.MouseMove -= Window_MouseMove;
    }

    private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Only allow dragging if Living Widgets Mode is enabled and not clicking on interactive elements
        if (!_settings.GetLivingWidgetsMode())
            return;

        // Don't start drag if clicking on interactive elements (textbox, buttons, list)
        var element = e.OriginalSource as FrameworkElement;
        if (element != null)
        {
            // Allow dragging only from non-interactive areas (borders, panels, window background)
            var clickedType = element.GetType().Name;
            if (clickedType == "TextBox" || clickedType == "Button" || clickedType == "ListBoxItem" ||
                clickedType == "ComboBox" || clickedType == "ScrollBar" || clickedType == "Thumb")
            {
                return;
            }
        }

        _isDragging = true;
        _dragStartPoint = e.GetPosition(this);
        this.CaptureMouse();
        DebugLogger.Log("Window dragging started");
    }

    private void Window_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            this.ReleaseMouseCapture();

            if (_settings.GetLivingWidgetsMode())
            {
                ApplyLiveLayoutForWindow(this);
                MoveAttachedFollowers(this);
                RefreshAttachmentMappings();
                TrackVisibleWindowBounds();
            }
            else
            {
                // Apply snap to screen edges if close
                SnapToScreenEdges();
            }

            DebugLogger.Log("Window dragging ended");
        }
    }

    private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var currentPosition = e.GetPosition(this);
            var offset = currentPosition - _dragStartPoint;

            this.Left += offset.X;
            this.Top += offset.Y;

            // Only update widget launcher position in Legacy mode (keep attached)
            // In Living Widgets Mode, widgets are independent
            var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
            if (!isLivingWidgetsMode)
            {
                UpdateWidgetLauncherPosition();
            }
        }
    }

    private void SnapToScreenEdges()
    {
        try
        {
            // Get the screen working area in DIPs for the screen containing the window
            var windowRect = new Rect(this.Left, this.Top, this.Width, this.Height);
            var workArea = ScreenHelper.GetWorkingAreaFromDipRect(windowRect, this);

            const int snapThreshold = 20; // pixels from edge to trigger snap

            // Snap to left edge
            if (Math.Abs(this.Left - workArea.Left) < snapThreshold)
            {
                this.Left = workArea.Left + 10; // 10px margin
                DebugLogger.Log("Snapped to left edge");
            }

            // Snap to right edge
            if (Math.Abs(this.Left + this.Width - workArea.Right) < snapThreshold)
            {
                this.Left = workArea.Right - this.Width - 10;
                DebugLogger.Log("Snapped to right edge");
            }

            // Snap to top edge
            if (Math.Abs(this.Top - workArea.Top) < snapThreshold)
            {
                this.Top = workArea.Top + 10;
                DebugLogger.Log("Snapped to top edge");
            }

            // Snap to bottom edge
            if (Math.Abs(this.Top + this.Height - workArea.Bottom) < snapThreshold)
            {
                this.Top = workArea.Bottom - this.Height - 10;
                DebugLogger.Log("Snapped to bottom edge");
            }

            // Snap to top-left corner
            if (Math.Abs(this.Left - workArea.Left) < snapThreshold &&
                Math.Abs(this.Top - workArea.Top) < snapThreshold)
            {
                this.Left = workArea.Left + 10;
                this.Top = workArea.Top + 10;
                DebugLogger.Log("Snapped to top-left corner");
            }

            // Snap to top-right corner
            if (Math.Abs(this.Left + this.Width - workArea.Right) < snapThreshold &&
                Math.Abs(this.Top - workArea.Top) < snapThreshold)
            {
                this.Left = workArea.Right - this.Width - 10;
                this.Top = workArea.Top + 10;
                DebugLogger.Log("Snapped to top-right corner");
            }

            // Snap to bottom-left corner
            if (Math.Abs(this.Left - workArea.Left) < snapThreshold &&
                Math.Abs(this.Top + this.Height - workArea.Bottom) < snapThreshold)
            {
                this.Left = workArea.Left + 10;
                this.Top = workArea.Bottom - this.Height - 10;
                DebugLogger.Log("Snapped to bottom-left corner");
            }

            // Snap to bottom-right corner
            if (Math.Abs(this.Left + this.Width - workArea.Right) < snapThreshold &&
                Math.Abs(this.Top + this.Height - workArea.Bottom) < snapThreshold)
            {
                this.Left = workArea.Right - this.Width - 10;
                this.Top = workArea.Bottom - this.Height - 10;
                DebugLogger.Log("Snapped to bottom-right corner");
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SnapToScreenEdges: Error: {ex.Message}");
        }
    }

    private void UpdateWidgetLauncherPosition()
    {
        if (_widgetLauncher != null && _widgetLauncher.Visibility == Visibility.Visible)
        {
            var windowWidth = this.ActualWidth > 0 ? this.ActualWidth : this.Width;
            _widgetLauncher.Left = this.Left + windowWidth + GetConfiguredWidgetGap();
            _widgetLauncher.Top = this.Top;
        }
    }

    private void StartDesktopFollower()
    {
        // Stop existing follower if any
        StopDesktopFollower();

        _desktopFollower = new Helpers.DesktopFollower();

        // Track all widget windows
        _desktopFollower.TrackWindow(this);

        if (_widgetLauncher != null)
        {
            _desktopFollower.TrackWindow(_widgetLauncher);
        }

        if (_timerOverlay != null)
        {
            _desktopFollower.TrackWindow(_timerOverlay);
        }

        if (_quickTasksOverlay != null)
        {
            _desktopFollower.TrackWindow(_quickTasksOverlay);
        }

        if (_docOverlay != null)
        {
            _desktopFollower.TrackWindow(_docOverlay);
        }

        if (_frequentProjectsOverlay != null)
        {
            _desktopFollower.TrackWindow(_frequentProjectsOverlay);
        }

        if (_quickLaunchOverlay != null)
        {
            _desktopFollower.TrackWindow(_quickLaunchOverlay);
        }

        if (_smartProjectSearchOverlay != null)
        {
            _desktopFollower.TrackWindow(_smartProjectSearchOverlay);
        }

        if (_cheatSheetOverlay != null)
        {
            _desktopFollower.TrackWindow(_cheatSheetOverlay);
        }

        if (_metricsViewerOverlay != null)
        {
            _desktopFollower.TrackWindow(_metricsViewerOverlay);
        }

        _desktopFollower.Start();
    }

    private void StopDesktopFollower()
    {
        if (_desktopFollower != null)
        {
            _desktopFollower.Stop();
            _desktopFollower.Dispose();
            _desktopFollower = null;
        }
    }

    /// <summary>
    /// Returns true if any managed widget window currently has focus (IsActive).
    /// Used by Window_Deactivated to prevent auto-hiding when the user clicks on a widget.
    /// </summary>
    private bool IsAnyManagedWidgetActive()
    {
        foreach (var w in new Window?[] {
            _widgetLauncher, _timerOverlay, _quickTasksOverlay, _docOverlay,
            _frequentProjectsOverlay, _quickLaunchOverlay, _smartProjectSearchOverlay,
            _cheatSheetOverlay, _metricsViewerOverlay, _projectInfoOverlay })
        {
            if (w != null && w.IsActive)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the widget ID string for a given window type (for pinned position lookup).
    /// </summary>
    private static string? GetWidgetId(Window window)
    {
        return window switch
        {
            TimerOverlay => WidgetIds.Timer,
            QuickTasksOverlay => WidgetIds.QuickTasks,
            DocQuickOpenOverlay => WidgetIds.DocQuickOpen,
            FrequentProjectsOverlay => WidgetIds.FrequentProjects,
            QuickLaunchOverlay => WidgetIds.QuickLaunch,
            SmartProjectSearchOverlay => WidgetIds.SmartProjectSearch,
            CheatSheetOverlay => WidgetIds.CheatSheet,
            MetricsViewerOverlay => WidgetIds.MetricsViewer,
            ProjectInfoOverlay => WidgetIds.ProjectInfo,
            _ => null
        };
    }

    /// <summary>
    /// Computes a position for a widget in non-live mode using the auto-grid layout manager.
    /// Returns (left, top) on success, or null if there's no room (shows a toast).
    /// </summary>
    private (double left, double top)? ComputeNonLivePosition(Window widget, string widgetId, System.Windows.Size widgetSize)
    {
        var anchorRect = GetWindowRect(this);
        var gap = GetConfiguredWidgetGap();
        var columnWidth = GetResponsiveColumnWidgetWidth();
        var layoutManager = new WidgetLayoutManager(gap, columnWidth, this);

        // Build occupied slot list from all currently visible managed widgets
        var occupiedSlots = new List<WidgetLayoutManager.OccupiedSlot>
        {
            new() { Bounds = anchorRect, Window = this }
        };

        if (_widgetLauncher != null && _widgetLauncher.Visibility == Visibility.Visible && _widgetLauncher.IsLoaded)
        {
            occupiedSlots.Add(new WidgetLayoutManager.OccupiedSlot
            {
                Bounds = GetWindowRect(_widgetLauncher),
                Window = _widgetLauncher
            });
        }

        foreach (var w in new Window?[] {
            _timerOverlay, _quickTasksOverlay, _docOverlay, _frequentProjectsOverlay,
            _quickLaunchOverlay, _smartProjectSearchOverlay, _cheatSheetOverlay,
            _metricsViewerOverlay, _projectInfoOverlay })
        {
            if (w != null && w != widget && w.Visibility == Visibility.Visible && w.IsLoaded)
            {
                occupiedSlots.Add(new WidgetLayoutManager.OccupiedSlot
                {
                    Bounds = GetWindowRect(w),
                    Window = w
                });
            }
        }

        // Check for pinned position
        (double left, double top)? pinned = null;
        var (pinnedLeft, pinnedTop) = _settings.GetWidgetPinnedPosition(widgetId);
        if (pinnedLeft.HasValue && pinnedTop.HasValue)
            pinned = (pinnedLeft.Value, pinnedTop.Value);

        var result = layoutManager.ComputePlacement(anchorRect, widgetSize, occupiedSlots, pinned);

        if (!result.Success)
        {
            ShowNoRoomToast(result.FailureReason ?? "Not enough screen space.");
            DebugLogger.Log($"ComputeNonLivePosition: No room for {widgetId} — {result.FailureReason}");
            return null;
        }

        return (result.Left, result.Top);
    }

    /// <summary>
    /// Re-arranges all visible widgets using the auto-grid layout manager.
    /// Called on startup after display config change and when widgets are hidden/shown in non-live mode.
    /// </summary>
    private void RearrangeNonLiveWidgets()
    {
        if (_settings.GetLivingWidgetsMode())
            return;

        var anchorRect = GetWindowRect(this);
        var gap = GetConfiguredWidgetGap();
        var columnWidth = GetResponsiveColumnWidgetWidth();
        var layoutManager = new WidgetLayoutManager(gap, columnWidth, this);

        // Collect all visible widgets with their sizes and pinned positions
        var widgets = new List<(Window window, System.Windows.Size size, (double left, double top)? pinnedPosition)>();

        // Widget launcher first (small, goes next to search)
        if (_widgetLauncher != null && _widgetLauncher.Visibility == Visibility.Visible && _widgetLauncher.IsLoaded)
        {
            var launcherRect = GetWindowRect(_widgetLauncher);
            widgets.Add((_widgetLauncher, new System.Windows.Size(launcherRect.Width, launcherRect.Height), null));
        }

        // Then all widget overlays in a consistent order
        var overlayWindows = new (Window? window, string widgetId)[]
        {
            (_timerOverlay, WidgetIds.Timer),
            (_quickTasksOverlay, WidgetIds.QuickTasks),
            (_docOverlay, WidgetIds.DocQuickOpen),
            (_frequentProjectsOverlay, WidgetIds.FrequentProjects),
            (_quickLaunchOverlay, WidgetIds.QuickLaunch),
            (_smartProjectSearchOverlay, WidgetIds.SmartProjectSearch),
            (_cheatSheetOverlay, WidgetIds.CheatSheet),
            (_metricsViewerOverlay, WidgetIds.MetricsViewer),
            (_projectInfoOverlay, WidgetIds.ProjectInfo),
        };

        foreach (var (window, widgetId) in overlayWindows)
        {
            if (window == null || window.Visibility != Visibility.Visible || !window.IsLoaded)
                continue;

            var rect = GetWindowRect(window);
            (double left, double top)? pinned = null;
            var (pinnedLeft, pinnedTop) = _settings.GetWidgetPinnedPosition(widgetId);
            if (pinnedLeft.HasValue && pinnedTop.HasValue)
                pinned = (pinnedLeft.Value, pinnedTop.Value);

            widgets.Add((window, new System.Windows.Size(rect.Width, rect.Height), pinned));
        }

        var results = layoutManager.ArrangeAll(anchorRect, widgets);

        var previousAutoArrange = _isAutoArrangingWidgets;
        _isAutoArrangingWidgets = true;
        try
        {
            foreach (var (window, result) in results)
            {
                if (result.Success)
                {
                    window.Left = result.Left;
                    window.Top = result.Top;
                    DebugLogger.Log($"RearrangeNonLiveWidgets: {window.GetType().Name} → ({result.Left:F0}, {result.Top:F0})");
                }
                else
                {
                    DebugLogger.Log($"RearrangeNonLiveWidgets: {window.GetType().Name} could not be placed — {result.FailureReason}");
                }
            }
        }
        finally
        {
            _isAutoArrangingWidgets = previousAutoArrange;
        }
    }

    /// <summary>
    /// Hides all visible non-live widgets, preserving Tag="WasVisible" so they can be restored.
    /// Called from HideOverlay in non-live mode.
    /// </summary>
    private void HideNonLiveWidgets()
    {
        foreach (var w in new Window?[] {
            _timerOverlay, _quickTasksOverlay, _docOverlay, _frequentProjectsOverlay,
            _quickLaunchOverlay, _smartProjectSearchOverlay, _cheatSheetOverlay,
            _metricsViewerOverlay, _projectInfoOverlay })
        {
            if (w != null && w.Visibility == Visibility.Visible)
            {
                // Tag="WasVisible" is already set when the widget was created/shown —
                // we keep it so ShowNonLiveWidgets can restore visibility.
                w.Visibility = Visibility.Hidden;
            }
        }
        DebugLogger.Log("HideNonLiveWidgets: All visible widgets hidden");
    }

    /// <summary>
    /// Re-shows widgets that had Tag="WasVisible" when HideNonLiveWidgets last ran.
    /// Called from ShowOverlay in non-live mode.
    /// </summary>
    private void ShowNonLiveWidgets()
    {
        foreach (var w in new Window?[] {
            _timerOverlay, _quickTasksOverlay, _docOverlay, _frequentProjectsOverlay,
            _quickLaunchOverlay, _smartProjectSearchOverlay, _cheatSheetOverlay,
            _metricsViewerOverlay, _projectInfoOverlay })
        {
            if (w != null && w.Tag is "WasVisible" && w.Visibility != Visibility.Visible)
            {
                w.Visibility = Visibility.Visible;
            }
        }
        DebugLogger.Log("ShowNonLiveWidgets: Restored previously visible widgets");
    }

    private void ShowNoRoomToast(string message)
    {
        try
        {
            var toast = new ToastNotification("Widget Layout", message, _settings.GetNotificationDurationMs());
            toast.Show();
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"ShowNoRoomToast: Failed to show toast: {ex.Message}");
        }
    }

    public void UpdateDraggingMode()
    {
        // Called when Living Widgets Mode setting changes
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();

        if (isLivingWidgetsMode)
        {
            EnableWindowDragging();
            this.Topmost = false; // Live on desktop, not always on top

            // Enable dragging and disable Topmost for widget launcher too
            if (_widgetLauncher != null)
            {
                _widgetLauncher.EnableDragging();
                _widgetLauncher.Topmost = false;
            }

            // Enable dragging and disable Topmost for timer overlay too if it exists
            if (_timerOverlay != null)
            {
                _timerOverlay.EnableDragging();
                _timerOverlay.Topmost = false;
            }

            // Enable dragging and disable Topmost for quick tasks overlay too if it exists
            if (_quickTasksOverlay != null)
            {
                _quickTasksOverlay.EnableDragging();
                _quickTasksOverlay.Topmost = false;
            }

            // Enable dragging and disable Topmost for doc overlay too if it exists
            if (_docOverlay != null)
            {
                _docOverlay.EnableDragging();
                _docOverlay.Topmost = false;
            }

            if (_frequentProjectsOverlay != null)
            {
                _frequentProjectsOverlay.EnableDragging();
                _frequentProjectsOverlay.Topmost = false;
            }

            if (_quickLaunchOverlay != null)
            {
                _quickLaunchOverlay.EnableDragging();
                _quickLaunchOverlay.Topmost = false;
            }

            if (_smartProjectSearchOverlay != null)
            {
                _smartProjectSearchOverlay.EnableDragging();
                _smartProjectSearchOverlay.Topmost = false;
            }

            if (_cheatSheetOverlay != null)
            {
                _cheatSheetOverlay.EnableDragging();
                _cheatSheetOverlay.Topmost = false;
            }

            if (_metricsViewerOverlay != null)
            {
                _metricsViewerOverlay.EnableDragging();
                _metricsViewerOverlay.Topmost = false;
            }

            // Start following desktop switches
            StartDesktopFollower();

            RefreshAttachmentMappings();
            TrackVisibleWindowBounds();

            DebugLogger.Log("Window dragging enabled (Living Widgets Mode ON) - Topmost disabled");
        }
        else
        {
            DisableWindowDragging();
            this.Topmost = true; // Legacy mode: always on top

            // Disable dragging and enable Topmost for widget launcher too
            if (_widgetLauncher != null)
            {
                _widgetLauncher.DisableDragging();
                _widgetLauncher.Topmost = true;
            }

            // Disable dragging and enable Topmost for timer overlay too if it exists
            if (_timerOverlay != null)
            {
                _timerOverlay.DisableDragging();
                _timerOverlay.Topmost = true;
            }

            // Disable dragging and enable Topmost for quick tasks overlay too if it exists
            if (_quickTasksOverlay != null)
            {
                _quickTasksOverlay.DisableDragging();
                _quickTasksOverlay.Topmost = true;
            }

            // Disable dragging and enable Topmost for doc overlay too if it exists
            if (_docOverlay != null)
            {
                _docOverlay.DisableDragging();
                _docOverlay.Topmost = true;
            }

            if (_frequentProjectsOverlay != null)
            {
                _frequentProjectsOverlay.DisableDragging();
                _frequentProjectsOverlay.Topmost = true;
            }

            if (_quickLaunchOverlay != null)
            {
                _quickLaunchOverlay.DisableDragging();
                _quickLaunchOverlay.Topmost = true;
            }

            if (_smartProjectSearchOverlay != null)
            {
                _smartProjectSearchOverlay.DisableDragging();
                _smartProjectSearchOverlay.Topmost = true;
            }

            if (_cheatSheetOverlay != null)
            {
                _cheatSheetOverlay.DisableDragging();
                _cheatSheetOverlay.Topmost = true;
            }

            if (_metricsViewerOverlay != null)
            {
                _metricsViewerOverlay.DisableDragging();
                _metricsViewerOverlay.Topmost = true;
            }

            // Stop following desktop switches
            StopDesktopFollower();

            _verticalAttachments.Clear();
            _lastWidgetBounds.Clear();

            DebugLogger.Log("Window dragging disabled (Living Widgets Mode OFF) - Topmost enabled");
        }
    }
}
