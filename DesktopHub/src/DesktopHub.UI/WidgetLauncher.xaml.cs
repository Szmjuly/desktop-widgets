using System;
using System.Windows;
using System.Windows.Input;

namespace DesktopHub.UI;

public partial class WidgetLauncher : Window
{
    public event EventHandler? TimerWidgetRequested;
    
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
}
