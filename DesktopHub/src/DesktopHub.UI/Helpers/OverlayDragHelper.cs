using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using WpfPoint = System.Windows.Point;

namespace DesktopHub.UI.Helpers;

/// <summary>
/// Provides reusable drag-to-move behavior for overlay windows.
/// Eliminates duplicated EnableDragging/DisableDragging/mouse handlers across all overlays.
/// </summary>
public static class OverlayDragHelper
{
    private static readonly ConditionalWeakTable<Window, DragState> _states = new();

    private sealed class DragState
    {
        public bool IsLivingWidgetsMode;
        public bool IsDragging;
        public WpfPoint DragStartPoint;
    }

    /// <summary>
    /// Interactive element type names that should not trigger drag.
    /// </summary>
    private static readonly HashSet<string> NonDraggableTypes = new()
    {
        "TextBox", "Button", "ListBoxItem", "ComboBox", "ScrollBar", "Thumb"
    };

    public static void EnableDragging(Window window)
    {
        var state = _states.GetOrCreateValue(window);
        state.IsLivingWidgetsMode = true;

        // Remove first to prevent duplicate subscriptions
        window.MouseLeftButtonDown -= OnMouseLeftButtonDown;
        window.MouseLeftButtonUp -= OnMouseLeftButtonUp;
        window.MouseMove -= OnMouseMove;

        window.MouseLeftButtonDown += OnMouseLeftButtonDown;
        window.MouseLeftButtonUp += OnMouseLeftButtonUp;
        window.MouseMove += OnMouseMove;
    }

    public static void DisableDragging(Window window)
    {
        var state = _states.GetOrCreateValue(window);
        state.IsLivingWidgetsMode = false;

        window.MouseLeftButtonDown -= OnMouseLeftButtonDown;
        window.MouseLeftButtonUp -= OnMouseLeftButtonUp;
        window.MouseMove -= OnMouseMove;
    }

    private static void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Window window) return;
        if (!_states.TryGetValue(window, out var state) || !state.IsLivingWidgetsMode) return;

        if (e.OriginalSource is FrameworkElement element)
        {
            var clickedType = element.GetType().Name;
            if (NonDraggableTypes.Contains(clickedType))
                return;

            // Don't intercept resize grip borders (used by MetricsViewerOverlay etc.)
            if (element.Tag is string tag &&
                (tag.Contains("Top") || tag.Contains("Bottom") || tag.Contains("Left") || tag.Contains("Right")))
                return;
        }

        state.IsDragging = true;
        state.DragStartPoint = e.GetPosition(window);
        window.CaptureMouse();
    }

    private static void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Window window) return;
        if (!_states.TryGetValue(window, out var state) || !state.IsDragging) return;

        state.IsDragging = false;
        window.ReleaseMouseCapture();
    }

    private static void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Window window) return;
        if (!_states.TryGetValue(window, out var state) || !state.IsDragging) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var currentPosition = e.GetPosition(window);
        var offset = currentPosition - state.DragStartPoint;
        window.Left += offset.X;
        window.Top += offset.Y;
    }
}
