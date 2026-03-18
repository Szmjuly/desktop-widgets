using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DesktopHub.UI;

/// <summary>
/// Chip-strip navigation: edge fade indicators, clickable left/right controls,
/// and hover-only mouse-wheel horizontal scrolling for the tag carousel and
/// search history chip rows.
/// </summary>
public partial class SearchOverlay
{
    private const double ChipScrollStep = 100.0;

    // ──────────────────────────────────────────────────────────────
    // Tag Carousel
    // ──────────────────────────────────────────────────────────────

    private void UpdateTagCarouselScrollIndicators()
    {
        if (TagCarouselScrollViewer == null) return;

        bool canScrollLeft  = TagCarouselScrollViewer.HorizontalOffset > 0;
        bool canScrollRight = TagCarouselScrollViewer.HorizontalOffset
                              < TagCarouselScrollViewer.ScrollableWidth - 0.5;

        TagCarouselLeftBtn.Visibility   = canScrollLeft  ? Visibility.Visible : Visibility.Collapsed;
        TagCarouselRightBtn.Visibility  = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
        TagCarouselLeftFade.Visibility  = canScrollLeft  ? Visibility.Visible : Visibility.Collapsed;
        TagCarouselRightFade.Visibility = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
    }

    private void TagCarouselScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        => UpdateTagCarouselScrollIndicators();

    private void TagCarouselScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        TagCarouselScrollViewer.ScrollToHorizontalOffset(
            TagCarouselScrollViewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void TagCarouselScrollLeft_Click(object sender, MouseButtonEventArgs e)
    {
        TagCarouselScrollViewer.ScrollToHorizontalOffset(
            Math.Max(0, TagCarouselScrollViewer.HorizontalOffset - ChipScrollStep));
    }

    private void TagCarouselScrollRight_Click(object sender, MouseButtonEventArgs e)
    {
        TagCarouselScrollViewer.ScrollToHorizontalOffset(
            Math.Min(TagCarouselScrollViewer.ScrollableWidth,
                     TagCarouselScrollViewer.HorizontalOffset + ChipScrollStep));
    }

    // ──────────────────────────────────────────────────────────────
    // History Row
    // ──────────────────────────────────────────────────────────────

    private void UpdateHistoryScrollIndicators()
    {
        if (HistoryScrollViewer == null) return;

        bool canScrollLeft  = HistoryScrollViewer.HorizontalOffset > 0;
        bool canScrollRight = HistoryScrollViewer.HorizontalOffset
                              < HistoryScrollViewer.ScrollableWidth - 0.5;

        HistoryLeftBtn.Visibility   = canScrollLeft  ? Visibility.Visible : Visibility.Collapsed;
        HistoryRightBtn.Visibility  = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
        HistoryLeftFade.Visibility  = canScrollLeft  ? Visibility.Visible : Visibility.Collapsed;
        HistoryRightFade.Visibility = canScrollRight ? Visibility.Visible : Visibility.Collapsed;
    }

    private void HistoryScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        => UpdateHistoryScrollIndicators();

    private void HistoryScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        HistoryScrollViewer.ScrollToHorizontalOffset(
            HistoryScrollViewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private void HistoryScrollLeft_Click(object sender, MouseButtonEventArgs e)
    {
        HistoryScrollViewer.ScrollToHorizontalOffset(
            Math.Max(0, HistoryScrollViewer.HorizontalOffset - ChipScrollStep));
    }

    private void HistoryScrollRight_Click(object sender, MouseButtonEventArgs e)
    {
        HistoryScrollViewer.ScrollToHorizontalOffset(
            Math.Min(HistoryScrollViewer.ScrollableWidth,
                     HistoryScrollViewer.HorizontalOffset + ChipScrollStep));
    }
}
