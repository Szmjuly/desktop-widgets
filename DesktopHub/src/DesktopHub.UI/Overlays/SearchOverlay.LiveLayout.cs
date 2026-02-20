using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class SearchOverlay
{
    private void RegisterWidgetWindow(Window? window)
    {
        if (window == null)
            return;

        window.LocationChanged -= WidgetWindow_LocationChanged;
        window.SizeChanged -= WidgetWindow_SizeChanged;
        window.IsVisibleChanged -= WidgetWindow_IsVisibleChanged;
        window.Closed -= WidgetWindow_Closed;
        window.MouseLeftButtonUp -= WidgetWindow_MouseLeftButtonUp;
        window.LostMouseCapture -= WidgetWindow_LostMouseCapture;

        window.LocationChanged += WidgetWindow_LocationChanged;
        window.SizeChanged += WidgetWindow_SizeChanged;
        window.IsVisibleChanged += WidgetWindow_IsVisibleChanged;
        window.Closed += WidgetWindow_Closed;
        window.MouseLeftButtonUp += WidgetWindow_MouseLeftButtonUp;
        window.LostMouseCapture += WidgetWindow_LostMouseCapture;
    }

    private void UnregisterWidgetWindow(Window? window)
    {
        if (window == null)
            return;

        window.LocationChanged -= WidgetWindow_LocationChanged;
        window.SizeChanged -= WidgetWindow_SizeChanged;
        window.IsVisibleChanged -= WidgetWindow_IsVisibleChanged;
        window.Closed -= WidgetWindow_Closed;
        window.MouseLeftButtonUp -= WidgetWindow_MouseLeftButtonUp;
        window.LostMouseCapture -= WidgetWindow_LostMouseCapture;
    }

    private void WidgetWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (sender is not Window window)
            return;

        if (_isAutoArrangingWidgets || !_settings.GetLivingWidgetsMode())
        {
            TrackVisibleWindowBounds();
            return;
        }

        HandleWindowMoved(window);
    }

    private void WidgetWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Window window)
            return;

        if (_isAutoArrangingWidgets || !_settings.GetLivingWidgetsMode())
        {
            TrackVisibleWindowBounds();
            return;
        }

        HandleWindowResized(window);
    }

    private void WidgetWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not Window window)
            return;

        if (!_settings.GetLivingWidgetsMode())
            return;

        if (window.Visibility == Visibility.Visible)
        {
            ApplyLiveLayoutForWindow(window);
        }
        else
        {
            DetachWindowFromAttachments(window);
        }

        RefreshAttachmentMappings();
        TrackVisibleWindowBounds();
    }

    private void WidgetWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Window window)
            return;

        FinalizeWindowDragLayout(window);
    }

    private void WidgetWindow_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Window window)
            return;

        FinalizeWindowDragLayout(window);
    }

    private void FinalizeWindowDragLayout(Window window)
    {
        if (_isAutoArrangingWidgets || !_settings.GetLivingWidgetsMode())
            return;

        if (window == this)
            return;

        if (window.Visibility != Visibility.Visible || !window.IsLoaded)
            return;

        var gap = GetConfiguredWidgetGap();
        var currentRect = GetWindowRect(window);

        void Finalize()
        {
            UpdateDynamicOverlayMaxHeight(window);
            RecalculateDocOverlayConstraints();
            MoveAttachedFollowers(window);
            RefreshAttachmentMappings();
            TrackVisibleWindowBounds();
        }

        // Check if drop position causes overlap
        if (CountRectOverlaps(currentRect, window) > 0)
        {
            // Invalid drop - animate back to pre-drag position, but only if that position is itself clean
            if (_lastWidgetBounds.TryGetValue(window, out var originalRect) && CountRectOverlaps(originalRect, window) == 0)
            {
                AnimateWindowToPosition(window, originalRect.Left, originalRect.Top, Finalize);
            }
            else
            {
                // Pre-drag position was also overlapping (e.g. search overlay grew after placement),
                // so resolve to a genuinely clear position
                var resolvedRect = ResolveWindowOverlaps(window, currentRect, gap);
                AnimateWindowToPosition(window, resolvedRect.Left, resolvedRect.Top, Finalize);
            }
        }
        else
        {
            // Valid drop - apply live layout (snap to edges, etc.)
            ApplyLiveLayoutForWindow(window);
            Finalize();
        }
    }

    private void AnimateWindowToPosition(Window window, double targetLeft, double targetTop, Action? onComplete = null)
    {
        const int animationDurationMs = 200;
        const int animationSteps = 12;
        var startLeft = window.Left;
        var startTop = window.Top;
        var currentStep = 0;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds((double)animationDurationMs / animationSteps)
        };

        timer.Tick += (s, e) =>
        {
            currentStep++;
            var isDone = currentStep >= animationSteps;

            // Ease-out: t goes 0→1, eased position avoids abrupt stop
            var t = isDone ? 1.0 : 1.0 - Math.Pow(1.0 - (double)currentStep / animationSteps, 2);
            var newLeft = startLeft + (targetLeft - startLeft) * t;
            var newTop = startTop + (targetTop - startTop) * t;

            var previousAutoArrange = _isAutoArrangingWidgets;
            _isAutoArrangingWidgets = true;
            try
            {
                window.Left = newLeft;
                window.Top = newTop;
            }
            finally
            {
                _isAutoArrangingWidgets = previousAutoArrange;
            }

            if (isDone)
            {
                timer.Stop();
                onComplete?.Invoke();
            }
        };

        timer.Start();
    }

    private void WidgetWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is not Window window)
            return;

        UnregisterWidgetWindow(window);
        DetachWindowFromAttachments(window);
        _lastWidgetBounds.Remove(window);
        RefreshAttachmentMappings();
        TrackVisibleWindowBounds();
    }

    private IEnumerable<Window> GetManagedWidgetWindows(bool includeHidden = false)
    {
        var windows = new Window?[]
        {
            this,
            _widgetLauncher,
            _timerOverlay,
            _quickTasksOverlay,
            _docOverlay,
            _frequentProjectsOverlay,
            _quickLaunchOverlay,
            _smartProjectSearchOverlay
        };

        var seen = new HashSet<Window>();
        foreach (var window in windows)
        {
            if (window == null)
                continue;

            if (!seen.Add(window))
                continue;

            if (!window.IsLoaded)
                continue;

            if (!includeHidden && window.Visibility != Visibility.Visible)
                continue;

            yield return window;
        }
    }

    private static bool IsWindowBeingDragged(Window window)
    {
        return window.IsMouseCaptured && System.Windows.Input.Mouse.LeftButton == MouseButtonState.Pressed;
    }

    private static Rect GetWindowRect(Window window)
    {
        var width = window.ActualWidth;
        if (width <= 1 || double.IsNaN(width))
            width = window.Width;
        if (width <= 1 || double.IsNaN(width))
            width = window.RenderSize.Width;

        var height = window.ActualHeight;
        if (height <= 1 || double.IsNaN(height))
            height = window.Height;
        if (height <= 1 || double.IsNaN(height))
            height = window.RenderSize.Height;

        return new Rect(window.Left, window.Top, Math.Max(1, width), Math.Max(1, height));
    }

    private Rect GetScreenWorkArea(Rect rect)
    {
        var center = new System.Drawing.Point(
            (int)Math.Round(rect.Left + rect.Width / 2.0),
            (int)Math.Round(rect.Top + rect.Height / 2.0)
        );
        var screen = System.Windows.Forms.Screen.FromPoint(center);
        return new Rect(screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, screen.WorkingArea.Height);
    }

    private double GetConfiguredWidgetGap()
    {
        return Math.Max(4, _settings.GetWidgetSnapGap());
    }

    private double GetResponsiveColumnWidgetWidth()
    {
        var referenceRect = GetWindowRect(this);
        var workArea = GetScreenWorkArea(referenceRect);
        var targetWidth = workArea.Width * 0.22;
        return Math.Round(Math.Clamp(targetWidth, 400.0, 460.0));
    }

    private void ApplyResponsiveWidgetWidth(Window window)
    {
        if (window is QuickTasksOverlay or DocQuickOpenOverlay or FrequentProjectsOverlay or QuickLaunchOverlay or SmartProjectSearchOverlay)
        {
            window.Width = GetResponsiveColumnWidgetWidth();
        }
    }

    private static double HorizontalOverlap(Rect a, Rect b)
    {
        return Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));
    }

    private static double VerticalOverlap(Rect a, Rect b)
    {
        return Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));
    }

    private static bool RectsOverlap(Rect a, Rect b)
    {
        return HorizontalOverlap(a, b) > 0.5 && VerticalOverlap(a, b) > 0.5;
    }

    private void MoveWindowTo(Window window, double left, double top)
    {
        if (Math.Abs(window.Left - left) < 0.5 && Math.Abs(window.Top - top) < 0.5)
            return;

        var previousAutoArrange = _isAutoArrangingWidgets;
        _isAutoArrangingWidgets = true;
        try
        {
            window.Left = left;
            window.Top = top;
        }
        finally
        {
            _isAutoArrangingWidgets = previousAutoArrange;
        }
    }

    private Rect ClampRectToScreen(Rect rect, double gap)
    {
        var workArea = GetScreenWorkArea(rect);

        var minLeft = workArea.Left + gap;
        var maxLeft = workArea.Right - gap - rect.Width;
        if (maxLeft < minLeft)
        {
            minLeft = workArea.Left;
            maxLeft = workArea.Right - rect.Width;
        }

        var minTop = workArea.Top + gap;
        var maxTop = workArea.Bottom - gap - rect.Height;
        if (maxTop < minTop)
        {
            minTop = workArea.Top;
            maxTop = workArea.Bottom - rect.Height;
        }

        var clampedLeft = Math.Max(minLeft, Math.Min(rect.Left, maxLeft));
        var clampedTop = Math.Max(minTop, Math.Min(rect.Top, maxTop));
        return new Rect(clampedLeft, clampedTop, rect.Width, rect.Height);
    }

    private Rect SnapRectToScreenEdges(Rect rect, double gap)
    {
        var workArea = GetScreenWorkArea(rect);

        var leftSnap = workArea.Left + gap;
        var rightSnap = workArea.Right - gap - rect.Width;
        var topSnap = workArea.Top + gap;
        var bottomSnap = workArea.Bottom - gap - rect.Height;

        if (Math.Abs(rect.Left - leftSnap) <= WidgetSnapThreshold)
            rect.X = leftSnap;
        else if (Math.Abs(rect.Left - rightSnap) <= WidgetSnapThreshold)
            rect.X = rightSnap;

        if (Math.Abs(rect.Top - topSnap) <= WidgetSnapThreshold)
            rect.Y = topSnap;
        else if (Math.Abs(rect.Top - bottomSnap) <= WidgetSnapThreshold)
            rect.Y = bottomSnap;

        return ClampRectToScreen(rect, gap);
    }

    private Rect SnapRectToOtherWindows(Window movingWindow, Rect rect, double gap)
    {
        var bestXDistance = WidgetSnapThreshold + 1;
        var bestYDistance = WidgetSnapThreshold + 1;
        double? snappedX = null;
        double? snappedY = null;

        void ConsiderX(double candidate)
        {
            var distance = Math.Abs(rect.Left - candidate);
            if (distance <= WidgetSnapThreshold && distance < bestXDistance)
            {
                bestXDistance = distance;
                snappedX = candidate;
            }
        }

        void ConsiderY(double candidate)
        {
            var distance = Math.Abs(rect.Top - candidate);
            if (distance <= WidgetSnapThreshold && distance < bestYDistance)
            {
                bestYDistance = distance;
                snappedY = candidate;
            }
        }

        foreach (var otherWindow in GetManagedWidgetWindows())
        {
            if (otherWindow == movingWindow)
                continue;

            var otherRect = GetWindowRect(otherWindow);
            var hasVerticalOverlap = VerticalOverlap(rect, otherRect) > 24;
            var hasHorizontalOverlap = HorizontalOverlap(rect, otherRect) > 24;

            if (hasVerticalOverlap)
            {
                ConsiderX(otherRect.Left);
                ConsiderX(otherRect.Right - rect.Width);
                ConsiderX(otherRect.Right + gap);
                ConsiderX(otherRect.Left - rect.Width - gap);
            }

            if (hasHorizontalOverlap)
            {
                ConsiderY(otherRect.Top);
                ConsiderY(otherRect.Bottom - rect.Height);
                ConsiderY(otherRect.Bottom + gap);
                ConsiderY(otherRect.Top - rect.Height - gap);
            }
        }

        if (snappedX.HasValue)
            rect.X = snappedX.Value;
        if (snappedY.HasValue)
            rect.Y = snappedY.Value;

        return rect;
    }

    private int CountRectOverlaps(Rect rect, Window movingWindow)
    {
        var overlapCount = 0;
        foreach (var otherWindow in GetManagedWidgetWindows())
        {
            if (otherWindow == movingWindow)
                continue;

            if (RectsOverlap(rect, GetWindowRect(otherWindow)))
                overlapCount++;
        }

        return overlapCount;
    }

    private Rect ResolveWindowOverlaps(Window movingWindow, Rect rect, double gap)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            Rect? overlap = null;
            foreach (var otherWindow in GetManagedWidgetWindows())
            {
                if (otherWindow == movingWindow)
                    continue;

                var otherRect = GetWindowRect(otherWindow);
                if (RectsOverlap(rect, otherRect))
                {
                    overlap = otherRect;
                    break;
                }
            }

            if (!overlap.HasValue)
                break;

            var overlapRect = overlap.Value;
            var candidates = new List<Rect>
            {
                new Rect(overlapRect.Left - rect.Width - gap, rect.Top, rect.Width, rect.Height),
                new Rect(overlapRect.Right + gap, rect.Top, rect.Width, rect.Height),
                new Rect(rect.Left, overlapRect.Top - rect.Height - gap, rect.Width, rect.Height),
                new Rect(rect.Left, overlapRect.Bottom + gap, rect.Width, rect.Height)
            };

            var bestCandidate = rect;
            var bestOverlapCount = int.MaxValue;
            var bestDistance = double.MaxValue;

            foreach (var candidate in candidates)
            {
                var clamped = ClampRectToScreen(candidate, gap);
                var overlapCount = CountRectOverlaps(clamped, movingWindow);
                var distance = Math.Abs(clamped.Left - rect.Left) + Math.Abs(clamped.Top - rect.Top);

                if (overlapCount < bestOverlapCount || (overlapCount == bestOverlapCount && distance < bestDistance))
                {
                    bestCandidate = clamped;
                    bestOverlapCount = overlapCount;
                    bestDistance = distance;
                }
            }

            if (Math.Abs(bestCandidate.Left - rect.Left) < 0.5 && Math.Abs(bestCandidate.Top - rect.Top) < 0.5)
                break;

            rect = bestCandidate;
        }

        return ClampRectToScreen(rect, gap);
    }

    private void ApplyLiveLayoutForWindow(Window window)
    {
        if (!_settings.GetLivingWidgetsMode())
            return;

        if (window.Visibility != Visibility.Visible || !window.IsLoaded)
            return;

        var gap = GetConfiguredWidgetGap();
        var rect = GetWindowRect(window);
        rect = SnapRectToScreenEdges(rect, gap);
        rect = SnapRectToOtherWindows(window, rect, gap);
        rect = ResolveWindowOverlaps(window, rect, gap);
        MoveWindowTo(window, rect.Left, rect.Top);
        UpdateDynamicOverlayMaxHeight(window);
    }

    private void RecalculateDocOverlayConstraints()
    {
        if (_docOverlay != null && _docOverlay.IsLoaded && _docOverlay.Visibility == Visibility.Visible)
            UpdateDynamicOverlayMaxHeight(_docOverlay);

        if (this.IsLoaded && this.Visibility == Visibility.Visible)
            UpdateDynamicOverlayMaxHeight(this);
    }

    private void UpdateDynamicOverlayMaxHeight(Window window)
    {
        if (window is not DocQuickOpenOverlay && window != this)
            return;

        var rect = GetWindowRect(window);
        var workArea = GetScreenWorkArea(rect);
        var gap = GetConfiguredWidgetGap();

        // Default ceiling: screen work area bottom
        var limitY = workArea.Bottom - gap;

        // For any widget directly below us (significant horizontal overlap), calculate how far
        // Doc can grow while keeping that widget on-screen when pushed down.
        // Formula: if Doc.Bottom = limitY, the widget below lands at limitY+gap,
        // its bottom = limitY + gap + widget.Height <= screenBottom - gap
        // => limitY <= screenBottom - widget.Height - 2*gap
        foreach (var other in GetManagedWidgetWindows())
        {
            if (other == window)
                continue;

            var otherRect = GetWindowRect(other);

            // Only widgets whose top is below our top position
            if (otherRect.Top <= window.Top)
                continue;

            // Must have at least 30% horizontal overlap to be considered "below" us
            var overlapAmount = HorizontalOverlap(rect, otherRect);
            var requiredOverlap = Math.Min(rect.Width, otherRect.Width) * 0.3;
            if (overlapAmount < requiredOverlap)
                continue;

            // Maximum bottom Doc can reach while the widget below still fits on screen after being pushed
            var pushLimit = workArea.Bottom - otherRect.Height - 2 * gap;
            limitY = Math.Min(limitY, pushLimit);
        }

        var minHeight = window is DocQuickOpenOverlay ? 200.0 : 140.0;
        window.MaxHeight = Math.Max(minHeight, limitY - window.Top);
    }

    private void DetachWindowFromAttachments(Window window)
    {
        _verticalAttachments.Remove(window);

        var dependents = _verticalAttachments
            .Where(kvp => kvp.Value == window)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var dependent in dependents)
        {
            _verticalAttachments.Remove(dependent);
        }
    }

    private void AttachNearestWindowBelow(Window anchor, Rect previousAnchorBounds)
    {
        var anchorRect = GetWindowRect(anchor);
        var maxAttachDistance = GetConfiguredWidgetGap() + 48;
        Window? bestFollower = null;
        var bestDistance = double.MaxValue;

        foreach (var candidate in GetManagedWidgetWindows())
        {
            if (candidate == anchor)
                continue;

            var candidateRect = GetWindowRect(candidate);
            if (candidateRect.Top < previousAnchorBounds.Bottom - WidgetSnapThreshold)
                continue;

            var overlapAmount = HorizontalOverlap(anchorRect, candidateRect);
            var requiredOverlap = Math.Min(anchorRect.Width, candidateRect.Width) * 0.25;
            if (overlapAmount < requiredOverlap)
                continue;

            var distance = candidateRect.Top - previousAnchorBounds.Bottom;
            if (distance > maxAttachDistance)
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestFollower = candidate;
            }
        }

        if (bestFollower != null)
        {
            _verticalAttachments[bestFollower] = anchor;
        }
    }

    private void AttachImpactedWindowsBelow(Window anchor, Rect previousAnchorBounds)
    {
        var anchorRect = GetWindowRect(anchor);
        var bottomDelta = anchorRect.Bottom - previousAnchorBounds.Bottom;
        if (Math.Abs(bottomDelta) <= 0.5)
            return;

        double minCaptureTop;
        double maxCaptureTop;

        if (bottomDelta > 0)
        {
            // Growing: capture widgets that were at/under the old bottom and now intersect the growth zone.
            minCaptureTop = previousAnchorBounds.Bottom - WidgetSnapThreshold;
            maxCaptureTop = anchorRect.Bottom + WidgetSnapThreshold;
        }
        else
        {
            // Shrinking: capture widgets that were likely pushed by us (around old bottom + gap)
            // so they can be pulled back up when the anchor contracts.
            var gap = GetConfiguredWidgetGap();
            minCaptureTop = anchorRect.Bottom - WidgetSnapThreshold;
            maxCaptureTop = previousAnchorBounds.Bottom + gap + WidgetSnapThreshold;
        }

        var attachedAny = false;

        foreach (var candidate in GetManagedWidgetWindows())
        {
            if (candidate == anchor)
                continue;

            var candidateRect = GetWindowRect(candidate);
            var isAlreadyFollower = _verticalAttachments.TryGetValue(candidate, out var existingAnchor) && existingAnchor == anchor;

            if (!isAlreadyFollower)
            {
                if (candidateRect.Top < minCaptureTop || candidateRect.Top > maxCaptureTop)
                    continue;

                var overlapAmount = HorizontalOverlap(anchorRect, candidateRect);
                var requiredOverlap = Math.Min(anchorRect.Width, candidateRect.Width) * 0.25;
                if (overlapAmount < requiredOverlap)
                    continue;
            }

            _verticalAttachments[candidate] = anchor;
            attachedAny = true;
        }

        if (!attachedAny)
        {
            AttachNearestWindowBelow(anchor, previousAnchorBounds);
        }
    }

    private void MoveAttachedFollowers(Window anchor)
    {
        MoveAttachedFollowers(anchor, new HashSet<Window>());
    }

    private void MoveAttachedFollowers(Window anchor, HashSet<Window> visited)
    {
        if (!visited.Add(anchor))
            return;

        var anchorRect = GetWindowRect(anchor);
        var gap = GetConfiguredWidgetGap();
        var isSearchAnchor = anchor == this;

        var followers = _verticalAttachments
            .Where(kvp => kvp.Value == anchor)
            .Select(kvp => kvp.Key)
            .Where(w => w.Visibility == Visibility.Visible && w.IsLoaded)
            .OrderBy(w => GetWindowRect(w).Top)
            .ToList();

        foreach (var follower in followers)
        {
            var followerRect = GetWindowRect(follower);
            var desiredTop = anchorRect.Bottom + gap;
            MoveWindowTo(follower, followerRect.Left, desiredTop);

            // Keep followers directly anchored to Search bottom during Search resize,
            // matching Doc-style push/pull behavior instead of lateral/overlap nudges.
            if (!isSearchAnchor)
            {
                ApplyLiveLayoutForWindow(follower);
            }

            MoveAttachedFollowers(follower, visited);
        }
    }

    private void NormalizeDocStartupGapIfNeeded()
    {
        if (!_settings.GetLivingWidgetsMode())
            return;

        if (_docOverlay == null || !_docOverlay.IsLoaded || _docOverlay.Visibility != Visibility.Visible)
            return;

        // If no project is loaded, Doc Quick Open is in its compact state.
        // Pull the nearest widget below back up so we don't preserve a stale "pushed-down" gap from a previous session.
        if (_docService?.ProjectInfo != null)
            return;

        var docRect = GetWindowRect(_docOverlay);
        var gap = GetConfiguredWidgetGap();
        Window? bestFollower = null;
        var bestDistance = double.MaxValue;

        foreach (var candidate in GetManagedWidgetWindows())
        {
            if (candidate == _docOverlay)
                continue;

            var candidateRect = GetWindowRect(candidate);
            if (candidateRect.Top <= docRect.Bottom + WidgetSnapThreshold)
                continue;

            var overlapAmount = HorizontalOverlap(docRect, candidateRect);
            var requiredOverlap = Math.Min(docRect.Width, candidateRect.Width) * 0.25;
            if (overlapAmount < requiredOverlap)
                continue;

            var distance = candidateRect.Top - docRect.Bottom;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestFollower = candidate;
            }
        }

        if (bestFollower == null)
            return;

        var followerRect = GetWindowRect(bestFollower);
        var desiredTop = docRect.Bottom + gap;

        if (Math.Abs(followerRect.Top - desiredTop) <= 0.5)
            return;

        MoveWindowTo(bestFollower, followerRect.Left, desiredTop);
        RefreshAttachmentMappings();
        MoveAttachedFollowers(_docOverlay);

        DebugLogger.Log($"NormalizeDocStartupGapIfNeeded: Pulled {bestFollower.GetType().Name} from y={followerRect.Top:F1} to y={desiredTop:F1} (Doc had no project loaded)");
    }

    private void RefreshAttachmentMappings()
    {
        if (!_settings.GetLivingWidgetsMode())
        {
            _verticalAttachments.Clear();
            return;
        }

        var windows = GetManagedWidgetWindows().ToList();
        _verticalAttachments.Clear();

        var targetGap = GetConfiguredWidgetGap();
        foreach (var follower in windows)
        {
            var followerRect = GetWindowRect(follower);
            Window? bestAnchor = null;
            var bestScore = double.MaxValue;

            foreach (var anchor in windows)
            {
                if (anchor == follower)
                    continue;

                var anchorRect = GetWindowRect(anchor);
                var verticalGap = followerRect.Top - anchorRect.Bottom;
                if (verticalGap < -WidgetSnapThreshold || verticalGap > targetGap + (WidgetSnapThreshold * 2))
                    continue;

                var overlapAmount = HorizontalOverlap(anchorRect, followerRect);
                var requiredOverlap = Math.Min(anchorRect.Width, followerRect.Width) * 0.25;
                if (overlapAmount < requiredOverlap)
                    continue;

                var score = Math.Abs(verticalGap - targetGap);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestAnchor = anchor;
                }
            }

            if (bestAnchor != null)
            {
                _verticalAttachments[follower] = bestAnchor;
            }
        }
    }

    private void TrackVisibleWindowBounds()
    {
        var visibleWindows = GetManagedWidgetWindows().ToHashSet();

        foreach (var window in visibleWindows)
        {
            _lastWidgetBounds[window] = GetWindowRect(window);
        }

        var staleWindows = _lastWidgetBounds.Keys
            .Where(w => !visibleWindows.Contains(w))
            .ToList();

        foreach (var staleWindow in staleWindows)
        {
            _lastWidgetBounds.Remove(staleWindow);
        }
    }

    private void HandleWindowMoved(Window window)
    {
        if (window.Visibility != Visibility.Visible || !window.IsLoaded)
        {
            TrackVisibleWindowBounds();
            return;
        }

        if (_verticalAttachments.ContainsKey(window))
        {
            _verticalAttachments.Remove(window);
        }

        var gap = GetConfiguredWidgetGap();
        var currentRect = GetWindowRect(window);

        if (IsWindowBeingDragged(window))
        {
            // During active drag, only clamp to screen edges - don't prevent overlap
            // This eliminates jitter by allowing free movement during drag
            var clampedRect = ClampRectToScreen(currentRect, gap);
            if (Math.Abs(clampedRect.Left - currentRect.Left) > 0.5 || Math.Abs(clampedRect.Top - currentRect.Top) > 0.5)
            {
                MoveWindowTo(window, clampedRect.Left, clampedRect.Top);
                currentRect = clampedRect;
            }

            // Don't update _lastWidgetBounds during drag - preserve pre-drag position for snap-back
            return;
        }

        ApplyLiveLayoutForWindow(window);
        RecalculateDocOverlayConstraints();
        MoveAttachedFollowers(window);
        RefreshAttachmentMappings();
        TrackVisibleWindowBounds();
    }

    private void HandleWindowResized(Window window)
    {
        if (window.Visibility != Visibility.Visible || !window.IsLoaded)
        {
            TrackVisibleWindowBounds();
            return;
        }

        if (_lastWidgetBounds.TryGetValue(window, out var previousBounds))
        {
            var currentBounds = GetWindowRect(window);
            var grewDownward = currentBounds.Height > previousBounds.Height + 0.5;
            var shrankDownward = currentBounds.Height < previousBounds.Height - 0.5;

            if (grewDownward || shrankDownward)
            {
                AttachImpactedWindowsBelow(window, previousBounds);
                MoveAttachedFollowers(window);

                if (window == this)
                {
                    var followerCount = _verticalAttachments.Count(kvp => kvp.Value == window);
                    DebugLogger.Log($"SearchResize: bottomDelta={(currentBounds.Bottom - previousBounds.Bottom):F1}, followers={followerCount}");
                }
            }
        }

        if (window == this)
        {
            // Keep Search Overlay anchored: constrain its max height like Doc Quick Open,
            // but don't run overlap-resolution nudges that can shift it left/right.
            UpdateDynamicOverlayMaxHeight(window);
            var gap = GetConfiguredWidgetGap();
            var rect = GetWindowRect(window);
            rect = SnapRectToScreenEdges(rect, gap);
            MoveWindowTo(window, rect.Left, rect.Top);
            TrackVisibleWindowBounds();
            return;
        }

        ApplyLiveLayoutForWindow(window);
        RefreshAttachmentMappings();
        TrackVisibleWindowBounds();
    }
}
