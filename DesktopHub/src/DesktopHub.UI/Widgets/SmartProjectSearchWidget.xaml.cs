using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using DesktopHub.Core.Models;
using DesktopHub.UI.Services;

namespace DesktopHub.UI.Widgets;

public partial class SmartProjectSearchWidget : System.Windows.Controls.UserControl
{
    private readonly SmartProjectSearchService _service;
    private CancellationTokenSource? _queryCts;

    public SmartProjectSearchWidget(SmartProjectSearchService service)
    {
        InitializeComponent();
        _service = service;

        _service.StateChanged += (_, _) => Dispatcher.Invoke(RenderState);
        _service.ScanningChanged += (_, _) => Dispatcher.Invoke(RenderState);

        Loaded += (_, _) =>
        {
            RenderState();
            SearchBox.Focus();
        };
    }

    public void FocusSearchBox()
    {
        Dispatcher.BeginInvoke(new Action(() => SearchBox.Focus()), System.Windows.Threading.DispatcherPriority.Input);
    }

    public async Task SetProjectAsync(string? projectPath, string? projectName = null)
    {
        await _service.SetProjectAsync(projectPath, projectName);
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _queryCts?.Cancel();
        _queryCts = new CancellationTokenSource();
        var token = _queryCts.Token;

        try
        {
            await Task.Delay(120, token);
            await _service.SetQueryAsync(SearchBox.Text);
        }
        catch (OperationCanceledException)
        {
            // Intentionally ignored for rapid typing debounce.
        }
    }

    private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Down && ResultsList.Items.Count > 0)
        {
            ResultsList.SelectedIndex = 0;
            var item = ResultsList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem;
            item?.Focus();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            if (!string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Clear();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && ResultsList.Items.Count > 0)
        {
            if (ResultsList.SelectedIndex < 0)
                ResultsList.SelectedIndex = 0;
            OpenSelectedResult();
            e.Handled = true;
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenSelectedResult();
    }

    private void ResultsList_PreviewMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var item = FindAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item == null) return;

        ResultsList.SelectedItem = item.DataContext;
        if (ResultsList.SelectedItem is not DocumentItem result) return;

        var menu = CreateDarkContextMenu();

        var openItem = new MenuItem { Header = "Open" };
        openItem.Click += (_, _) => OpenFile(result.Path);
        menu.Items.Add(openItem);

        var openFolderItem = new MenuItem { Header = "Open Containing Folder" };
        openFolderItem.Click += (_, _) => OpenContainingFolder(result.Path);
        menu.Items.Add(openFolderItem);

        menu.Items.Add(new Separator());

        var copyPathItem = new MenuItem { Header = "Copy Full Path" };
        copyPathItem.Click += (_, _) =>
        {
            System.Windows.Clipboard.SetText(result.Path);
            StatusText.Text = "Copied file path to clipboard.";
        };
        menu.Items.Add(copyPathItem);

        var copyNameItem = new MenuItem { Header = "Copy File Name" };
        copyNameItem.Click += (_, _) =>
        {
            System.Windows.Clipboard.SetText(result.FileName);
            StatusText.Text = "Copied file name to clipboard.";
        };
        menu.Items.Add(copyNameItem);

        ResultsList.ContextMenu = menu;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void ResultsList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OpenSelectedResult();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            SearchBox.Focus();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && ResultsList.SelectedItem is DocumentItem selected)
        {
            System.Windows.Clipboard.SetText(selected.Path);
            StatusText.Text = "Copied file path to clipboard.";
            e.Handled = true;
        }
    }

    private void OpenSelectedResult()
    {
        if (ResultsList.SelectedItem is not DocumentItem result)
            return;

        OpenFile(result.Path);
    }

    private void OpenFile(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to open file: {ex.Message}";
        }
    }

    private void OpenContainingFolder(string filePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (dir != null && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{filePath}\"",
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to open folder: {ex.Message}";
        }
    }

    private void RenderState()
    {
        var results = _service.Results;
        ProjectLabel.Text = _service.ActiveProjectLabel;
        ResultsList.ItemsSource = results;
        ScanningProgress.Visibility = _service.IsScanning ? Visibility.Visible : Visibility.Collapsed;
        StatusText.Text = _service.IsScanning
            ? "Scanning selected project..."
            : _service.StatusText;

        if (results.Count > 0)
        {
            var top = results[0];
            DebugLogger.Log($"SmartSearch UI: RenderState count={results.Count} top='{top.RelativePath}' scanning={_service.IsScanning}");
        }
        else
        {
            DebugLogger.Log($"SmartSearch UI: RenderState count=0 scanning={_service.IsScanning} status='{_service.StatusText}'");
        }

        if (_service.ActiveProjectLabel != "No project selected")
        {
            HintText.Text = $"Search in {_service.ActiveProjectLabel}";
        }
        else
        {
            HintText.Text = "Try: fault current letter | fpl::pdf | fpl::pdf|word | latest fault current letter";
        }
    }

    private static ContextMenu CreateDarkContextMenu()
    {
        var menuBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
        var menuBorder = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        var itemFg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0xF7, 0xFA));
        var itemHoverBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));

        var itemTemplate = new ControlTemplate(typeof(MenuItem));
        var itemBorderFactory = new FrameworkElementFactory(typeof(Border));
        itemBorderFactory.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
        itemBorderFactory.SetValue(Border.PaddingProperty, new Thickness(12, 6, 12, 6));
        itemBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        var contentPresenter = new FrameworkElementFactory(typeof(ContentPresenter));
        contentPresenter.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        contentPresenter.SetValue(TextBlock.ForegroundProperty, itemFg);
        contentPresenter.SetValue(TextBlock.FontSizeProperty, 13.0);
        itemBorderFactory.AppendChild(contentPresenter);
        var itemTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        itemTrigger.Setters.Add(new Setter(Border.BackgroundProperty, itemHoverBg, "ItemBorder"));
        itemTemplate.VisualTree = itemBorderFactory;
        itemTemplate.VisualTree.Name = "ItemBorder";

        var itemStyle = new Style(typeof(MenuItem));
        itemStyle.Setters.Add(new Setter(MenuItem.TemplateProperty, itemTemplate));
        itemStyle.Setters.Add(new Setter(MenuItem.CursorProperty, System.Windows.Input.Cursors.Hand));

        var contextMenuTemplate = new ControlTemplate(typeof(ContextMenu));
        var menuBorderFactory = new FrameworkElementFactory(typeof(Border));
        menuBorderFactory.SetValue(Border.BackgroundProperty, menuBg);
        menuBorderFactory.SetValue(Border.BorderBrushProperty, menuBorder);
        menuBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        menuBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        menuBorderFactory.SetValue(Border.PaddingProperty, new Thickness(4));
        var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        menuBorderFactory.AppendChild(itemsPresenter);
        contextMenuTemplate.VisualTree = menuBorderFactory;

        var menu = new ContextMenu
        {
            Template = contextMenuTemplate,
            HasDropShadow = false
        };
        menu.Resources[typeof(MenuItem)] = itemStyle;
        return menu;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
                return match;
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
