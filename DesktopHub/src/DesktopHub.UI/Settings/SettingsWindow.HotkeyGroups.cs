using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DesktopHub.Core.Models;
using DesktopHub.UI.Helpers;

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
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            };
            addBtn.SetResourceReference(System.Windows.Controls.Button.BackgroundProperty, "HoverMediumBrush");
            addBtn.SetResourceReference(System.Windows.Controls.Button.ForegroundProperty, "TextPrimaryBrush");
            addBtn.SetResourceReference(System.Windows.Controls.Button.BorderBrushProperty, "CardBorderBrush");
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

        // Detect conflict: hotkey is assigned but failed to register globally
        bool isConflicted =
            group.Key != 0 &&
            (_failedHotkeysProvider?.Invoke()?.Contains((group.Modifiers, group.Key)) ?? false);
        KnownHotkeyRegistry.ConflictMatch? conflictApp = isConflicted
            ? KnownHotkeyRegistry.FindRunningConflict(group.Modifiers, group.Key)
            : null;

        var outerBorder = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 8),
        };
        outerBorder.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        outerBorder.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");

        var stack = new StackPanel();
        outerBorder.Child = stack;

        // Row header: group number + key binding recorder + [open conflict app] + remove button
        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
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
            BorderThickness = new Thickness(isConflicted ? 2 : 1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 5, 10, 5),
            Cursor = System.Windows.Input.Cursors.Hand,
            MinWidth = 140,
            Tag = false, // recording state
        };
        keyBox.SetResourceReference(Border.BackgroundProperty, "FaintOverlayBrush");
        if (isConflicted)
        {
            keyBox.SetResourceReference(Border.BorderBrushProperty, "RedBrush");
            keyBox.ToolTip = conflictApp != null
                ? $"Conflict: {conflictApp.DisplayName} is currently using {FormatHotkey(group.Modifiers, group.Key)}.\nPick a different combo, or close {conflictApp.DisplayName} to free the shortcut."
                : $"Conflict: another app is using {FormatHotkey(group.Modifiers, group.Key)}.\nPick a different combo, or close the conflicting app to free the shortcut.";
        }
        else
        {
            keyBox.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
        }
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

        // Quick-open conflicting app button (only when a known conflicting app is running).
        // Compact icon button — uses the app's own icon if we can extract it, otherwise a glyph fallback.
        if (conflictApp != null)
        {
            var appIcon = KnownHotkeyRegistry.TryGetProcessIcon(conflictApp.Process);

            object buttonContent;
            if (appIcon != null)
            {
                var img = new System.Windows.Controls.Image
                {
                    Source = appIcon,
                    Width = 16,
                    Height = 16,
                    Stretch = System.Windows.Media.Stretch.Uniform,
                };
                System.Windows.Media.RenderOptions.SetBitmapScalingMode(
                    img, System.Windows.Media.BitmapScalingMode.HighQuality);
                buttonContent = img;
            }
            else
            {
                // Fallback glyph: "open in new window" arrow
                buttonContent = new TextBlock
                {
                    Text = "\u2197", // ↗
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                };
            }

            var openAppBtn = new System.Windows.Controls.Button
            {
                Content = buttonContent,
                Width = 28,
                Height = 28,
                Margin = new Thickness(8, 0, 0, 0),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = $"Open {conflictApp.DisplayName}",
                Template = BuildRoundedButtonTemplate(6),
            };
            openAppBtn.SetResourceReference(System.Windows.Controls.Button.BackgroundProperty, "CardBorderBrush");
            openAppBtn.SetResourceReference(System.Windows.Controls.Button.ForegroundProperty, "TextBrush");
            openAppBtn.SetResourceReference(System.Windows.Controls.Button.BorderBrushProperty, "BorderBrush");
            var capturedProcess = conflictApp.Process;
            openAppBtn.Click += (s, e) => KnownHotkeyRegistry.BringToForeground(capturedProcess);
            Grid.SetColumn(openAppBtn, 3);
            headerRow.Children.Add(openAppBtn);
        }

        // Remove button (hidden for group 1 if it's the only group)
        var removeBtn = new System.Windows.Controls.Button
        {
            Content = "\u2715",
            FontSize = 11,
            Width = 28,
            Height = 28,
            Margin = new Thickness(8, 0, 0, 0),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = groups.Count > 1 ? Visibility.Visible : Visibility.Collapsed,
            Template = BuildRoundedButtonTemplate(6),
        };
        removeBtn.SetResourceReference(System.Windows.Controls.Button.BackgroundProperty, "RedBackgroundBrush");
        removeBtn.SetResourceReference(System.Windows.Controls.Button.ForegroundProperty, "RedBrush");
        removeBtn.Click += (s, e) =>
        {
            var newGroups = _settings.GetHotkeyGroups();
            newGroups.RemoveAt(capturedIndex);
            _settings.SetHotkeyGroups(newGroups);
            _ = _settings.SaveAsync();
            _onHotkeyChanged?.Invoke();
            RebuildHotkeyGroupsPanel(newGroups);
        };
        Grid.SetColumn(removeBtn, 4);
        headerRow.Children.Add(removeBtn);

        stack.Children.Add(headerRow);

        // Conflict banner (inline, under the header)
        if (isConflicted)
        {
            var bannerText = new TextBlock
            {
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
                Text = conflictApp != null
                    ? $"\u26A0 {conflictApp.DisplayName} is currently using this shortcut. Pick a different combo or close {conflictApp.DisplayName}."
                    : "\u26A0 This shortcut is in use by another app. Pick a different combo or close the other app."
            };
            bannerText.SetResourceReference(TextBlock.ForegroundProperty, "RedBrush");
            stack.Children.Add(bannerText);
        }

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
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(8, 3, 8, 3),
                        Margin = new Thickness(0, 2, 6, 2),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Child = chipText,
                    };
                    chip.SetResourceReference(Border.BackgroundProperty, "CardBrush");
                    chip.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
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

    /// <summary>
    /// Builds a rounded-corner button template that honors Background/BorderBrush/BorderThickness.
    /// Used for the remove (×) button and the "Open [App]" conflict button.
    /// </summary>
    private static ControlTemplate BuildRoundedButtonTemplate(double cornerRadius)
    {
        var template = new ControlTemplate(typeof(System.Windows.Controls.Button));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(cornerRadius));
        border.SetBinding(Border.BackgroundProperty,
            new System.Windows.Data.Binding("Background") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderBrushProperty,
            new System.Windows.Data.Binding("BorderBrush") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });
        border.SetBinding(Border.BorderThicknessProperty,
            new System.Windows.Data.Binding("BorderThickness") { RelativeSource = System.Windows.Data.RelativeSource.TemplatedParent });

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
        content.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        border.AppendChild(content);

        template.VisualTree = border;
        return template;
    }

    private void ApplyPillStyle(Border pill, TextBlock pillText, bool active)
    {
        if (active)
        {
            pill.SetResourceReference(Border.BackgroundProperty, "AccentLightBrush");
            pill.SetResourceReference(Border.BorderBrushProperty, "AccentBrush");
            pillText.SetResourceReference(TextBlock.ForegroundProperty, "BlueBrush");
        }
        else
        {
            pill.SetResourceReference(Border.BackgroundProperty, "CardBrush");
            pill.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
            pillText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        }
    }
}
