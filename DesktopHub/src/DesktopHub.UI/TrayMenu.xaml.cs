using System.Windows;
using System.Windows.Input;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class TrayMenu : Window
{
    private readonly Action _onOpenSearch;
    private readonly Action _onRescanProjects;
    private readonly Action _onCheckForUpdates;
    private readonly Action _onSettings;
    private readonly Action _onExit;
    private bool _isExiting;
    private bool _itemClicked;
    private bool _isClosing;
    private System.Windows.Threading.DispatcherTimer? _deactivateTimer;
    private DateTime _openedTime;

    public TrayMenu(Action onOpenSearch, Action onRescanProjects, Action onCheckForUpdates, Action onSettings, Action onExit)
    {
        DebugLogger.Log("TrayMenu: Constructor called");
        InitializeComponent();
        
        _onOpenSearch = onOpenSearch;
        _onRescanProjects = onRescanProjects;
        _onCheckForUpdates = onCheckForUpdates;
        _onSettings = onSettings;
        _onExit = onExit;

        var version = System.Reflection.Assembly.GetExecutingAssembly()
            .GetName().Version?.ToString(3) ?? "1.0.0";
        VersionText.Text = $"v{version}";

        // Setup transparency when window handle is available
        SourceInitialized += (s, e) =>
        {
            WindowBlur.SetupTransparency(this);
            UpdateRootClip(8);
            this.Background = null;
        };

        // Update clip on resize
        SizeChanged += (s, e) =>
        {
            UpdateRootClip(8);
        };

        Loaded += (s, e) =>
        {
            PositionNearCursor();
            _openedTime = DateTime.Now;
            DebugLogger.Log($"TrayMenu: Loaded at {_openedTime:HH:mm:ss.fff}");
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
            // Get cursor position
            var point = System.Windows.Forms.Cursor.Position;
            
            // Get screen working area
            var screen = System.Windows.Forms.Screen.FromPoint(point);
            var workingArea = screen.WorkingArea;
            
            // Position menu near cursor but keep it on screen
            var menuWidth = this.ActualWidth > 0 ? this.ActualWidth : 220;
            var menuHeight = this.ActualHeight > 0 ? this.ActualHeight : 300;
            
            DebugLogger.Log($"TrayMenu: Positioning - Cursor({point.X},{point.Y}), MenuSize({menuWidth}x{menuHeight}), WorkArea({workingArea.Left},{workingArea.Top},{workingArea.Width}x{workingArea.Height})");
            
            // Position menu so cursor is inside menu bounds (20px from bottom-right corner)
            // This way user's cursor is already on the menu when it opens
            double left = point.X - menuWidth + 20;
            double top = point.Y - menuHeight + 20;
            
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

    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Prevent double-close crash
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
        
        // Check mouse position IMMEDIATELY (before user can move it)
        var isMouseOver = this.IsMouseOver || IsMouseOverWindow();
        DebugLogger.Log($"TrayMenu: Window_Deactivated - _isExiting={_isExiting}, _itemClicked={_itemClicked}, IsMouseOver={isMouseOver}, timeSinceOpen={timeSinceOpen.TotalMilliseconds:F0}ms");
        
        // If item was clicked, close immediately
        if (_itemClicked)
        {
            DebugLogger.Log("TrayMenu: Window_Deactivated - CLOSING (item clicked)");
            _isClosing = true;
            this.Close();
            return;
        }
        
        if (_isExiting)
        {
            DebugLogger.Log("TrayMenu: Window_Deactivated - Ignoring (exiting)");
            return;
        }
        
        // If mouse is over the window, don't close
        if (isMouseOver)
        {
            DebugLogger.Log("TrayMenu: Window_Deactivated - NOT closing (mouse is over window)");
            return;
        }
        
        // Mouse is not over window, close it
        DebugLogger.Log("TrayMenu: Window_Deactivated - CLOSING (mouse not over)");
        _isClosing = true;
        this.Close();
    }

    private void Window_LostFocus(object sender, RoutedEventArgs e)
    {
        DebugLogger.Log($"TrayMenu: Window_LostFocus - ignoring (handled by Window_Deactivated)");
    }
    
    private bool IsMouseOverWindow()
    {
        try
        {
            var mousePos = System.Windows.Forms.Cursor.Position;
            var windowRect = new System.Drawing.Rectangle(
                (int)this.Left,
                (int)this.Top,
                (int)this.ActualWidth,
                (int)this.ActualHeight
            );
            
            bool contains = windowRect.Contains(mousePos);
            DebugLogger.Log($"TrayMenu: IsMouseOverWindow check - Mouse({mousePos.X},{mousePos.Y}) Window({windowRect.Left},{windowRect.Top},{windowRect.Width},{windowRect.Height}) = {contains}");
            return contains;
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"TrayMenu: IsMouseOverWindow error: {ex.Message}");
            return false;
        }
    }

    private void UpdateRootClip(double radiusDip)
    {
        try
        {
            if (RootBorder.ActualWidth <= 0 || RootBorder.ActualHeight <= 0)
            {
                return;
            }
            
            var rect = new System.Windows.Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight);
            RootBorder.Clip = new System.Windows.Media.RectangleGeometry(rect, radiusDip, radiusDip);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"TrayMenu: UpdateRootClip error: {ex.Message}");
        }
    }
}
