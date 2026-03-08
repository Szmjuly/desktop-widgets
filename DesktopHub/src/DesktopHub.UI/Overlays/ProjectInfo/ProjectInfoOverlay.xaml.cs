using System.Windows;
using System.Windows.Input;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class ProjectInfoOverlay : Window
{
    private readonly ISettingsService _settings;
    private readonly ProjectInfoWidget _widget;

    public ProjectInfoWidget Widget => _widget;

    public ProjectInfoOverlay(IProjectTagService tagService, ITagVocabularyService? vocabService, ISettingsService settings)
    {
        InitializeComponent();
        _settings = settings;

        _widget = new ProjectInfoWidget(tagService, vocabService);
        WidgetHost.Content = _widget;
    }

    public void EnableDragging() => OverlayDragHelper.EnableDragging(this);
    public void DisableDragging() => OverlayDragHelper.DisableDragging(this);

    public void UpdateTransparency() =>
        OverlayHelper.ApplyTransparency(RootBorder, _settings.GetWidgetTransparency(Core.Models.WidgetIds.ProjectInfo), "ProjectInfoOverlay");

    public void SetUpdateIndicatorVisible(bool visible) =>
        OverlayHelper.SetUpdateIndicatorVisible(UpdateIndicator, visible);

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideAndLock();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Hide the overlay and re-lock the widget so edits require an explicit unlock next time.
    /// </summary>
    public void HideAndLock()
    {
        Visibility = Visibility.Hidden;
        Tag = null;
        _widget.ResetLock();
    }
}
