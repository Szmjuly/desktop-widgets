using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopHub.Core.Abstractions;
using DesktopHub.UI.Helpers;

namespace DesktopHub.UI;

public partial class WidgetLauncher : Window
{
    public event EventHandler? SearchWidgetRequested;
    public event EventHandler? TimerWidgetRequested;
    public event EventHandler? QuickTasksWidgetRequested;
    public event EventHandler? DocQuickOpenRequested;
    public event EventHandler? FrequentProjectsRequested;
    public event EventHandler? QuickLaunchRequested;
    public event EventHandler? SmartProjectSearchRequested;
    public event EventHandler? CheatSheetRequested;
    public event EventHandler? MetricsViewerRequested;
    public event EventHandler? DeveloperPanelRequested;
    public event EventHandler? ProjectInfoRequested;
    private readonly ISettingsService _settings;
    
    public WidgetLauncher(ISettingsService settings)
    {
        InitializeComponent();
        _settings = settings;
        
        // Apply initial button visibility from settings
        UpdateSearchButtonVisibility(_settings.GetSearchWidgetEnabled());
        UpdateTimerButtonVisibility(_settings.GetTimerWidgetEnabled());
        UpdateQuickTasksButtonVisibility(_settings.GetQuickTasksWidgetEnabled());
        UpdateDocButtonVisibility(_settings.GetDocWidgetEnabled());
        UpdateFrequentProjectsButtonVisibility(_settings.GetFrequentProjectsWidgetEnabled());
        UpdateQuickLaunchButtonVisibility(_settings.GetQuickLaunchWidgetEnabled());
        var smartSearchVisible = _settings.GetSmartProjectSearchWidgetEnabled() &&
                                 !_settings.GetSmartProjectSearchAttachToSearchOverlayMode();
        UpdateSmartProjectSearchButtonVisibility(smartSearchVisible);
        UpdateCheatSheetButtonVisibility(_settings.GetCheatSheetWidgetEnabled());
        // Always start hidden; SearchOverlay enables this based on DEV role.
        UpdateDeveloperPanelButtonVisibility(false);
        UpdateProjectInfoButtonVisibility(_settings.GetWidgetEnabled(Core.Models.WidgetIds.ProjectInfo));
        Loaded += (_, _) => RefreshLayoutFromSettings();
        RefreshLayoutFromSettings();
    }

    public void RefreshLayoutFromSettings()
    {
        if (WidgetButtonsScroller == null)
            return;

        var maxVisible = Math.Clamp(_settings.GetWidgetLauncherMaxVisibleWidgets(), 1, 12);

        var rowHeights = new List<double>();
        var buttons = new Border?[]
        {
            SearchWidgetButton,
            TimerWidgetButton,
            QuickTasksWidgetButton,
            FrequentProjectsButton,
            QuickLaunchButton,
            DocQuickOpenButton,
            SmartProjectSearchButton,
            MetricsViewerButton,
            DeveloperPanelButton,
            ProjectInfoButton,
            CheatSheetButton
        };

        foreach (var button in buttons)
        {
            if (button == null || button.Visibility != Visibility.Visible)
                continue;

            var measuredHeight = button.ActualHeight;
            if (measuredHeight <= 1 || double.IsNaN(measuredHeight))
                measuredHeight = button.DesiredSize.Height;
            if (measuredHeight <= 1 || double.IsNaN(measuredHeight))
                measuredHeight = 44;

            measuredHeight += button.Margin.Top + button.Margin.Bottom;
            rowHeights.Add(measuredHeight);
        }

        var fallbackRowHeight = 52.0;
        var rowAverage = fallbackRowHeight;
        if (rowHeights.Count > 0)
        {
            var total = 0.0;
            for (var i = 0; i < rowHeights.Count; i++)
                total += rowHeights[i];
            rowAverage = total / rowHeights.Count;
        }

        var targetHeight = 0.0;
        for (var i = 0; i < maxVisible; i++)
        {
            targetHeight += i < rowHeights.Count ? rowHeights[i] : rowAverage;
        }

        WidgetButtonsScroller.MaxHeight = Math.Max(120, Math.Floor(targetHeight - 1));
    }
    
    public void UpdateTransparency() =>
        OverlayHelper.ApplyTransparency(RootBorder, _settings.GetWidgetLauncherTransparency(), "WidgetLauncher");

    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        Hide();
    }
    
    private void SearchWidgetButton_Click(object sender, MouseButtonEventArgs e)
    {
        SearchWidgetRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private void TimerWidgetButton_Click(object sender, MouseButtonEventArgs e)
    {
        TimerWidgetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void QuickTasksWidgetButton_Click(object sender, MouseButtonEventArgs e)
    {
        QuickTasksWidgetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DocQuickOpenButton_Click(object sender, MouseButtonEventArgs e)
    {
        DocQuickOpenRequested?.Invoke(this, EventArgs.Empty);
    }

    private void FrequentProjectsButton_Click(object sender, MouseButtonEventArgs e)
    {
        FrequentProjectsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void QuickLaunchButton_Click(object sender, MouseButtonEventArgs e)
    {
        QuickLaunchRequested?.Invoke(this, EventArgs.Empty);
    }

    private void SmartProjectSearchButton_Click(object sender, MouseButtonEventArgs e)
    {
        SmartProjectSearchRequested?.Invoke(this, EventArgs.Empty);
    }

    private void CheatSheetButton_Click(object sender, MouseButtonEventArgs e)
    {
        CheatSheetRequested?.Invoke(this, EventArgs.Empty);
    }

    private void MetricsViewerButton_Click(object sender, MouseButtonEventArgs e)
    {
        MetricsViewerRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ProjectInfoButton_Click(object sender, MouseButtonEventArgs e)
    {
        ProjectInfoRequested?.Invoke(this, EventArgs.Empty);
    }

    private void DeveloperPanelButton_Click(object sender, MouseButtonEventArgs e)
    {
        DeveloperPanelRequested?.Invoke(this, EventArgs.Empty);
    }
    
    public void UpdateSearchButtonVisibility(bool visible)
    {
        if (SearchWidgetButton != null)
            SearchWidgetButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateTimerButtonVisibility(bool visible)
    {
        if (TimerWidgetButton != null)
            TimerWidgetButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateQuickTasksButtonVisibility(bool visible)
    {
        if (QuickTasksWidgetButton != null)
            QuickTasksWidgetButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateDocButtonVisibility(bool visible)
    {
        if (DocQuickOpenButton != null)
            DocQuickOpenButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateFrequentProjectsButtonVisibility(bool visible)
    {
        if (FrequentProjectsButton != null)
            FrequentProjectsButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateQuickLaunchButtonVisibility(bool visible)
    {
        if (QuickLaunchButton != null)
            QuickLaunchButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateSmartProjectSearchButtonVisibility(bool visible)
    {
        if (SmartProjectSearchButton != null)
            SmartProjectSearchButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateCheatSheetButtonVisibility(bool visible)
    {
        if (CheatSheetButton != null)
            CheatSheetButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateMetricsViewerButtonVisibility(bool visible)
    {
        if (MetricsViewerButton != null)
            MetricsViewerButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateProjectInfoButtonVisibility(bool visible)
    {
        if (ProjectInfoButton != null)
            ProjectInfoButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void UpdateDeveloperPanelButtonVisibility(bool visible)
    {
        if (DeveloperPanelButton != null)
            DeveloperPanelButton.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
        RefreshLayoutFromSettings();
    }

    public void SetUpdateIndicatorVisible(bool visible) =>
        OverlayHelper.SetUpdateIndicatorVisible(UpdateIndicator, visible);
    
    private void Window_Deactivated(object sender, EventArgs e)
    {
        // Don't auto-hide like the search overlay
        // This is controlled by the hotkey toggle
    }
    
    public void EnableDragging() => OverlayDragHelper.EnableDragging(this);
    public void DisableDragging() => OverlayDragHelper.DisableDragging(this);
    
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (OverlayHelper.IsCloseShortcutPressed(e, _settings))
        {
            DebugLogger.Log("WidgetLauncher: Close shortcut pressed -> Hiding");
            Visibility = Visibility.Hidden;
            e.Handled = true;
        }
    }
}
