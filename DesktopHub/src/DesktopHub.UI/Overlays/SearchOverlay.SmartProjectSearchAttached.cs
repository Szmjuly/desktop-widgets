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
    private const double SmartSearchWindowGap = 8;

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

        var rootBorder = new Border
        {
            Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(0xE8, 0x12, 0x12, 0x12)),
            CornerRadius = new CornerRadius(12),
            BorderBrush = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(0x3A, 0x2A, 0x2A, 0x2A)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(18),
            ClipToBounds = true,
        };

        var glassBorder = new Border
        {
            CornerRadius = new CornerRadius(10),
            BorderThickness = new Thickness(1),
            BorderBrush = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)),
            Background = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromArgb(0x14, 0x00, 0x00, 0x00)),
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
            Foreground = new WpfMedia.SolidColorBrush(WpfMedia.Color.FromRgb(0xF5, 0xF7, 0xFA)),
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
        var targetHeight = _isResultsCollapsed ? OverlayCollapsedBaseHeight : OverlayExpandedBaseHeight;

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
