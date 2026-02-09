using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DesktopHub.Core.Models;
using DesktopHub.UI.Services;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DesktopHub.UI.Widgets;

public partial class QuickTasksWidget : System.Windows.Controls.UserControl
{
    private readonly TaskService _taskService;
    private bool _isSearchMode = false;

    public QuickTasksWidget(TaskService taskService)
    {
        InitializeComponent();
        _taskService = taskService;
        _taskService.TasksChanged += OnTasksChanged;

        Loaded += async (s, e) =>
        {
            try
            {
                await _taskService.InitializeAsync();
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"QuickTasksWidget: Init error: {ex.Message}");
            }
        };
    }

    private void OnTasksChanged(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            RenderTasks();
            UpdateDateLabel();
            UpdateStatusText();
        });
    }

    // --- Date Navigation ---

    private async void PrevDayButton_Click(object sender, MouseButtonEventArgs e)
    {
        await _taskService.GoToPreviousDayAsync();
    }

    private async void NextDayButton_Click(object sender, MouseButtonEventArgs e)
    {
        await _taskService.GoToNextDayAsync();
    }

    private async void TodayButton_Click(object sender, MouseButtonEventArgs e)
    {
        await _taskService.GoToTodayAsync();
    }

    // --- Add Task ---

    private void AddTaskButton_Click(object sender, MouseButtonEventArgs e)
    {
        NewTaskPanel.Visibility = Visibility.Visible;
        NewTaskInput.Text = "";
        NewTaskInput.Focus();
    }

    private async void NewTaskInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var title = NewTaskInput.Text.Trim();
            if (!string.IsNullOrEmpty(title))
            {
                await _taskService.AddTaskAsync(title);
                NewTaskInput.Text = "";
                NewTaskPanel.Visibility = Visibility.Collapsed;
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            NewTaskPanel.Visibility = Visibility.Collapsed;
            e.Handled = true;
        }
    }

    private void NewTaskInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NewTaskInput.Text))
        {
            NewTaskPanel.Visibility = Visibility.Collapsed;
        }
    }

    // --- Search ---

    private void SearchToggle_Click(object sender, MouseButtonEventArgs e)
    {
        _isSearchMode = !_isSearchMode;
        SearchPanel.Visibility = _isSearchMode ? Visibility.Visible : Visibility.Collapsed;
        SearchResultsScroller.Visibility = Visibility.Collapsed;

        if (_isSearchMode)
        {
            SearchInput.Text = "";
            SearchInput.Focus();
        }
        else
        {
            // Return to normal view
            SearchResultsPanel.Children.Clear();
            RenderTasks();
        }
    }

    private async void SearchInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        var query = SearchInput.Text.Trim();
        if (query.Length < 2)
        {
            SearchResultsScroller.Visibility = Visibility.Collapsed;
            SearchResultsPanel.Children.Clear();
            return;
        }

        var results = await _taskService.SearchAsync(query);
        RenderSearchResults(results);
    }

    private void SearchInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _isSearchMode = false;
            SearchPanel.Visibility = Visibility.Collapsed;
            SearchResultsScroller.Visibility = Visibility.Collapsed;
            SearchResultsPanel.Children.Clear();
            e.Handled = true;
        }
    }

    // --- Rendering ---

    private void UpdateDateLabel()
    {
        if (DateTime.TryParse(_taskService.CurrentDate, out var date))
        {
            DateLabel.Text = date.ToString("MMM d, yyyy");
        }

        TodayButtonText.Foreground = _taskService.IsToday
            ? (WpfBrush)FindResource("AccentBrush")
            : (WpfBrush)FindResource("DimTextBrush");
    }

    private async void UpdateStatusText()
    {
        try
        {
            var (active, completed) = await _taskService.GetCountsAsync();
            StatusText.Text = completed > 0
                ? $"{active} active \u00B7 {completed} done"
                : $"{active} active";
        }
        catch
        {
            StatusText.Text = "";
        }
    }

    private void RenderTasks()
    {
        TaskListPanel.Children.Clear();

        var tasks = _taskService.CurrentTasks;
        if (tasks.Count == 0)
        {
            EmptyState.Visibility = Visibility.Visible;
            TaskScrollViewer.Visibility = Visibility.Collapsed;
            return;
        }

        EmptyState.Visibility = Visibility.Collapsed;
        TaskScrollViewer.Visibility = Visibility.Visible;

        foreach (var task in tasks)
        {
            TaskListPanel.Children.Add(CreateTaskRow(task));
        }
    }

    private Border CreateTaskRow(TaskItem task)
    {
        var isCompact = _taskService.Config.CompactMode;
        var row = new Border
        {
            Background = WpfBrushes.Transparent,
            CornerRadius = new CornerRadius(isCompact ? 4 : 6),
            Padding = isCompact ? new Thickness(6, 3, 6, 3) : new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 0, isCompact ? 1 : 2),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = task.Id
        };

        // Hover effect
        row.MouseEnter += (s, e) => row.Background = (WpfBrush)FindResource("HoverBrush");
        row.MouseLeave += (s, e) => row.Background = WpfBrushes.Transparent;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });  // Checkbox
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });   // Priority dot
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Title
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });     // Delete

        // Checkbox
        var checkbox = new Border
        {
            Width = 14,
            Height = 14,
            CornerRadius = new CornerRadius(3),
            BorderBrush = task.IsCompleted
                ? (WpfBrush)FindResource("AccentBrush")
                : new WpfSolidColorBrush(WpfColor.FromArgb(0x50, 0xF5, 0xF7, 0xFA)),
            BorderThickness = new Thickness(1.5),
            Background = task.IsCompleted
                ? (WpfBrush)FindResource("AccentBrush")
                : WpfBrushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        if (task.IsCompleted)
        {
            checkbox.Child = new TextBlock
            {
                Text = "\u2713",
                FontSize = 9,
                Foreground = new WpfSolidColorBrush(WpfColor.FromRgb(0x12, 0x12, 0x12)),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, -1, 0, 0)
            };
        }

        checkbox.MouseLeftButtonDown += async (s, e) =>
        {
            e.Handled = true;
            await _taskService.ToggleTaskCompletionAsync(task.Id);
        };

        Grid.SetColumn(checkbox, 0);
        grid.Children.Add(checkbox);

        // Priority dot
        var priorityDot = new Border
        {
            Width = 5,
            Height = 5,
            CornerRadius = new CornerRadius(2.5),
            Background = task.Priority switch
            {
                "high" => (WpfBrush)FindResource("HighPriorityBrush"),
                "low" => (WpfBrush)FindResource("LowPriorityBrush"),
                _ => (WpfBrush)FindResource("NormalPriorityBrush")
            },
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        Grid.SetColumn(priorityDot, 1);
        grid.Children.Add(priorityDot);

        // Title
        var titleBlock = new TextBlock
        {
            Text = task.Title,
            FontSize = 12,
            Foreground = (WpfBrush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(4, 0, 0, 0)
        };

        if (task.IsCompleted)
        {
            titleBlock.TextDecorations = TextDecorations.Strikethrough;
            titleBlock.Opacity = _taskService.Config.CompletedOpacity;
        }

        Grid.SetColumn(titleBlock, 2);
        grid.Children.Add(titleBlock);

        // Delete button (visible on hover)
        var deleteBtn = new TextBlock
        {
            Text = "\u2715",
            FontSize = 10,
            Foreground = new WpfSolidColorBrush(WpfColor.FromArgb(0x00, 0xFF, 0x52, 0x52)),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(4, 0, 0, 0)
        };

        row.MouseEnter += (s, e) => deleteBtn.Foreground = new WpfSolidColorBrush(WpfColor.FromArgb(0x80, 0xFF, 0x52, 0x52));
        row.MouseLeave += (s, e) => deleteBtn.Foreground = new WpfSolidColorBrush(WpfColor.FromArgb(0x00, 0xFF, 0x52, 0x52));

        deleteBtn.MouseLeftButtonDown += async (s, e) =>
        {
            e.Handled = true;
            await _taskService.DeleteTaskAsync(task.Id);
        };

        Grid.SetColumn(deleteBtn, 3);
        grid.Children.Add(deleteBtn);

        // Right-click context menu for priority/category
        row.MouseRightButtonDown += (s, e) =>
        {
            e.Handled = true;
            ShowTaskContextMenu(task, row);
        };

        row.Child = grid;

        // Completed task opacity
        if (task.IsCompleted)
        {
            row.Opacity = _taskService.Config.CompletedOpacity;
        }

        return row;
    }

    private Style CreateDarkMenuItemStyle()
    {
        var style = new Style(typeof(MenuItem));
        var textColor = new WpfSolidColorBrush(WpfColor.FromRgb(0xE0, 0xE0, 0xE0));
        var bgColor = new WpfSolidColorBrush(WpfColor.FromRgb(0x1E, 0x1E, 0x1E));
        var hoverBg = new WpfSolidColorBrush(WpfColor.FromArgb(0x30, 0x4F, 0xC3, 0xF7));
        var borderColor = new WpfSolidColorBrush(WpfColor.FromArgb(0x30, 0xFF, 0xFF, 0xFF));

        style.Setters.Add(new Setter(MenuItem.ForegroundProperty, textColor));
        style.Setters.Add(new Setter(MenuItem.BackgroundProperty, bgColor));
        style.Setters.Add(new Setter(MenuItem.BorderBrushProperty, WpfBrushes.Transparent));
        style.Setters.Add(new Setter(MenuItem.PaddingProperty, new Thickness(8, 4, 8, 4)));

        var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(MenuItem.BackgroundProperty, hoverBg));
        hoverTrigger.Setters.Add(new Setter(MenuItem.BorderBrushProperty, borderColor));
        style.Triggers.Add(hoverTrigger);

        return style;
    }

    private void ShowTaskContextMenu(TaskItem task, Border row)
    {
        var menuItemStyle = CreateDarkMenuItemStyle();
        var menuBg = new WpfSolidColorBrush(WpfColor.FromRgb(0x1E, 0x1E, 0x1E));
        var textColor = new WpfSolidColorBrush(WpfColor.FromRgb(0xE0, 0xE0, 0xE0));

        var menu = new ContextMenu
        {
            Background = menuBg,
            BorderBrush = new WpfSolidColorBrush(WpfColor.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
            Foreground = textColor
        };
        menu.Resources[typeof(MenuItem)] = menuItemStyle;

        // Priority submenu
        var priorityHeader = new MenuItem { Header = "Priority" };
        foreach (var p in new[] { "high", "normal", "low" })
        {
            var priority = p;
            var item = new MenuItem
            {
                Header = char.ToUpper(priority[0]) + priority.Substring(1) + (task.Priority == priority ? " \u2713" : "")
            };
            item.Click += async (s, e) => await _taskService.SetTaskPriorityAsync(task.Id, priority);
            priorityHeader.Items.Add(item);
        }
        menu.Items.Add(priorityHeader);

        // Category submenu
        var categoryHeader = new MenuItem { Header = "Category" };
        var noneItem = new MenuItem
        {
            Header = "None" + (task.Category == null ? " \u2713" : "")
        };
        noneItem.Click += async (s, e) => await _taskService.SetTaskCategoryAsync(task.Id, null);
        categoryHeader.Items.Add(noneItem);

        foreach (var cat in _taskService.Config.Categories)
        {
            var category = cat;
            var item = new MenuItem
            {
                Header = category + (task.Category == category ? " \u2713" : "")
            };
            item.Click += async (s, e) => await _taskService.SetTaskCategoryAsync(task.Id, category);
            categoryHeader.Items.Add(item);
        }
        menu.Items.Add(categoryHeader);

        menu.Items.Add(new Separator());

        // Delete
        var deleteItem = new MenuItem
        {
            Header = "Delete",
            Foreground = new WpfSolidColorBrush(WpfColor.FromRgb(0xFF, 0x52, 0x52))
        };
        deleteItem.Click += async (s, e) => await _taskService.DeleteTaskAsync(task.Id);
        menu.Items.Add(deleteItem);

        row.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void RenderSearchResults(List<TaskItem> results)
    {
        SearchResultsPanel.Children.Clear();

        if (results.Count == 0)
        {
            SearchResultsScroller.Visibility = Visibility.Collapsed;
            return;
        }

        SearchResultsScroller.Visibility = Visibility.Visible;

        string? lastDate = null;
        foreach (var task in results)
        {
            // Date group header
            if (task.Date != lastDate)
            {
                lastDate = task.Date;
                if (DateTime.TryParse(task.Date, out var date))
                {
                    var header = new TextBlock
                    {
                        Text = date.ToString("MMM d, yyyy"),
                        FontSize = 10,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (WpfBrush)FindResource("DimTextBrush"),
                        Margin = new Thickness(8, 6, 0, 2)
                    };
                    SearchResultsPanel.Children.Add(header);
                }
            }

            SearchResultsPanel.Children.Add(CreateTaskRow(task));
        }
    }
}
