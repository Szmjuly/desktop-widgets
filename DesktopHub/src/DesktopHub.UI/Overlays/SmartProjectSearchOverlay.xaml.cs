using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class SmartProjectSearchOverlay : Window
{
    private readonly ISettingsService _settings;
    private bool _isDragging;
    private System.Windows.Point _dragStartPoint;
    private bool _isLivingWidgetsMode;

    public SmartProjectSearchWidget Widget { get; }

    public SmartProjectSearchOverlay(SmartProjectSearchService service, ISettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        Widget = new SmartProjectSearchWidget(service);
        WidgetHost.Content = Widget;
    }

    public void EnableDragging()
    {
        _isLivingWidgetsMode = true;

        MouseLeftButtonDown -= Overlay_MouseLeftButtonDown;
        MouseLeftButtonUp -= Overlay_MouseLeftButtonUp;
        MouseMove -= Overlay_MouseMove;

        MouseLeftButtonDown += Overlay_MouseLeftButtonDown;
        MouseLeftButtonUp += Overlay_MouseLeftButtonUp;
        MouseMove += Overlay_MouseMove;
    }

    public void DisableDragging()
    {
        _isLivingWidgetsMode = false;
        MouseLeftButtonDown -= Overlay_MouseLeftButtonDown;
        MouseLeftButtonUp -= Overlay_MouseLeftButtonUp;
        MouseMove -= Overlay_MouseMove;
    }

    public void UpdateTransparency()
    {
        try
        {
            var transparency = _settings.GetSmartProjectSearchWidgetTransparency();
            var alpha = (byte)(transparency * 255);
            RootBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(alpha, 0x12, 0x12, 0x12));
        }
        catch
        {
            // ignore
        }
    }

    public void SetUpdateIndicatorVisible(bool visible)
    {
        if (UpdateIndicator != null)
            UpdateIndicator.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Overlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_isLivingWidgetsMode)
            return;

        if (e.OriginalSource is FrameworkElement element)
        {
            var clickedType = element.GetType().Name;
            if (clickedType == "TextBox" || clickedType == "Button" || clickedType == "ListBoxItem" ||
                clickedType == "ComboBox" || clickedType == "ScrollBar" || clickedType == "Thumb")
            {
                return;
            }
        }

        _isDragging = true;
        _dragStartPoint = e.GetPosition(this);
        CaptureMouse();
    }

    private void Overlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
            return;

        _isDragging = false;
        ReleaseMouseCapture();
    }

    private void Overlay_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed)
            return;

        var currentPosition = e.GetPosition(this);
        var offset = currentPosition - _dragStartPoint;
        Left += offset.X;
        Top += offset.Y;
    }

    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Hide();
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
            Visibility = Visibility.Hidden;
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
