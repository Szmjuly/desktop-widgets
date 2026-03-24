using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DesktopHub.UI.Widgets;

public partial class DeveloperPanelWidget
{
    // ════════════════════════════════════════════════════════════
    // NODE PILLS (cached)
    // ════════════════════════════════════════════════════════════

    private async Task DiscoverNodePillsAsync()
    {
        NodePillsPanel.Children.Clear();

        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            NodeCountText.Text = "Firebase unavailable";
            return;
        }

        // Fetch all nodes and cache them
        _nodeCache = new Dictionary<string, Dictionary<string, object>?>();
        int found = 0;

        foreach (var nodeName in KnownNodes)
        {
            try
            {
                var data = await _firebaseService.GetNodeAsync(nodeName);
                if (data == null) continue;

                _nodeCache[nodeName] = data;
                found++;
            }
            catch { }
        }

        NodeCountText.Text = $"{found} nodes";
        RebuildNodePillsFromCache();
    }

    private void RebuildNodePillsFromCache()
    {
        NodePillsPanel.Children.Clear();
        if (_nodeCache == null) return;

        foreach (var kvp in _nodeCache)
        {
            var pill = CreateNodePill(kvp.Key, kvp.Value?.Count ?? 0);
            NodePillsPanel.Children.Add(pill);
        }
    }

    private UIElement CreateNodePill(string nodeName, int childCount)
    {
        var nameBlock = new TextBlock
        {
            Text = nodeName,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };

        var countBlock = new TextBlock
        {
            Text = childCount.ToString("N0"),
            FontSize = 10,
            Foreground = FindBrush("TextTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.Add(nameBlock);
        stack.Children.Add(countBlock);

        var isSelected = _selectedNodePill == nodeName;
        var bgBrush = isSelected ? FindBrush("AccentBrush") : FindBrush("SurfaceBrush");
        var fgForSelected = isSelected ? Brushes.White : FindBrush("TextPrimaryBrush");
        var borderBrush = isSelected ? FindBrush("AccentBrush") : FindBrush("BorderBrush");

        if (isSelected)
        {
            nameBlock.Foreground = Brushes.White;
            countBlock.Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255));
        }

        var border = new Border
        {
            Background = bgBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 6, 6),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = stack
        };

        border.MouseEnter += (_, _) => { if (_selectedNodePill != nodeName) border.Background = FindBrush("HoverMediumBrush"); };
        border.MouseLeave += (_, _) => { if (_selectedNodePill != nodeName) border.Background = FindBrush("SurfaceBrush"); };
        border.MouseLeftButtonDown += async (_, _) =>
        {
            _selectedNodePill = nodeName;
            NodePathBox.Text = nodeName;
            await GetNodeAsync();
            // Just rebuild pills from cache - no network call
            RebuildNodePillsFromCache();
        };

        return border;
    }

    // ════════════════════════════════════════════════════════════
    // TREE VIEW
    // ════════════════════════════════════════════════════════════

    private void RenderNodeData()
    {
        if (_lastNodeData == null) return;

        if (_dbShowJson)
        {
            NodeJsonBox.Text = JsonSerializer.Serialize(_lastNodeData, new JsonSerializerOptions { WriteIndented = true });
            NodeJsonBox.Visibility = Visibility.Visible;
            DbTreeView.Visibility = Visibility.Collapsed;
            DbViewToggleBtn.Content = "Tree";
        }
        else
        {
            BuildTreeView(_lastNodeData);
            DbTreeView.Visibility = Visibility.Visible;
            NodeJsonBox.Visibility = Visibility.Collapsed;
            DbViewToggleBtn.Content = "JSON";
        }
    }

    private void BuildTreeView(Dictionary<string, object>? data)
    {
        DbTreeView.Items.Clear();
        if (data == null) return;

        foreach (var kvp in data.OrderBy(k => k.Key))
        {
            var item = CreateTreeItem(kvp.Key, kvp.Value);
            DbTreeView.Items.Add(item);
        }
    }

    private TreeViewItem CreateTreeItem(string key, object? value)
    {
        var item = new TreeViewItem { IsExpanded = false };

        if (value is Dictionary<string, object> dict)
        {
            item.Header = CreateTreeHeader(key, "obj", $"{dict.Count} keys", "#42A5F5");
            foreach (var kvp in dict.OrderBy(k => k.Key))
                item.Items.Add(CreateTreeItem(kvp.Key, kvp.Value));
        }
        else if (value is List<object> list)
        {
            item.Header = CreateTreeHeader(key, "arr", $"{list.Count} items", "#AB47BC");
            for (int i = 0; i < list.Count; i++)
                item.Items.Add(CreateTreeItem($"[{i}]", list[i]));
        }
        else if (value is IDictionary rawDict)
        {
            item.Header = CreateTreeHeader(key, "obj", $"{rawDict.Count} keys", "#42A5F5");
            foreach (DictionaryEntry entry in rawDict)
                item.Items.Add(CreateTreeItem(entry.Key?.ToString() ?? "?", entry.Value));
        }
        else if (value is IList rawList)
        {
            item.Header = CreateTreeHeader(key, "arr", $"{rawList.Count} items", "#AB47BC");
            for (int i = 0; i < rawList.Count; i++)
                item.Items.Add(CreateTreeItem($"[{i}]", rawList[i]));
        }
        else
        {
            var (typeTag, color) = GetTypeInfo(value);
            var displayValue = value?.ToString() ?? "null";
            if (displayValue.Length > 100) displayValue = displayValue[..100] + "...";
            item.Header = CreateTreeHeader(key, typeTag, displayValue, color);
        }

        return item;
    }

    private UIElement CreateTreeHeader(string key, string typeTag, string valuePreview, string color)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };

        panel.Children.Add(new TextBlock
        {
            Text = key,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("TextPrimaryBrush"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        });

        var tagColor = (Color)ColorConverter.ConvertFromString(color);
        panel.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(40, tagColor.R, tagColor.G, tagColor.B)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = typeTag,
                FontSize = 9,
                Foreground = new SolidColorBrush(tagColor),
                FontWeight = FontWeights.Bold
            }
        });

        panel.Children.Add(new TextBlock
        {
            Text = valuePreview,
            Foreground = FindBrush("TextSecondaryBrush"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxWidth = 400
        });

        return panel;
    }

    private static (string TypeTag, string Color) GetTypeInfo(object? value) => value switch
    {
        string => ("str", "#66BB6A"),
        bool => ("bool", "#FFA726"),
        int or long or float or double or decimal => ("num", "#42A5F5"),
        null => ("null", "#78909C"),
        _ => ("?", "#78909C")
    };

    // ════════════════════════════════════════════════════════════
    // NODE CRUD
    // ════════════════════════════════════════════════════════════

    private async Task GetNodeAsync()
    {
        if (!EnsureDevForAction("get firebase node")) return;
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase service unavailable.");
            return;
        }

        var path = (NodePathBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            AppendOutput("Node path is required.");
            return;
        }

        var node = await _firebaseService.GetNodeAsync(path);
        if (node == null)
        {
            _lastNodeData = null;
            NodeJsonBox.Text = "{}";
            DbTreeView.Items.Clear();
            AppendOutput($"No data found for '{path}'.");
            return;
        }

        _lastNodeData = node;
        RenderNodeData();
        AppendOutput($"Fetched node '{path}'.");
    }

    private async Task SetNodeAsync()
    {
        if (!EnsureDevForAction("set firebase node")) return;
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase service unavailable.");
            return;
        }

        var path = (NodePathBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            AppendOutput("Node path is required.");
            return;
        }

        // Always read from JSON box for set operations
        object? payload;
        var raw = (NodeJsonBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            AppendOutput("JSON payload is empty. Switch to JSON view and enter data.");
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            payload = JsonElementToObject(doc.RootElement);
        }
        catch (Exception ex)
        {
            AppendOutput($"Invalid JSON payload: {ex.Message}");
            return;
        }

        var result = await _firebaseService.SetNodeAsync(path, payload ?? new Dictionary<string, object>());
        AppendOutput(result ? $"Updated node '{path}'." : $"Failed to update node '{path}'.");
    }

    private async Task DeleteNodeAsync()
    {
        if (!EnsureDevForAction("delete firebase node")) return;
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase service unavailable.");
            return;
        }

        var path = (NodePathBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            AppendOutput("Node path is required.");
            return;
        }

        if (!ConfirmDangerous($"Delete Firebase node '{path}'?"))
        {
            AppendOutput("Delete cancelled.");
            return;
        }

        var result = await _firebaseService.DeleteNodeAsync(path);
        AppendOutput(result ? $"Deleted node '{path}'." : $"Failed to delete node '{path}'.");
    }

    // ════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ════════════════════════════════════════════════════════════

    private async void GetNode_Click(object sender, RoutedEventArgs e) => await GetNodeAsync();
    private async void SetNode_Click(object sender, RoutedEventArgs e) => await SetNodeAsync();
    private async void DeleteNode_Click(object sender, RoutedEventArgs e) => await DeleteNodeAsync();

    private async void RefreshNodes_Click(object sender, RoutedEventArgs e)
    {
        _nodeCache = null;
        _selectedNodePill = null;
        await DiscoverNodePillsAsync();
    }

    private void ToggleDbView_Click(object sender, RoutedEventArgs e)
    {
        _dbShowJson = !_dbShowJson;
        RenderNodeData();
    }
}
