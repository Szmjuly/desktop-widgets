using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using LoadExtractor.Core.Models;
using LoadExtractor.Core.Services;

namespace LoadExtractor.UI;

public partial class MainWindow : Window
{
    private readonly HapPdfExtractor _pdf1Extractor = new();
    private readonly DesignLoadExtractor _pdf2Extractor = new();
    private readonly AirSystemSizingExtractor _pdf3Extractor = new();
    private readonly DataCombiner _combiner = new();
    private readonly ExcelExporter _excelExporter = new();
    private readonly TranePdfExtractor _traneExtractor = new();
    private readonly TraneExcelExporter _traneExcelExporter = new();
    private readonly TraceDesignCoolingPdfExtractor _traceDesignCoolingExtractor = new();

    private string? _pdf1Path;
    private string? _pdf2Path;
    private string? _pdf3Path;
    private string? _tranePdfPath;
    private string? _traneDesignCoolingPdfPath;
    private bool _singlePdfModeEnabled;
    private bool _useCombinedPdf;
    private string? _combinedPdfPath;
    private Dictionary<PdfType, List<int>>? _combinedPageBuckets;
    private HapProject? _pdf1Data;
    private List<SpaceComponentLoads>? _pdf2Data;
    private List<AirSystemSizingData>? _pdf3Data;
    private List<CombinedSpaceData>? _combinedData;
    private List<TraneRoomLoad>? _traneData;
    private List<TraneRoomLoad>? _traneFilteredData;
    private bool _isUpdatingTraneFilters;
    private bool _isUpdatingTraneDeltaT;
    private string? _lastExportPath;
    private string? _lastTraneExportPath;

    private enum SidebarMode { All, BySystem, ByZone }
    private SidebarMode _currentMode = SidebarMode.All;

    private static readonly SolidColorBrush GreenDot = new(Color.FromRgb(76, 175, 80));

    public MainWindow()
    {
        InitializeComponent();
        ApplyExtractorPageChrome();
        Logger.Info("MainWindow initialized");
    }

    private void MainExtractorTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!Equals(sender, MainExtractorTabs))
            return;
        ApplyExtractorPageChrome();
    }

    private void ApplyExtractorPageChrome()
    {
        if (MainExtractorTabs.SelectedIndex <= 0)
        {
            PageExtractorTitle.Text = "HAP Extractor";
            PageExtractorSubtitle.Text =
                "Load Zone Sizing Summary + Space Design Load Summary PDFs to extract and export component loads";
            SinglePdfModeToggle.Visibility = Visibility.Visible;
        }
        else
        {
            PageExtractorTitle.Text = "Trane Extractor";
            PageExtractorSubtitle.Text =
                "Load a TRACE Room Checksums PDF to extract room loads, airflow, area, and engineering checks";
            SinglePdfModeToggle.Visibility = Visibility.Collapsed;
        }
    }

    private void BeginProcessing(string status)
    {
        ProcessingProgressBar.Visibility = Visibility.Visible;
        ProcessingProgressBar.IsIndeterminate = true;
        ProcessingProgressBar.Value = 0;

        ProcessButton.IsEnabled = false;
        ExportExcelButton.IsEnabled = false;
        MattsWayButton.IsEnabled = false;

        StatusText.Text = status;
    }

    private void ReportProgress(string status, double percent, bool indeterminate = false)
    {
        StatusText.Text = status;
        // Keep the bar as a consistent "working" indicator that matches the app theme.
        ProcessingProgressBar.IsIndeterminate = true;
        ProcessingProgressBar.Value = 0;
    }

    private void EndProcessing(string status)
    {
        ProcessingProgressBar.Visibility = Visibility.Collapsed;
        ProcessingProgressBar.IsIndeterminate = true;
        ProcessingProgressBar.Value = 0;
        StatusText.Text = status;

        UpdateProcessButton();
        ExportExcelButton.IsEnabled = _combinedData != null;
        MattsWayButton.IsEnabled = _combinedData != null;
        OpenExportLocationButton.IsEnabled = !string.IsNullOrEmpty(_lastExportPath) && File.Exists(_lastExportPath);
    }

    private void SinglePdfModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        _singlePdfModeEnabled = SinglePdfModeToggle.IsChecked == true;
        PdfInputRowsPanel.Visibility = _singlePdfModeEnabled ? Visibility.Collapsed : Visibility.Visible;
        CombinedPdfInputRow.Visibility = _singlePdfModeEnabled ? Visibility.Visible : Visibility.Collapsed;
        UpdateProcessButton();
        Logger.Info($"Single combined PDF mode: {(_singlePdfModeEnabled ? "ON" : "OFF")}");
    }

    // ── PDF Browse ──────────────────────────────────────────────

    private void BrowsePdf1_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Zone Sizing Summary PDF",
            Filter = "PDF Files (*.pdf)|*.pdf",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
            SetPdf1(dialog.FileName);
    }

    private void BrowsePdf2_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Space Design Load Summary PDF",
            Filter = "PDF Files (*.pdf)|*.pdf",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
            SetPdf2(dialog.FileName);
    }

    private void BrowsePdf3_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Air System Sizing Summary PDF",
            Filter = "PDF Files (*.pdf)|*.pdf",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
            SetPdf3(dialog.FileName);
    }

    private async void BrowseCombinedPdf_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Combined HAP PDF",
            Filter = "PDF Files (*.pdf)|*.pdf",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
            await LoadCombinedPdfAsync(dialog.FileName, processAfterLoad: false);
    }

    private void SetPdf1(string path)
    {
        ClearCombinedPdfState();
        _pdf1Path = path;
        Pdf1PathText.Text = path;
        Pdf1PathText.Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush");
        Pdf1StatusDot.Foreground = GreenDot;
        UpdateProcessButton();
        Logger.Info($"PDF 1 loaded: {path}");
        StatusText.Text = string.IsNullOrEmpty(_pdf2Path)
            ? "Zone Sizing Summary loaded. Load Space Design Load Summary to continue."
            : "Both PDFs loaded. Click 'Process & Combine' to extract data.";
    }

    private void SetPdf2(string path)
    {
        ClearCombinedPdfState();
        _pdf2Path = path;
        Pdf2PathText.Text = path;
        Pdf2PathText.Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush");
        Pdf2StatusDot.Foreground = GreenDot;
        UpdateProcessButton();
        Logger.Info($"PDF 2 loaded: {path}");
        StatusText.Text = string.IsNullOrEmpty(_pdf1Path)
            ? "Space Design Load Summary loaded. Load Zone Sizing Summary to continue."
            : "Both PDFs loaded. Click 'Process & Combine' to extract data.";
    }

    private void SetPdf3(string path)
    {
        ClearCombinedPdfState();
        _pdf3Path = path;
        Pdf3PathText.Text = path;
        Pdf3PathText.Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush");
        Pdf3StatusDot.Foreground = GreenDot;
        Logger.Info($"PDF 3 loaded: {path}");
        StatusText.Text = "Air System Sizing Summary loaded.";
    }

    // ── Drag & Drop ──────────────────────────────────────────────

    private void EmptyState_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            bool hasPdf = files.Any(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
            e.Effects = hasPdf ? DragDropEffects.Copy : DragDropEffects.None;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private async void EmptyState_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var pdfFiles = files.Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();

        if (pdfFiles.Count == 0)
        {
            StatusText.Text = "No PDF files found in dropped items.";
            return;
        }

        if (_singlePdfModeEnabled && pdfFiles.Count == 1)
        {
            await LoadCombinedPdfAsync(pdfFiles[0], processAfterLoad: true);
            return;
        }

        string? zoneSizingPath = null;
        string? designLoadPath = null;
        string? airSystemSizingPath = null;
        var unknowns = new List<string>();

        foreach (var pdf in pdfFiles)
        {
            var pdfType = PdfClassifier.Classify(pdf);
            switch (pdfType)
            {
                case PdfType.ZoneSizingSummary:
                    zoneSizingPath = pdf;
                    break;
                case PdfType.SpaceDesignLoadSummary:
                    designLoadPath = pdf;
                    break;
                case PdfType.AirSystemSizingSummary:
                    airSystemSizingPath = pdf;
                    break;
                default:
                    unknowns.Add(Path.GetFileName(pdf));
                    break;
            }
        }

        if (unknowns.Count > 0 && zoneSizingPath == null && designLoadPath == null && airSystemSizingPath == null)
        {
            MessageBox.Show(
                $"Could not identify PDF type for:\n\n{string.Join("\n", unknowns)}\n\n" +
                "Expected a Zone Sizing Summary, Space Design Load Summary, or Air System Sizing Summary from HAP.",
                "Unrecognized PDF", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (zoneSizingPath != null) SetPdf1(zoneSizingPath);
        if (designLoadPath != null) SetPdf2(designLoadPath);
        if (airSystemSizingPath != null) SetPdf3(airSystemSizingPath);

        // Auto-process if both are now loaded
        if (!string.IsNullOrEmpty(_pdf1Path) && !string.IsNullOrEmpty(_pdf2Path))
            await ProcessAsync();
    }

    private async Task LoadCombinedPdfAsync(string pdf, bool processAfterLoad)
    {
        try
        {
            BeginProcessing("Scanning combined PDF pages...");

            var buckets = await Task.Run(() =>
            {
                using var document = UglyToad.PdfPig.PdfDocument.Open(pdf);
                return PdfClassifier.ClassifyPages(document, assignUnknownToPrevious: true);
            });

            var zonePages = buckets.GetValueOrDefault(PdfType.ZoneSizingSummary) ?? new List<int>();
            var designPages = buckets.GetValueOrDefault(PdfType.SpaceDesignLoadSummary) ?? new List<int>();
            var airSizingPages = buckets.GetValueOrDefault(PdfType.AirSystemSizingSummary) ?? new List<int>();
            var unknownPages = buckets.GetValueOrDefault(PdfType.Unknown) ?? new List<int>();

            if (zonePages.Count == 0 || designPages.Count == 0)
            {
                EndProcessing("Combined PDF is missing required sections (Zone Sizing Summary and/or Design Load Summary).");
                MessageBox.Show(
                    "Could not find required sections in the combined PDF.\n\n" +
                    "Required:\n" +
                    "- Zone Sizing Summary\n" +
                    "- Space Design Load Summary\n\n" +
                    "Make sure the merged PDF contains those report pages and the headers are present.",
                    "Combined PDF Missing Sections", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetCombinedPdfState(pdf, buckets);

            var status = $"Combined PDF loaded: {zonePages.Count} zone pages, {designPages.Count} design pages";
            if (airSizingPages.Count > 0)
                status += $", {airSizingPages.Count} air sizing pages";
            if (unknownPages.Count > 0)
                status += $", {unknownPages.Count} unclassified pages";

            if (unknownPages.Count >= 5)
            {
                MessageBox.Show(
                    $"Some pages in the combined PDF could not be classified by header text.\n\n" +
                    $"Unclassified pages: {unknownPages.Count}\n\n" +
                    "This may still work if those pages are irrelevant, but if results look wrong, re-export from HAP or ensure the section headers are present on continuation pages.",
                    "Combined PDF Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            if (processAfterLoad)
            {
                StatusText.Text = status + ". Processing...";
                await ProcessAsync();
            }
            else
            {
                EndProcessing(status + ". Click 'Process & Combine' to extract data.");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to classify combined PDF", ex);
            EndProcessing($"Error: {ex.Message}");
            MessageBox.Show($"Failed to read combined PDF:\n\n{ex.Message}",
                "PDF Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateProcessButton()
    {
        if (_singlePdfModeEnabled && (!_useCombinedPdf || _combinedPageBuckets == null))
        {
            ProcessButton.IsEnabled = false;
            return;
        }

        if (_useCombinedPdf && _combinedPageBuckets != null)
        {
            var hasZone = _combinedPageBuckets.TryGetValue(PdfType.ZoneSizingSummary, out var zonePages) && zonePages.Count > 0;
            var hasDesign = _combinedPageBuckets.TryGetValue(PdfType.SpaceDesignLoadSummary, out var designPages) && designPages.Count > 0;
            ProcessButton.IsEnabled = hasZone && hasDesign && !string.IsNullOrEmpty(_combinedPdfPath);
            return;
        }

        ProcessButton.IsEnabled = !string.IsNullOrEmpty(_pdf1Path) && !string.IsNullOrEmpty(_pdf2Path);
    }

    // -- Trane PDF workflow -------------------------------------------------

    private void BrowseTranePdf_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Trane Room Checksums PDF",
            Filter = "PDF Files (*.pdf)|*.pdf",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
            SetTranePdf(dialog.FileName);
    }

    private void BrowseTraneDesignCoolingPdf_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Optional: TRACE Design Cooling Load Summary PDF",
            Filter = "PDF Files (*.pdf)|*.pdf",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
            SetTraneDesignCoolingPdf(dialog.FileName);
    }

    private void ClearTraneDesignCoolingPdf_Click(object sender, RoutedEventArgs e)
    {
        ClearTraneDesignCoolingPdf();
    }

    private void ClearTraneDesignCoolingPdf()
    {
        _traneDesignCoolingPdfPath = null;
        TraneDesignPdfPathText.Text = "Not loaded";
        TraneDesignPdfPathText.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
        TraneDesignPdfStatusDot.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
    }

    private void SetTraneDesignCoolingPdf(string path)
    {
        var pdfType = PdfClassifier.Classify(path);
        if (pdfType != PdfType.TraceDesignCoolingLoadSummary)
        {
            MessageBox.Show(
                "This PDF was not recognized as a TRACE Design Cooling Load Summary (CES room report).\n\n" +
                "Use the report that shows \"Design Cooling Load Summary\", \"Room - …\", and \"System - AHU-…\".\n\n" +
                $"Classifier result: {pdfType}",
                "Wrong PDF type", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _traneDesignCoolingPdfPath = path;
        TraneDesignPdfPathText.Text = path;
        TraneDesignPdfPathText.Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush");
        TraneDesignPdfStatusDot.Foreground = GreenDot;
        Logger.Info($"Optional Design Cooling PDF loaded: {path}");
        TraneStatusText.Text =
            "Design Cooling Summary loaded. It will be merged on Process using room/zone names.";
    }

    private void SetTranePdf(string path)
    {
        _tranePdfPath = path;
        _traneData = null;
        _traneFilteredData = null;
        _lastTraneExportPath = null;
        ClearTraneDesignCoolingPdf();

        TranePdfPathText.Text = path;
        TranePdfPathText.Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush");
        TranePdfStatusDot.Foreground = GreenDot;

        ClearTraneFilterInputs();
        TraneDataGrid.ItemsSource = null;
        TraneCalculatedAirflowText.Text = "";
        TraneResultsPanel.Visibility = Visibility.Collapsed;
        TraneResultsInfoCard.Visibility = Visibility.Collapsed;
        TraneEmptyState.Visibility = Visibility.Visible;

        UpdateTraneButtons();
        TraneStatusText.Text = "Trane PDF loaded. Click 'Process Trane PDF' to extract room data.";
        Logger.Info($"Trane PDF loaded: {path}");
    }

    private void UpdateTraneButtons()
    {
        ProcessTraneButton.IsEnabled = !string.IsNullOrEmpty(_tranePdfPath);
        ExportTraneExcelButton.IsEnabled = _traneData is { Count: > 0 };
        OpenTraneExportLocationButton.IsEnabled = !string.IsNullOrEmpty(_lastTraneExportPath) && File.Exists(_lastTraneExportPath);
    }

    private void BeginTraneProcessing(string status)
    {
        TraneProcessingProgressBar.Visibility = Visibility.Visible;
        TraneProcessingProgressBar.IsIndeterminate = true;
        TraneProcessingProgressBar.Value = 0;

        ProcessTraneButton.IsEnabled = false;
        ExportTraneExcelButton.IsEnabled = false;
        OpenTraneExportLocationButton.IsEnabled = !string.IsNullOrEmpty(_lastTraneExportPath) && File.Exists(_lastTraneExportPath);
        TraneStatusText.Text = status;
    }

    private void EndTraneProcessing(string status)
    {
        TraneProcessingProgressBar.Visibility = Visibility.Collapsed;
        TraneProcessingProgressBar.IsIndeterminate = true;
        TraneProcessingProgressBar.Value = 0;
        TraneStatusText.Text = status;
        UpdateTraneButtons();
    }

    private async void ProcessTrane_Click(object sender, RoutedEventArgs e)
    {
        await ProcessTraneAsync();
    }

    private async Task ProcessTraneAsync()
    {
        if (string.IsNullOrEmpty(_tranePdfPath)) return;

        try
        {
            var tranePdf = _tranePdfPath;
            var mainHint = PdfClassifier.Classify(tranePdf);
            if (mainHint == PdfType.TraceDesignCoolingLoadSummary)
            {
                MessageBox.Show(
                    "The main file looks like a Design Cooling Load Summary, not Room Checksums.\n\n" +
                    "Load the Room Checksums PDF in the first row, and put Design Cooling in the optional row.",
                    "Wrong PDF for main slot", MessageBoxButton.OK, MessageBoxImage.Warning);
                EndTraneProcessing("Use Room Checksums as the main PDF.");
                return;
            }

            Logger.Info($"Starting Trane extraction: {tranePdf}");
            BeginTraneProcessing("Extracting Trane room pages...");

            var extracted = await Task.Run(() => _traneExtractor.Extract(tranePdf));
            _traneData = extracted;

            if (_traneData.Count == 0)
            {
                _traneFilteredData = null;
                TraneDataGrid.ItemsSource = null;
                TraneResultsPanel.Visibility = Visibility.Collapsed;
                TraneResultsInfoCard.Visibility = Visibility.Collapsed;
                TraneEmptyState.Visibility = Visibility.Visible;
                EndTraneProcessing("No Trane Room Checksums pages found in the selected PDF.");
                MessageBox.Show(
                    "No Trane Room Checksums pages were found in the selected PDF.\n\n" +
                    "Make sure the PDF contains the Room Checksums By CES report pages.",
                    "No Trane Rooms Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            List<TraceDesignCoolingRoomExtract> designRows = new();
            if (!string.IsNullOrEmpty(_traneDesignCoolingPdfPath))
            {
                TraneStatusText.Text = "Merging Design Cooling Summary pages…";
                designRows = await Task.Run(() => _traceDesignCoolingExtractor.Extract(_traneDesignCoolingPdfPath!));
                TraneDesignCoolingMerge.AttachAndCrossCheck(_traneData, designRows);
            }
            else
            {
                TraneDesignCoolingMerge.AttachAndCrossCheck(_traneData, designRows);
            }

            TraneResultsPanel.Visibility = Visibility.Visible;
            TraneEmptyState.Visibility = Visibility.Collapsed;
            TraneResultsInfoCard.Visibility = Visibility.Visible;
            TraneProjectNameText.Text = _traneData.Select(r => r.ProjectName)
                .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? "Unknown Project";
            UpdateTraneCalculatedAirflow();
            ApplyTraneFilters();

            var linked = _traneData.Count(r => r.DesignCooling != null);
            Logger.Info($"Trane extraction complete: {_traneData.Count} rooms; {linked} linked to Design Cooling");
            EndTraneProcessing(designRows.Count > 0
                ? $"Processed {_traneData.Count} room pages — {linked} matched to Design Cooling ({designRows.Count} DC pages)."
                : $"Processed {_traneData.Count} Trane room pages.");
        }
        catch (Exception ex)
        {
            Logger.Error("Trane extraction failed", ex);
            EndTraneProcessing($"Error: {ex.Message}");
            MessageBox.Show($"Failed to process Trane PDF:\n\n{ex.Message}\n\nLog: {Logger.LogFilePath}",
                "Trane Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void TraneEmptyState_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var pdfs = files.Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();
        if (pdfs.Count == 0)
        {
            TraneStatusText.Text = "No PDF files found in dropped items.";
            return;
        }

        if (pdfs.Count >= 2)
        {
            string? checksumsPdf = null;
            string? designPdf = null;
            foreach (var p in pdfs)
            {
                var t = PdfClassifier.Classify(p);
                if (t == PdfType.TraneRoomChecksumsReport)
                    checksumsPdf = p;
                else if (t == PdfType.TraceDesignCoolingLoadSummary)
                    designPdf = p;
            }

            if (checksumsPdf == null)
                checksumsPdf = pdfs.FirstOrDefault(p => !string.Equals(p, designPdf, StringComparison.OrdinalIgnoreCase))
                               ?? pdfs[0];
            if (checksumsPdf != null)
                SetTranePdf(checksumsPdf);
            if (designPdf != null)
                SetTraneDesignCoolingPdf(designPdf);
            await ProcessTraneAsync();
            return;
        }

        SetTranePdf(pdfs[0]);
        await ProcessTraneAsync();
    }

    private void TraneEmptyState_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            e.Effects = files.Any(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void TraneFilter_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isUpdatingTraneFilters)
            ApplyTraneFilters();
    }

    private void TraneDeltaT_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_isUpdatingTraneDeltaT)
            UpdateTraneCalculatedAirflow();
    }

    private void ClearTraneFilters_Click(object sender, RoutedEventArgs e)
    {
        ClearTraneFilterInputs();
        ApplyTraneFilters();
    }

    private void ClearTraneFilterInputs()
    {
        _isUpdatingTraneFilters = true;
        TraneRoomFilterBox.Text = "";
        TraneTonsMinFilterBox.Text = "";
        TraneTonsMaxFilterBox.Text = "";
        TraneTotalMbhMinFilterBox.Text = "";
        TraneTotalMbhMaxFilterBox.Text = "";
        TraneSensibleMbhMinFilterBox.Text = "";
        TraneSensibleMbhMaxFilterBox.Text = "";
        TraneAirflowMinFilterBox.Text = "";
        TraneAirflowMaxFilterBox.Text = "";
        TraneAreaMinFilterBox.Text = "";
        TraneAreaMaxFilterBox.Text = "";
        TraneSqFtTonMinFilterBox.Text = "";
        TraneSqFtTonMaxFilterBox.Text = "";
        TranePeopleMinFilterBox.Text = "";
        TranePeopleMaxFilterBox.Text = "";
        _isUpdatingTraneFilters = false;
    }

    private void ApplyTraneFilters()
    {
        if (_traneData == null)
            return;

        var roomFilter = TraneRoomFilterBox.Text.Trim();

        var filtered = _traneData
            .Where(item =>
                MatchesRoomFilter(item, roomFilter) &&
                MatchesRangeFilter(item.TotalCapacityTons, TraneTonsMinFilterBox.Text, TraneTonsMaxFilterBox.Text) &&
                MatchesRangeFilter(item.TotalCapacityMbh, TraneTotalMbhMinFilterBox.Text, TraneTotalMbhMaxFilterBox.Text) &&
                MatchesRangeFilter(item.SensibleCapacityMbh, TraneSensibleMbhMinFilterBox.Text, TraneSensibleMbhMaxFilterBox.Text) &&
                MatchesRangeFilter(item.CoilAirflowCfm, TraneAirflowMinFilterBox.Text, TraneAirflowMaxFilterBox.Text) &&
                MatchesRangeFilter(item.GrossFloorAreaSqFt, TraneAreaMinFilterBox.Text, TraneAreaMaxFilterBox.Text) &&
                MatchesRangeFilter(item.SqFtPerTon, TraneSqFtTonMinFilterBox.Text, TraneSqFtTonMaxFilterBox.Text) &&
                MatchesRangeFilter(item.NumberOfPeople, TranePeopleMinFilterBox.Text, TranePeopleMaxFilterBox.Text))
            .ToList();

        _traneFilteredData = filtered;
        TraneDataGrid.ItemsSource = filtered;
        TraneRoomCountText.Text = filtered.Count == _traneData.Count
            ? _traneData.Count.ToString()
            : $"{filtered.Count} / {_traneData.Count}";
        UpdateTraneCalculatedAirflowSummary();
    }

    private static bool MatchesRoomFilter(TraneRoomLoad item, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        return item.RoomName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               item.RoomNumber.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
               item.RoomDisplayName.Contains(filter, StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateTraneCalculatedAirflow()
    {
        if (_traneData == null)
            return;

        if (!TryParseTraneDeltaT(out var deltaT))
        {
            foreach (var item in _traneData)
                item.CalculatedAirflowCfm = null;

            TraneDataGrid.Items.Refresh();
            UpdateTraneCalculatedAirflowSummary();
            return;
        }

        foreach (var item in _traneData)
            item.CalculatedAirflowCfm = CalculateAirflowCfm(item.SensibleCapacityMbh, deltaT);

        TraneDataGrid.Items.Refresh();
        UpdateTraneCalculatedAirflowSummary();
    }

    private void UpdateTraneCalculatedAirflowSummary()
    {
        if (_traneData == null)
        {
            TraneCalculatedAirflowText.Text = "";
            return;
        }

        if (!TryParseTraneDeltaT(out _))
        {
            TraneCalculatedAirflowText.Text = "Invalid Delta T";
            return;
        }

        var source = _traneFilteredData ?? _traneData;
        var total = source.Where(r => r.CalculatedAirflowCfm.HasValue)
            .Sum(r => r.CalculatedAirflowCfm.GetValueOrDefault());
        TraneCalculatedAirflowText.Text = $"{total:N0} cfm";
    }

    private static double? CalculateAirflowCfm(double? sensibleCapacityMbh, double deltaT)
    {
        if (!sensibleCapacityMbh.HasValue || deltaT <= 0)
            return null;

        return (sensibleCapacityMbh.Value * 1000) / (1.08 * deltaT);
    }

    private static bool MatchesRangeFilter(double? value, string minText, string maxText)
    {
        var hasMin = !string.IsNullOrWhiteSpace(minText);
        var hasMax = !string.IsNullOrWhiteSpace(maxText);
        if (!hasMin && !hasMax)
            return true;

        if (!value.HasValue)
            return false;

        var actual = value.Value;
        double? min = null;
        double? max = null;

        if (hasMin)
        {
            if (!TryParseFilterNumber(minText, out var parsedMin))
                return false;

            min = parsedMin;
        }

        if (hasMax)
        {
            if (!TryParseFilterNumber(maxText, out var parsedMax))
                return false;

            max = parsedMax;
        }

        if (min.HasValue && max.HasValue && min > max)
            (min, max) = (max, min);

        return (!min.HasValue || actual >= min.Value) &&
               (!max.HasValue || actual <= max.Value);
    }

    private static bool TryParseFilterNumber(string text, out double value)
    {
        return double.TryParse(
            text.Trim().Replace(",", "", StringComparison.Ordinal),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    // ── Process & Combine ───────────────────────────────────────

    private async void Process_Click(object sender, RoutedEventArgs e)
    {
        await ProcessAsync();
    }

    private async Task ProcessAsync()
    {
        if (_useCombinedPdf)
        {
            if (string.IsNullOrEmpty(_combinedPdfPath) || _combinedPageBuckets == null) return;
        }
        else
        {
            if (string.IsNullOrEmpty(_pdf1Path) || string.IsNullOrEmpty(_pdf2Path)) return;
        }

        try
        {
            Logger.Info("Starting extraction...");
            BeginProcessing("Preparing...");
            ReportProgress("Extracting PDF 1 (Zone Sizing Summary)...", 10, indeterminate: true);

            if (_useCombinedPdf)
            {
                var combinedPath = _combinedPdfPath!;
                var buckets = _combinedPageBuckets;
                await Task.Run(() =>
                {
                    using var document = UglyToad.PdfPig.PdfDocument.Open(combinedPath);
                    var zonePages = buckets![PdfType.ZoneSizingSummary];
                    var designPages = buckets[PdfType.SpaceDesignLoadSummary];

                    _pdf1Data = _pdf1Extractor.Extract(document, zonePages);
                    _pdf1Data.SourceFile = combinedPath;

                    _pdf2Data = _pdf2Extractor.Extract(document, designPages);

                    if (buckets.TryGetValue(PdfType.AirSystemSizingSummary, out var airSizingPages) &&
                        airSizingPages.Count > 0)
                        _pdf3Data = _pdf3Extractor.Extract(document, airSizingPages);
                    else
                        _pdf3Data = null;
                });

                Logger.Info($"PDF 1 extracted (combined mode): {_pdf1Data!.AirSystems.Count} systems, " +
                            $"{_pdf1Data.AirSystems.Sum(s => s.Spaces.Count)} spaces");
                Logger.Info($"PDF 2 extracted (combined mode): {_pdf2Data!.Count} space component load tables");
                if (_pdf3Data != null)
                    Logger.Info($"PDF 3 extracted (combined mode): {_pdf3Data.Count} air system sizing entries");
            }
            else
            {
                var pdf1 = _pdf1Path!;
                var pdf2 = _pdf2Path!;
                var pdf3 = _pdf3Path;
                await Task.Run(() =>
                {
                    _pdf1Data = _pdf1Extractor.Extract(pdf1);
                    _pdf2Data = _pdf2Extractor.Extract(pdf2);
                    _pdf3Data = !string.IsNullOrEmpty(pdf3) ? _pdf3Extractor.Extract(pdf3!) : null;
                });

                Logger.Info($"PDF 1 extracted: {_pdf1Data!.AirSystems.Count} systems, " +
                            $"{_pdf1Data.AirSystems.Sum(s => s.Spaces.Count)} spaces");
                Logger.Info($"PDF 2 extracted: {_pdf2Data!.Count} space component load tables");
                if (_pdf3Data != null)
                    Logger.Info($"PDF 3 extracted: {_pdf3Data.Count} air system sizing entries");
            }

            ReportProgress("Combining data...", 75, indeterminate: true);
            var combined = await Task.Run(() => _combiner.Combine(_pdf1Data!, _pdf2Data!));
            _combinedData = combined;
            Logger.Info($"Combined: {combined.Count} records");

            // Update UI
            ReportProgress("Rendering results...", 95, indeterminate: true);
            ProjectNameText.Text = _pdf1Data.ProjectName;
            MatchedCountText.Text = _combinedData.Count.ToString();
            ResultsInfoCard.Visibility = Visibility.Visible;
            ResultsPanel.Visibility = Visibility.Visible;
            EmptyState.Visibility = Visibility.Collapsed;
            ExportExcelButton.IsEnabled = true;
            MattsWayButton.IsEnabled = true;

            // Populate sidebar and grid
            SetSidebarMode(SidebarMode.All);

            var statusParts = $"Processed: {_pdf1Data.AirSystems.Count} systems, " +
                              $"{_pdf2Data.Count} spaces extracted, " +
                              $"{_combinedData.Count} combined";
            if (_pdf3Data != null)
                statusParts += $", {_pdf3Data.Count} air system sizing entries";
            EndProcessing(statusParts + $" — Log: {Logger.LogFilePath}");
        }
        catch (Exception ex)
        {
            Logger.Error("Process_Click failed", ex);
            EndProcessing($"Error: {ex.Message}");
            MessageBox.Show($"Failed to process PDFs:\n\n{ex.Message}\n\nLog: {Logger.LogFilePath}",
                "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetCombinedPdfState(string pdfPath, Dictionary<PdfType, List<int>> buckets)
    {
        _useCombinedPdf = true;
        _combinedPdfPath = pdfPath;
        _combinedPageBuckets = buckets;

        _pdf1Path = pdfPath;
        _pdf2Path = pdfPath;
        _pdf3Path = (buckets.TryGetValue(PdfType.AirSystemSizingSummary, out var air) && air.Count > 0) ? pdfPath : null;

        CombinedPdfPathText.Text = pdfPath;
        CombinedPdfPathText.Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush");
        CombinedPdfStatusDot.Foreground = GreenDot;

        Pdf1PathText.Text = pdfPath;
        Pdf1PathText.Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush");
        Pdf1StatusDot.Foreground = GreenDot;

        Pdf2PathText.Text = pdfPath;
        Pdf2PathText.Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush");
        Pdf2StatusDot.Foreground = GreenDot;

        if (_pdf3Path != null)
        {
            Pdf3PathText.Text = pdfPath;
            Pdf3PathText.Foreground = (SolidColorBrush)FindResource("TextPrimaryBrush");
            Pdf3StatusDot.Foreground = GreenDot;
        }
        else
        {
            Pdf3PathText.Text = "Not loaded";
            Pdf3PathText.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
            Pdf3StatusDot.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
        }

        UpdateProcessButton();
    }

    private void ClearCombinedPdfState()
    {
        _useCombinedPdf = false;
        _combinedPdfPath = null;
        _combinedPageBuckets = null;
        CombinedPdfPathText.Text = "Not loaded";
        CombinedPdfPathText.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
        CombinedPdfStatusDot.Foreground = (SolidColorBrush)FindResource("TextMutedBrush");
    }

    // ── Sidebar Mode Switching ──────────────────────────────────

    private void ModeAll_Click(object sender, RoutedEventArgs e) => SetSidebarMode(SidebarMode.All);
    private void ModeSystem_Click(object sender, RoutedEventArgs e) => SetSidebarMode(SidebarMode.BySystem);
    private void ModeZone_Click(object sender, RoutedEventArgs e) => SetSidebarMode(SidebarMode.ByZone);

    private void SetSidebarMode(SidebarMode mode)
    {
        if (_combinedData == null) return;
        _currentMode = mode;

        ModeAllButton.Style = (Style)FindResource(mode == SidebarMode.All ? "AccentButton" : "SubtleButton");
        ModeSystemButton.Style = (Style)FindResource(mode == SidebarMode.BySystem ? "AccentButton" : "SubtleButton");
        ModeZoneButton.Style = (Style)FindResource(mode == SidebarMode.ByZone ? "AccentButton" : "SubtleButton");

        SearchBox.Text = "";

        // Search bar visible in all modes
        SearchBar.Visibility = Visibility.Visible;

        switch (mode)
        {
            case SidebarMode.All:
                // No sidebar, full grid, search filters grid rows
                SidebarBorder.Visibility = Visibility.Collapsed;
                SidebarGrid.ColumnDefinitions[0].Width = new GridLength(0);
                ShowDataInGrid(_combinedData);
                DataGridHeaderText.Text = $"All zones ({_combinedData.Count})";
                break;

            case SidebarMode.BySystem:
                // Sidebar lists system names
                SidebarBorder.Visibility = Visibility.Visible;
                SidebarGrid.ColumnDefinitions[0].Width = new GridLength(200);
                PopulateSystemList("");
                break;

            case SidebarMode.ByZone:
                // Sidebar lists zone names
                SidebarBorder.Visibility = Visibility.Visible;
                SidebarGrid.ColumnDefinitions[0].Width = new GridLength(200);
                PopulateZoneList("");
                break;
        }

        Logger.Info($"Sidebar mode changed to {mode}");
    }

    private void PopulateSystemList(string filter)
    {
        if (_combinedData == null) return;
        var systems = _combinedData
            .Select(d => d.SystemName)
            .Distinct()
            .Where(s => string.IsNullOrEmpty(filter) || s.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s)
            .ToList();

        SidebarList.ItemTemplate = CreateSystemTemplate();
        SidebarList.ItemsSource = systems;
        if (systems.Count > 0)
            SidebarList.SelectedIndex = 0;
    }

    private void PopulateZoneList(string filter)
    {
        if (_combinedData == null) return;
        var zones = _combinedData
            .Where(d => string.IsNullOrEmpty(filter) ||
                        d.RoomName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                        d.SystemName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            .ToList();

        SidebarList.ItemTemplate = CreateZoneTemplate();
        SidebarList.ItemsSource = zones;
        if (zones.Count > 0)
            SidebarList.SelectedIndex = 0;
    }

    private DataTemplate CreateSystemTemplate()
    {
        var template = new DataTemplate();
        var factory = new FrameworkElementFactory(typeof(TextBlock));
        factory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding());
        factory.SetValue(TextBlock.FontSizeProperty, 12.0);
        factory.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        factory.SetValue(TextBlock.ForegroundProperty, FindResource("TextPrimaryBrush"));
        factory.SetValue(TextBlock.PaddingProperty, new Thickness(8, 6, 8, 6));
        template.VisualTree = factory;
        return template;
    }

    private DataTemplate CreateZoneTemplate()
    {
        var template = new DataTemplate(typeof(CombinedSpaceData));
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.MarginProperty, new Thickness(8, 4, 8, 4));

        var name = new FrameworkElementFactory(typeof(TextBlock));
        name.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("RoomName"));
        name.SetValue(TextBlock.FontSizeProperty, 12.0);
        name.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        name.SetValue(TextBlock.ForegroundProperty, FindResource("TextPrimaryBrush"));
        name.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        panel.AppendChild(name);

        var sub = new FrameworkElementFactory(typeof(TextBlock));
        sub.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("SystemName"));
        sub.SetValue(TextBlock.FontSizeProperty, 10.0);
        sub.SetValue(TextBlock.ForegroundProperty, FindResource("AccentBrush"));
        sub.SetValue(TextBlock.MarginProperty, new Thickness(0, 2, 0, 0));
        panel.AppendChild(sub);

        template.VisualTree = panel;
        return template;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var filter = SearchBox.Text.Trim();
        if (_currentMode == SidebarMode.All)
        {
            // Filter the grid directly
            if (_combinedData == null) return;
            var filtered = _combinedData
                .Where(d => string.IsNullOrEmpty(filter) ||
                            d.RoomName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                            d.SystemName.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .ToList();
            ShowDataInGrid(filtered);
            DataGridHeaderText.Text = $"All zones ({filtered.Count})";
        }
        else if (_currentMode == SidebarMode.BySystem)
            PopulateSystemList(filter);
        else if (_currentMode == SidebarMode.ByZone)
            PopulateZoneList(filter);
    }

    private void SidebarList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_combinedData == null) return;

        if (_currentMode == SidebarMode.BySystem && SidebarList.SelectedItem is string systemName)
        {
            // Show card view for system (all zones in that system)
            var filtered = _combinedData.Where(d => d.SystemName == systemName).ToList();
            ShowSystemCardView(systemName, filtered);
            DataGridHeaderText.Text = $"— {systemName} ({filtered.Count} zones)";
        }
        else if (_currentMode == SidebarMode.ByZone && SidebarList.SelectedItem is CombinedSpaceData selected)
        {
            ShowSingleZoneView(selected);
            DataGridHeaderText.Text = $"— {selected.RoomName} ({selected.SystemName})";
        }
    }

    // ── View Switching ──────────────────────────────────────────

    private void ShowSystemCardView(string systemName, List<CombinedSpaceData> items)
    {
        DataGridPanel.Visibility = Visibility.Collapsed;
        SingleZonePanel.Visibility = Visibility.Visible;
        SingleZoneContent.Children.Clear();

        AddSectionHeader($"System: {systemName} — {items.Count} zones");

        foreach (var item in items)
        {
            // Divider
            SingleZoneContent.Children.Add(new Border
            {
                Height = 1,
                Background = (Brush)FindResource("BorderBrush"),
                Margin = new Thickness(0, 12, 0, 4)
            });
            BuildZoneCard(item);
        }
    }

    private void ShowSingleZoneView(CombinedSpaceData item)
    {
        DataGridPanel.Visibility = Visibility.Collapsed;
        SingleZonePanel.Visibility = Visibility.Visible;
        SingleZoneContent.Children.Clear();
        BuildZoneCard(item);
    }

    // ── Zone Card Builder ────────────────────────────────────────

    private void BuildZoneCard(CombinedSpaceData item)
    {
        var cl = item.ComponentLoads;

        // Zone info header
        AddSectionHeader($"{item.RoomName}  —  {item.SystemName}  —  {item.FloorAreaSqFt:N0} ft²");

        // Totals card
        AddCardTitle("Totals");
        AddKeyValueRow("People", $"{item.TotalPeopleDetails:N0}");
        AddKeyValueRow("Sensible (BTU/hr)", $"{item.TotalCoolingSensible:N0}");
        AddKeyValueRow("Latent (BTU/hr)", $"{item.TotalCoolingLatent:N0}");
        AddCopyButton(() => $"People\tSensible\tLatent\n{item.TotalPeopleDetails:N0}\t{item.TotalCoolingSensible:N0}\t{item.TotalCoolingLatent:N0}");

        if (cl == null) return;

        // Envelope section: Window & Skylight → Ceiling
        AddCardTitle("Window & Skylight → Ceiling");
        AddTableHeader("Component", "Area", "Sensible (Cooling)", "Sensible (Heating)");
        foreach (var r in cl.EnvelopeRows)
            AddTableRow(r.RowName, r.CoolingDetails, $"{r.CoolingSensible:N0}", $"{r.HeatingSensible:N0}");
        AddCopyButton(() =>
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Component\tArea\tSensible (Cooling)\tSensible (Heating)");
            foreach (var r in cl.EnvelopeRows)
                sb.AppendLine($"{r.RowName}\t{r.CoolingDetails}\t{r.CoolingSensible:N0}\t{r.HeatingSensible:N0}");
            return sb.ToString();
        });

        // Internal gains: Overhead Lighting → Electrical Equipment
        AddCardTitle("Overhead Lighting → Electrical Equipment");
        AddTableHeader("Component", "Details", "Sensible (Cooling)", "");
        foreach (var r in cl.InternalGainRows)
            AddTableRow(r.RowName, r.CoolingDetails, $"{r.CoolingSensible:N0}", "");
        AddCopyButton(() =>
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Component\tDetails\tSensible (Cooling)");
            foreach (var r in cl.InternalGainRows)
                sb.AppendLine($"{r.RowName}\t{r.CoolingDetails}\t{r.CoolingSensible:N0}");
            return sb.ToString();
        });

        // People
        AddCardTitle("People");
        AddKeyValueRow("Sensible", $"{cl.People.CoolingSensible:N0}");
        AddKeyValueRow("Latent", $"{cl.People.CoolingLatent:N0}");
        AddCopyButton(() => $"Sensible\tLatent\n{cl.People.CoolingSensible:N0}\t{cl.People.CoolingLatent:N0}");

        // Infiltration + Misc
        AddCardTitle("Infiltration / Miscellaneous");
        AddKeyValueRow("Infiltration Sensible", $"{cl.Infiltration.CoolingSensible:N0}");
        AddKeyValueRow("Miscellaneous Sensible", $"{cl.Miscellaneous.CoolingSensible:N0}");
        AddCopyButton(() => $"Infiltration\tMiscellaneous\n{cl.Infiltration.CoolingSensible:N0}\t{cl.Miscellaneous.CoolingSensible:N0}");

        // Safety Factor
        AddCardTitle("Safety Factor");
        AddKeyValueRow("Details", cl.SafetyFactor.CoolingDetails);
        AddKeyValueRow("Sensible", $"{cl.SafetyFactor.CoolingSensible:N0}");
        AddKeyValueRow("Latent", $"{cl.SafetyFactor.CoolingLatent:N0}");
        AddCopyButton(() => $"Details\tSensible\tLatent\n{cl.SafetyFactor.CoolingDetails}\t{cl.SafetyFactor.CoolingSensible:N0}\t{cl.SafetyFactor.CoolingLatent:N0}");
    }

    private void AddSectionHeader(string text)
    {
        SingleZoneContent.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 18, FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            Margin = new Thickness(0, 0, 0, 12)
        });
    }

    private void AddCardTitle(string text)
    {
        SingleZoneContent.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 14, FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)FindResource("AccentBrush"),
            Margin = new Thickness(0, 16, 0, 6)
        });
    }

    private void AddKeyValueRow(string label, string value)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var lbl = new TextBlock { Text = label, FontSize = 12, Foreground = (Brush)FindResource("TextSecondaryBrush") };
        Grid.SetColumn(lbl, 0);
        grid.Children.Add(lbl);

        var val = new TextBlock { Text = value, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)FindResource("TextPrimaryBrush") };
        Grid.SetColumn(val, 1);
        grid.Children.Add(val);

        SingleZoneContent.Children.Add(grid);
    }

    private void AddTableHeader(string c1, string c2, string c3, string c4)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

        var accent = (Brush)FindResource("AccentBrush");
        string[] cols = { c1, c2, c3, c4 };
        for (int i = 0; i < 4; i++)
        {
            if (string.IsNullOrEmpty(cols[i])) continue;
            var tb = new TextBlock { Text = cols[i], FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = accent };
            Grid.SetColumn(tb, i);
            grid.Children.Add(tb);
        }
        SingleZoneContent.Children.Add(grid);
    }

    private void AddTableRow(string c1, string c2, string c3, string c4)
    {
        var grid = new Grid { Margin = new Thickness(0, 1, 0, 1) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });

        var primary = (Brush)FindResource("TextPrimaryBrush");
        var secondary = (Brush)FindResource("TextSecondaryBrush");
        string[] cols = { c1, c2, c3, c4 };
        for (int i = 0; i < 4; i++)
        {
            if (string.IsNullOrEmpty(cols[i])) continue;
            var tb = new TextBlock { Text = cols[i], FontSize = 12, Foreground = i == 0 ? secondary : primary };
            Grid.SetColumn(tb, i);
            grid.Children.Add(tb);
        }
        SingleZoneContent.Children.Add(grid);
    }

    private void AddCopyButton(Func<string> getClipboardText)
    {
        var btn = new Button
        {
            Content = "📋 Copy to Clipboard",
            FontSize = 11,
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 6, 0, 0),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Cursor = System.Windows.Input.Cursors.Hand,
            Background = (Brush)FindResource("CardBrush"),
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            BorderBrush = (Brush)FindResource("BorderBrush"),
            BorderThickness = new Thickness(1),
        };
        btn.Click += (s, e) =>
        {
            try
            {
                System.Windows.Clipboard.SetText(getClipboardText());
                btn.Content = "✓ Copied!";
                var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                timer.Tick += (_, _) => { btn.Content = "📋 Copy to Clipboard"; timer.Stop(); };
                timer.Start();
            }
            catch (Exception ex)
            {
                Logger.Error("Copy to clipboard failed", ex);
            }
        };
        SingleZoneContent.Children.Add(btn);
    }

    // ── DataGrid Building (multi-zone) ──────────────────────────

    private static readonly string[] EnvNames = { "Win/Sky Solar", "Wall Trans", "Roof Trans", "Window Trans",
                                                   "Skylight Trans", "Door Loads", "Floor Trans", "Partitions", "Ceiling" };
    private static readonly string[] IgNames = { "Overhead Ltg", "Task Ltg", "Elec Equip" };

    private void ShowDataInGrid(List<CombinedSpaceData> items)
    {
        try
        {
            DataGridPanel.Visibility = Visibility.Visible;
            SingleZonePanel.Visibility = Visibility.Collapsed;

            CombinedDataGrid.Columns.Clear();
            CombinedDataGrid.AutoGenerateColumns = false;

            var dt = BuildDataTable(items);

            foreach (DataColumn dc in dt.Columns)
            {
                var binding = new System.Windows.Data.Binding($"[{dc.ColumnName}]");
                if (dc.DataType == typeof(double))
                    binding.StringFormat = "N0";

                var dgCol = new DataGridTextColumn
                {
                    Header = dc.ColumnName,
                    Binding = binding,
                    CanUserSort = false,
                    MinWidth = 55,
                };

                if (dc.ColumnName == "Room Name")
                    dgCol.Width = new DataGridLength(1, DataGridLengthUnitType.Star);
                else if (dc.ColumnName == "System")
                    dgCol.Width = 75;
                else
                    dgCol.Width = new DataGridLength(1, DataGridLengthUnitType.SizeToCells);

                CombinedDataGrid.Columns.Add(dgCol);
            }

            CombinedDataGrid.ItemsSource = dt.DefaultView;
        }
        catch (Exception ex)
        {
            Logger.Error("ShowDataInGrid failed", ex);
        }
    }

    private DataTable BuildDataTable(List<CombinedSpaceData> items)
    {
        var dt = new DataTable();

        // Fixed columns
        dt.Columns.Add("Room Name", typeof(string));
        dt.Columns.Add("System", typeof(string));
        dt.Columns.Add("SQFT", typeof(double));
        dt.Columns.Add("People", typeof(double));
        dt.Columns.Add("Sensible", typeof(double));
        dt.Columns.Add("Latent", typeof(double));

        // Envelope rows: item name + sub-column with renamed labels
        foreach (var name in EnvNames)
        {
            dt.Columns.Add($"{name}\nArea", typeof(string));
            dt.Columns.Add($"{name}\nSens (Cool)", typeof(double));
            dt.Columns.Add($"{name}\nSens (Heat)", typeof(double));
        }

        foreach (var name in IgNames)
        {
            dt.Columns.Add($"{name}\nDetails", typeof(string));
            dt.Columns.Add($"{name}\nSens (Cool)", typeof(double));
        }

        dt.Columns.Add("People\nSensible", typeof(double));
        dt.Columns.Add("People\nLatent", typeof(double));
        dt.Columns.Add("Infiltration\nSensible", typeof(double));
        dt.Columns.Add("Miscellaneous\nSensible", typeof(double));
        dt.Columns.Add("Safety Factor\nDetails", typeof(string));
        dt.Columns.Add("Safety Factor\nSensible", typeof(double));
        dt.Columns.Add("Safety Factor\nLatent", typeof(double));

        foreach (var item in items)
        {
            var row = dt.NewRow();
            int col = 0;

            row[col++] = item.RoomName;
            row[col++] = item.SystemName;
            row[col++] = item.FloorAreaSqFt;
            row[col++] = item.TotalPeopleDetails;
            row[col++] = item.TotalCoolingSensible;
            row[col++] = item.TotalCoolingLatent;

            var cl = item.ComponentLoads;
            if (cl != null)
            {
                foreach (var envRow in cl.EnvelopeRows)
                {
                    row[col++] = envRow.CoolingDetails;
                    row[col++] = envRow.CoolingSensible;
                    row[col++] = envRow.HeatingSensible;
                }
                foreach (var igRow in cl.InternalGainRows)
                {
                    row[col++] = igRow.CoolingDetails;
                    row[col++] = igRow.CoolingSensible;
                }
                row[col++] = cl.People.CoolingSensible;
                row[col++] = cl.People.CoolingLatent;
                row[col++] = cl.Infiltration.CoolingSensible;
                row[col++] = cl.Miscellaneous.CoolingSensible;
                row[col++] = cl.SafetyFactor.CoolingDetails;
                row[col++] = cl.SafetyFactor.CoolingSensible;
                row[col++] = cl.SafetyFactor.CoolingLatent;
            }

            dt.Rows.Add(row);
        }

        return dt;
    }

    // ── Export ───────────────────────────────────────────────────

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (_combinedData == null || _pdf1Data == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export to Excel",
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"{SanitizeFileName(_pdf1Data.ProjectName)}_Component_Loads.xlsx"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            Logger.Info($"Exporting Excel to {dialog.FileName}");
            _excelExporter.Export(dialog.FileName, _combinedData);
            Logger.Info("Excel export complete");
            _lastExportPath = dialog.FileName;
            OpenExportLocationButton.IsEnabled = true;
            StatusText.Text = $"Exported to {dialog.FileName}";
            OpenSpreadsheet(dialog.FileName, "Excel Export");
        }
        catch (Exception ex)
        {
            Logger.Error("Excel export failed", ex);
            MessageBox.Show($"Failed to export Excel:\n\n{ex.Message}",
                "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportMattsWay_Click(object sender, RoutedEventArgs e)
    {
        if (_pdf1Data == null) return;

        var dialog = new SaveFileDialog
        {
            Title = "Export Matt's Way",
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"{SanitizeFileName(_pdf1Data.ProjectName)}_MattsWay.xlsx"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            Logger.Info($"Exporting Matt's Way to {dialog.FileName}");
            _excelExporter.ExportMattsWay(dialog.FileName, _pdf1Data, _pdf3Data);
            Logger.Info("Matt's Way export complete");
            _lastExportPath = dialog.FileName;
            OpenExportLocationButton.IsEnabled = true;
            StatusText.Text = $"Exported to {dialog.FileName}";
            OpenSpreadsheet(dialog.FileName, "Matt's Way Export");
        }
        catch (Exception ex)
        {
            Logger.Error("Matt's Way export failed", ex);
            MessageBox.Show($"Failed to export Matt's Way:\n\n{ex.Message}",
                "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportTraneExcel_Click(object sender, RoutedEventArgs e)
    {
        if (_traneData == null || _traneData.Count == 0) return;

        if (!TryGetTraneDeltaT(out var deltaT))
        {
            MessageBox.Show("Delta T must be a number greater than 0.",
                "Invalid Delta T", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var projectName = _traneData
            .Select(r => r.ProjectName)
            .FirstOrDefault(p => !string.IsNullOrWhiteSpace(p)) ?? "Trane";

        var dialog = new SaveFileDialog
        {
            Title = "Export Trane Rooms",
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"{SanitizeFileName(projectName)}_Trane_Room_Loads.xlsx"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            Logger.Info($"Exporting Trane Excel to {dialog.FileName}");
            _traneExcelExporter.Export(dialog.FileName, _traneData, deltaT);
            Logger.Info("Trane Excel export complete");
            _lastTraneExportPath = dialog.FileName;
            OpenTraneExportLocationButton.IsEnabled = true;
            TraneStatusText.Text = $"Exported to {dialog.FileName} using Delta T {deltaT:0.##}";
            OpenSpreadsheet(dialog.FileName, "Trane Export");
        }
        catch (Exception ex)
        {
            Logger.Error("Trane Excel export failed", ex);
            MessageBox.Show($"Failed to export Trane Excel:\n\n{ex.Message}",
                "Trane Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private bool TryGetTraneDeltaT(out double deltaT)
    {
        if (!TryParseTraneDeltaT(out deltaT))
            return false;

        _isUpdatingTraneDeltaT = true;
        TraneDeltaTBox.Text = deltaT.ToString("0.##", CultureInfo.InvariantCulture);
        _isUpdatingTraneDeltaT = false;
        return true;
    }

    private bool TryParseTraneDeltaT(out double deltaT)
    {
        var text = TraneDeltaTBox.Text.Trim();
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out deltaT) && deltaT > 0;
    }

    private void OpenExportLocation_Click(object sender, RoutedEventArgs e)
    {
        OpenContainingFolder(_lastExportPath);
    }

    private void OpenTraneExportLocation_Click(object sender, RoutedEventArgs e)
    {
        OpenContainingFolder(_lastTraneExportPath);
    }

    private void OpenSpreadsheet(string filePath, string title)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to open {filePath}", ex);
            MessageBox.Show($"The spreadsheet was created, but Windows could not open it automatically:\n\n{ex.Message}",
                title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenContainingFolder(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Logger.Error($"Failed to open containing folder for {filePath}", ex);
            MessageBox.Show($"Could not open the save location:\n\n{ex.Message}",
                "Open Save Location", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }
        return name;
    }
}
