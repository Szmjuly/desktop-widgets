using System.Windows;
using System.Windows.Input;
using DesktopHub.Core.Abstractions;
using DesktopHub.Infrastructure.Firebase;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class DeveloperPanelOverlay : Window
{
    private readonly ISettingsService _settings;
    private readonly DeveloperPanelWidget _widget;

    public DeveloperPanelWidget Widget => _widget;

    public DeveloperPanelOverlay(ISettingsService settings, IFirebaseService? firebaseService)
    {
        InitializeComponent();
        _settings = settings;

        _widget = new DeveloperPanelWidget(firebaseService);
        WidgetHost.Content = _widget;
    }

    public void EnableDragging() => OverlayDragHelper.EnableDragging(this);
    public void DisableDragging() => OverlayDragHelper.DisableDragging(this);

    public void UpdateTransparency() =>
        OverlayHelper.ApplyTransparency(RootBorder, _settings.GetWidgetTransparency(Core.Models.WidgetIds.DeveloperPanel), "DeveloperPanelOverlay");

    public void SetUpdateIndicatorVisible(bool visible) =>
        OverlayHelper.SetUpdateIndicatorVisible(UpdateIndicator, visible);

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (OverlayHelper.IsCloseShortcutPressed(e, _settings))
        {
            Visibility = Visibility.Hidden;
            Tag = null;
            e.Handled = true;
        }
    }
}
