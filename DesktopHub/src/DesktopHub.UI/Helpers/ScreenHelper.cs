using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Provides DPI-aware coordinate conversion between WinForms physical pixels
/// and WPF device-independent pixels (DIPs).
///
/// WinForms APIs (Screen.WorkingArea, Cursor.Position) return physical pixels.
/// WPF Window.Left/Top use DIPs. Without conversion, coordinates are wrong
/// whenever DPI scaling != 100% (common in RDP sessions, high-DPI monitors).
/// </summary>
internal static class ScreenHelper
{
    /// <summary>
    /// Gets the DPI scale factor from a specific WPF visual.
    /// Falls back to system DPI if the visual is unavailable.
    /// </summary>
    public static double GetDpiScale(Visual? visual)
    {
        try
        {
            if (visual != null)
            {
                var source = PresentationSource.FromVisual(visual);
                if (source?.CompositionTarget != null)
                    return source.CompositionTarget.TransformToDevice.M11;
            }
        }
        catch { }

        return GetSystemDpiScale();
    }

    /// <summary>
    /// Gets the system-level DPI scale factor (primary monitor).
    /// </summary>
    public static double GetSystemDpiScale()
    {
        try
        {
            // Try application main window first
            var mainWindow = System.Windows.Application.Current?.MainWindow;
            if (mainWindow != null)
            {
                var source = PresentationSource.FromVisual(mainWindow);
                if (source?.CompositionTarget != null)
                    return source.CompositionTarget.TransformToDevice.M11;
            }
        }
        catch { }

        // Fallback: GDI system DPI
        try
        {
            using var g = System.Drawing.Graphics.FromHwnd(IntPtr.Zero);
            return g.DpiX / 96.0;
        }
        catch { }

        return 1.0;
    }

    /// <summary>
    /// Gets the WinForms cursor position converted to WPF DIP coordinates.
    /// </summary>
    public static System.Windows.Point GetCursorPositionInDips(Visual? referenceVisual = null)
    {
        var point = System.Windows.Forms.Cursor.Position;
        var dpi = GetDpiScale(referenceVisual);
        return new System.Windows.Point(point.X / dpi, point.Y / dpi);
    }

    /// <summary>
    /// Converts a WinForms Screen working area (physical pixels) to a WPF Rect (DIPs).
    /// </summary>
    public static Rect GetWorkingAreaInDips(System.Windows.Forms.Screen screen, Visual? referenceVisual = null)
    {
        var wa = screen.WorkingArea;
        var dpi = GetDpiScale(referenceVisual);
        return new Rect(wa.Left / dpi, wa.Top / dpi, wa.Width / dpi, wa.Height / dpi);
    }

    /// <summary>
    /// Gets the screen containing the specified WPF DIP coordinates,
    /// and returns its working area in DIPs.
    /// Converts DIP → physical for Screen.FromPoint, then physical → DIP for the result.
    /// </summary>
    public static Rect GetWorkingAreaFromDipPoint(double dipX, double dipY, Visual? referenceVisual = null)
    {
        var dpi = GetDpiScale(referenceVisual);
        var physPoint = new System.Drawing.Point(
            (int)Math.Round(dipX * dpi),
            (int)Math.Round(dipY * dpi));
        var screen = System.Windows.Forms.Screen.FromPoint(physPoint);
        return GetWorkingAreaInDips(screen, referenceVisual);
    }

    /// <summary>
    /// Gets the screen containing the specified WPF DIP rectangle center,
    /// and returns its working area in DIPs.
    /// </summary>
    public static Rect GetWorkingAreaFromDipRect(Rect dipRect, Visual? referenceVisual = null)
    {
        var centerX = dipRect.Left + dipRect.Width / 2.0;
        var centerY = dipRect.Top + dipRect.Height / 2.0;
        return GetWorkingAreaFromDipPoint(centerX, centerY, referenceVisual);
    }

    /// <summary>
    /// Gets the primary screen's working area in DIPs.
    /// </summary>
    public static Rect GetPrimaryWorkingAreaInDips(Visual? referenceVisual = null)
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen;
        if (screen == null)
            return new Rect(0, 0, 1920, 1080);
        return GetWorkingAreaInDips(screen, referenceVisual);
    }

    /// <summary>
    /// Returns DIP-converted working areas for all attached screens,
    /// ordered by physical X position (left-to-right).
    /// </summary>
    public static List<Rect> GetAllScreensInDips(Visual? referenceVisual = null)
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var result = new List<Rect>(screens.Length);
        foreach (var screen in screens.OrderBy(s => s.Bounds.Left))
        {
            result.Add(GetWorkingAreaInDips(screen, referenceVisual));
        }
        return result;
    }

    /// <summary>
    /// Returns a display-config fingerprint (screen count + resolutions) that can be
    /// compared across sessions to detect monitor changes (office → RDP, dock/undock, etc.).
    /// </summary>
    public static string GetDisplayConfigFingerprint()
    {
        try
        {
            var screens = System.Windows.Forms.Screen.AllScreens
                .OrderBy(s => s.Bounds.Left)
                .ThenBy(s => s.Bounds.Top);
            var parts = screens.Select(s => $"{s.Bounds.Width}x{s.Bounds.Height}@{s.Bounds.Left},{s.Bounds.Top}");
            return string.Join("|", parts);
        }
        catch
        {
            return "unknown";
        }
    }
}
