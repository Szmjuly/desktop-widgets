using System;
using System.Windows;
using System.Windows.Input;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class QuickTasksOverlay : Window
{
    private readonly ISettingsService _settings;

    public QuickTasksOverlay(TaskService taskService, ISettingsService settings)
    {
        if (taskService == null) throw new ArgumentNullException(nameof(taskService));
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        InitializeComponent();
        _settings = settings;

        var widget = new QuickTasksWidget(taskService);
        WidgetHost.Content = widget;
    }

    public void EnableDragging() => OverlayDragHelper.EnableDragging(this);
    public void DisableDragging() => OverlayDragHelper.DisableDragging(this);

    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Hide();
    }

    public void SetUpdateIndicatorVisible(bool visible) =>
        OverlayHelper.SetUpdateIndicatorVisible(UpdateIndicator, visible);

    public void UpdateTransparency() =>
        OverlayHelper.ApplyTransparency(RootBorder, _settings.GetQuickTasksWidgetTransparency(), "QuickTasksOverlay");

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (OverlayHelper.IsCloseShortcutPressed(e, _settings))
        {
            DebugLogger.Log("QuickTasksOverlay: Close shortcut pressed -> Hiding");
            Visibility = Visibility.Hidden;
            e.Handled = true;
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!OverlayHelper.HandleOnClosingHide(e, this))
            base.OnClosing(e);
    }
}
