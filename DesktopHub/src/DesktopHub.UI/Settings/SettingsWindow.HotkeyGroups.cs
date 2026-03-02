using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DesktopHub.Core.Models;

namespace DesktopHub.UI;

// Hotkey groups UI: building, recording, widget pill assignment
public partial class SettingsWindow
{
    // ===== Hotkey Groups =====

    private void LoadHotkeyGroupsUI()
    {
        if (_settings == null) return;
        var groups = _settings.GetHotkeyGroups();
        RebuildHotkeyGroupsPanel(groups);
    }

    private void RebuildHotkeyGroupsPanel(List<HotkeyGroup> groups)
    {
        HotkeyGroupsPanel.Children.Clear();
        for (int i = 0; i < groups.Count; i++)
        {
            var groupRow = BuildHotkeyGroupRow(groups, i);
            HotkeyGroupsPanel.Children.Add(groupRow);
        }
        // Add group button — max 5 groups
        if (groups.Count < 5)
        {
            var addBtn = new System.Windows.Controls.Button
            {
                Content = "+ Add Hotkey Group",
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(14, 7, 14, 7),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                BorderThickness = new Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            };
            addBtn.Click += (s, e) =>
            {
                var newGroups = _settings.GetHotkeyGroups();
                newGroups.Add(new HotkeyGroup { Modifiers = 0, Key = 0, Widgets = new List<string>() });
                _settings.SetHotkeyGroups(newGroups);
                _ = _settings.SaveAsync();
                RebuildHotkeyGroupsPanel(newGroups);
            };
            HotkeyGroupsPanel.Children.Add(addBtn);
        }
    }

    private UIElement BuildHotkeyGroupRow(List<HotkeyGroup> groups, int index)
    {
        var group = groups[index];
        var outerBorder = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 8),
        };

        var stack = new StackPanel();
        outerBorder.Child = stack;

        // Row header: group number + key binding recorder + remove button
        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var groupLabel = new TextBlock
        {
            Text = $"Group {index + 1}",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        Grid.SetColumn(groupLabel, 0);
        headerRow.Children.Add(groupLabel);

        // Key binding box
        var keyText = new TextBlock
        {
            Text = group.Key != 0 ? FormatHotkey(group.Modifiers, group.Key) : "Click to set hotkey",
            FontSize = 12,
            Foreground = group.Key != 0
                ? (System.Windows.Media.Brush)FindResource("TextBrush")
                : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
        };
        var recordingText = new TextBlock
        {
            Text = "Press any key combo...",
            FontSize = 12,
            Foreground = (System.Windows.Media.Brush)FindResource("PrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        var keyBox = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x25, 0x00, 0x00, 0x00)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 5, 10, 5),
            Cursor = System.Windows.Input.Cursors.Hand,
            MinWidth = 140,
            Tag = false, // recording state
        };
        var keyBoxInner = new Grid();
        keyBoxInner.Children.Add(keyText);
        keyBoxInner.Children.Add(recordingText);
        keyBox.Child = keyBoxInner;
        Grid.SetColumn(keyBox, 1);

        int capturedIndex = index;
        keyBox.MouseDown += (s, e) =>
        {
            bool isRecording = (bool)keyBox.Tag;
            if (!isRecording)
            {
                keyBox.Tag = true;
                keyText.Visibility = Visibility.Collapsed;
                recordingText.Visibility = Visibility.Visible;
                keyBox.BorderBrush = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
                _activeGroupKeyBox = keyBox;
                _activeGroupKeyText = keyText;
                _activeGroupRecordingText = recordingText;
                _activeGroupIndex = capturedIndex;
                this.Focus();
            }
        };
        headerRow.Children.Add(keyBox);

        // Remove button (hidden for group 1 if it's the only group)
        var removeBtn = new System.Windows.Controls.Button
        {
            Content = "\u2715",
            FontSize = 11,
            Width = 28,
            Height = 28,
            Margin = new Thickness(8, 0, 0, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x18, 0xFF, 0x40, 0x40)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x80, 0x80)),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = groups.Count > 1 ? Visibility.Visible : Visibility.Collapsed,
        };
        removeBtn.Click += (s, e) =>
        {
            var newGroups = _settings.GetHotkeyGroups();
            newGroups.RemoveAt(capturedIndex);
            _settings.SetHotkeyGroups(newGroups);
            _ = _settings.SaveAsync();
            _onHotkeyChanged?.Invoke();
            RebuildHotkeyGroupsPanel(newGroups);
        };
        Grid.SetColumn(removeBtn, 3);
        headerRow.Children.Add(removeBtn);

        stack.Children.Add(headerRow);

        // Widget pill row
        var pillLabel = new TextBlock
        {
            Text = group.Key != 0 ? "Widgets in this group:" : "Widgets in this group (set a hotkey to activate):",
            FontSize = 11,
            Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
            Margin = new Thickness(0, 10, 0, 6),
        };
        stack.Children.Add(pillLabel);

        var pillPanel = new WrapPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        foreach (var widgetId in WidgetIds.All)
        {
            bool inGroup = group.Widgets.Contains(widgetId);
            // Check if widget is in another group
            int ci = capturedIndex;
            bool inOtherGroup = groups.Where((g, gi) => gi != ci).Any(g => g.Widgets.Contains(widgetId));
            if (inGroup == false && inOtherGroup) continue; // hide widgets owned by another group (exclusive)

            var pill = BuildWidgetPill(widgetId, inGroup, capturedIndex, groups);
            pillPanel.Children.Add(pill);
        }
        stack.Children.Add(pillPanel);

        // Show unassigned widgets note
        var allAssigned = WidgetIds.All.All(id => groups.Any(g => g.Widgets.Contains(id)));
        if (!allAssigned && index == groups.Count - 1)
        {
            var unassignedPanel = new WrapPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            var unassignedLabel = new TextBlock
            {
                Text = "Unassigned (no hotkey): ",
                FontSize = 11,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            unassignedPanel.Children.Add(unassignedLabel);
            foreach (var widgetId in WidgetIds.All)
            {
                bool inAnyGroup = groups.Any(g => g.Widgets.Contains(widgetId));
                if (!inAnyGroup)
                {
                    var capturedWidgetId = widgetId;
                    var chipText = new TextBlock
                    {
                        Text = WidgetIds.DisplayName(widgetId),
                        FontSize = 11,
                        Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                        IsHitTestVisible = false,
                    };
                    var chip = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(8, 3, 8, 3),
                        Margin = new Thickness(0, 2, 6, 2),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Child = chipText,
                    };
                    chip.MouseLeftButtonUp += (s, ev) =>
                    {
                        var newGroups = _settings.GetHotkeyGroups();
                        // Find first group that has a hotkey assigned
                        var targetIdx = newGroups.FindIndex(g => g.Key != 0);
                        if (targetIdx >= 0)
                        {
                            newGroups[targetIdx].Widgets.Add(capturedWidgetId);
                            _settings.SetHotkeyGroups(newGroups);
                            _ = _settings.SaveAsync();
                            _onHotkeyChanged?.Invoke();
                            RebuildHotkeyGroupsPanel(newGroups);
                        }
                    };
                    unassignedPanel.Children.Add(chip);
                }
            }
            stack.Children.Add(unassignedPanel);
        }

        return outerBorder;
    }

    private UIElement BuildWidgetPill(string widgetId, bool inGroup, int groupIndex, List<HotkeyGroup> groups)
    {
        var pill = new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 2, 6, 2),
            Cursor = System.Windows.Input.Cursors.Hand,
            BorderThickness = new Thickness(1),
        };
        var pillText = new TextBlock { Text = WidgetIds.DisplayName(widgetId), FontSize = 12, IsHitTestVisible = false };
        pill.Child = pillText;
        ApplyPillStyle(pill, pillText, inGroup);

        pill.MouseLeftButtonUp += (s, e) =>
        {
            var newGroups = _settings.GetHotkeyGroups();
            bool nowIn = newGroups[groupIndex].Widgets.Contains(widgetId);
            if (!nowIn)
            {
                // Exclusive: remove from any other group first
                foreach (var g in newGroups)
                    g.Widgets.Remove(widgetId);
                newGroups[groupIndex].Widgets.Add(widgetId);
            }
            else
            {
                newGroups[groupIndex].Widgets.Remove(widgetId);
            }
            _settings.SetHotkeyGroups(newGroups);
            _ = _settings.SaveAsync();
            _onHotkeyChanged?.Invoke();
            RebuildHotkeyGroupsPanel(newGroups);
        };

        return pill;
    }

    private static void ApplyPillStyle(Border pill, TextBlock pillText, bool active)
    {
        if (active)
        {
            pill.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0x58, 0xC4, 0xFF));
            pill.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xA0, 0x58, 0xC4, 0xFF));
            pillText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA8, 0xD8, 0xFF));
        }
        else
        {
            pill.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
            pill.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            pillText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB0, 0xB8, 0xC8));
        }
    }
}
