using System.Windows;
using System.Windows.Input;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class SmartProjectSearchOverlay : Window
{
    private readonly ISettingsService _settings;

    public SmartProjectSearchWidget Widget { get; }

    public SmartProjectSearchOverlay(SmartProjectSearchService service, ISettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        Widget = new SmartProjectSearchWidget(service);
        WidgetHost.Content = Widget;
    }

    public void EnableDragging() => OverlayDragHelper.EnableDragging(this);
    public void DisableDragging() => OverlayDragHelper.DisableDragging(this);

    public void UpdateTransparency() =>
        OverlayHelper.ApplyTransparency(RootBorder, _settings.GetSmartProjectSearchWidgetTransparency(), "SmartProjectSearchOverlay");

    public void SetUpdateIndicatorVisible(bool visible) =>
        OverlayHelper.SetUpdateIndicatorVisible(UpdateIndicator, visible);

    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Hide();
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (OverlayHelper.IsCloseShortcutPressed(e, _settings))
        {
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
