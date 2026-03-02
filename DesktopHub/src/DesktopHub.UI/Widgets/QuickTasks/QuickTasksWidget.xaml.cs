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
        Dispatcher.BeginInvoke(() =>
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

                // Track task creation (char count only, not content)
                var (active, _) = await _taskService.GetCountsAsync();
                TelemetryAccessor.TrackQuickTask(
                    TelemetryEventType.TaskCreated,
                    charCount: title.Length,
                    taskCountAtTime: active);

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

        if (_taskService.IsToday)
        {
            TodayButtonText.Text = "Today";
            TodayButtonText.Foreground = (WpfBrush)FindResource("AccentBrush");
        }
        else
        {
            TodayButtonText.Text = date.ToString("MMM d");
            TodayButtonText.Foreground = (WpfBrush)FindResource("DimTextBrush");
        }
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

            // Track task completion toggle
            TelemetryAccessor.TrackQuickTask(
                task.IsCompleted ? TelemetryEventType.TaskCreated : TelemetryEventType.TaskCompleted);
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

            // Track task deletion
            TelemetryAccessor.TrackQuickTask(TelemetryEventType.TaskDeleted);
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

    private static ContextMenu CreateDarkContextMenu()
    {
        var menuBg = new WpfSolidColorBrush(WpfColor.FromRgb(0x1E, 0x1E, 0x1E));
        var menuBorder = new WpfSolidColorBrush(WpfColor.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        var itemFg = new WpfSolidColorBrush(WpfColor.FromRgb(0xE0, 0xE0, 0xE0));
        var hoverBg = new WpfSolidColorBrush(WpfColor.FromArgb(0x30, 0x4F, 0xC3, 0xF7));
        var transparentBrush = WpfBrushes.Transparent;

        // Build a MenuItem ControlTemplate that fully replaces WPF default chrome
        var itemTemplate = new ControlTemplate(typeof(MenuItem));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "Bd";
        borderFactory.SetValue(Border.BackgroundProperty, transparentBrush);
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 6, 10, 6));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFactory.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(contentFactory);
        itemTemplate.VisualTree = borderFactory;

        var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "Bd"));
        itemTemplate.Triggers.Add(hoverTrigger);

        // MenuItem style using the custom template
        var itemStyle = new Style(typeof(MenuItem));
        itemStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty, itemFg));
        itemStyle.Setters.Add(new Setter(MenuItem.TemplateProperty, itemTemplate));
        itemStyle.Setters.Add(new Setter(MenuItem.CursorProperty, System.Windows.Input.Cursors.Hand));

        // ContextMenu with custom template to remove system chrome
        var contextMenuTemplate = new ControlTemplate(typeof(ContextMenu));
        var menuBorderFactory = new FrameworkElementFactory(typeof(Border));
        menuBorderFactory.SetValue(Border.BackgroundProperty, menuBg);
        menuBorderFactory.SetValue(Border.BorderBrushProperty, menuBorder);
        menuBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        menuBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        menuBorderFactory.SetValue(Border.PaddingProperty, new Thickness(2, 4, 2, 4));

        var shadowEffect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 12,
            ShadowDepth = 2,
            Opacity = 0.5,
            Color = WpfColor.FromRgb(0, 0, 0)
        };
        menuBorderFactory.SetValue(Border.EffectProperty, shadowEffect);

        var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        menuBorderFactory.AppendChild(itemsPresenter);
        contextMenuTemplate.VisualTree = menuBorderFactory;

        // Separator with custom template
        var sepTemplate = new ControlTemplate(typeof(Separator));
        var sepBorderFactory = new FrameworkElementFactory(typeof(Border));
        sepBorderFactory.SetValue(Border.BackgroundProperty,
            new WpfSolidColorBrush(WpfColor.FromArgb(0x20, 0xFF, 0xFF, 0xFF)));
        sepBorderFactory.SetValue(Border.HeightProperty, 1.0);
        sepBorderFactory.SetValue(Border.MarginProperty, new Thickness(8, 4, 8, 4));
        sepTemplate.VisualTree = sepBorderFactory;
        var sepStyle = new Style(typeof(Separator));
        sepStyle.Setters.Add(new Setter(Separator.TemplateProperty, sepTemplate));

        var menu = new ContextMenu
        {
            Template = contextMenuTemplate,
            HasDropShadow = false
        };
        menu.Resources[typeof(MenuItem)] = itemStyle;
        menu.Resources[typeof(Separator)] = sepStyle;

        return menu;
    }

    private static MenuItem CreateDarkSubmenuItem(string header)
    {
        var menuBg = new WpfSolidColorBrush(WpfColor.FromRgb(0x1E, 0x1E, 0x1E));
        var menuBorder = new WpfSolidColorBrush(WpfColor.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        var itemFg = new WpfSolidColorBrush(WpfColor.FromRgb(0xE0, 0xE0, 0xE0));
        var hoverBg = new WpfSolidColorBrush(WpfColor.FromArgb(0x30, 0x4F, 0xC3, 0xF7));
        var transparentBrush = WpfBrushes.Transparent;

        // SubmenuHeader ControlTemplate with PART_Popup
        var template = new ControlTemplate(typeof(MenuItem));

        var gridFactory = new FrameworkElementFactory(typeof(Grid));

        // Visible row
        var bdFactory = new FrameworkElementFactory(typeof(Border));
        bdFactory.Name = "Bd";
        bdFactory.SetValue(Border.BackgroundProperty, transparentBrush);
        bdFactory.SetValue(Border.PaddingProperty, new Thickness(10, 6, 10, 6));
        bdFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        bdFactory.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));

        var dockFactory = new FrameworkElementFactory(typeof(DockPanel));

        // Arrow indicator on the right
        var arrowFactory = new FrameworkElementFactory(typeof(TextBlock));
        arrowFactory.SetValue(DockPanel.DockProperty, Dock.Right);
        arrowFactory.SetValue(TextBlock.TextProperty, "\u203A");
        arrowFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
        arrowFactory.SetValue(TextBlock.ForegroundProperty,
            new WpfSolidColorBrush(WpfColor.FromArgb(0x60, 0xE0, 0xE0, 0xE0)));
        arrowFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        arrowFactory.SetValue(TextBlock.MarginProperty, new Thickness(16, 0, 0, 0));
        dockFactory.AppendChild(arrowFactory);

        // Header content
        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        dockFactory.AppendChild(contentFactory);

        bdFactory.AppendChild(dockFactory);
        gridFactory.AppendChild(bdFactory);

        // Popup for submenu items
        var popupFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.Primitives.Popup));
        popupFactory.Name = "PART_Popup";
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.PlacementProperty,
            System.Windows.Controls.Primitives.PlacementMode.Right);
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.AllowsTransparencyProperty, true);
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.HorizontalOffsetProperty, -2.0);
        popupFactory.SetValue(System.Windows.Controls.Primitives.Popup.VerticalOffsetProperty, -4.0);
        popupFactory.SetBinding(System.Windows.Controls.Primitives.Popup.IsOpenProperty,
            new System.Windows.Data.Binding("IsSubmenuOpen")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)
            });

        // Dark popup border
        var popupBorderFactory = new FrameworkElementFactory(typeof(Border));
        popupBorderFactory.SetValue(Border.BackgroundProperty, menuBg);
        popupBorderFactory.SetValue(Border.BorderBrushProperty, menuBorder);
        popupBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        popupBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        popupBorderFactory.SetValue(Border.PaddingProperty, new Thickness(2, 4, 2, 4));
        var popupShadow = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 12, ShadowDepth = 2, Opacity = 0.5,
            Color = WpfColor.FromRgb(0, 0, 0)
        };
        popupBorderFactory.SetValue(Border.EffectProperty, popupShadow);

        var popupItemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        popupBorderFactory.AppendChild(popupItemsPresenter);
        popupFactory.AppendChild(popupBorderFactory);

        gridFactory.AppendChild(popupFactory);
        template.VisualTree = gridFactory;

        // Hover trigger
        var subHoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        subHoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "Bd"));
        template.Triggers.Add(subHoverTrigger);

        // Leaf item style for child items
        var leafTemplate = new ControlTemplate(typeof(MenuItem));
        var leafBorder = new FrameworkElementFactory(typeof(Border));
        leafBorder.Name = "Bd";
        leafBorder.SetValue(Border.BackgroundProperty, transparentBrush);
        leafBorder.SetValue(Border.PaddingProperty, new Thickness(10, 6, 10, 6));
        leafBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        leafBorder.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));
        var leafContent = new FrameworkElementFactory(typeof(ContentPresenter));
        leafContent.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        leafContent.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        leafBorder.AppendChild(leafContent);
        leafTemplate.VisualTree = leafBorder;
        var leafHover = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        leafHover.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "Bd"));
        leafTemplate.Triggers.Add(leafHover);

        var leafStyle = new Style(typeof(MenuItem));
        leafStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty, itemFg));
        leafStyle.Setters.Add(new Setter(MenuItem.TemplateProperty, leafTemplate));
        leafStyle.Setters.Add(new Setter(MenuItem.CursorProperty, System.Windows.Input.Cursors.Hand));

        var menuItem = new MenuItem
        {
            Header = header,
            Template = template,
            Foreground = itemFg,
            Cursor = System.Windows.Input.Cursors.Hand,
            ItemContainerStyle = leafStyle
        };

        return menuItem;
    }

    private void ShowTaskContextMenu(TaskItem task, Border row)
    {
        var menu = CreateDarkContextMenu();

        // Priority submenu
        var priorityHeader = CreateDarkSubmenuItem("Priority");
        foreach (var p in new[] { "high", "normal", "low" })
        {
            var priority = p;
            var item = new MenuItem
            {
                Header = char.ToUpper(priority[0]) + priority.Substring(1) + (task.Priority == priority ? "  \u2713" : "")
            };
            item.Click += async (s, e) => await _taskService.SetTaskPriorityAsync(task.Id, priority);
            priorityHeader.Items.Add(item);
        }
        menu.Items.Add(priorityHeader);

        // Category submenu
        var categoryHeader = CreateDarkSubmenuItem("Category");
        var noneItem = new MenuItem
        {
            Header = "None" + (task.Category == null ? "  \u2713" : "")
        };
        noneItem.Click += async (s, e) => await _taskService.SetTaskCategoryAsync(task.Id, null);
        categoryHeader.Items.Add(noneItem);

        foreach (var cat in _taskService.Config.Categories)
        {
            var category = cat;
            var item = new MenuItem
            {
                Header = category + (task.Category == category ? "  \u2713" : "")
            };
            item.Click += async (s, e) => await _taskService.SetTaskCategoryAsync(task.Id, category);
            categoryHeader.Items.Add(item);
        }
        menu.Items.Add(categoryHeader);

        // Move to Date submenu
        var moveHeader = CreateDarkSubmenuItem("Move to Date");

        var todayItem = new MenuItem { Header = "Today" };
        todayItem.Click += async (s, e) => await _taskService.MoveTaskToDateAsync(task.Id, DateTime.Now.Date);
        moveHeader.Items.Add(todayItem);

        var tomorrowItem = new MenuItem { Header = "Tomorrow" };
        tomorrowItem.Click += async (s, e) => await _taskService.MoveTaskToDateAsync(task.Id, DateTime.Now.Date.AddDays(1));
        moveHeader.Items.Add(tomorrowItem);

        var nextMondayDate = DateTime.Now.Date;
        while (nextMondayDate.DayOfWeek != DayOfWeek.Monday) nextMondayDate = nextMondayDate.AddDays(1);
        if (nextMondayDate > DateTime.Now.Date)
        {
            var nextMonItem = new MenuItem { Header = $"Next Monday ({nextMondayDate:MMM d})" };
            nextMonItem.Click += async (s, e) => await _taskService.MoveTaskToDateAsync(task.Id, nextMondayDate);
            moveHeader.Items.Add(nextMonItem);
        }

        var pickDateItem = new MenuItem { Header = "Pick Date..." };
        pickDateItem.Click += (s, e) => ShowDatePickerForTask(task.Id);
        moveHeader.Items.Add(pickDateItem);

        menu.Items.Add(moveHeader);

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

    private void ShowDatePickerForTask(string taskId)
    {
        ShowDatePickerPopup(async (date) =>
        {
            await _taskService.MoveTaskToDateAsync(taskId, date);
        });
    }

    private void ShowDatePickerPopup(Func<DateTime, Task> onDateSelected)
    {
        var bgBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0x1E, 0x1E, 0x1E));
        var textBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0xE0, 0xE0, 0xE0));
        var accentBrush = new WpfSolidColorBrush(WpfColor.FromRgb(0x4F, 0xC3, 0xF7));
        var borderBrush = new WpfSolidColorBrush(WpfColor.FromArgb(0x40, 0xFF, 0xFF, 0xFF));

        // Use a borderless window so the picker can be dragged
        var pickerWindow = new Window
        {
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = WpfBrushes.Transparent,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            Topmost = true,
            ShowInTaskbar = false
        };

        // Position near the mouse
        var mousePos = System.Windows.Forms.Control.MousePosition;
        pickerWindow.Left = mousePos.X - 20;
        pickerWindow.Top = mousePos.Y - 20;

        var calendar = new System.Windows.Controls.Calendar
        {
            SelectedDate = DateTime.TryParse(_taskService.CurrentDate, out var currentDate) ? currentDate : DateTime.Now.Date,
            Background = bgBrush,
            Foreground = textBrush,
            BorderThickness = new Thickness(0),
            FontSize = 14,
            LayoutTransform = new System.Windows.Media.ScaleTransform(1.5, 1.5)
        };

        calendar.SelectedDatesChanged += async (s, e) =>
        {
            if (calendar.SelectedDate.HasValue)
            {
                pickerWindow.Close();
                await onDateSelected(calendar.SelectedDate.Value);
            }
        };

        // Walk the visual tree after load and force dark styling on everything
        calendar.Loaded += (s, e) => ApplyDarkCalendarTheme(calendar, bgBrush, textBrush, accentBrush);

        // Drag handle at the top
        var dragBar = new Border
        {
            Background = new WpfSolidColorBrush(WpfColor.FromRgb(0x28, 0x28, 0x28)),
            Height = 28,
            CornerRadius = new CornerRadius(8, 8, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        var dragBarContent = new DockPanel { LastChildFill = true };
        var titleText = new TextBlock
        {
            Text = "Select Date",
            Foreground = textBrush,
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0)
        };
        var closeBtn = new TextBlock
        {
            Text = "\u2715",
            Foreground = new WpfSolidColorBrush(WpfColor.FromArgb(0x80, 0xE0, 0xE0, 0xE0)),
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            Cursor = System.Windows.Input.Cursors.Hand
        };
        closeBtn.MouseLeftButtonDown += (s, e) => pickerWindow.Close();
        DockPanel.SetDock(closeBtn, Dock.Right);
        dragBarContent.Children.Add(closeBtn);
        dragBarContent.Children.Add(titleText);
        dragBar.Child = dragBarContent;
        dragBar.MouseLeftButtonDown += (s, e) => pickerWindow.DragMove();

        var calendarContainer = new Border
        {
            Background = bgBrush,
            Padding = new Thickness(8, 4, 8, 8),
            Child = calendar
        };

        var mainStack = new StackPanel();
        mainStack.Children.Add(dragBar);
        mainStack.Children.Add(calendarContainer);

        var outerBorder = new Border
        {
            Background = bgBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            ClipToBounds = true,
            Child = mainStack,
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 4,
                Opacity = 0.6,
                Color = WpfColor.FromRgb(0, 0, 0)
            }
        };

        pickerWindow.Content = outerBorder;

        // Close when clicking outside
        pickerWindow.Deactivated += (s, e) =>
        {
            try { pickerWindow.Close(); } catch { }
        };

        pickerWindow.Show();
    }

    private static void ApplyDarkCalendarTheme(DependencyObject root,
        WpfSolidColorBrush bgBrush, WpfSolidColorBrush textBrush, WpfSolidColorBrush accentBrush)
    {
        var childCount = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < childCount; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);

            // Style all TextBlocks (day-of-week headers, month/year label, etc.)
            if (child is TextBlock tb)
            {
                tb.Foreground = textBrush;
            }

            // Style Buttons (nav arrows, header button) with clean rounded appearance
            if (child is System.Windows.Controls.Button btn)
            {
                btn.Foreground = textBrush;
                btn.Background = WpfBrushes.Transparent;
                btn.BorderBrush = WpfBrushes.Transparent;
                btn.Cursor = System.Windows.Input.Cursors.Hand;

                // Replace Previous/Next text content with < and >
                var content = btn.Content?.ToString() ?? "";
                if (content.StartsWith("Prev", StringComparison.OrdinalIgnoreCase))
                {
                    btn.Content = "<";
                    btn.FontSize = 14;
                    btn.FontWeight = FontWeights.Bold;
                }
                else if (content.StartsWith("Next", StringComparison.OrdinalIgnoreCase))
                {
                    btn.Content = ">";
                    btn.FontSize = 14;
                    btn.FontWeight = FontWeights.Bold;
                }

                // Replace default button template with a clean rounded one
                var btnTemplate = new ControlTemplate(typeof(System.Windows.Controls.Button));
                var btnBorder = new FrameworkElementFactory(typeof(Border));
                btnBorder.Name = "BtnBd";
                btnBorder.SetValue(Border.BackgroundProperty, WpfBrushes.Transparent);
                btnBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
                btnBorder.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));
                var btnContent = new FrameworkElementFactory(typeof(ContentPresenter));
                btnContent.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
                btnContent.SetValue(ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);
                btnBorder.AppendChild(btnContent);
                btnTemplate.VisualTree = btnBorder;
                var btnHover = new Trigger { Property = System.Windows.Controls.Button.IsMouseOverProperty, Value = true };
                btnHover.Setters.Add(new Setter(Border.BackgroundProperty,
                    new WpfSolidColorBrush(WpfColor.FromArgb(0x30, 0x4F, 0xC3, 0xF7)), "BtnBd"));
                btnTemplate.Triggers.Add(btnHover);
                btn.Template = btnTemplate;
            }

            // Style Borders (remove any light backgrounds)
            if (child is Border bd)
            {
                if (bd.Background is WpfSolidColorBrush scb)
                {
                    var c = scb.Color;
                    // Replace any light/white backgrounds with dark
                    if (c.R > 0x60 && c.G > 0x60 && c.B > 0x60 && c.A > 0x20)
                    {
                        bd.Background = bgBrush;
                    }
                }
                bd.BorderBrush = WpfBrushes.Transparent;
            }

            // Style CalendarDayButtons
            if (child is System.Windows.Controls.Primitives.CalendarDayButton dayBtn)
            {
                dayBtn.Foreground = textBrush;
                dayBtn.Background = WpfBrushes.Transparent;
                dayBtn.BorderBrush = WpfBrushes.Transparent;
                if (dayBtn.IsToday)
                {
                    dayBtn.Foreground = accentBrush;
                    dayBtn.FontWeight = FontWeights.Bold;
                }
                if (dayBtn.IsSelected)
                {
                    dayBtn.Background = new WpfSolidColorBrush(WpfColor.FromArgb(0x60, 0x4F, 0xC3, 0xF7));
                    dayBtn.Foreground = WpfBrushes.White;
                }
                if (dayBtn.IsInactive)
                {
                    dayBtn.Foreground = new WpfSolidColorBrush(WpfColor.FromArgb(0x50, 0xE0, 0xE0, 0xE0));
                }
            }

            // Style CalendarButtons (month/year picker)
            if (child is System.Windows.Controls.Primitives.CalendarButton calBtn)
            {
                calBtn.Foreground = textBrush;
                calBtn.Background = WpfBrushes.Transparent;
                calBtn.BorderBrush = WpfBrushes.Transparent;
            }

            // Recurse into children
            ApplyDarkCalendarTheme(child, bgBrush, textBrush, accentBrush);
        }
    }

    private void DateLabel_Click(object sender, MouseButtonEventArgs e)
    {
        ShowDatePickerPopup(async (date) =>
        {
            await _taskService.GoToDateAsync(date);
        });
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
