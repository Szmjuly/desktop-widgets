using System;
using System.Windows;
using System.Windows.Controls;
using WpfMedia = System.Windows.Media;
using System.Windows.Media.Animation;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class SearchOverlay
{
    private Window? _smartProjectSearchAttachedWindow;
    private Border? _smartProjectSearchAttachedRootBorder;
    private const double SmartSearchWindowGap = 8;
    private bool _suspendRootClipUpdates;

    private void ApplySmartProjectSearchAttachModeState()
    {
        var attachModeEnabled = _settings.GetSmartProjectSearchAttachToSearchOverlayMode();

        if (SmartProjectSearchAttachToggleButton != null)
            SmartProjectSearchAttachToggleButton.Visibility = attachModeEnabled ? Visibility.Visible : Visibility.Collapsed;

        if (!attachModeEnabled)
        {
            HideSmartSearchAttachedWindow();
            return;
        }

        EnsureSmartProjectSearchAttachedWidget();

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

    private void EnsureSmartSearchAttachedWindow()
    {
        if (_smartProjectSearchAttachedWindow != null)
            return;

        EnsureSmartProjectSearchAttachedWidget();
        if (_smartProjectSearchAttachedWidget == null)
            return;

        _smartProjectSearchAttachedWindow = new Window
        {
            Owner = this,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = WpfMedia.Brushes.Transparent,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            ResizeMode = ResizeMode.NoResize,
            Width = this.Width,
            Height = SmartProjectSearchAttachedPanelExpandedHeight,
            SizeToContent = SizeToContent.Manual,
        };

        var transparency = _settings.GetSmartProjectSearchWidgetTransparency();
        var alpha = (byte)(transparency * 255);
        var rootBorder = new Border
        {
            Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(alpha,
                Helpers.ThemeHelper.GetColor("WindowBackgroundColor").R,
                Helpers.ThemeHelper.GetColor("WindowBackgroundColor").G,
                Helpers.ThemeHelper.GetColor("WindowBackgroundColor").B)),
            CornerRadius = new CornerRadius(12),
            BorderBrush = Helpers.ThemeHelper.Border,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18),
            ClipToBounds = true,
        };

        var glassBorder = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = Helpers.ThemeHelper.CardBorder,
            Background = new WpfMedia.SolidColorBrush(Helpers.ThemeHelper.GetColor("GlassBackgroundColor")),
            IsHitTestVisible = false,
            Margin = new Thickness(2),
        };

        var contentGrid = new Grid();
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        contentGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = new TextBlock
        {
            Text = "Smart Project Search",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = Helpers.ThemeHelper.TextPrimary,
            Margin = new Thickness(0, 0, 0, 8),
        };
        Grid.SetRow(header, 0);
        contentGrid.Children.Add(header);

        var widgetHost = new ContentControl { Content = _smartProjectSearchAttachedWidget };
        Grid.SetRow(widgetHost, 1);
        contentGrid.Children.Add(widgetHost);

        var outerGrid = new Grid();
        outerGrid.Children.Add(glassBorder);
        outerGrid.Children.Add(new Border { Background = WpfMedia.Brushes.Transparent, Padding = new Thickness(18), Child = contentGrid });

        rootBorder.Child = outerGrid;
        _smartProjectSearchAttachedRootBorder = rootBorder;
        _smartProjectSearchAttachedWindow.Content = rootBorder;

        // Prevent this window from causing the overlay to auto-hide
        _smartProjectSearchAttachedWindow.Activated += (_, _) =>
        {
            _deactivateTimer?.Stop();
        };
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
            CollapseToggleBtn.ToolTip = "Collapse project list";
        }

        _userManuallySizedResults = true;
        var expanding = !_isSmartProjectSearchAttachedPanelExpanded;
        SetSmartProjectSearchAttachedPanelExpanded(expanding, true);

        if (expanding && _smartProjectSearchAttachedWidget != null)
            _smartProjectSearchAttachedWidget.FocusSearchBox();
    }

    private void SetSmartProjectSearchAttachedPanelExpanded(bool expanded, bool animate)
    {
        if (!_settings.GetSmartProjectSearchAttachToSearchOverlayMode())
            expanded = false;

        _isSmartProjectSearchAttachedPanelExpanded = expanded;

        if (expanded)
        {
            ShowSmartSearchAttachedWindow(animate);
        }
        else
        {
            HideSmartSearchAttachedWindow();
        }
    }

    private void ShowSmartSearchAttachedWindow(bool animate)
    {
        EnsureSmartSearchAttachedWindow();
        if (_smartProjectSearchAttachedWindow == null)
            return;

        PositionSmartSearchAttachedWindow();

        _smartProjectSearchAttachedWindow.Width = this.Width;
        _smartProjectSearchAttachedWindow.Height = SmartProjectSearchAttachedPanelExpandedHeight;

        if (animate)
        {
            // Start with 0 height and animate to full height (slide down effect)
            _smartProjectSearchAttachedWindow.Height = 0;
            _smartProjectSearchAttachedWindow.Visibility = Visibility.Visible;
            _smartProjectSearchAttachedWindow.Show();

            var slideAnimation = new DoubleAnimation(0, SmartProjectSearchAttachedPanelExpandedHeight, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            _smartProjectSearchAttachedWindow.BeginAnimation(Window.HeightProperty, slideAnimation);
        }
        else
        {
            _smartProjectSearchAttachedWindow.Visibility = Visibility.Visible;
            _smartProjectSearchAttachedWindow.Show();
        }
    }

    internal void UpdateSmartSearchAttachedWindowTransparency()
    {
        if (_smartProjectSearchAttachedRootBorder == null) return;
        var transparency = _settings.GetSmartProjectSearchWidgetTransparency();
        var alpha = (byte)(transparency * 255);
        _smartProjectSearchAttachedRootBorder.Background =
            new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(alpha,
                Helpers.ThemeHelper.GetColor("WindowBackgroundColor").R,
                Helpers.ThemeHelper.GetColor("WindowBackgroundColor").G,
                Helpers.ThemeHelper.GetColor("WindowBackgroundColor").B));
    }

    private void HideSmartSearchAttachedWindow()
    {
        _isSmartProjectSearchAttachedPanelExpanded = false;

        if (_smartProjectSearchAttachedWindow == null)
            return;

        _smartProjectSearchAttachedWindow.Visibility = Visibility.Hidden;
    }

    private void PositionSmartSearchAttachedWindow()
    {
        if (_smartProjectSearchAttachedWindow == null)
            return;

        _smartProjectSearchAttachedWindow.Left = this.Left;
        _smartProjectSearchAttachedWindow.Top = this.Top + this.ActualHeight + SmartSearchWindowGap;
    }

    internal bool IsSmartSearchAttachedWindowActive =>
        _smartProjectSearchAttachedWindow != null && _smartProjectSearchAttachedWindow.IsActive;

    private void UpdateOverlayHeightForCurrentState(bool animate)
    {
        // For expanded mode we use a fixed overlay height. Skip expensive layout measuring
        // to keep carrot-expand interaction responsive with large result sets.
        if (!_isResultsCollapsed)
        {
            var expandedTargetHeight = OverlayExpandedBaseHeight;
            DebugLogger.LogSeparator("UpdateOverlayHeightForCurrentState (expanded-fast-path)");
            DebugLogger.LogVariable("_isResultsCollapsed", _isResultsCollapsed);
            DebugLogger.LogVariable("ResultsList.Items.Count", ResultsList.Items.Count);
            DebugLogger.LogVariable("targetHeight", expandedTargetHeight);

            if (!animate || !IsLoaded)
            {
                Height = expandedTargetHeight;
                ResultsContainer.Opacity = 1;
                return;
            }

            // Do NOT suspend root clip updates during expansion — the clip must track the
            // growing window height so ResultsContainer (at y≈collapsed-height) is not masked.
            _suspendRootClipUpdates = false;
            ResultsContainer.Opacity = 1;
            ResultsContainer.BeginAnimation(UIElement.OpacityProperty, null);

            var expandedAnimation = new DoubleAnimation(Height, expandedTargetHeight, TimeSpan.FromMilliseconds(170))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            expandedAnimation.Completed += (_, _) =>
            {
                UpdateRootClip(12);
                ResultsContainer.Opacity = 1;
            };

            BeginAnimation(Window.HeightProperty, expandedAnimation, HandoffBehavior.SnapshotAndReplace);
            return;
        }

        double collapsedHeight;

        if (IsLoaded)
        {
            // Force a layout pass so that any recently-changed Visibility states are
            // committed before we read ActualHeight — without this, rows made visible
            // just before this call still report 0.
            UpdateLayout();

            // Ask WPF for the exact desired height of the content area.
            // ResultsContainer is already Collapsed so the star row contributes 0.
            // DesiredSize includes ContentBorder's own Padding automatically.
            ContentBorder.Measure(new System.Windows.Size(ContentBorder.ActualWidth, double.PositiveInfinity));
            const double clipCompensation = 2;
            collapsedHeight = Math.Ceiling(
                ContentBorder.DesiredSize.Height
                + RootBorder.BorderThickness.Top
                + RootBorder.BorderThickness.Bottom
                + clipCompensation);

            DebugLogger.LogSeparator("UpdateOverlayHeightForCurrentState (measure)");
            DebugLogger.LogVariable("_isResultsCollapsed", _isResultsCollapsed);
            DebugLogger.LogVariable("ResultsContainer.Visibility", ResultsContainer.Visibility);
            DebugLogger.LogVariable("TagCarouselContainer.Visibility", TagCarouselContainer.Visibility);
            DebugLogger.LogVariable("HistoryAndCollapseContainer.Visibility", HistoryAndCollapseContainer.Visibility);
            DebugLogger.LogVariable("ContentBorder.ActualWidth", ContentBorder.ActualWidth);
            DebugLogger.LogVariable("ContentBorder.DesiredSize.Height", ContentBorder.DesiredSize.Height);
            DebugLogger.LogVariable("RootBorder.BorderThickness (T+B)", RootBorder.BorderThickness.Top + RootBorder.BorderThickness.Bottom);
            DebugLogger.LogVariable("clipCompensation", clipCompensation);
            DebugLogger.LogVariable("collapsedHeight", collapsedHeight);
        }
        else
        {
            // Fallback: layout hasn't run yet, use the design-time constant.
            collapsedHeight = OverlayCollapsedBaseHeight;
            if (TagCarouselContainer.Visibility == Visibility.Visible)
                collapsedHeight += 36;
            if (HistoryAndCollapseContainer.Visibility == Visibility.Visible)
                collapsedHeight += 36;

            DebugLogger.LogSeparator("UpdateOverlayHeightForCurrentState (fallback)");
            DebugLogger.LogVariable("_isResultsCollapsed", _isResultsCollapsed);
            DebugLogger.LogVariable("collapsedHeight", collapsedHeight);
        }

        var targetHeight = collapsedHeight;

        if (!animate || !IsLoaded)
        {
            DebugLogger.LogVariable("targetHeight", targetHeight);
            Height = targetHeight;
            ResultsContainer.Opacity = 1;
            return;
        }

        DebugLogger.LogVariable("targetHeight", targetHeight);
        _suspendRootClipUpdates = true;
        var heightAnimation = new DoubleAnimation(Height, targetHeight, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
        };
        heightAnimation.Completed += (_, _) =>
        {
            _suspendRootClipUpdates = false;
            UpdateRootClip(12);
            ResultsContainer.Opacity = 1;
        };

        BeginAnimation(Window.HeightProperty, heightAnimation, HandoffBehavior.SnapshotAndReplace);
    }
}
