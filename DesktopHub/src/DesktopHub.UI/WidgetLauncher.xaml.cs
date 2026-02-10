using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class WidgetLauncher : Window
{
    public event EventHandler? TimerWidgetRequested;
    public event EventHandler? QuickTasksWidgetRequested;
    public event EventHandler? DocQuickOpenRequested;
    private bool _isDragging = false;
    private System.Windows.Point _dragStartPoint;
    private bool _isLivingWidgetsMode = false;
    private readonly ISettingsService _settings;
    
    public WidgetLauncher(ISettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
    }
    
    public void UpdateTransparency()
    {
        try
        {
            var transparency = _settings.GetWidgetLauncherTransparency();
            var alpha = (byte)(transparency * 255);
            
            if (RootBorder != null)
            {
                RootBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x18, 0x18, 0x18));
            }
            
            DebugLogger.Log($"WidgetLauncher: Transparency updated to {transparency:F2}");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"WidgetLauncher: UpdateTransparency error: {ex.Message}");
        }
    }
    
    private void TimerWidgetButton_Click(object sender, MouseButtonEventArgs e)
    {
        TimerWidgetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void QuickTasksWidgetButton_Click(object sender, MouseButtonEventArgs e)
    {
        QuickTasksWidgetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DocQuickOpenButton_Click(object sender, MouseButtonEventArgs e)
    {
        DocQuickOpenRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Don't auto-hide like the search overlay
        // This is controlled by the hotkey toggle
    }
    
    public void EnableDragging()
    {
        _isLivingWidgetsMode = true;
        
        // Remove handlers first to prevent duplicates when switching modes
        this.MouseLeftButtonDown -= WidgetLauncher_MouseLeftButtonDown;
        this.MouseLeftButtonUp -= WidgetLauncher_MouseLeftButtonUp;
        this.MouseMove -= WidgetLauncher_MouseMove;
        
        // Add handlers
        this.MouseLeftButtonDown += WidgetLauncher_MouseLeftButtonDown;
        this.MouseLeftButtonUp += WidgetLauncher_MouseLeftButtonUp;
        this.MouseMove += WidgetLauncher_MouseMove;
    }
    
    public void DisableDragging()
    {
        _isLivingWidgetsMode = false;
        this.MouseLeftButtonDown -= WidgetLauncher_MouseLeftButtonDown;
        this.MouseLeftButtonUp -= WidgetLauncher_MouseLeftButtonUp;
        this.MouseMove -= WidgetLauncher_MouseMove;
    }
    
    private void WidgetLauncher_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!_isLivingWidgetsMode)
            return;
            
        // Don't start drag if clicking on interactive elements
        var element = e.OriginalSource as FrameworkElement;
        if (element != null)
        {
            var clickedType = element.GetType().Name;
            if (clickedType == "Button" || clickedType == "Border" && element.Name.Contains("Button"))
            {
                return;
            }
        }
        
        _isDragging = true;
        _dragStartPoint = e.GetPosition(this);
        this.CaptureMouse();
    }
    
    private void WidgetLauncher_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            this.ReleaseMouseCapture();
        }
    }
    
    private void WidgetLauncher_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == System.Windows.Input.MouseButtonState.Pressed)
        {
            var currentPosition = e.GetPosition(this);
            var offset = currentPosition - _dragStartPoint;
            
            this.Left += offset.X;
            this.Top += offset.Y;
        }
    }
    
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        // Check if close shortcut was pressed
        var (closeModifiers, closeKey) = _settings.GetCloseShortcut();
        var currentModifiers = 0;
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            currentModifiers |= (int)GlobalHotkey.MOD_CONTROL;
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0)
            currentModifiers |= (int)GlobalHotkey.MOD_ALT;
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0)
            currentModifiers |= (int)GlobalHotkey.MOD_SHIFT;
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Windows) != 0)
            currentModifiers |= (int)GlobalHotkey.MOD_WIN;
        
        var currentKey = System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.Key);
        
        if (currentModifiers == closeModifiers && currentKey == closeKey)
        {
            DebugLogger.Log($"WidgetLauncher: Close shortcut pressed -> Hiding widget launcher");
            this.Visibility = Visibility.Hidden;
            e.Handled = true;
        }
    }
}
