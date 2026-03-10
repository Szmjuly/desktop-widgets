using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfSize = System.Windows.Size;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Manages automatic grid-based widget placement for non-Living-Widgets mode.
/// Widgets are arranged in columns (top-to-bottom, left-to-right) starting from
/// the right edge of the SearchOverlay anchor. Overflows to secondary monitors
/// when the primary screen is full. DPI-aware via <see cref="ScreenHelper"/>.
/// </summary>
internal sealed class WidgetLayoutManager
{
    /// <summary>
    /// Result of a layout placement request.
    /// </summary>
    internal sealed class PlacementResult
    {
        public bool Success { get; init; }
        public double Left { get; init; }
        public double Top { get; init; }

        /// <summary>Human-readable reason when Success is false.</summary>
        public string? FailureReason { get; init; }

        public static PlacementResult Fail(string reason) => new() { Success = false, FailureReason = reason };
        public static PlacementResult Ok(double left, double top) => new() { Success = true, Left = left, Top = top };
    }

    /// <summary>
    /// Describes a widget that is already placed on screen (used as input to the layout algorithm).
    /// </summary>
    internal sealed class OccupiedSlot
    {
        public required Rect Bounds { get; init; }
        public required Window Window { get; init; }
    }

    private readonly double _gap;
    private readonly double _columnWidth;
    private readonly Visual? _referenceVisual;

    public WidgetLayoutManager(double gap, double columnWidth, Visual? referenceVisual)
    {
        _gap = Math.Max(4, gap);
        _columnWidth = Math.Max(100, columnWidth);
        _referenceVisual = referenceVisual;
    }

    /// <summary>
    /// Computes the position for a new widget given the anchor (SearchOverlay) rect
    /// and the set of already-placed widgets.
    /// </summary>
    /// <param name="anchorRect">Rect of the SearchOverlay in DIPs.</param>
    /// <param name="widgetSize">Desired size (width, height) of the widget to place.</param>
    /// <param name="occupiedSlots">Rects of all currently visible widgets (including anchor).</param>
    /// <param name="pinnedPosition">If the user has pinned a position, try it first.</param>
    public PlacementResult ComputePlacement(
        Rect anchorRect,
        WpfSize widgetSize,
        IReadOnlyList<OccupiedSlot> occupiedSlots,
        (double left, double top)? pinnedPosition = null)
    {
        var allScreens = ScreenHelper.GetAllScreensInDips(_referenceVisual);
        if (allScreens.Count == 0)
            return PlacementResult.Fail("No screens detected.");

        // If the user has a pinned position, validate it first
        if (pinnedPosition.HasValue)
        {
            var pinnedRect = new Rect(pinnedPosition.Value.left, pinnedPosition.Value.top,
                widgetSize.Width, widgetSize.Height);

            if (IsPositionValid(pinnedRect, occupiedSlots, allScreens))
                return PlacementResult.Ok(pinnedRect.Left, pinnedRect.Top);

            // Pinned position is invalid (off-screen or overlapping) — try clamped version
            var clampedPinned = ClampToNearestScreen(pinnedRect, allScreens);
            if (clampedPinned.HasValue && IsPositionValid(clampedPinned.Value, occupiedSlots, allScreens))
                return PlacementResult.Ok(clampedPinned.Value.Left, clampedPinned.Value.Top);

            // Pinned position is not salvageable — fall through to auto-grid
        }

        // Find which screen contains the anchor
        var anchorScreen = FindContainingScreen(anchorRect, allScreens) ?? allScreens[0];

        // Try placing on the anchor's screen first, then overflow to others
        var orderedScreens = new List<Rect> { anchorScreen };
        orderedScreens.AddRange(allScreens.Where(s => s != anchorScreen));

        foreach (var screen in orderedScreens)
        {
            var result = TryPlaceOnScreen(screen, anchorRect, widgetSize, occupiedSlots, screen == anchorScreen);
            if (result != null)
                return result;
        }

        return PlacementResult.Fail("Not enough screen space. Close a widget or enable Living Widgets Mode for free positioning.");
    }

    /// <summary>
    /// Recomputes positions for all provided widgets in grid order.
    /// Returns a list of (Window, newLeft, newTop) tuples.
    /// Widgets that don't fit are returned with Success=false.
    /// </summary>
    public List<(Window window, PlacementResult result)> ArrangeAll(
        Rect anchorRect,
        IReadOnlyList<(Window window, WpfSize size, (double left, double top)? pinnedPosition)> widgets)
    {
        var results = new List<(Window, PlacementResult)>();
        var occupiedSlots = new List<OccupiedSlot>
        {
            new() { Bounds = anchorRect, Window = null! }
        };

        foreach (var (window, size, pinned) in widgets)
        {
            var placement = ComputePlacement(anchorRect, size, occupiedSlots, pinned);
            results.Add((window, placement));

            if (placement.Success)
            {
                occupiedSlots.Add(new OccupiedSlot
                {
                    Bounds = new Rect(placement.Left, placement.Top, size.Width, size.Height),
                    Window = window
                });
            }
        }

        return results;
    }

    private PlacementResult? TryPlaceOnScreen(
        Rect screen,
        Rect anchorRect,
        WpfSize widgetSize,
        IReadOnlyList<OccupiedSlot> occupiedSlots,
        bool isAnchorScreen)
    {
        // Determine column start X:
        // On the anchor screen, columns start to the right of the anchor.
        // On overflow screens, columns start from the left edge.
        double columnStartX;
        if (isAnchorScreen)
        {
            columnStartX = anchorRect.Right + _gap;
        }
        else
        {
            columnStartX = screen.Left + _gap;
        }

        // Try columns left-to-right across the screen
        for (var colX = columnStartX; colX + widgetSize.Width + _gap <= screen.Right; colX += _columnWidth + _gap)
        {
            // Try rows top-to-bottom within this column
            for (var rowY = screen.Top + _gap; rowY + widgetSize.Height + _gap <= screen.Bottom; rowY += 1)
            {
                var candidateRect = new Rect(colX, rowY, widgetSize.Width, widgetSize.Height);

                if (!OverlapsAnyOccupied(candidateRect, occupiedSlots))
                {
                    return PlacementResult.Ok(candidateRect.Left, candidateRect.Top);
                }

                // Skip past the bottom of the overlapping slot to avoid pixel-by-pixel scanning
                var blockingBottom = GetBlockingBottom(candidateRect, occupiedSlots);
                if (blockingBottom.HasValue)
                {
                    rowY = blockingBottom.Value + _gap - 1; // -1 because loop will +1
                }
            }
        }

        // Also try columns to the LEFT of the anchor on the anchor screen
        if (isAnchorScreen)
        {
            for (var colX = anchorRect.Left - _gap - widgetSize.Width;
                 colX >= screen.Left + _gap;
                 colX -= _columnWidth + _gap)
            {
                for (var rowY = screen.Top + _gap; rowY + widgetSize.Height + _gap <= screen.Bottom; rowY += 1)
                {
                    var candidateRect = new Rect(colX, rowY, widgetSize.Width, widgetSize.Height);

                    if (!OverlapsAnyOccupied(candidateRect, occupiedSlots))
                    {
                        return PlacementResult.Ok(candidateRect.Left, candidateRect.Top);
                    }

                    var blockingBottom = GetBlockingBottom(candidateRect, occupiedSlots);
                    if (blockingBottom.HasValue)
                    {
                        rowY = blockingBottom.Value + _gap - 1;
                    }
                }
            }
        }

        return null; // No room on this screen
    }

    private bool IsPositionValid(Rect rect, IReadOnlyList<OccupiedSlot> occupiedSlots, List<Rect> screens)
    {
        // Must be fully within at least one screen
        var containingScreen = FindContainingScreen(rect, screens);
        if (containingScreen == null)
            return false;

        var screen = containingScreen.Value;
        if (rect.Left < screen.Left || rect.Right > screen.Right ||
            rect.Top < screen.Top || rect.Bottom > screen.Bottom)
            return false;

        return !OverlapsAnyOccupied(rect, occupiedSlots);
    }

    private static bool OverlapsAnyOccupied(Rect candidate, IReadOnlyList<OccupiedSlot> slots)
    {
        foreach (var slot in slots)
        {
            if (RectsOverlap(candidate, slot.Bounds))
                return true;
        }
        return false;
    }

    private static double? GetBlockingBottom(Rect candidate, IReadOnlyList<OccupiedSlot> slots)
    {
        double? maxBottom = null;
        foreach (var slot in slots)
        {
            if (RectsOverlap(candidate, slot.Bounds))
            {
                var bottom = slot.Bounds.Bottom;
                if (!maxBottom.HasValue || bottom > maxBottom.Value)
                    maxBottom = bottom;
            }
        }
        return maxBottom;
    }

    private static bool RectsOverlap(Rect a, Rect b)
    {
        return a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;
    }

    private static Rect? FindContainingScreen(Rect rect, List<Rect> screens)
    {
        var centerX = rect.Left + rect.Width / 2.0;
        var centerY = rect.Top + rect.Height / 2.0;

        foreach (var screen in screens)
        {
            if (centerX >= screen.Left && centerX <= screen.Right &&
                centerY >= screen.Top && centerY <= screen.Bottom)
            {
                return screen;
            }
        }
        return null;
    }

    private static Rect? ClampToNearestScreen(Rect rect, List<Rect> screens)
    {
        Rect? bestScreen = null;
        var bestDistance = double.MaxValue;

        foreach (var screen in screens)
        {
            var cx = Math.Max(screen.Left, Math.Min(rect.Left + rect.Width / 2, screen.Right));
            var cy = Math.Max(screen.Top, Math.Min(rect.Top + rect.Height / 2, screen.Bottom));
            var dx = (rect.Left + rect.Width / 2) - cx;
            var dy = (rect.Top + rect.Height / 2) - cy;
            var distance = dx * dx + dy * dy;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestScreen = screen;
            }
        }

        if (bestScreen == null)
            return null;

        var s = bestScreen.Value;
        var clampedLeft = Math.Max(s.Left, Math.Min(rect.Left, s.Right - rect.Width));
        var clampedTop = Math.Max(s.Top, Math.Min(rect.Top, s.Bottom - rect.Height));
        return new Rect(clampedLeft, clampedTop, rect.Width, rect.Height);
    }
}
