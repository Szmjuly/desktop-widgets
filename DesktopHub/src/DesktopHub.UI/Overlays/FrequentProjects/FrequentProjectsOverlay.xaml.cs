using System;
using System.Windows;
using System.Windows.Input;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class FrequentProjectsOverlay : Window
{
    private readonly ISettingsService _settings;

    public FrequentProjectsWidget? Widget { get; private set; }

    public event Action<string>? OnProjectSelectedForSearch;

    public FrequentProjectsOverlay(IProjectLaunchDataStore launchStore, ISettingsService settings)
    {
        if (launchStore == null) throw new ArgumentNullException(nameof(launchStore));
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        InitializeComponent();
        _settings = settings;

        Widget = new FrequentProjectsWidget(launchStore, _settings);
        Widget.OnProjectSelectedForSearch += (path) => OnProjectSelectedForSearch?.Invoke(path);
        WidgetHost.Content = Widget;
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
        OverlayHelper.ApplyTransparency(RootBorder, _settings.GetFrequentProjectsWidgetTransparency(), "FrequentProjectsOverlay");

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (OverlayHelper.IsCloseShortcutPressed(e, _settings))
        {
            DebugLogger.Log("FrequentProjectsOverlay: Close shortcut pressed -> Hiding");
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
