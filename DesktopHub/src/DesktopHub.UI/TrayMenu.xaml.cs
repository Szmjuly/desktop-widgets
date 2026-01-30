using System.Windows;
using System.Windows.Input;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class TrayMenu : Window
{
    private readonly Action _onOpenSearch;
    private readonly Action _onRescanProjects;
    private readonly Action _onSettings;
    private readonly Action _onExit;
    private bool _isExiting;

    public TrayMenu(Action onOpenSearch, Action onRescanProjects, Action onSettings, Action onExit)
    {
        InitializeComponent();
        
        _onOpenSearch = onOpenSearch;
        _onRescanProjects = onRescanProjects;
        _onSettings = onSettings;
        _onExit = onExit;

        Loaded += (s, e) =>
        {
            PositionNearCursor();
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
            var menuWidth = this.Width;
            var menuHeight = this.Height;
            
            double left = point.X;
            double top = point.Y;
            
            // Adjust if menu would go off screen
            if (left + menuWidth > workingArea.Right)
            {
                left = workingArea.Right - menuWidth;
            }
            
            if (top + menuHeight > workingArea.Bottom)
            {
                top = point.Y - menuHeight;
            }
            
            this.Left = left;
            this.Top = top;
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"TrayMenu: PositionNearCursor error: {ex.Message}");
        }
    }

    private void OpenSearch_Click(object sender, MouseButtonEventArgs e)
    {
        this.Close();
        _onOpenSearch?.Invoke();
    }

    private void RescanProjects_Click(object sender, MouseButtonEventArgs e)
    {
        this.Close();
        _onRescanProjects?.Invoke();
    }

    private void Settings_Click(object sender, MouseButtonEventArgs e)
    {
        _isExiting = true;
        this.Close();
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            _onSettings?.Invoke();
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void Exit_Click(object sender, MouseButtonEventArgs e)
    {
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
        if (!_isExiting)
        {
            this.Close();
        }
    }

    private void Window_LostFocus(object sender, RoutedEventArgs e)
    {
        if (!_isExiting)
        {
            this.Close();
        }
    }

}
