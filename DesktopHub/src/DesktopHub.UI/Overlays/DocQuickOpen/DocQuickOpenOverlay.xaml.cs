using System.Windows;
using System.Windows.Input;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class DocQuickOpenOverlay : Window
{
    private readonly ISettingsService _settings;
    private readonly DocQuickOpenWidget _widget;

    public DocQuickOpenWidget Widget => _widget;

    public DocQuickOpenOverlay(DocOpenService docService, ISettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        _widget = new DocQuickOpenWidget(docService);
        WidgetHost.Content = _widget;
    }

    public void EnableDragging() => OverlayDragHelper.EnableDragging(this);
    public void DisableDragging() => OverlayDragHelper.DisableDragging(this);

    public void UpdateTransparency() =>
        OverlayHelper.ApplyTransparency(RootBorder, _settings.GetDocWidgetTransparency(), "DocQuickOpenOverlay");

    public void SetUpdateIndicatorVisible(bool visible) =>
        OverlayHelper.SetUpdateIndicatorVisible(UpdateIndicator, visible);

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Visibility = Visibility.Hidden;
            Tag = null;
            e.Handled = true;
        }
    }
}
