using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using DesktopHub.UI.Services;

namespace DesktopHub.UI;

public partial class SearchOverlay
{
    public async void OnDriveSettingsChanged()
    {
        try
        {
            DebugLogger.Log("OnDriveSettingsChanged: Drive settings changed, reloading projects...");

            // Reload projects from database to apply new filtering
            await Dispatcher.InvokeAsync(async () =>
            {
                await LoadProjectsAsync();

                // Trigger a background scan to pick up newly enabled drives
                _ = BackgroundScanAsync();
            });
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnDriveSettingsChanged: Error reloading projects: {ex.Message}");
        }
    }

    public void UpdateSearchWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetSearchWidgetEnabled();
            _widgetLauncher.UpdateSearchButtonVisibility(enabled);
            DebugLogger.Log($"UpdateSearchWidgetButton: Search button visibility set to {enabled}");
        }
    }

    public void UpdateWidgetLauncherLayout()
    {
        _widgetLauncher?.RefreshLayoutFromSettings();
        DebugLogger.Log("UpdateWidgetLauncherLayout: Launcher layout refreshed from settings");
    }

    public void UpdateTimerWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetTimerWidgetEnabled();
            _widgetLauncher.UpdateTimerButtonVisibility(enabled);
            DebugLogger.Log($"UpdateTimerWidgetButton: Timer button visibility set to {enabled}");
        }
    }

    public void UpdateQuickTasksWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetQuickTasksWidgetEnabled();
            _widgetLauncher.UpdateQuickTasksButtonVisibility(enabled);
            DebugLogger.Log($"UpdateQuickTasksWidgetButton: QuickTasks button visibility set to {enabled}");
        }
    }

    public void UpdateDocWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetDocWidgetEnabled();
            _widgetLauncher.UpdateDocButtonVisibility(enabled);
            DebugLogger.Log($"UpdateDocWidgetButton: Doc button visibility set to {enabled}");
        }
    }

    public void UpdateFrequentProjectsWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetFrequentProjectsWidgetEnabled();
            _widgetLauncher.UpdateFrequentProjectsButtonVisibility(enabled);
            DebugLogger.Log($"UpdateFrequentProjectsWidgetButton: visibility set to {enabled}");
        }
    }

    public void UpdateFrequentProjectsLayout()
    {
        if (_frequentProjectsOverlay != null && _frequentProjectsOverlay.IsVisible)
        {
            var left = _frequentProjectsOverlay.Left;
            var top = _frequentProjectsOverlay.Top;
            _frequentProjectsOverlay.Close();
            _frequentProjectsOverlay = null;

            CreateFrequentProjectsOverlay(left, top);
            _frequentProjectsOverlay?.Show();
            DebugLogger.Log("UpdateFrequentProjectsLayout: Recreated Frequent Projects overlay with new layout");
        }
    }

    public void UpdateQuickLaunchWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetQuickLaunchWidgetEnabled();
            _widgetLauncher.UpdateQuickLaunchButtonVisibility(enabled);
            DebugLogger.Log($"UpdateQuickLaunchWidgetButton: visibility set to {enabled}");
        }
    }

    public void UpdateSmartProjectSearchWidgetButton()
    {
        var attachModeEnabled = _settings.GetSmartProjectSearchAttachToSearchOverlayMode();

        if (_widgetLauncher != null)
        {
            var launcherVisible = _settings.GetSmartProjectSearchWidgetEnabled() && !attachModeEnabled;
            _widgetLauncher.UpdateSmartProjectSearchButtonVisibility(launcherVisible);
            DebugLogger.Log($"UpdateSmartProjectSearchWidgetButton: launcher visibility set to {launcherVisible} (attach mode: {attachModeEnabled})");
        }

        ApplySmartProjectSearchAttachModeState();
    }

    public void UpdateCheatSheetWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetCheatSheetWidgetEnabled();
            _widgetLauncher.UpdateCheatSheetButtonVisibility(enabled);
            DebugLogger.Log($"UpdateCheatSheetWidgetButton: visibility set to {enabled}");
        }
    }

    public void SetMetricsViewerEnabled(bool enabled)
    {
        if (_widgetLauncher != null)
        {
            _widgetLauncher.UpdateMetricsViewerButtonVisibility(enabled);
            DebugLogger.Log($"SetMetricsViewerEnabled: visibility set to {enabled}");
        }
    }

    public void UpdateMetricsViewerWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetWidgetEnabled(Core.Models.WidgetIds.MetricsViewer);
            _widgetLauncher.UpdateMetricsViewerButtonVisibility(enabled);
            DebugLogger.Log($"UpdateMetricsViewerWidgetButton: visibility set to {enabled}");
        }
    }

    public void UpdateQuickLaunchLayout()
    {
        if (_quickLaunchOverlay != null && _quickLaunchOverlay.IsVisible)
        {
            // Get current position before closing
            var left = _quickLaunchOverlay.Left;
            var top = _quickLaunchOverlay.Top;
            _quickLaunchOverlay.Close();
            _quickLaunchOverlay = null;

            // Recreate with new layout
            CreateQuickLaunchOverlay(left, top);
            _quickLaunchOverlay?.Show();
            DebugLogger.Log("UpdateQuickLaunchLayout: Recreated Quick Launch overlay with new layout");
        }
    }

    public void RefreshLiveWidgetLayout()
    {
        if (!_settings.GetLivingWidgetsMode())
            return;

        foreach (var window in GetManagedWidgetWindows().ToList())
        {
            ApplyLiveLayoutForWindow(window);
        }

        RefreshAttachmentMappings();
        TrackVisibleWindowBounds();
    }

    public void SetUpdateIndicatorVisible(bool visible)
    {
        if (UpdateIndicator != null)
            UpdateIndicator.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateIndicator_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2)
            return;

        e.Handled = true;
        _trayIcon?.BeginUpdateFromIndicator();
    }

    public UpdateCheckService? UpdateCheckService => _updateCheckService;
    public UpdateIndicatorManager? UpdateIndicatorManager => _updateIndicatorManager;

    private void InitializeUpdateCheckService()
    {
        try
        {
            var app = System.Windows.Application.Current as App;
            var firebaseManager = app?.FirebaseManager;

            if (firebaseManager == null)
            {
                DebugLogger.Log("InitializeUpdateCheckService: FirebaseManager not available, skipping");
                return;
            }

            _updateCheckService = new UpdateCheckService(_settings, () => firebaseManager.CheckForUpdatesAsync());
            _updateCheckService.UpdateAvailable += (sender, updateInfo) =>
            {
                DebugLogger.Log($"Update notification received: v{updateInfo.LatestVersion} available");
                _updateIndicatorManager?.SetUpdateAvailable(true);

                // Auto-install if user opted in
                if (_settings.GetAutoUpdateInstallEnabled() && _trayIcon != null)
                {
                    DebugLogger.Log("UpdateCheckService: AutoUpdateInstall enabled — triggering silent update");
                    _ = Task.Run(async () =>
                    {
                        try { await _trayIcon.DownloadAndInstallUpdateAsync(updateInfo, silent: true); }
                        catch (Exception ex) { DebugLogger.Log($"UpdateCheckService: Auto-install failed: {ex.Message}"); }
                    });
                }
            };
            _updateCheckService.UpdateDismissed += (sender, _) =>
            {
                _updateIndicatorManager?.SetUpdateAvailable(false);
            };
            _updateCheckService.Start();
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"InitializeUpdateCheckService: Error: {ex.Message}");
        }
    }

    public void RefreshUpdateIndicator()
    {
        _updateIndicatorManager?.Refresh();
    }

    public void RestartUpdateCheckService()
    {
        _updateCheckService?.Restart();
    }

    /// <summary>
    /// Called when ThemeService switches themes. Re-applies transparency so all overlays
    /// pick up the new theme's background color instead of keeping the old baked-in color.
    /// </summary>
    private void OnThemeChanged(string resolvedTheme)
    {
        Dispatcher.BeginInvoke(() =>
        {
            DebugLogger.Log($"OnThemeChanged: Theme switched to {resolvedTheme}, re-applying transparency on all overlays");
            UpdateTransparency();
        });
    }

    public void UpdateTransparency()
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                // Update search overlay transparency
                var overlayTransparency = _settings.GetOverlayTransparency();
                var overlayAlpha = (byte)(overlayTransparency * 255);
                var bgBase = Helpers.ThemeHelper.GetColor("WindowBackgroundColor");
                RootBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(overlayAlpha, bgBase.R, bgBase.G, bgBase.B));

                DebugLogger.Log($"UpdateTransparency: SearchOverlay transparency updated to {overlayTransparency:F2}");

                // Update widget launcher transparency if it exists
                if (_widgetLauncher != null)
                {
                    _widgetLauncher.UpdateTransparency();
                }

                // Update timer overlay transparency if it exists
                if (_timerOverlay != null)
                {
                    _timerOverlay.UpdateTransparency();
                }

                // Update quick tasks overlay transparency if it exists
                if (_quickTasksOverlay != null)
                {
                    _quickTasksOverlay.UpdateTransparency();
                }

                // Update doc overlay transparency if it exists
                if (_docOverlay != null)
                {
                    _docOverlay.UpdateTransparency();
                }

                // Update frequent projects overlay transparency if it exists
                if (_frequentProjectsOverlay != null)
                {
                    _frequentProjectsOverlay.UpdateTransparency();
                }

                // Update quick launch overlay transparency if it exists
                if (_quickLaunchOverlay != null)
                {
                    _quickLaunchOverlay.UpdateTransparency();
                }

                if (_smartProjectSearchOverlay != null)
                {
                    _smartProjectSearchOverlay.UpdateTransparency();
                }

                if (_cheatSheetOverlay != null)
                {
                    _cheatSheetOverlay.UpdateTransparency();
                }

                if (_projectInfoOverlay != null)
                {
                    _projectInfoOverlay.UpdateTransparency();
                }

                if (_metricsViewerOverlay != null)
                {
                    _metricsViewerOverlay.UpdateTransparency();
                }

                UpdateSmartSearchAttachedWindowTransparency();
            });
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"UpdateTransparency: Error updating transparency: {ex.Message}");
        }
    }
}
