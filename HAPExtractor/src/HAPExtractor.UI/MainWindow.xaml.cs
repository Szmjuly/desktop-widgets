using System.Data;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using HAPExtractor.Core.Models;
using HAPExtractor.Core.Services;
using HAPExtractor.Infrastructure.Firebase.Models;

namespace HAPExtractor.UI;

public partial class MainWindow : Window
{
    private readonly HapPdfExtractor _pdf1Extractor = new();
    private readonly DesignLoadExtractor _pdf2Extractor = new();
    private readonly AirSystemSizingExtractor _pdf3Extractor = new();
    private readonly DataCombiner _combiner = new();
    private readonly ExcelExporter _excelExporter = new();

    private string? _pdf1Path;
    private string? _pdf2Path;
    private string? _pdf3Path;
    private HapProject? _pdf1Data;
    private List<SpaceComponentLoads>? _pdf2Data;
    private List<AirSystemSizingData>? _pdf3Data;
    private List<CombinedSpaceData>? _combinedData;

    private System.Threading.Timer? _updateCheckTimer;

    private enum SidebarMode { All, BySystem, ByZone }
    private SidebarMode _currentMode = SidebarMode.All;

    private static readonly SolidColorBrush GreenDot = new(Color.FromRgb(76, 175, 80));

    public MainWindow()
    {
        InitializeComponent();
        Logger.Info("MainWindow initialized");

        Closed += MainWindow_Closed;

        // Start background update check (15s initial delay, 30min interval)
        _updateCheckTimer = new System.Threading.Timer(
            async _ => await BackgroundUpdateCheckAsync(),
            null,
            TimeSpan.FromSeconds(15),
            TimeSpan.FromMinutes(30));
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _updateCheckTimer?.Dispose();
        _updateCheckTimer = null;
    }

    // ── Update Checking ─────────────────────────────────────────

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdatesButton.IsEnabled = false;
        CheckUpdatesButton.Content = "Checking...";

        Task.Run(async () =>
        {
            try
            {
                var app = Application.Current as App;
                var firebaseManager = app?.FirebaseManager;

                if (firebaseManager == null)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Update checking unavailable (offline mode).",
                            "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                    });
                    return;
                }

                var updateInfo = await firebaseManager.CheckForUpdatesAsync();

                Dispatcher.Invoke(() =>
                {
                    if (updateInfo == null)
                    {
                        MessageBox.Show("Could not check for updates. Please try again later.",
                            "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (updateInfo.UpdateAvailable)
                    {
                        var message = $"Version {FormatVersion(updateInfo.LatestVersion)} is available!\n\nCurrent: {FormatVersion(updateInfo.CurrentVersion)}";
                        if (!string.IsNullOrEmpty(updateInfo.ReleaseNotes))
                            message += $"\n\n{updateInfo.ReleaseNotes}";
                        message += "\n\nWould you like to download and install this update now?";

                        var result = MessageBox.Show(message, "Update Available",
                            MessageBoxButton.YesNo, MessageBoxImage.Information);

                        if (result == MessageBoxResult.Yes)
                        {
                            Logger.Info("User confirmed update installation");
                            _ = Task.Run(async () => await DownloadAndInstallUpdateAsync(updateInfo));
                        }
                    }
                    else
                    {
                        MessageBox.Show($"You're up to date! (v{FormatVersion(updateInfo.CurrentVersion)})",
                            "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Error("Update check failed", ex);
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("Update check failed. Please try again later.",
                        "Check for Updates", MessageBoxButton.OK, MessageBoxImage.Warning);
                });
            }
            finally
            {
                Dispatcher.Invoke(() =>
                {
                    CheckUpdatesButton.Content = "Check for Updates";
                    CheckUpdatesButton.IsEnabled = true;
                });
            }
        });
    }

    private async Task BackgroundUpdateCheckAsync()
    {
        try
        {
            var app = Application.Current as App;
            var firebaseManager = app?.FirebaseManager;
            if (firebaseManager == null) return;

            var updateInfo = await firebaseManager.CheckForUpdatesAsync();
            if (updateInfo != null && updateInfo.UpdateAvailable)
            {
                Logger.Info($"Background update check: v{updateInfo.LatestVersion} available");
                _ = Dispatcher.BeginInvoke(() =>
                {
                    StatusText.Text = $"Update available: v{FormatVersion(updateInfo.LatestVersion)} — click 'Check for Updates' to install";
                });
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Background update check failed", ex);
        }
    }

    public async Task DownloadAndInstallUpdateAsync(UpdateInfo updateInfo, bool silent = false)
    {
        try
        {
            Logger.Info($"Download and install update: v{updateInfo.LatestVersion} from {updateInfo.DownloadUrl}");

            if (string.IsNullOrEmpty(updateInfo.DownloadUrl))
            {
                Logger.Error("Download URL is empty");
                if (!silent)
                    Dispatcher.Invoke(() => MessageBox.Show("Update download URL is missing.",
                        "Update Error", MessageBoxButton.OK, MessageBoxImage.Warning));
                return;
            }

            _ = Dispatcher.BeginInvoke(() =>
                StatusText.Text = $"Downloading v{FormatVersion(updateInfo.LatestVersion)}...");

            var tempPath = Path.Combine(Path.GetTempPath(), $"HAPExtractor-{updateInfo.LatestVersion}.exe");

            using (var client = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 10
            }))
            {
                client.Timeout = TimeSpan.FromMinutes(5);
                client.DefaultRequestHeaders.Add("User-Agent", "HAPExtractor-AutoUpdater/1.0");

                var response = await client.GetAsync(updateInfo.DownloadUrl);
                response.EnsureSuccessStatusCode();

                var updateData = await response.Content.ReadAsByteArrayAsync();
                Logger.Info($"Download complete, size: {updateData.Length} bytes");

                await File.WriteAllBytesAsync(tempPath, updateData);
            }

            var currentExePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(currentExePath) || currentExePath.EndsWith(".dll"))
                currentExePath = Path.Combine(AppContext.BaseDirectory, "HAPExtractor.exe");

            var updateBatchPath = Path.Combine(Path.GetTempPath(), "HAPExtractor-Update.bat");
            var batchContent = $@"@echo off
echo Waiting for HAPExtractor to close...
timeout /t 3 /nobreak > nul

:WAIT_LOOP
tasklist /FI ""IMAGENAME eq HAPExtractor.exe"" 2>NUL | find /I /N ""HAPExtractor.exe"">NUL
if ""%ERRORLEVEL%""==""0"" (
    timeout /t 1 /nobreak > nul
    goto WAIT_LOOP
)

echo Process closed, replacing exe...
timeout /t 1 /nobreak > nul

:DELETE_RETRY
if exist ""{currentExePath}"" (
    del /F /Q ""{currentExePath}"" 2>NUL
    if exist ""{currentExePath}"" (
        timeout /t 1 /nobreak > nul
        goto DELETE_RETRY
    )
)

copy /Y ""{tempPath}"" ""{currentExePath}"" > nul

if not exist ""{currentExePath}"" (
    echo ERROR: Failed to copy new version!
    pause
    exit /b 1
)

del ""{tempPath}"" > nul

echo Starting updated version...
timeout /t 1 /nobreak > nul
start """" ""{currentExePath}""

(goto) 2>nul & del ""%~f0""
";

            await File.WriteAllTextAsync(updateBatchPath, batchContent);
            Logger.Info($"Created update script at {updateBatchPath}");

            if (silent)
            {
                Logger.Info("Silent mode — auto-restarting in 10 seconds");
                _ = Dispatcher.BeginInvoke(() =>
                    StatusText.Text = $"Update pushed by admin — restarting in 10 seconds to install v{FormatVersion(updateInfo.LatestVersion)}...");

                await Task.Delay(10_000);
            }

            Dispatcher.Invoke(() =>
            {
                if (!silent)
                {
                    var result = MessageBox.Show(
                        "Update ready to install!\n\nHAPExtractor will restart to complete the update.",
                        "Install Update", MessageBoxButton.OKCancel, MessageBoxImage.Information);

                    if (result != MessageBoxResult.OK)
                    {
                        Logger.Info("User cancelled installation");
                        StatusText.Text = "Update cancelled.";
                        return;
                    }
                }

                Logger.Info("Starting update installer");
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = updateBatchPath,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                };
                System.Diagnostics.Process.Start(startInfo);
                Application.Current.Shutdown();
            });
        }
        catch (Exception ex)
        {
            Logger.Error("Failed to download/install update", ex);
            if (!silent)
                Dispatcher.Invoke(() => MessageBox.Show("Update failed. Please try again later.",
                    "Update Error", MessageBoxButton.OK, MessageBoxImage.Warning));
        }
    }

    private static string FormatVersion(string version)
    {
        // Strip trailing ".0" components for display: "1.8.0.0" → "1.8.0"
        if (Version.TryParse(version, out var v))
        {
            if (v.Revision == 0 && v.Build == 0) return $"{v.Major}.{v.Minor}";
            if (v.Revision == 0) return $"{v.Major}.{v.Minor}.{v.Build}";
            return v.ToString();
        }
        return version;
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

    private void SetPdf1(string path)
    {
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

    private void EmptyState_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        var pdfFiles = files.Where(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)).ToList();

        if (pdfFiles.Count == 0)
        {
            StatusText.Text = "No PDF files found in dropped items.";
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
            Process_Click(sender, new RoutedEventArgs());
    }

    private void UpdateProcessButton()
    {
        ProcessButton.IsEnabled = !string.IsNullOrEmpty(_pdf1Path) && !string.IsNullOrEmpty(_pdf2Path);
    }

    // ── Process & Combine ───────────────────────────────────────

    private void Process_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_pdf1Path) || string.IsNullOrEmpty(_pdf2Path)) return;

        try
        {
            Logger.Info("Starting extraction...");
            StatusText.Text = "Extracting PDF 1 (Zone Sizing Summary)...";
            _pdf1Data = _pdf1Extractor.Extract(_pdf1Path);
            Logger.Info($"PDF 1 extracted: {_pdf1Data.AirSystems.Count} systems, " +
                        $"{_pdf1Data.AirSystems.Sum(s => s.Spaces.Count)} spaces");

            StatusText.Text = "Extracting PDF 2 (Space Design Load Summary)...";
            _pdf2Data = _pdf2Extractor.Extract(_pdf2Path);
            Logger.Info($"PDF 2 extracted: {_pdf2Data.Count} space component load tables");

            // PDF 3 (optional)
            if (!string.IsNullOrEmpty(_pdf3Path))
            {
                StatusText.Text = "Extracting PDF 3 (Air System Sizing Summary)...";
                _pdf3Data = _pdf3Extractor.Extract(_pdf3Path);
                Logger.Info($"PDF 3 extracted: {_pdf3Data.Count} air system sizing entries");
                foreach (var s in _pdf3Data)
                    Logger.Info($"  {s.SystemName}: ft²/Ton={s.SqftPerTon}, FloorArea={s.FloorArea}, Tons={s.TotalCoilLoadTons}");
            }
            else
            {
                _pdf3Data = null;
            }

            StatusText.Text = "Combining data...";
            _combinedData = _combiner.Combine(_pdf1Data, _pdf2Data);
            Logger.Info($"Combined: {_combinedData.Count} records");

            // Update UI
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
            StatusText.Text = statusParts + $" — Log: {Logger.LogFilePath}";
        }
        catch (Exception ex)
        {
            Logger.Error("Process_Click failed", ex);
            StatusText.Text = $"Error: {ex.Message}";
            MessageBox.Show($"Failed to process PDFs:\n\n{ex.Message}\n\nLog: {Logger.LogFilePath}",
                "Processing Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
            StatusText.Text = $"Exported to {dialog.FileName}";
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
            StatusText.Text = $"Exported to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            Logger.Error("Matt's Way export failed", ex);
            MessageBox.Show($"Failed to export Matt's Way:\n\n{ex.Message}",
                "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
