using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using HeicConvert.Core;
namespace HeicConvert.App;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<string> _queue = [];
    private readonly UiSettings _settings = UiSettings.Load();
    private CancellationTokenSource? _convertCts;

    public MainWindow()
    {
        InitializeComponent();
        QueueList.ItemsSource = _queue;
        _queue.CollectionChanged += (_, _) => UpdateQueueHint();

        OutputDirBox.Text = _settings.OutputDirectory;
        foreach (ComboBoxItem item in FormatCombo.Items)
        {
            if (item.Tag is string tag && tag == _settings.Format)
            {
                FormatCombo.SelectedItem = item;
                break;
            }
        }

        if (FormatCombo.SelectedItem == null)
        {
            FormatCombo.SelectedIndex = 0;
        }

        QualitySlider.Value = _settings.Quality;
        RecursiveCheck.IsChecked = _settings.Recursive;
        OverwriteCheck.IsChecked = _settings.Overwrite;

        UpdateQualityLabel();
        UpdateQueueHint();
    }

    private void UpdateQueueHint()
    {
        QueueEmptyHint.Visibility = _queue.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateQualityLabel()
    {
        var q = (int)QualitySlider.Value;
        var hint = q >= 85 ? "high quality" : q >= 60 ? "balanced" : "smaller files";
        QualityLabel.Text = $"{q} — {hint}";
    }

    private void QualitySlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (QualityLabel != null)
        {
            UpdateQualityLabel();
        }
    }

    private void DropZone_OnPreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            e.Effects = System.Windows.DragDropEffects.Copy;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void DropZone_OnDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        if (e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        EnqueuePaths(paths);
    }

    private void EnqueuePaths(IEnumerable<string> paths)
    {
        foreach (var raw in paths)
        {
            var full = Path.GetFullPath(raw);
            if (!File.Exists(full) && !Directory.Exists(full))
            {
                continue;
            }

            if (!_queue.Contains(full, StringComparer.OrdinalIgnoreCase))
            {
                _queue.Add(full);
            }
        }
    }

    private void BrowseOutput_OnClick(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose output folder for converted images.",
            UseDescriptionForTitle = true,
            SelectedPath = Directory.Exists(OutputDirBox.Text.Trim()) ? OutputDirBox.Text.Trim() : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            OutputDirBox.Text = dlg.SelectedPath;
        }
    }

    private void AddFiles_OnClick(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "HEIC / HEIF images|*.heic;*.heif|All files|*.*"
        };

        if (dlg.ShowDialog() == true)
        {
            EnqueuePaths(dlg.FileNames);
        }
    }

    private void AddFolder_OnClick(object sender, RoutedEventArgs e)
    {
        using var dlg = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Choose a folder containing HEIC images.",
            UseDescriptionForTitle = true
        };

        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            EnqueuePaths(new[] { dlg.SelectedPath });
        }
    }

    private void Clear_OnClick(object sender, RoutedEventArgs e)
    {
        _queue.Clear();
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e)
    {
        _convertCts?.Cancel();
    }

    private async void Convert_OnClick(object sender, RoutedEventArgs e)
    {
        if (_queue.Count == 0)
        {
            System.Windows.MessageBox.Show(this, "Add at least one file or folder to the queue.", "HEIC Convert", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var outputDir = OutputDirBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputDir))
        {
            System.Windows.MessageBox.Show(this, "Choose an output folder.", "HEIC Convert", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var format = GetSelectedFormat();
        if (format is not ("jpg" or "png"))
        {
            System.Windows.MessageBox.Show(this, "Format must be JPEG or PNG.", "HEIC Convert", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var quality = (int)QualitySlider.Value;
        if (quality is < 1 or > 100)
        {
            System.Windows.MessageBox.Show(this, "Quality must be between 1 and 100.", "HEIC Convert", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var recursive = RecursiveCheck.IsChecked == true;
        var overwrite = OverwriteCheck.IsChecked == true;

        _settings.OutputDirectory = outputDir;
        _settings.Format = format;
        _settings.Quality = quality;
        _settings.Recursive = recursive;
        _settings.Overwrite = overwrite;
        _settings.Save();

        try
        {
            Directory.CreateDirectory(outputDir);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Could not create output folder:\n{ex.Message}", "HEIC Convert", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _convertCts = new CancellationTokenSource();
        var token = _convertCts.Token;

        SetConvertingUi(true);
        StatusText.Text = "Preparing…";
        ProgressBar.Value = 0;

        var paths = _queue.ToList();
        int grandTotal;
        try
        {
            grandTotal = await Task.Run(() => paths.Sum(p => HeicConverter.CollectSourceFiles(p, recursive).Count()), token);
        }
        catch (OperationCanceledException)
        {
            SetConvertingUi(false);
            return;
        }

        if (grandTotal == 0)
        {
            StatusText.Text = "No HEIC or HEIF files found in the queue.";
            ProgressBar.Maximum = 1;
            ProgressBar.Value = 0;
            SetConvertingUi(false);
            System.Windows.MessageBox.Show(this, "No .heic or .heif files were found for the current queue and settings.", "HEIC Convert", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        ProgressBar.Maximum = grandTotal;
        ProgressBar.Value = 0;

        var converted = 0;
        var skipped = 0;
        var failed = 0;
        var processedOffset = 0;

        try
        {
            foreach (var root in paths)
            {
                token.ThrowIfCancellationRequested();

                var opts = new ConvertOptions
                {
                    InputPath = root,
                    OutputDirectory = outputDir,
                    Format = format,
                    Quality = quality,
                    Recursive = recursive,
                    Overwrite = overwrite
                };

                var progress = new Progress<ConversionProgress>(p =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        ProgressBar.Value = processedOffset + p.CurrentIndex;
                        StatusText.Text = $"Converting {processedOffset + p.CurrentIndex} of {grandTotal}";
                    });
                });

                var batch = await Task.Run(
                    () => HeicConverter.RunConversion(
                        opts,
                        progress,
                        cancellationToken: token),
                    token);

                converted += batch.Converted;
                skipped += batch.Skipped;
                failed += batch.Failed;
                processedOffset += batch.TotalSourceFiles;
            }

            StatusText.Text = $"Done. Converted {converted}, skipped {skipped}, failed {failed}.";
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "HEIC Convert", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Error.";
        }
        finally
        {
            SetConvertingUi(false);
            _convertCts?.Dispose();
            _convertCts = null;
        }
    }

    private string GetSelectedFormat()
    {
        if (FormatCombo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
        {
            return HeicConverter.NormalizeFormat(tag);
        }

        return "jpg";
    }

    private void SetConvertingUi(bool busy)
    {
        ConvertBtn.IsEnabled = !busy;
        ClearBtn.IsEnabled = !busy;
        AddFilesBtn.IsEnabled = !busy;
        AddFolderBtn.IsEnabled = !busy;
        BrowseOutputBtn.IsEnabled = !busy;
        DropZone.AllowDrop = !busy;
        QueueList.IsEnabled = !busy;
        CancelBtn.IsEnabled = busy;
        OutputDirBox.IsEnabled = !busy;
        FormatCombo.IsEnabled = !busy;
        QualitySlider.IsEnabled = !busy;
        RecursiveCheck.IsEnabled = !busy;
        OverwriteCheck.IsEnabled = !busy;
    }
}
