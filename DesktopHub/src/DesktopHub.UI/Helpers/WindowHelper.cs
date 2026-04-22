using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
// DebugLogger lives in namespace DesktopHub.UI

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Shared window helper utilities used by multiple windows/overlays for
/// rounded corner clipping and common window setup patterns.
/// </summary>
public static class WindowHelper
{
    /// <summary>
    /// Applies a rounded-corner clip geometry to the given border element.
    /// Used by SearchOverlay, SettingsWindow, TrayMenu, and dialogs.
    /// </summary>
    public static void UpdateRootClip(Border rootBorder, double radiusDip, string? callerName = null)
    {
        try
        {
            if (rootBorder.ActualWidth <= 0 || rootBorder.ActualHeight <= 0)
                return;

            var rect = new Rect(0, 0, rootBorder.ActualWidth, rootBorder.ActualHeight);
            rootBorder.Clip = new RectangleGeometry(rect, radiusDip, radiusDip);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"{callerName ?? "WindowHelper"}: UpdateRootClip error: {ex.Message}");
        }
    }

    /// <summary>
    /// Centers a window on the screen that currently contains the cursor.
    /// DPI-aware (works correctly on high-DPI, multi-monitor, and RDP sessions
    /// where WPF's built-in CenterScreen / CenterOwner falls back to the
    /// primary monitor and looks visually off).
    ///
    /// Call after the window has been sized -- inside Loaded, or after
    /// SizeToContent has resolved. For auto-size windows, call in Loaded.
    /// </summary>
    public static void CenterOnCursorScreen(Window window)
    {
        if (window == null) return;
        try
        {
            var work = ScreenHelper.GetWorkingAreaFromDipPoint(
                ScreenHelper.GetCursorPositionInDips(window).X,
                ScreenHelper.GetCursorPositionInDips(window).Y,
                window);

            var width = window.ActualWidth > 0 ? window.ActualWidth : window.Width;
            var height = window.ActualHeight > 0 ? window.ActualHeight : window.Height;
            if (double.IsNaN(width) || width <= 0) width = 400;
            if (double.IsNaN(height) || height <= 0) height = 200;

            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = work.Left + (work.Width - width) / 2.0;
            window.Top = work.Top + (work.Height - height) / 2.0;
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"WindowHelper.CenterOnCursorScreen: {ex.Message}");
        }
    }
}
