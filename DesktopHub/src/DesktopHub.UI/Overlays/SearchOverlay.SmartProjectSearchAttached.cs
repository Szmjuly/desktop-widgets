using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class SearchOverlay
{
    private void ApplySmartProjectSearchAttachModeState()
    {
        var attachModeEnabled = _settings.GetSmartProjectSearchAttachToSearchOverlayMode();

        if (SmartProjectSearchAttachToggleButton != null)
            SmartProjectSearchAttachToggleButton.Visibility = attachModeEnabled ? Visibility.Visible : Visibility.Collapsed;

        if (!attachModeEnabled)
        {
            SetSmartProjectSearchAttachedPanelExpanded(false, false);
            if (SmartProjectSearchAttachedHost != null)
                SmartProjectSearchAttachedHost.Content = null;

            return;
        }

        EnsureSmartProjectSearchAttachedWidget();

        if (SmartProjectSearchAttachedHost != null && _smartProjectSearchAttachedWidget != null)
            SmartProjectSearchAttachedHost.Content = _smartProjectSearchAttachedWidget;

        if (_smartProjectSearchOverlay != null)
        {
            _smartProjectSearchOverlay.Visibility = Visibility.Hidden;
            _smartProjectSearchOverlay.Tag = null;

            if (_settings.GetSmartProjectSearchWidgetVisible())
            {
                _settings.SetSmartProjectSearchWidgetVisible(false);
                _ = _settings.SaveAsync();
            }
        }

        UpdateOverlayHeightForCurrentState(false);
    }

    private void EnsureSmartProjectSearchAttachedWidget()
    {
        if (_smartProjectSearchAttachedWidget != null || _smartProjectSearchService == null)
            return;

        _smartProjectSearchAttachedWidget = new SmartProjectSearchWidget(_smartProjectSearchService);

        if (ResultsList.SelectedItem is ProjectViewModel vm)
        {
            _ = _smartProjectSearchService.SetProjectAsync(vm.Path, $"{vm.FullNumber} {vm.Name}");
        }
    }

    private void SmartProjectSearchAttachToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_settings.GetSmartProjectSearchAttachToSearchOverlayMode())
            return;

        if (_isResultsCollapsed)
        {
            _isResultsCollapsed = false;
            ResultsContainer.Visibility = Visibility.Visible;
            CollapseIconRotation.Angle = 0;
        }

        _userManuallySizedResults = true;
        SetSmartProjectSearchAttachedPanelExpanded(!_isSmartProjectSearchAttachedPanelExpanded, true);
    }

    private void SetSmartProjectSearchAttachedPanelExpanded(bool expanded, bool animate)
    {
        if (!_settings.GetSmartProjectSearchAttachToSearchOverlayMode())
            expanded = false;

        _isSmartProjectSearchAttachedPanelExpanded = expanded;

        if (SmartProjectSearchAttachedPanelContainer == null)
        {
            UpdateOverlayHeightForCurrentState(animate);
            return;
        }

        var targetHeight = expanded ? SmartProjectSearchAttachedPanelExpandedHeight : 0;

        if (!animate || !IsLoaded)
        {
            SmartProjectSearchAttachedPanelContainer.MaxHeight = targetHeight;
            SmartProjectSearchAttachedPanelContainer.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            UpdateOverlayHeightForCurrentState(false);
            return;
        }

        var fromHeight = SmartProjectSearchAttachedPanelContainer.MaxHeight;
        if (double.IsNaN(fromHeight))
            fromHeight = expanded ? 0 : SmartProjectSearchAttachedPanelExpandedHeight;

        if (expanded)
            SmartProjectSearchAttachedPanelContainer.Visibility = Visibility.Visible;

        var panelAnimation = new DoubleAnimation(fromHeight, targetHeight, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        if (!expanded)
        {
            panelAnimation.Completed += (_, _) =>
            {
                if (!_isSmartProjectSearchAttachedPanelExpanded)
                    SmartProjectSearchAttachedPanelContainer.Visibility = Visibility.Collapsed;
            };
        }

        SmartProjectSearchAttachedPanelContainer.BeginAnimation(FrameworkElement.MaxHeightProperty, panelAnimation, HandoffBehavior.SnapshotAndReplace);
        UpdateOverlayHeightForCurrentState(true);
    }

    private void UpdateOverlayHeightForCurrentState(bool animate)
    {
        var baseHeight = _isResultsCollapsed ? OverlayCollapsedBaseHeight : OverlayExpandedBaseHeight;
        var attachedHeight = !_isResultsCollapsed &&
                             _settings.GetSmartProjectSearchAttachToSearchOverlayMode() &&
                             _isSmartProjectSearchAttachedPanelExpanded
            ? SmartProjectSearchAttachedPanelExpandedHeight
            : 0;

        var targetHeight = baseHeight + attachedHeight;

        if (!animate || !IsLoaded)
        {
            Height = targetHeight;
            return;
        }

        var heightAnimation = new DoubleAnimation(Height, targetHeight, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };

        BeginAnimation(Window.HeightProperty, heightAnimation, HandoffBehavior.SnapshotAndReplace);
    }
}
