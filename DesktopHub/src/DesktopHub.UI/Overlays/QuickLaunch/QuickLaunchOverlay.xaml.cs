using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class QuickLaunchOverlay : Window
{
    private readonly ISettingsService _settings;
    private bool _isDragging = false;
    private System.Windows.Point _dragStartPoint;
    private bool _isLivingWidgetsMode = false;

    public QuickLaunchWidget? Widget { get; private set; }

    public QuickLaunchOverlay(ISettingsService settings)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        InitializeComponent();
        _settings = settings;

        Widget = new QuickLaunchWidget(settings);
        WidgetHost.Content = Widget;
    }

    public void EnableDragging()
    {
        _isLivingWidgetsMode = true;

        this.MouseLeftButtonDown -= Overlay_MouseLeftButtonDown;
        this.MouseLeftButtonUp -= Overlay_MouseLeftButtonUp;
        this.MouseMove -= Overlay_MouseMove;

        this.MouseLeftButtonDown += Overlay_MouseLeftButtonDown;
        this.MouseLeftButtonUp += Overlay_MouseLeftButtonUp;
        this.MouseMove += Overlay_MouseMove;
    }

    public void DisableDragging()
    {
        _isLivingWidgetsMode = false;
        this.MouseLeftButtonDown -= Overlay_MouseLeftButtonDown;
        this.MouseLeftButtonUp -= Overlay_MouseLeftButtonUp;
        this.MouseMove -= Overlay_MouseMove;
    }

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isLivingWidgetsMode) return;

        var element = e.OriginalSource as FrameworkElement;
        if (element != null)
        {
            var clickedType = element.GetType().Name;
            if (clickedType == "TextBox" || clickedType == "Button" || clickedType == "ListBoxItem" ||
                clickedType == "ComboBox" || clickedType == "ScrollBar" || clickedType == "Thumb")
                return;
        }

        _isDragging = true;
        _dragStartPoint = e.GetPosition(this);
        this.CaptureMouse();
    }

    private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            this.ReleaseMouseCapture();
        }
    }

    private void Overlay_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            var currentPosition = e.GetPosition(this);
            var offset = currentPosition - _dragStartPoint;
            this.Left += offset.X;
            this.Top += offset.Y;
        }
    }

    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Hide();
    }

    public void SetUpdateIndicatorVisible(bool visible)
    {
        if (UpdateIndicator != null)
            UpdateIndicator.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public void UpdateTransparency()
    {
        try
        {
            var transparency = _settings.GetQuickLaunchWidgetTransparency();
            var alpha = (byte)(transparency * 255);

            if (RootBorder != null)
            {
                RootBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x12, 0x12, 0x12));
            }

            DebugLogger.Log($"QuickLaunchOverlay: Transparency updated to {transparency:F2}");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"QuickLaunchOverlay: UpdateTransparency error: {ex.Message}");
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var (closeModifiers, closeKey) = _settings.GetCloseShortcut();
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

        if (currentModifiers == closeModifiers && currentKey == closeKey)
        {
            DebugLogger.Log("QuickLaunchOverlay: Close shortcut pressed -> Hiding");
            this.Visibility = Visibility.Hidden;
            e.Handled = true;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        var app = System.Windows.Application.Current;
        if (app == null || app.ShutdownMode == ShutdownMode.OnExplicitShutdown)
        {
            base.OnClosing(e);
            return;
        }

        e.Cancel = true;
        Hide();
    }
}
