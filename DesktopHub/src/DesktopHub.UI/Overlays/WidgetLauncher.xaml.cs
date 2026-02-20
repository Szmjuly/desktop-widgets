using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class WidgetLauncher : Window
{
    public event EventHandler? SearchWidgetRequested;
    public event EventHandler? TimerWidgetRequested;
    public event EventHandler? QuickTasksWidgetRequested;
    public event EventHandler? DocQuickOpenRequested;
    public event EventHandler? FrequentProjectsRequested;
    public event EventHandler? QuickLaunchRequested;
    public event EventHandler? SmartProjectSearchRequested;
    private bool _isDragging = false;
    private System.Windows.Point _dragStartPoint;
    private bool _isLivingWidgetsMode = false;
    private readonly ISettingsService _settings;
    
    public WidgetLauncher(ISettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
        
        // Apply initial button visibility from settings
        UpdateSearchButtonVisibility(_settings.GetSearchWidgetEnabled());
        UpdateTimerButtonVisibility(_settings.GetTimerWidgetEnabled());
        UpdateQuickTasksButtonVisibility(_settings.GetQuickTasksWidgetEnabled());
        UpdateDocButtonVisibility(_settings.GetDocWidgetEnabled());
        UpdateFrequentProjectsButtonVisibility(_settings.GetFrequentProjectsWidgetEnabled());
        UpdateQuickLaunchButtonVisibility(_settings.GetQuickLaunchWidgetEnabled());
        var smartSearchVisible = _settings.GetSmartProjectSearchWidgetEnabled() &&
                                 !_settings.GetSmartProjectSearchAttachToSearchOverlayMode();
        UpdateSmartProjectSearchButtonVisibility(smartSearchVisible);
        Loaded += (_, _) => RefreshLayoutFromSettings();
        RefreshLayoutFromSettings();
    }

    public void RefreshLayoutFromSettings()
    {
        if (WidgetButtonsScroller == null)
            return;

        var maxVisible = Math.Clamp(_settings.GetWidgetLauncherMaxVisibleWidgets(), 1, 12);

        var rowHeights = new List<double>();
        var buttons = new Border?[]
        {
            SearchWidgetButton,
            TimerWidgetButton,
            QuickTasksWidgetButton,
            FrequentProjectsButton,
            QuickLaunchButton,
            DocQuickOpenButton,
            SmartProjectSearchButton
        };

        foreach (var button in buttons)
        {
            if (button == null || button.Visibility != Visibility.Visible)
                continue;

            var measuredHeight = button.ActualHeight;
            if (measuredHeight <= 1 || double.IsNaN(measuredHeight))
                measuredHeight = button.DesiredSize.Height;
            if (measuredHeight <= 1 || double.IsNaN(measuredHeight))
                measuredHeight = 44;

            measuredHeight += button.Margin.Top + button.Margin.Bottom;
            rowHeights.Add(measuredHeight);
        }

        var fallbackRowHeight = 52.0;
        var rowAverage = fallbackRowHeight;
        if (rowHeights.Count > 0)
        {
            var total = 0.0;
            for (var i = 0; i < rowHeights.Count; i++)
                total += rowHeights[i];
            rowAverage = total / rowHeights.Count;
        }

        var targetHeight = 0.0;
        for (var i = 0; i < maxVisible; i++)
        {
            targetHeight += i < rowHeights.Count ? rowHeights[i] : rowAverage;
        }

        WidgetButtonsScroller.MaxHeight = Math.Max(120, Math.Floor(targetHeight - 1));
    }
    
    public void UpdateTransparency()
    {
        try
        {
            var transparency = _settings.GetWidgetLauncherTransparency();
            var alpha = (byte)(transparency * 255);
            
            if (RootBorder != null)
            {
                RootBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x12, 0x12, 0x12));
            }
            
            DebugLogger.Log($"WidgetLauncher: Transparency updated to {transparency:F2}");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"WidgetLauncher: UpdateTransparency error: {ex.Message}");
        }
    }
    
    private void SearchWidgetButton_Click(object sender, MouseButtonEventArgs e)
    {
        SearchWidgetRequested?.Invoke(this, EventArgs.Empty);
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

    private void FrequentProjectsButton_Click(object sender, MouseButtonEventArgs e)
    {
        FrequentProjectsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void QuickLaunchButton_Click(object sender, MouseButtonEventArgs e)
    {
        QuickLaunchRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SmartProjectSearchButton_Click(object sender, MouseButtonEventArgs e)
    {
        SmartProjectSearchRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public void UpdateSearchButtonVisibility(bool visible)
    {
        if (SearchWidgetButton != null)
            SearchWidgetButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateTimerButtonVisibility(bool visible)
    {
        if (TimerWidgetButton != null)
            TimerWidgetButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateQuickTasksButtonVisibility(bool visible)
    {
        if (QuickTasksWidgetButton != null)
            QuickTasksWidgetButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateDocButtonVisibility(bool visible)
    {
        if (DocQuickOpenButton != null)
            DocQuickOpenButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateFrequentProjectsButtonVisibility(bool visible)
    {
        if (FrequentProjectsButton != null)
            FrequentProjectsButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateQuickLaunchButtonVisibility(bool visible)
    {
        if (QuickLaunchButton != null)
            QuickLaunchButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateSmartProjectSearchButtonVisibility(bool visible)
    {
        if (SmartProjectSearchButton != null)
            SmartProjectSearchButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void SetUpdateIndicatorVisible(bool visible)
    {
        if (UpdateIndicator != null)
            UpdateIndicator.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
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
