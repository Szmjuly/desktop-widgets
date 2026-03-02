using System;
using System.Windows;
using System.Windows.Input;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class MetricsViewerOverlay : Window
{
    private readonly ISettingsService _settings;

    // Resize state
    private bool _isResizing;
    private string _resizeDirection = string.Empty;
    private System.Windows.Point _resizeStartPoint;
    private double _resizeStartWidth;
    private double _resizeStartHeight;
    private double _resizeStartLeft;
    private double _resizeStartTop;

    public MetricsViewerWidget Widget { get; }

    public MetricsViewerOverlay(ISettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        Widget = new MetricsViewerWidget();
        Widget.SetSettingsService(settings);
        WidgetHost.Content = Widget;
    }

    public void EnableDragging() => OverlayDragHelper.EnableDragging(this);
    public void DisableDragging() => OverlayDragHelper.DisableDragging(this);

    public void SetUpdateIndicatorVisible(bool visible) =>
        OverlayHelper.SetUpdateIndicatorVisible(UpdateIndicator, visible);

    public void UpdateTransparency() =>
        OverlayHelper.ApplyTransparency(RootBorder, _settings.GetWidgetTransparency(DesktopHub.Core.Models.WidgetIds.MetricsViewer), "MetricsViewerOverlay");

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (OverlayHelper.IsCloseShortcutPressed(e, _settings))
        {
            DebugLogger.Log("MetricsViewerOverlay: Close shortcut pressed -> Hiding");
            Visibility = Visibility.Hidden;
            e.Handled = true;
        }
    }

    // ===== Resize handlers =====

    private void Resize_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement el && el.Tag is string dir)
        {
            _isResizing = true;
            _resizeDirection = dir;
            _resizeStartPoint = PointToScreen(e.GetPosition(this));
            _resizeStartWidth = this.Width;
            _resizeStartHeight = this.Height;
            _resizeStartLeft = this.Left;
            _resizeStartTop = this.Top;
            el.CaptureMouse();
            e.Handled = true;
        }
    }

    private void Resize_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizing)
        {
            _isResizing = false;
            _resizeDirection = string.Empty;
            if (sender is FrameworkElement el)
                el.ReleaseMouseCapture();
            e.Handled = true;
        }
    }

    private void Resize_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isResizing || e.LeftButton != MouseButtonState.Pressed) return;

        var current = PointToScreen(e.GetPosition(this));
        var dx = current.X - _resizeStartPoint.X;
        var dy = current.Y - _resizeStartPoint.Y;
        var dir = _resizeDirection;

        var newLeft = _resizeStartLeft;
        var newTop = _resizeStartTop;
        var newWidth = _resizeStartWidth;
        var newHeight = _resizeStartHeight;

        if (dir.Contains("Right"))
            newWidth = Math.Max(this.MinWidth, _resizeStartWidth + dx);
        if (dir.Contains("Bottom"))
            newHeight = Math.Max(this.MinHeight, _resizeStartHeight + dy);
        if (dir.Contains("Left"))
        {
            newWidth = Math.Max(this.MinWidth, _resizeStartWidth - dx);
            if (newWidth > this.MinWidth)
                newLeft = _resizeStartLeft + dx;
            else
                newLeft = _resizeStartLeft + (_resizeStartWidth - this.MinWidth);
        }
        if (dir.Contains("Top"))
        {
            newHeight = Math.Max(this.MinHeight, _resizeStartHeight - dy);
            if (newHeight > this.MinHeight)
                newTop = _resizeStartTop + dy;
            else
                newTop = _resizeStartTop + (_resizeStartHeight - this.MinHeight);
        }

        this.Left = newLeft;
        this.Top = newTop;
        this.Width = newWidth;
        this.Height = newHeight;

        e.Handled = true;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!OverlayHelper.HandleOnClosingHide(e, this))
            base.OnClosing(e);
    }
}
