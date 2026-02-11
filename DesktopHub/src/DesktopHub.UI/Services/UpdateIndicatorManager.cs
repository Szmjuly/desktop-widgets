using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace DesktopHub.UI.Services;

public class UpdateIndicatorManager
{
    private readonly List<WidgetEntry> _widgets = new();
    private bool _isUpdateAvailable;
    private int _nextAutoPriority = 5;

    public bool IsUpdateAvailable => _isUpdateAvailable;

    public void RegisterWidget(string name, int priority, Window window, Action<bool> setIndicatorVisible)
    {
        // Remove existing registration for same name
        _widgets.RemoveAll(w => w.Name == name);

        _widgets.Add(new WidgetEntry
        {
            Name = name,
            Priority = priority,
            Window = window,
            SetIndicatorVisible = setIndicatorVisible
        });

        _widgets.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        DebugLogger.Log($"UpdateIndicatorManager: Registered widget '{name}' with priority {priority} (total: {_widgets.Count})");

        // Re-evaluate which widget should show the indicator
        if (_isUpdateAvailable)
        {
            Refresh();
        }
    }

    public int GetNextAutoPriority()
    {
        return _nextAutoPriority++;
    }

    public void SetUpdateAvailable(bool available)
    {
        _isUpdateAvailable = available;
        Refresh();
        DebugLogger.Log($"UpdateIndicatorManager: Update available = {available}");
    }

    public void Refresh()
    {
        if (!_isUpdateAvailable)
        {
            // Hide all indicators
            foreach (var widget in _widgets)
            {
                try { widget.SetIndicatorVisible(false); }
                catch { }
            }
            return;
        }

        // Find the highest-priority visible widget
        WidgetEntry? target = null;
        foreach (var widget in _widgets)
        {
            try
            {
                if (widget.Window.IsVisible && widget.Window.Visibility == Visibility.Visible)
                {
                    target = widget;
                    break; // First visible widget in priority order
                }
            }
            catch { }
        }

        // Show indicator only on the target, hide on all others
        foreach (var widget in _widgets)
        {
            try
            {
                var shouldShow = widget == target;
                widget.SetIndicatorVisible(shouldShow);
            }
            catch { }
        }

        if (target != null)
        {
            DebugLogger.Log($"UpdateIndicatorManager: Showing indicator on '{target.Name}' (priority {target.Priority})");
        }
        else
        {
            DebugLogger.Log("UpdateIndicatorManager: No visible widget to show indicator on");
        }
    }

    private class WidgetEntry
    {
        public required string Name { get; init; }
        public required int Priority { get; init; }
        public required Window Window { get; init; }
        public required Action<bool> SetIndicatorVisible { get; init; }
    }
}
