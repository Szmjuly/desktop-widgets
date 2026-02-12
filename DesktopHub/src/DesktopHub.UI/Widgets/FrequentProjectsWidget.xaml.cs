using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.UI.Helpers;
using WpfColor = System.Windows.Media.Color;
using WpfBrush = System.Windows.Media.SolidColorBrush;
using WpfColorConverter = System.Windows.Media.ColorConverter;

namespace DesktopHub.UI.Widgets;

public partial class FrequentProjectsWidget : System.Windows.Controls.UserControl
{
    private readonly IProjectLaunchDataStore _launchStore;
    private readonly ISettingsService? _settings;
    private bool _isGridMode;
    private const double GridTileWidth = 110;
    private const double GridTileMargin = 10;
    private const int GridColumns = 3;
    private DateTime _lastClickTime = DateTime.MinValue;
    private const int DoubleClickThresholdMs = 300;

    public event Action<string>? OnProjectSelectedForSearch;

    public FrequentProjectsWidget(IProjectLaunchDataStore launchStore) : this(launchStore, null) { }

    public FrequentProjectsWidget(IProjectLaunchDataStore launchStore, ISettingsService? settings)
    {
        InitializeComponent();
        _launchStore = launchStore;
        _settings = settings;

        Loaded += async (_, _) => await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        try
        {
            _isGridMode = _settings?.GetFrequentProjectsGridMode() ?? false;
            var maxShown = _settings?.GetMaxFrequentProjectsShown() ?? 5;
            var topProjects = await _launchStore.GetTopProjectsAsync(maxShown);

            ProjectTilesPanel.Children.Clear();
            ProjectTilesGrid.Children.Clear();

            if (topProjects.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                ProjectTilesPanel.Visibility = Visibility.Collapsed;
                GridScrollViewer.Visibility = Visibility.Collapsed;
                StatusText.Text = "No launches recorded";
                return;
            }

            EmptyState.Visibility = Visibility.Collapsed;

            if (_isGridMode)
            {
                ProjectTilesPanel.Visibility = Visibility.Collapsed;
                GridScrollViewer.Visibility = Visibility.Visible;

                // Fixed 3-column grid; WrapPanel constrained to fit exactly 3 tiles per row
                var gridWidth = GridColumns * (GridTileWidth + GridTileMargin);
                ProjectTilesGrid.MaxWidth = gridWidth;

                for (int i = 0; i < topProjects.Count; i++)
                {
                    var record = topProjects[i];
                    var tile = CreateProjectGridTile(record, i + 1);
                    ProjectTilesGrid.Children.Add(tile);
                }
            }
            else
            {
                ProjectTilesPanel.Visibility = Visibility.Visible;
                GridScrollViewer.Visibility = Visibility.Collapsed;

                for (int i = 0; i < topProjects.Count; i++)
                {
                    var record = topProjects[i];
                    var tile = CreateProjectTile(record, i + 1);
                    ProjectTilesPanel.Children.Add(tile);
                }
            }

            StatusText.Text = $"Top {topProjects.Count} launched projects";
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"FrequentProjectsWidget: RefreshAsync error: {ex.Message}");
        }
    }

    private Border CreateProjectGridTile(ProjectLaunchRecord record, int rank)
    {
        // Rank badge (top-right corner)
        var rankBadge = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = rank <= 3
                ? new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#304FC3F7"))
                : new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#10F5F7FA")),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 0, 0),
            Child = new TextBlock
            {
                Text = rank.ToString(),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = rank <= 3
                    ? new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#4FC3F7"))
                    : new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#90A4AE")),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        // Project number (large)
        var numberText = new TextBlock
        {
            Text = record.FullNumber,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#4FC3F7")),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 3)
        };

        // Project name (small, wrapped)
        var nameText = new TextBlock
        {
            Text = record.Name,
            FontSize = 9,
            Foreground = new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#C0F5F7FA")),
            TextTrimming = TextTrimming.CharacterEllipsis,
            TextWrapping = TextWrapping.Wrap,
            MaxHeight = 28,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 0, 0, 3)
        };

        // Launch count (bottom)
        var countText = new TextBlock
        {
            Text = $"⚡{record.LaunchCount}",
            FontSize = 9,
            Foreground = new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#90A4AE")),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };

        var stack = new StackPanel
        {
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        stack.Children.Add(numberText);
        stack.Children.Add(nameText);
        stack.Children.Add(countText);

        // Grid container for rank badge overlay
        var grid = new Grid();
        grid.Children.Add(stack);
        grid.Children.Add(rankBadge);

        // Tile border
        var tile = new Border
        {
            Background = new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#10F5F7FA")),
            CornerRadius = new CornerRadius(10),
            Width = GridTileWidth,
            Height = 92,
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 10, 10),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = grid,
            Tag = (record.Path, $"{record.FullNumber} {record.Name}", record.FullNumber, record.Name),
            ToolTip = $"{record.FullNumber} - {record.Name}\n{record.Path}"
        };

        tile.MouseLeftButtonDown += Tile_Click;
        tile.MouseEnter += (s, _) =>
        {
            if (s is Border b) b.Background = new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#25F5F7FA"));
        };
        tile.MouseLeave += (s, _) =>
        {
            if (s is Border b) b.Background = new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#10F5F7FA"));
        };

        return tile;
    }

    private Border CreateProjectTile(ProjectLaunchRecord record, int rank)
    {
        // Rank badge
        var rankBadge = new Border
        {
            Width = 22,
            Height = 22,
            CornerRadius = new CornerRadius(11),
            Background = rank <= 3
                ? new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#304FC3F7"))
                : new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#10F5F7FA")),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Child = new TextBlock
            {
                Text = rank.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = rank <= 3
                    ? new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#4FC3F7"))
                    : new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#90A4AE")),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };

        // Project info
        var numberText = new TextBlock
        {
            Text = record.FullNumber,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#4FC3F7"))
        };

        var nameText = new TextBlock
        {
            Text = record.Name,
            FontSize = 11,
            Foreground = new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#F5F7FA")),
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var infoStack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };
        infoStack.Children.Add(numberText);
        infoStack.Children.Add(nameText);

        // Launch count badge
        var countText = new TextBlock
        {
            Text = record.LaunchCount.ToString(),
            FontSize = 10,
            Foreground = new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#90A4AE")),
            VerticalAlignment = VerticalAlignment.Center
        };

        var countBadge = new Border
        {
            Background = new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#10F5F7FA")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 2, 6, 2),
            VerticalAlignment = VerticalAlignment.Center,
            Child = countText,
            ToolTip = $"Launched {record.LaunchCount} time{(record.LaunchCount == 1 ? "" : "s")}"
        };

        // Layout
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        Grid.SetColumn(rankBadge, 0);
        Grid.SetColumn(infoStack, 1);
        Grid.SetColumn(countBadge, 2);

        grid.Children.Add(rankBadge);
        grid.Children.Add(infoStack);
        grid.Children.Add(countBadge);

        // Tile border
        var tile = new Border
        {
            Background = new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#10F5F7FA")),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = grid,
            Tag = (record.Path, $"{record.FullNumber} {record.Name}", record.FullNumber, record.Name),
            ToolTip = record.Path
        };

        tile.MouseLeftButtonDown += Tile_Click;
        tile.MouseEnter += (s, _) =>
        {
            if (s is Border b) b.Background = new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#25F5F7FA"));
        };
        tile.MouseLeave += (s, _) =>
        {
            if (s is Border b) b.Background = new WpfBrush((WpfColor)WpfColorConverter.ConvertFromString("#10F5F7FA"));
        };

        return tile;
    }

    private async void Tile_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border tile && tile.Tag is (string path, string displayText, string fullNumber, string name))
        {
            var now = DateTime.Now;
            var elapsed = (now - _lastClickTime).TotalMilliseconds;
            _lastClickTime = now;

            if (elapsed < DoubleClickThresholdMs)
            {
                // Double-click: Open folder in explorer + record launch
                try
                {
                    Process.Start("explorer.exe", path);
                    await _launchStore.RecordLaunchAsync(path, fullNumber, name);
                    await RefreshAsync();
                    DebugLogger.Log($"FrequentProjectsWidget: Double-clicked project at {path}, opening folder + recorded launch");
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"FrequentProjectsWidget: Failed to open {path}: {ex.Message}");
                }
            }
            else
            {
                // Single-click: Load project number + name into search field
                OnProjectSelectedForSearch?.Invoke(displayText);
                DebugLogger.Log($"FrequentProjectsWidget: Single-clicked project, loading '{displayText}' into search");
            }
        }
    }

    private async void RefreshButton_Click(object sender, MouseButtonEventArgs e)
    {
        await RefreshAsync();
    }
}
