using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ProjectSearcher.UI;

/// <summary>
/// Provides Windows 10+ blur-behind effect for WPF windows
/// Matches coffee-stock-widget implementation
/// </summary>
public static class WindowBlur
{
    #region P/Invoke Declarations

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("dwmapi.dll")]
    private static extern int DwmIsCompositionEnabled(ref bool pfEnabled);

    #endregion

    #region Constants

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWCP_ROUND = 2;
    private const int DWMSBT_NONE = 0;

    #endregion

    #region Structs

    private enum WINDOWCOMPOSITIONATTRIB
    {
        WCA_ACCENT_POLICY = 19
    }

    private enum ACCENT_STATE
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        ACCENT_INVALID_STATE = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ACCENT_POLICY
    {
        public ACCENT_STATE AccentState;
        public uint AccentFlags;
        public uint GradientColor;
        public uint AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWCOMPOSITIONATTRIBDATA
    {
        public WINDOWCOMPOSITIONATTRIB Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    #endregion

    /// <summary>
    /// Setup window for transparency - call in SourceInitialized event
    /// </summary>
    public static void SetupTransparency(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        DebugLogger.Log($"WindowBlur.SetupTransparency: hwnd = {hwnd}");
        if (hwnd == IntPtr.Zero)
        {
            DebugLogger.Log("WindowBlur.SetupTransparency: FAILED - hwnd is zero");
            return;
        }

        try
        {
            // Extend DWM frame into entire client area (enables transparent background composition)
            var margins = new MARGINS { cxLeftWidth = -1 };
            var result = DwmExtendFrameIntoClientArea(hwnd, ref margins);
            DebugLogger.Log($"WindowBlur.SetupTransparency: DwmExtendFrameIntoClientArea result = {result} (0 = success)");

            // Set composition target background to transparent
            var src = HwndSource.FromHwnd(hwnd);
            if (src != null)
            {
                var beforeColor = src.CompositionTarget.BackgroundColor;
                DebugLogger.Log($"WindowBlur.SetupTransparency: CompositionTarget background BEFORE: {beforeColor}");
                
                src.CompositionTarget.BackgroundColor = Colors.Transparent;
                
                var afterColor = src.CompositionTarget.BackgroundColor;
                DebugLogger.Log($"WindowBlur.SetupTransparency: CompositionTarget background AFTER: {afterColor}");
                DebugLogger.Log($"  IsTransparent: {afterColor == Colors.Transparent}");
            }
            else
            {
                DebugLogger.Log("WindowBlur.SetupTransparency: WARNING - HwndSource is null");
            }

            // Apply Win11 rounded corners and disable system backdrop
            ApplyWin11WindowAttributes(hwnd);
            DebugLogger.Log("WindowBlur.SetupTransparency: Complete");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"WindowBlur.SetupTransparency: EXCEPTION - {ex}");
        }
    }

    private static void ApplyWin11WindowAttributes(IntPtr hwnd)
    {
        try
        {
            // Request rounded corners on Win11
            int corner = DWMWCP_ROUND;
            DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));

            // Disable system backdrop to avoid conflicts with AccentPolicy blur
            int backdrop = DWMSBT_NONE;
            DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }
        catch { }
    }

    /// <summary>
    /// Enables blur-behind effect for the window
    /// </summary>
    /// <param name="window">The window to apply blur to</param>
    /// <param name="useAcrylic">True for acrylic (stronger tint), false for standard blur</param>
    public static void EnableBlur(Window window, bool useAcrylic = false)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        DebugLogger.Log($"WindowBlur.EnableBlur: hwnd = {hwnd}, useAcrylic = {useAcrylic}");
        if (hwnd == IntPtr.Zero)
        {
            DebugLogger.Log("WindowBlur.EnableBlur: FAILED - hwnd is zero");
            return;
        }

        // Check Windows version and DWM composition state
        try
        {
            var osVersion = Environment.OSVersion;
            DebugLogger.Log($"WindowBlur.EnableBlur: OS Version = {osVersion.VersionString}");
            DebugLogger.Log($"WindowBlur.EnableBlur: OS Platform = {osVersion.Platform}");
            
            bool isDwmEnabled = false;
            DwmIsCompositionEnabled(ref isDwmEnabled);
            DebugLogger.Log($"WindowBlur.EnableBlur: DWM Composition Enabled = {isDwmEnabled}");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"WindowBlur.EnableBlur: Version check failed - {ex.Message}");
        }

        EnableBlurBehind(hwnd, true, useAcrylic);
    }

    /// <summary>
    /// Disables blur effect
    /// </summary>
    public static void DisableBlur(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        EnableBlurBehind(hwnd, false, false);
    }

    private static void EnableBlurBehind(IntPtr hwnd, bool enable, bool acrylic)
    {
        try
        {
            // Use low alpha for blur (~9%), higher for acrylic (~80%)
            byte alpha = acrylic ? (byte)0xCC : (byte)0x18;
            
            // Create gradient color in ABGR format (dark shell color)
            uint gradient = ToAbgr(System.Windows.Media.Color.FromArgb(alpha, 0x12, 0x12, 0x12));
            DebugLogger.Log($"WindowBlur.EnableBlurBehind: alpha = {alpha:X2}, gradient = {gradient:X8}, acrylic = {acrylic}");

            var accent = new ACCENT_POLICY
            {
                AccentState = enable 
                    ? (acrylic ? ACCENT_STATE.ACCENT_ENABLE_ACRYLICBLURBEHIND : ACCENT_STATE.ACCENT_ENABLE_BLURBEHIND) 
                    : ACCENT_STATE.ACCENT_DISABLED,
                AccentFlags = 2, // blur all borders
                GradientColor = gradient,
                AnimationId = 0
            };

            DebugLogger.Log($"WindowBlur.EnableBlurBehind: AccentState = {accent.AccentState}");

            int size = Marshal.SizeOf(accent);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WINDOWCOMPOSITIONATTRIBDATA
                {
                    Attribute = WINDOWCOMPOSITIONATTRIB.WCA_ACCENT_POLICY,
                    Data = ptr,
                    SizeOfData = size
                };
                var result = SetWindowCompositionAttribute(hwnd, ref data);
                DebugLogger.Log($"WindowBlur.EnableBlurBehind: SetWindowCompositionAttribute result = {result}");
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
            DebugLogger.Log("WindowBlur.EnableBlurBehind: Complete");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"WindowBlur.EnableBlurBehind: EXCEPTION - {ex}");
        }
    }

    private static uint ToAbgr(System.Windows.Media.Color c) => (uint)(c.A << 24 | c.B << 16 | c.G << 8 | c.R);

    /// <summary>
    /// Apply rounded corners using window region (DPI-aware)
    /// </summary>
    public static void ApplyRoundedCorners(Window window, double radiusDip = 12)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;

        try
        {
            // Get DPI scaling
            var source = PresentationSource.FromVisual(window);
            double scaleX = 1.0, scaleY = 1.0;
            if (source?.CompositionTarget != null)
            {
                var m = source.CompositionTarget.TransformToDevice;
                scaleX = m.M11;
                scaleY = m.M22;
            }

            // Calculate physical dimensions
            int w = Math.Max(1, (int)Math.Round(window.ActualWidth * scaleX));
            int h = Math.Max(1, (int)Math.Round(window.ActualHeight * scaleY));
            int rx = Math.Max(1, (int)Math.Round(radiusDip * scaleX)) * 2;
            int ry = Math.Max(1, (int)Math.Round(radiusDip * scaleY)) * 2;

            IntPtr rgn = CreateRoundRectRgn(0, 0, w + 1, h + 1, rx, ry);
            SetWindowRgn(hwnd, rgn, true);
            // System owns the region after SetWindowRgn, do not delete
        }
        catch { }
    }

    /// <summary>
    /// Update WPF clipping geometry on a border element
    /// </summary>
    public static void UpdateClip(FrameworkElement element, double radiusDip = 12)
    {
        try
        {
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0) return;
            var rect = new Rect(0, 0, element.ActualWidth, element.ActualHeight);
            element.Clip = new RectangleGeometry(rect, radiusDip, radiusDip);
        }
        catch { }
    }
}
