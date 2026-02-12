using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopHub.Core.Abstractions;
using DesktopHub.Infrastructure.Settings;
using DesktopHub.UI.Helpers;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace DesktopHub.UI.Widgets;

public partial class QuickLaunchWidget : System.Windows.Controls.UserControl
{
    private QuickLaunchConfig _config = new();
    private readonly ISettingsService? _settings;
    private bool _isHorizontalMode;
    private const int MaxHorizontalVisible = 5;
    private const double HorizontalTileWidth = 72;

    public QuickLaunchWidget()
    {
        InitializeComponent();
        Loaded += async (_, _) => await InitializeAsync();
    }

    public QuickLaunchWidget(ISettingsService settings) : this()
    {
        _settings = settings;
    }

    private async Task InitializeAsync()
    {
        _config = await QuickLaunchConfig.LoadAsync();
        _isHorizontalMode = _settings?.GetQuickLaunchHorizontalMode() ?? false;
        RenderItems();
    }

    private void RenderItems()
    {
        ItemsPanel.Children.Clear();
        HorizontalItemsPanel.Children.Clear();

        var items = _config.Items.OrderBy(i => i.SortOrder).ToList();

        if (items.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            ItemsScroller.Visibility = Visibility.Collapsed;
            HorizontalItemsScroller.Visibility = Visibility.Collapsed;
            StatusText.Text = "0 items";
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;

        if (_isHorizontalMode)
        {
            ItemsScroller.Visibility = Visibility.Collapsed;
            HorizontalItemsScroller.Visibility = Visibility.Visible;

            // Cap visible width to MaxHorizontalVisible tiles, scroll for the rest
            var visibleCount = Math.Min(items.Count, MaxHorizontalVisible);
            HorizontalItemsScroller.MaxWidth = visibleCount * (HorizontalTileWidth + 6);

            foreach (var item in items)
            {
                var tile = CreateHorizontalItemTile(item);
                HorizontalItemsPanel.Children.Add(tile);
            }
        }
        else
        {
            ItemsScroller.Visibility = Visibility.Visible;
            HorizontalItemsScroller.Visibility = Visibility.Collapsed;

            foreach (var item in items)
            {
                var tile = CreateItemTile(item);
                ItemsPanel.Children.Add(tile);
            }
        }

        StatusText.Text = $"{items.Count} item{(items.Count == 1 ? "" : "s")}";
    }

    private Border CreateItemTile(QuickLaunchItem item)
    {
        // Icon
        var iconText = new TextBlock
        {
            Text = item.Icon,
            FontSize = 18,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0)
        };

        // Name + path
        var nameText = new TextBlock
        {
            Text = item.Name,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#F5F7FA")),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var pathText = new TextBlock
        {
            Text = item.Path,
            FontSize = 10,
            Foreground = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#777777")),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        infoStack.Children.Add(nameText);
        infoStack.Children.Add(pathText);

        // Delete button
        var deleteBtn = new Border
        {
            Background = System.Windows.Media.Brushes.Transparent,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(4, 2, 4, 2),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = item.Id,
            Child = new TextBlock
            {
                Text = "\u2715",
                FontSize = 10,
                Foreground = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#60F5F7FA"))
            }
        };
        deleteBtn.MouseEnter += (s, _) =>
        {
            if (s is Border b) b.Background = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#30FF5252"));
        };
        deleteBtn.MouseLeave += (s, _) =>
        {
            if (s is Border b) b.Background = System.Windows.Media.Brushes.Transparent;
        };
        deleteBtn.MouseLeftButtonDown += DeleteItem_Click;

        // Layout
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(iconText, 0);
        Grid.SetColumn(infoStack, 1);
        Grid.SetColumn(deleteBtn, 2);

        grid.Children.Add(iconText);
        grid.Children.Add(infoStack);
        grid.Children.Add(deleteBtn);

        var tile = new Border
        {
            Background = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#10F5F7FA")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = grid,
            Tag = item.Path,
            ToolTip = item.Path
        };

        tile.MouseLeftButtonDown += Tile_Click;
        tile.MouseEnter += (s, _) =>
        {
            if (s is Border b) b.Background = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#25F5F7FA"));
        };
        tile.MouseLeave += (s, _) =>
        {
            if (s is Border b) b.Background = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#10F5F7FA"));
        };

        return tile;
    }

    private Border CreateHorizontalItemTile(QuickLaunchItem item)
    {
        // Icon — large and centered
        var iconText = new TextBlock
        {
            Text = item.Icon,
            FontSize = 24,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 4)
        };

        // Name — two lines max, centered
        var nameText = new TextBlock
        {
            Text = item.Name,
            FontSize = 10,
            Foreground = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#C0F5F7FA")),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 28,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center
        };

        var stack = new StackPanel
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(iconText);
        stack.Children.Add(nameText);

        var tile = new Border
        {
            Background = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#10F5F7FA")),
            CornerRadius = new CornerRadius(10),
            Width = HorizontalTileWidth,
            Height = HorizontalTileWidth + 12,
            Padding = new Thickness(4, 6, 4, 4),
            Margin = new Thickness(0, 0, 6, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = stack,
            Tag = item.Path,
            ToolTip = $"{item.Name}\n{item.Path}"
        };

        tile.MouseLeftButtonDown += Tile_Click;
        tile.MouseEnter += (s, _) =>
        {
            if (s is Border b) b.Background = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#25F5F7FA"));
        };
        tile.MouseLeave += (s, _) =>
        {
            if (s is Border b) b.Background = new SolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString("#10F5F7FA"));
        };

        return tile;
    }

    private void Tile_Click(object sender, MouseButtonEventArgs e)
    {
        // Don't launch if the delete button was clicked
        if (e.OriginalSource is FrameworkElement fe)
        {
            var parent = fe;
            while (parent != null)
            {
                if (parent.Tag is string tagStr && Guid.TryParse(tagStr, out _))
                    return; // Click was on the delete button
                parent = VisualTreeHelper.GetParent(parent) as FrameworkElement;
            }
        }

        if (sender is Border tile && tile.Tag is string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                DebugLogger.Log($"QuickLaunchWidget: Launched {path}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"QuickLaunchWidget: Failed to launch {path}: {ex.Message}");
            }
        }
    }

    private async void DeleteItem_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is Border btn && btn.Tag is string itemId)
        {
            _config.Items.RemoveAll(i => i.Id == itemId);
            await _config.SaveAsync();
            RenderItems();
        }
    }

    private void AddItemButton_Click(object sender, MouseButtonEventArgs e)
    {
        AddPanel.Visibility = Visibility.Visible;
        NameInput.Text = "";
        PathInput.Text = "";
        NameInput.Focus();
    }

    private void CancelAdd_Click(object sender, MouseButtonEventArgs e)
    {
        AddPanel.Visibility = Visibility.Collapsed;
    }

    private async void SaveAdd_Click(object sender, MouseButtonEventArgs e)
    {
        await SaveNewItem();
    }

    private async void PathInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await SaveNewItem();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            AddPanel.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
    }

    private async Task SaveNewItem()
    {
        var path = PathInput.Text.Trim();
        if (string.IsNullOrEmpty(path)) return;

        var name = NameInput.Text.Trim();
        if (string.IsNullOrEmpty(name))
        {
            // Auto-generate name from path
            name = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(name))
                name = path;
        }

        // Determine icon based on path type
        var icon = "📁";
        if (System.IO.File.Exists(path))
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            icon = ext switch
            {
                ".exe" or ".bat" or ".cmd" or ".ps1" => "⚡",
                ".pdf" => "📄",
                ".doc" or ".docx" => "📝",
                ".xls" or ".xlsx" => "📊",
                ".ppt" or ".pptx" => "📽",
                ".dwg" or ".rvt" or ".dxf" => "📐",
                _ => "📄"
            };
        }
        else if (path.StartsWith("http://") || path.StartsWith("https://"))
        {
            icon = "🌐";
        }

        var item = new QuickLaunchItem
        {
            Name = name,
            Path = path,
            Icon = icon,
            SortOrder = _config.Items.Count
        };

        _config.Items.Add(item);
        await _config.SaveAsync();

        AddPanel.Visibility = Visibility.Collapsed;
        RenderItems();
    }

    private void BrowseButton_Click(object sender, MouseButtonEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a folder to add to Quick Launch",
            ShowNewFolderButton = false
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            PathInput.Text = dialog.SelectedPath;
            if (string.IsNullOrWhiteSpace(NameInput.Text))
            {
                NameInput.Text = System.IO.Path.GetFileName(dialog.SelectedPath);
            }
        }
    }

    // ===== Drag and Drop Support =====

    private void Widget_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
            // Visual feedback - highlight the widget
            this.Opacity = 0.85;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Widget_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        this.Opacity = 1.0;
        e.Handled = true;
    }

    private async void Widget_Drop(object sender, System.Windows.DragEventArgs e)
    {
        this.Opacity = 1.0;

        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop)) return;

        var files = e.Data.GetData(System.Windows.DataFormats.FileDrop) as string[];
        if (files == null || files.Length == 0) return;

        foreach (var filePath in files)
        {
            // Skip if already exists
            if (_config.Items.Any(i => string.Equals(i.Path, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                DebugLogger.Log($"QuickLaunchWidget: Skipping duplicate drop: {filePath}");
                continue;
            }

            var name = System.IO.Path.GetFileName(filePath);
            if (string.IsNullOrEmpty(name)) name = filePath;

            // Determine icon
            var icon = "📁";
            if (System.IO.File.Exists(filePath))
            {
                var ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
                icon = ext switch
                {
                    ".exe" or ".bat" or ".cmd" or ".ps1" => "⚡",
                    ".pdf" => "📄",
                    ".doc" or ".docx" => "📝",
                    ".xls" or ".xlsx" => "📊",
                    ".ppt" or ".pptx" => "📽",
                    ".dwg" or ".rvt" or ".dxf" => "📐",
                    ".lnk" => "🔗",
                    _ => "📄"
                };

                // For .lnk shortcuts, try to get the target name
                if (ext == ".lnk")
                {
                    name = System.IO.Path.GetFileNameWithoutExtension(filePath);
                }
            }

            var item = new QuickLaunchItem
            {
                Name = name,
                Path = filePath,
                Icon = icon,
                SortOrder = _config.Items.Count
            };

            _config.Items.Add(item);
            DebugLogger.Log($"QuickLaunchWidget: Added via drop: {name} -> {filePath}");
        }

        await _config.SaveAsync();
        RenderItems();
        e.Handled = true;
    }
}
