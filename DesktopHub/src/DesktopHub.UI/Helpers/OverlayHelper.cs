using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopHub.Core.Abstractions;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Shared utility methods for overlay windows — eliminates duplicated boilerplate
/// for close shortcuts, OnClosing hide behavior, transparency, and update indicators.
/// </summary>
public static class OverlayHelper
{
    /// <summary>
    /// Checks whether the current key event matches the configured close shortcut.
    /// Call from Window_KeyDown handlers. Returns true if the shortcut matched (caller should hide and set e.Handled).
    /// </summary>
    public static bool IsCloseShortcutPressed(System.Windows.Input.KeyEventArgs e, ISettingsService settings)
    {
        var (closeModifiers, closeKey) = settings.GetCloseShortcut();
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
        return currentModifiers == closeModifiers && currentKey == closeKey;
    }

    /// <summary>
    /// Standard OnClosing behavior: hide instead of close unless the app is shutting down.
    /// Call from OnClosing override. Returns true if the close was cancelled (window hidden instead).
    /// </summary>
    public static bool HandleOnClosingHide(System.ComponentModel.CancelEventArgs e, Window window)
    {
        var app = System.Windows.Application.Current;
        if (app == null || app.ShutdownMode == ShutdownMode.OnExplicitShutdown)
            return false;

        e.Cancel = true;
        window.Hide();
        return true;
    }

    /// <summary>
    /// Sets UpdateIndicator visibility. Pass the named element from XAML.
    /// </summary>
    public static void SetUpdateIndicatorVisible(FrameworkElement? indicator, bool visible)
    {
        if (indicator != null)
            indicator.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// Applies transparency to a RootBorder with the standard dark background (0x12, 0x12, 0x12).
    /// </summary>
    public static void ApplyTransparency(Border? rootBorder, double transparency, string overlayName)
    {
        try
        {
            var alpha = (byte)(transparency * 255);
            if (rootBorder != null)
            {
                rootBorder.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x12, 0x12, 0x12));
            }
            DebugLogger.Log($"{overlayName}: Transparency updated to {transparency:F2}");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"{overlayName}: UpdateTransparency error: {ex.Message}");
        }
    }

    /// <summary>
    /// Helper to walk the visual tree upward and find an ancestor of type T.
    /// </summary>
    public static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
