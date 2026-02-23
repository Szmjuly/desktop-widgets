using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class SearchOverlay
{
    private void Window_Deactivated(object sender, EventArgs e)
    {
        DebugLogger.LogSeparator("WINDOW DEACTIVATED");
        DebugLogger.LogHeader("Window Lost Focus");
        DebugLogger.LogVariable("Window.IsActive", this.IsActive);
        DebugLogger.LogVariable("Window.IsFocused", this.IsFocused);
        DebugLogger.LogVariable("Window.Visibility", this.Visibility);
        DebugLogger.LogVariable("_isTogglingViaHotkey", _isTogglingViaHotkey);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);

        // Don't auto-hide if Living Widgets Mode is enabled
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        DebugLogger.LogVariable("LivingWidgetsMode", isLivingWidgetsMode);
        if (isLivingWidgetsMode)
        {
            DebugLogger.Log("Window_Deactivated: IGNORING - Living Widgets Mode is enabled");
            return;
        }

        // Don't auto-hide if we're in the middle of a hotkey toggle
        if (_isTogglingViaHotkey)
        {
            DebugLogger.Log("Window_Deactivated: IGNORING - hotkey toggle in progress");
            return;
        }

        // Don't process if window is closing or not loaded
        if (!IsLoaded || IsClosing)
        {
            DebugLogger.Log("Window_Deactivated: IGNORING - window is closing or not loaded");
            return;
        }

        // Use a delayed auto-hide to avoid race conditions with hotkey toggles
        // Cancel any existing timer
        if (_deactivateTimer != null)
        {
            DebugLogger.Log("Window_Deactivated: Stopping existing deactivate timer");
            _deactivateTimer.Stop();
        }

        // Create new timer with 150ms delay
        DebugLogger.Log("Window_Deactivated: Starting 150ms delay timer before auto-hide");
        _deactivateTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _deactivateTimer.Tick += (s, args) =>
        {
            DebugLogger.LogHeader("Deactivate Timer Tick (after 150ms)");
            _deactivateTimer?.Stop();

            // Additional safety check - don't process if window is closing or not loaded
            if (!IsLoaded || _isClosing)
            {
                DebugLogger.Log("Deactivate Timer: IGNORING - window is closing or not loaded");
                return;
            }

            DebugLogger.LogVariable("Window.IsActive (now)", this.IsActive);
            DebugLogger.LogVariable("_isTogglingViaHotkey (now)", _isTogglingViaHotkey);
            DebugLogger.LogVariable("Window.Visibility (now)", this.Visibility);
            DebugLogger.LogVariable("_isClosing", _isClosing);

            // Check if widget launcher, timer overlay, or smart search attached window is active
            var isWidgetLauncherActive = _widgetLauncher != null && _widgetLauncher.IsActive;
            var isTimerOverlayActive = _timerOverlay != null && _timerOverlay.IsActive;
            var isSmartSearchActive = IsSmartSearchAttachedWindowActive;

            DebugLogger.LogVariable("WidgetLauncher.IsActive", isWidgetLauncherActive);
            DebugLogger.LogVariable("TimerOverlay.IsActive", isTimerOverlayActive);
            DebugLogger.LogVariable("SmartSearchAttached.IsActive", isSmartSearchActive);

            // Double-check we're still deactivated and not toggling
            // Don't auto-hide if widget launcher, timer overlay, or smart search window is active
            if (!this.IsActive && !_isTogglingViaHotkey && this.Visibility == Visibility.Visible
                && !isWidgetLauncherActive && !isTimerOverlayActive && !isSmartSearchActive && !_isClosing)
            {
                DebugLogger.Log("Window_Deactivated: Conditions met -> AUTO-HIDING overlay");
                HideOverlay();
            }
            else
            {
                DebugLogger.Log("Window_Deactivated: SKIPPING auto-hide:");
                DebugLogger.LogVariable("  Reason: IsActive", this.IsActive);
                DebugLogger.LogVariable("  Reason: _isTogglingViaHotkey", _isTogglingViaHotkey);
                DebugLogger.LogVariable("  Reason: Visibility", this.Visibility);
                DebugLogger.LogVariable("  Reason: _isClosing", _isClosing);
            }
        };
        _deactivateTimer.Start();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        DebugLogger.Log("Window_Closing: Setting closing state");
        _isClosing = true;

        // Stop desktop follower
        StopDesktopFollower();

        // Save Living Widgets Mode-specific positions (search overlay, widget launcher)
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        if (isLivingWidgetsMode)
        {
            _settings.SetSearchOverlayPosition(this.Left, this.Top);
            _settings.SetSearchOverlayVisible(this.Visibility == Visibility.Visible);

            if (_widgetLauncher != null)
            {
                _settings.SetWidgetLauncherPosition(_widgetLauncher.Left, _widgetLauncher.Top);
                // Save widget launcher visibility state
                _settings.SetWidgetLauncherVisible(_widgetLauncher.Visibility == Visibility.Visible);
            }
        }

        // Always save individual widget positions and visibility (regardless of Living Widgets Mode)
        if (_timerOverlay != null)
        {
            _settings.SetTimerWidgetPosition(_timerOverlay.Left, _timerOverlay.Top);
            _settings.SetTimerWidgetVisible(_timerOverlay.Visibility == Visibility.Visible);
            DebugLogger.Log($"Window_Closing: Saved timer position: ({_timerOverlay.Left}, {_timerOverlay.Top}), visible: {_timerOverlay.Visibility == Visibility.Visible}");
        }
        else
        {
            _settings.SetTimerWidgetVisible(false);
        }

        if (_quickTasksOverlay != null)
        {
            _settings.SetQuickTasksWidgetPosition(_quickTasksOverlay.Left, _quickTasksOverlay.Top);
            _settings.SetQuickTasksWidgetVisible(_quickTasksOverlay.Visibility == Visibility.Visible);
            DebugLogger.Log($"Window_Closing: Saved quick tasks position: ({_quickTasksOverlay.Left}, {_quickTasksOverlay.Top}), visible: {_quickTasksOverlay.Visibility == Visibility.Visible}");
        }
        else
        {
            _settings.SetQuickTasksWidgetVisible(false);
        }

        if (_docOverlay != null)
        {
            _settings.SetDocWidgetPosition(_docOverlay.Left, _docOverlay.Top);
            _settings.SetDocWidgetVisible(_docOverlay.Visibility == Visibility.Visible);
            DebugLogger.Log($"Window_Closing: Saved doc overlay position: ({_docOverlay.Left}, {_docOverlay.Top}), visible: {_docOverlay.Visibility == Visibility.Visible}");
        }
        else
        {
            _settings.SetDocWidgetVisible(false);
        }

        if (_frequentProjectsOverlay != null)
        {
            _settings.SetFrequentProjectsWidgetPosition(_frequentProjectsOverlay.Left, _frequentProjectsOverlay.Top);
            _settings.SetFrequentProjectsWidgetVisible(_frequentProjectsOverlay.Visibility == Visibility.Visible);
            DebugLogger.Log($"Window_Closing: Saved frequent projects overlay position: ({_frequentProjectsOverlay.Left}, {_frequentProjectsOverlay.Top})");
        }
        else
        {
            _settings.SetFrequentProjectsWidgetVisible(false);
        }

        if (_quickLaunchOverlay != null)
        {
            _settings.SetQuickLaunchWidgetPosition(_quickLaunchOverlay.Left, _quickLaunchOverlay.Top);
            _settings.SetQuickLaunchWidgetVisible(_quickLaunchOverlay.Visibility == Visibility.Visible);
            DebugLogger.Log($"Window_Closing: Saved quick launch overlay position: ({_quickLaunchOverlay.Left}, {_quickLaunchOverlay.Top})");
        }
        else
        {
            _settings.SetQuickLaunchWidgetVisible(false);
        }

        if (_smartProjectSearchOverlay != null)
        {
            _settings.SetSmartProjectSearchWidgetPosition(_smartProjectSearchOverlay.Left, _smartProjectSearchOverlay.Top);
            _settings.SetSmartProjectSearchWidgetVisible(_smartProjectSearchOverlay.Visibility == Visibility.Visible);
            DebugLogger.Log($"Window_Closing: Saved smart search overlay position: ({_smartProjectSearchOverlay.Left}, {_smartProjectSearchOverlay.Top})");
        }
        else
        {
            _settings.SetSmartProjectSearchWidgetVisible(false);
        }

        // Save async but don't await (app is closing)
        _ = _settings.SaveAsync();
        DebugLogger.Log("Window_Closing: Saved widget positions and visibility state");
    }

    private void ShowLoading(bool show)
    {
        LoadingIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private static System.Windows.Media.Color Blend(System.Windows.Media.Color baseColor, System.Windows.Media.Color tint, double amount)
    {
        amount = Math.Max(0, Math.Min(1, amount));
        byte r = (byte)Math.Round(baseColor.R + (tint.R - baseColor.R) * amount);
        byte g = (byte)Math.Round(baseColor.G + (tint.G - baseColor.G) * amount);
        byte b = (byte)Math.Round(baseColor.B + (tint.B - baseColor.B) * amount);
        return System.Windows.Media.Color.FromRgb(r, g, b);
    }

    private static double GetLuminance(System.Windows.Media.Color color)
    {
        return (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
    }

    public void ReloadHotkey()
    {
        Dispatcher.Invoke(async () =>
        {
            try
            {
                _hotkey?.Dispose();

                await _settings.LoadAsync();
                var (modifiers, key) = _settings.GetHotkey();

                _hotkey = new GlobalHotkey(this, (uint)modifiers, (uint)key);
                _hotkey.HotkeyPressed += OnHotkeyPressed;

                StatusText.Text = $"Hotkey updated to {FormatHotkey(modifiers, key)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to update hotkey: {ex.Message}";
            }
        });
    }

    private void StartIpcListener()
    {
        _ipcCts = new CancellationTokenSource();
        var token = _ipcCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream("DesktopHub_IPC", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    DebugLogger.Log("IPC: Waiting for connection...");

                    await server.WaitForConnectionAsync(token);
                    DebugLogger.Log("IPC: Client connected");

                    using var reader = new StreamReader(server);
                    var command = await reader.ReadLineAsync();

                    if (!string.IsNullOrEmpty(command))
                    {
                        DebugLogger.Log($"IPC: Received command: {command}");
                        await Dispatcher.InvokeAsync(() => HandleIpcCommand(command));
                    }
                }
                catch (OperationCanceledException)
                {
                    DebugLogger.Log("IPC: Listener cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"IPC: Error: {ex.Message}");
                    await Task.Delay(1000, token);
                }
            }
        }, token);
    }

    private void HandleIpcCommand(string command)
    {
        DebugLogger.Log($"IPC: Handling command: {command}");

        switch (command)
        {
            case "SHOW_OVERLAY":
                if (this.Visibility != Visibility.Visible)
                {
                    ShowOverlay();
                }
                else
                {
                    this.Activate();
                    SearchBox.Focus();
                }
                break;

            case "SHOW_SETTINGS":
                _trayIcon?.ShowSettings();
                break;

            case "CLOSE_APP":
                var confirmed = ConfirmationDialog.Show("Are you sure you want to exit DesktopHub?", this);
                if (confirmed)
                {
                    System.Windows.Application.Current.Shutdown();
                }
                break;

            default:
                DebugLogger.Log($"IPC: Unknown command: {command}");
                break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _ipcCts?.Cancel();
        _ipcCts?.Dispose();
        _hotkey?.Dispose();
        _trayIcon?.Dispose();
        base.OnClosed(e);
    }

    private void UpdateRootClip(double radiusDip)
    {
        try
        {
            DebugLogger.Log($"UpdateRootClip: Starting with radiusDip={radiusDip}");
            DebugLogger.Log($"  RootBorder.ActualWidth: {RootBorder.ActualWidth}");
            DebugLogger.Log($"  RootBorder.ActualHeight: {RootBorder.ActualHeight}");
            DebugLogger.Log($"  RootBorder.Width: {RootBorder.Width}");
            DebugLogger.Log($"  RootBorder.Height: {RootBorder.Height}");
            DebugLogger.Log($"  RootBorder.Margin: {RootBorder.Margin}");
            DebugLogger.Log($"  RootBorder.Padding: {RootBorder.Padding}");
            DebugLogger.Log($"  RootBorder.BorderThickness: {RootBorder.BorderThickness}");
            DebugLogger.Log($"  RootBorder.CornerRadius: {RootBorder.CornerRadius}");

            if (RootBorder.ActualWidth <= 0 || RootBorder.ActualHeight <= 0)
            {
                DebugLogger.Log("  Skipping clip - RootBorder not rendered yet");
                return;
            }

            var rect = new System.Windows.Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight);
            RootBorder.Clip = new System.Windows.Media.RectangleGeometry(rect, radiusDip, radiusDip);
            DebugLogger.Log($"  Applied clip geometry: {rect.Width}x{rect.Height} with radius {radiusDip}");
            DebugLogger.Log($"  RootBorder.Clip: {RootBorder.Clip}");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"UpdateRootClip: EXCEPTION - {ex}");
        }
    }
}
