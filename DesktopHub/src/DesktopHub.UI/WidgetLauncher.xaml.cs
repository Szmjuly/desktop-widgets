using System;
using System.Windows;
using System.Windows.Input;

namespace DesktopHub.UI;

public partial class WidgetLauncher : Window
{
    public event EventHandler? TimerWidgetRequested;
    private bool _isDragging = false;
    private System.Windows.Point _dragStartPoint;
    private bool _isLivingWidgetsMode = false;
    
    public WidgetLauncher()
    {
        InitializeComponent();
    }
    
    private void TimerWidgetButton_Click(object sender, MouseButtonEventArgs e)
    {
        TimerWidgetRequested?.Invoke(this, EventArgs.Empty);
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
}
