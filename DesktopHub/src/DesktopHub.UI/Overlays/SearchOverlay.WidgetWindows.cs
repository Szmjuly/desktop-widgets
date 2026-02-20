using System;
using System.Windows;
using System.Windows.Forms;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class SearchOverlay
{
    private void CreateTimerOverlay(double? left = null, double? top = null)
    {
        _timerOverlay = new TimerOverlay(_timerService, _settings);
        RegisterWidgetWindow(_timerOverlay);
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        _timerOverlay.Topmost = !isLivingWidgetsMode;

        // Use provided position, then saved position, then default
        var (savedLeft, savedTop) = _settings.GetTimerWidgetPosition();
        _timerOverlay.Left = left ?? savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
        _timerOverlay.Top = top ?? savedTop ?? this.Top;

        if (isLivingWidgetsMode)
            _timerOverlay.EnableDragging();

        _timerOverlay.Show();
        _timerOverlay.Tag = "WasVisible";

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
                    DebugLogger.Log("OnTimerWidgetRequested: Timer overlay hidden");
                }
                else
                {
                    _timerOverlay.Visibility = Visibility.Visible;
                    _timerOverlay.Tag = "WasVisible";
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

        // Use provided position, then saved position, then default
        var (savedLeft, savedTop) = _settings.GetQuickTasksWidgetPosition();
        _quickTasksOverlay.Left = left ?? savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
        _quickTasksOverlay.Top = top ?? savedTop ?? this.Top;

        if (isLivingWidgetsMode)
            _quickTasksOverlay.EnableDragging();

        _quickTasksOverlay.Show();
        _quickTasksOverlay.Tag = "WasVisible";

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
                    DebugLogger.Log("OnQuickTasksWidgetRequested: Quick Tasks overlay hidden");
                }
                else
                {
                    _quickTasksOverlay.Visibility = Visibility.Visible;
                    _quickTasksOverlay.Tag = "WasVisible";
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

        var (savedLeft, savedTop) = _settings.GetDocWidgetPosition();
        _docOverlay.Left = left ?? savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
        _docOverlay.Top = top ?? savedTop ?? (this.Top + 100);

        if (isLivingWidgetsMode)
            _docOverlay.EnableDragging();

        _docOverlay.Show();
        _docOverlay.Tag = "WasVisible";
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
                    DebugLogger.Log("OnDocQuickOpenRequested: Doc overlay hidden");
                }
                else
                {
                    _docOverlay.Visibility = Visibility.Visible;
                    _docOverlay.Tag = "WasVisible";
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
                SearchBox.Text = path;
                SearchBox.Focus();
                SearchBox.CaretIndex = path.Length;
                DebugLogger.Log($"FrequentProjectsOverlay: Loaded project path into search field: {path}");
            });
        };
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        _frequentProjectsOverlay.Topmost = !isLivingWidgetsMode;

        var (savedLeft, savedTop) = _settings.GetFrequentProjectsWidgetPosition();
        _frequentProjectsOverlay.Left = left ?? savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
        _frequentProjectsOverlay.Top = top ?? savedTop ?? (this.Top + 200);

        if (isLivingWidgetsMode)
            _frequentProjectsOverlay.EnableDragging();

        _frequentProjectsOverlay.Show();
        _frequentProjectsOverlay.UpdateTransparency();
        _frequentProjectsOverlay.Tag = "WasVisible";

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
                    DebugLogger.Log("OnFrequentProjectsRequested: Frequent projects overlay hidden");
                }
                else
                {
                    _frequentProjectsOverlay.Visibility = Visibility.Visible;
                    _frequentProjectsOverlay.Tag = "WasVisible";
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

        var (savedLeft, savedTop) = _settings.GetQuickLaunchWidgetPosition();
        _quickLaunchOverlay.Left = left ?? savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
        _quickLaunchOverlay.Top = top ?? savedTop ?? (this.Top + 300);

        if (isLivingWidgetsMode)
            _quickLaunchOverlay.EnableDragging();

        _quickLaunchOverlay.Show();
        _quickLaunchOverlay.UpdateTransparency();
        _quickLaunchOverlay.Tag = "WasVisible";

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
                    DebugLogger.Log("OnQuickLaunchRequested: Quick launch overlay hidden");
                }
                else
                {
                    _quickLaunchOverlay.Visibility = Visibility.Visible;
                    _quickLaunchOverlay.Tag = "WasVisible";
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
        if (_smartProjectSearchService == null)
            return;

        _smartProjectSearchOverlay = new SmartProjectSearchOverlay(_smartProjectSearchService, _settings);
        ApplyResponsiveWidgetWidth(_smartProjectSearchOverlay);
        RegisterWidgetWindow(_smartProjectSearchOverlay);
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        _smartProjectSearchOverlay.Topmost = !isLivingWidgetsMode;

        var (savedLeft, savedTop) = _settings.GetSmartProjectSearchWidgetPosition();
        _smartProjectSearchOverlay.Left = left ?? savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
        _smartProjectSearchOverlay.Top = top ?? savedTop ?? (this.Top + 380);

        if (isLivingWidgetsMode)
            _smartProjectSearchOverlay.EnableDragging();

        _smartProjectSearchOverlay.Show();
        _smartProjectSearchOverlay.UpdateTransparency();
        _smartProjectSearchOverlay.Tag = "WasVisible";

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
                    DebugLogger.Log("OnSmartProjectSearchRequested: Smart search overlay hidden");
                }
                else
                {
                    _smartProjectSearchOverlay.Visibility = Visibility.Visible;
                    _smartProjectSearchOverlay.Tag = "WasVisible";
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

    private void PositionTimerOverlayOnSameScreen()
    {
        if (_timerOverlay == null)
            return;

        try
        {
            // Get the screen containing the search overlay
            var searchOverlayCenter = new System.Drawing.Point(
                (int)(this.Left + this.Width / 2),
                (int)(this.Top + this.Height / 2)
            );
            var screen = Screen.FromPoint(searchOverlayCenter);
            var workArea = screen.WorkingArea;

            // Position timer in bottom-right corner of the same screen
            _timerOverlay.Left = workArea.Right - _timerOverlay.Width - 20;
            _timerOverlay.Top = workArea.Bottom - _timerOverlay.Height - 20;

            DebugLogger.Log($"PositionTimerOverlayOnSameScreen: Timer positioned at ({_timerOverlay.Left}, {_timerOverlay.Top}) on screen {screen.DeviceName}");
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
            // Get the screen containing the window
            var windowCenter = new System.Drawing.Point(
                (int)(this.Left + this.Width / 2),
                (int)(this.Top + this.Height / 2)
            );
            var screen = Screen.FromPoint(windowCenter);
            var workArea = screen.WorkingArea;

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

            // Stop following desktop switches
            StopDesktopFollower();

            _verticalAttachments.Clear();
            _lastWidgetBounds.Clear();

            DebugLogger.Log("Window dragging disabled (Living Widgets Mode OFF) - Topmost enabled");
        }
    }
}
