using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class TrayMenu : Window
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private readonly ISettingsService? _settings;
    private readonly Action _onOpenSearch;
    private readonly Action _onRescanProjects;
    private readonly Action _onCheckForUpdates;
    private readonly Action _onSettings;
    private readonly Action _onExit;
    private bool _isExiting;
    private bool _itemClicked;
    private bool _isClosing;
    private DateTime _openedTime;
    private DispatcherTimer? _dismissTimer;
    private IntPtr _hwnd;

    public TrayMenu(Action onOpenSearch, Action onRescanProjects, Action onCheckForUpdates, Action onSettings, Action onExit, bool isUpdateAvailable = false, ISettingsService? settings = null)
    {
        DebugLogger.Log("TrayMenu: Constructor called");
        InitializeComponent();
        
        _settings = settings;
        _onOpenSearch = onOpenSearch;
        _onRescanProjects = onRescanProjects;
        _onCheckForUpdates = onCheckForUpdates;
        _onSettings = onSettings;
        _onExit = onExit;

        var version = System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "1.0.0";
        VersionText.Text = $"v{version}";

        if (isUpdateAvailable)
            TrayUpdateIndicator.Visibility = Visibility.Visible;

        // Setup transparency when window handle is available
        SourceInitialized += (s, e) =>
        {
            WindowBlur.SetupTransparency(this);
            WindowHelper.UpdateRootClip(RootBorder, 8, "TrayMenu");
            this.Background = null;
        };

        // Update clip on resize
        SizeChanged += (s, e) =>
        {
            WindowHelper.UpdateRootClip(RootBorder, 8, "TrayMenu");
        };

        Loaded += (s, e) =>
        {
            // Apply TrayMenu transparency from settings
            if (_settings != null)
            {
                var alpha = (byte)(_settings.GetWidgetTransparency(WidgetIds.TrayMenu) * 255);
                var bgBase = Helpers.ThemeHelper.GetColor("WindowBackgroundDeepColor");
                RootBorder.Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(alpha, bgBase.R, bgBase.G, bgBase.B));
            }

            PositionNearCursor();
            _openedTime = DateTime.Now;
            DebugLogger.Log($"TrayMenu: Loaded at {_openedTime:HH:mm:ss.fff}");

            // Get our HWND and force ourselves to be the foreground window
            // so that Deactivated fires reliably in most cases
            _hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            SetForegroundWindow(_hwnd);

            // Start a dismiss timer as a fallback for cases where Deactivated
            // does NOT fire (e.g. user clicks the Windows taskbar)
            _dismissTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _dismissTimer.Tick += DismissTimer_Tick;
            _dismissTimer.Start();
        };
        
        // Log all mouse activity
        PreviewMouseDown += (s, e) =>
        {
            DebugLogger.Log($"TrayMenu: PreviewMouseDown - Button={e.ChangedButton}, Source={e.Source?.GetType().Name}, OriginalSource={e.OriginalSource?.GetType().Name}");
        };
        
        MouseDown += (s, e) =>
        {
            DebugLogger.Log($"TrayMenu: MouseDown - Button={e.ChangedButton}, Source={e.Source?.GetType().Name}");
        };
    }

    private void PositionNearCursor()
    {
        try
        {
            // Get cursor position in WPF DIPs (not physical pixels)
            var cursorDip = ScreenHelper.GetCursorPositionInDips(this);
            
            // Get screen working area in DIPs
            var workingArea = ScreenHelper.GetWorkingAreaFromDipPoint(cursorDip.X, cursorDip.Y, this);
            
            // Position menu near cursor but keep it on screen
            var menuWidth = this.ActualWidth > 0 ? this.ActualWidth : 220;
            var menuHeight = this.ActualHeight > 0 ? this.ActualHeight : 300;
            
            DebugLogger.Log($"TrayMenu: Positioning - CursorDip({cursorDip.X:F0},{cursorDip.Y:F0}), MenuSize({menuWidth}x{menuHeight}), WorkArea({workingArea.Left:F0},{workingArea.Top:F0},{workingArea.Width:F0}x{workingArea.Height:F0})");
            
            // Position menu so cursor is inside menu bounds (20px from bottom-right corner)
            // This way user's cursor is already on the menu when it opens
            double left = cursorDip.X - menuWidth + 20;
            double top = cursorDip.Y - menuHeight + 20;
            
            // Keep menu on screen horizontally
            if (left < workingArea.Left)
            {
                left = workingArea.Left;
            }
            if (left + menuWidth > workingArea.Right)
            {
                left = workingArea.Right - menuWidth;
            }
            
            // Keep menu on screen vertically
            if (top < workingArea.Top)
            {
                top = workingArea.Top;
            }
            if (top + menuHeight > workingArea.Bottom)
            {
                top = workingArea.Bottom - menuHeight;
            }
            
            this.Left = left;
            this.Top = top;
            
            DebugLogger.Log($"TrayMenu: Positioned at ({left},{top})");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"TrayMenu: PositionNearCursor error: {ex.Message}");
        }
    }

    private void OpenSearch_Click(object sender, MouseButtonEventArgs e)
    {
        DebugLogger.Log($"TrayMenu: OpenSearch_Click - Button={e.ChangedButton}, _itemClicked={_itemClicked}");
        _itemClicked = true;
        DebugLogger.Log("TrayMenu: OpenSearch_Click - Set _itemClicked=true, closing window");
        this.Close();
        _onOpenSearch?.Invoke();
    }

    private void RescanProjects_Click(object sender, MouseButtonEventArgs e)
    {
        DebugLogger.Log($"TrayMenu: RescanProjects_Click - Button={e.ChangedButton}, _itemClicked={_itemClicked}");
        _itemClicked = true;
        DebugLogger.Log("TrayMenu: RescanProjects_Click - Set _itemClicked=true, closing window");
        this.Close();
        _onRescanProjects?.Invoke();
    }

    private void CheckForUpdates_Click(object sender, MouseButtonEventArgs e)
    {
        DebugLogger.Log($"TrayMenu: CheckForUpdates_Click - Button={e.ChangedButton}, _itemClicked={_itemClicked}");
        _itemClicked = true;
        DebugLogger.Log("TrayMenu: CheckForUpdates_Click - Set _itemClicked=true, closing window");
        this.Close();
        _onCheckForUpdates?.Invoke();
    }

    private void Settings_Click(object sender, MouseButtonEventArgs e)
    {
        DebugLogger.Log($"TrayMenu: Settings_Click - Button={e.ChangedButton}, _itemClicked={_itemClicked}");
        _itemClicked = true;
        DebugLogger.Log("TrayMenu: Settings_Click - Set _itemClicked=true, closing window");
        _isExiting = true;
        this.Close();
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            _onSettings?.Invoke();
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void Exit_Click(object sender, MouseButtonEventArgs e)
    {
        DebugLogger.Log($"TrayMenu: Exit_Click - Button={e.ChangedButton}, _itemClicked={_itemClicked}");
        _itemClicked = true;
        DebugLogger.Log("TrayMenu: Exit_Click - Set _itemClicked=true, hiding window");
        _isExiting = true;
        this.Hide();
        
        // Use Application dispatcher to ensure window is fully hidden before showing dialog
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                _onExit?.Invoke();
            }
            finally
            {
                this.Close();
            }
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    /// <summary>
    /// Safe close that prevents the double-close crash.
    /// All code paths that want to dismiss the menu should call this.
    /// </summary>
    private void SafeClose(string reason)
    {
        if (_isClosing) return;
        _isClosing = true;
        _dismissTimer?.Stop();
        DebugLogger.Log($"TrayMenu: SafeClose - {reason}");
        try { this.Close(); } catch (InvalidOperationException) { /* already closing */ }
    }

    private void DismissTimer_Tick(object? sender, EventArgs e)
    {
        if (_isClosing || _isExiting || _itemClicked) return;

        var timeSinceOpen = DateTime.Now - _openedTime;
        if (timeSinceOpen.TotalMilliseconds < 500) return; // grace period

        // If the foreground window is no longer us, something else was clicked
        var fg = GetForegroundWindow();
        if (fg != IntPtr.Zero && fg != _hwnd)
        {
            // Double-check mouse isn't over the menu (user may have clicked
            // a child popup or tooltip that changed foreground temporarily)
            if (!IsMouseOverWindow())
            {
                SafeClose($"DismissTimer - foreground changed to 0x{fg:X}");
            }
        }
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        if (_isClosing)
        {
            DebugLogger.Log("TrayMenu: Window_Deactivated - Ignoring (already closing)");
            return;
        }
        
        // Grace period: give user time to move mouse from tray icon to menu
        var timeSinceOpen = DateTime.Now - _openedTime;
        if (timeSinceOpen.TotalMilliseconds < 500)
        {
            DebugLogger.Log($"TrayMenu: Window_Deactivated - Ignoring (grace period, {timeSinceOpen.TotalMilliseconds:F0}ms since open)");
            return;
        }
        
        if (_isExiting)
        {
            DebugLogger.Log("TrayMenu: Window_Deactivated - Ignoring (exiting)");
            return;
        }
        
        if (_itemClicked)
        {
            SafeClose("Deactivated - item clicked");
            return;
        }
        
        // If mouse is over the window, don't close (user just moved focus briefly)
        if (IsMouseOverWindow())
        {
            DebugLogger.Log("TrayMenu: Window_Deactivated - NOT closing (mouse is over window)");
            return;
        }
        
        SafeClose("Deactivated - mouse not over");
    }

    private void Window_LostFocus(object sender, RoutedEventArgs e)
    {
        DebugLogger.Log($"TrayMenu: Window_LostFocus - ignoring (handled by Window_Deactivated)");
    }

    protected override void OnClosed(EventArgs e)
    {
        _dismissTimer?.Stop();
        _dismissTimer = null;
        base.OnClosed(e);
    }
    
    private bool IsMouseOverWindow()
    {
        try
        {
            // Compare in WPF DIP coordinate space
            var cursorDip = ScreenHelper.GetCursorPositionInDips(this);
            var windowRect = new Rect(this.Left, this.Top, this.ActualWidth, this.ActualHeight);
            
            bool contains = windowRect.Contains(cursorDip);
            DebugLogger.Log($"TrayMenu: IsMouseOverWindow check - Mouse({cursorDip.X:F0},{cursorDip.Y:F0}) Window({windowRect.Left:F0},{windowRect.Top:F0},{windowRect.Width:F0},{windowRect.Height:F0}) = {contains}");
            return contains;
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"TrayMenu: IsMouseOverWindow error: {ex.Message}");
            return false;
        }
    }

}
