using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

        Loaded += (_, _) => RenderState();
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

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OpenSelectedResult();
    }

    private void ResultsList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            OpenSelectedResult();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control && ResultsList.SelectedItem is SmartProjectSearchResult selected)
        {
            System.Windows.Clipboard.SetText(selected.Path);
            StatusText.Text = "Copied file path to clipboard.";
            e.Handled = true;
        }
    }

    private void OpenSelectedResult()
    {
        if (ResultsList.SelectedItem is not SmartProjectSearchResult result)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = result.Path,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to open file: {ex.Message}";
        }
    }

    private void RenderState()
    {
        ProjectLabel.Text = _service.ActiveProjectLabel;
        ResultsList.ItemsSource = _service.Results;
        StatusText.Text = _service.StatusText;

        if (_service.IsScanning)
            StatusText.Text = "Scanning selected project...";
    }
}
