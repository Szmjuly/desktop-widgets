using System.Windows;
using System.Windows.Input;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class CheatSheetOverlay : Window
{
    private readonly ISettingsService _settings;

    public CheatSheetWidget Widget { get; }

    public CheatSheetOverlay(CheatSheetService service, ISettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        Widget = new CheatSheetWidget(service);
        Widget.DesiredWidthChanged += OnDesiredWidthChanged;
        WidgetHost.Content = Widget;
    }

    private void OnDesiredWidthChanged(double desiredWidth)
    {
        if (Math.Abs(Width - desiredWidth) < 1)
            return;

        Width = desiredWidth;
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
        OverlayHelper.ApplyTransparency(RootBorder, _settings.GetCheatSheetWidgetTransparency(), "CheatSheetOverlay");

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (OverlayHelper.IsCloseShortcutPressed(e, _settings))
        {
            DebugLogger.Log("CheatSheetOverlay: Close shortcut pressed -> Hiding");
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
