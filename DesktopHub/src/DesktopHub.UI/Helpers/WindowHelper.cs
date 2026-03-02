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
}
