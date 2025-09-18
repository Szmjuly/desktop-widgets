using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using CoffeeStockWidget.Core.Models;
using CoffeeStockWidget.Core.Services;
using CoffeeStockWidget.Infrastructure.Net;
using CoffeeStockWidget.Infrastructure.Storage;
using CoffeeStockWidget.Scraping.BlackAndWhite;
using CoffeeStockWidget.Scraping.Brandywine;
using CoffeeStockWidget.Core.Abstractions;
using Forms = System.Windows.Forms;
using System.Runtime.InteropServices;
using CoffeeStockWidget.Infrastructure.Settings;

namespace CoffeeStockWidget.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly HttpFetcher _http = new(minDelayPerHostMs: 1200);
    private readonly Dictionary<string, ISiteScraper> _scrapers;
    private readonly SqliteDataStore _store;
    private readonly ChangeDetector _detector = new();
    private Source _source;
    private readonly Forms.NotifyIcon _notifyIcon;
    private CancellationTokenSource? _loopCts;
    private bool _paused;
    private bool _attachToDesktop;
    private readonly SettingsService _settings = new();
    private readonly List<Source> _sources;
    private Dictionary<string, string> _customColors = new(StringComparer.OrdinalIgnoreCase);
    private DateTimeOffset? _lastAcknowledgedUtc;
    private DateTimeOffset _lastEventUtc;
    private ViewMode _viewMode = ViewMode.Normal;
    private System.Windows.Media.Color _currentShellColor = System.Windows.Media.Color.FromArgb(0xE5, 0x12, 0x12, 0x12);

    // Controls whether item hover shows a light accent-tinted overlay (bound in XAML)
    public static readonly DependencyProperty AccentHoverTintEnabledProperty =
        DependencyProperty.Register(nameof(AccentHoverTintEnabled), typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

    public bool AccentHoverTintEnabled
    {
        get => (bool)GetValue(AccentHoverTintEnabledProperty);
        set => SetValue(AccentHoverTintEnabledProperty, value);
    }

    private void ApplyRoundedCorners(double radiusDip)
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;
            var source = System.Windows.PresentationSource.FromVisual(this);
            double scaleX = 1.0, scaleY = 1.0;
            if (source?.CompositionTarget != null)
            {
                var m = source.CompositionTarget.TransformToDevice;
                scaleX = m.M11; scaleY = m.M22;
            }
            int w = Math.Max(1, (int)Math.Round(ActualWidth * scaleX));
            int h = Math.Max(1, (int)Math.Round(ActualHeight * scaleY));
            int rx = Math.Max(1, (int)Math.Round(radiusDip * scaleX)) * 2;
            int ry = Math.Max(1, (int)Math.Round(radiusDip * scaleY)) * 2;
            IntPtr rgn = CreateRoundRectRgn(0, 0, w + 1, h + 1, rx, ry);
            SetWindowRgn(hwnd, rgn, true);
            // Do not delete region; the system owns it after SetWindowRgn
        }
        catch { }
    }

    private enum ViewMode
    {
        Normal,
        AllAggregated
    }

    public MainWindow()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            try
            {
                var dir = AppDomain.CurrentDomain.BaseDirectory;
                var path = System.IO.Path.Combine(dir, "WindowInit.log");
                System.IO.File.WriteAllText(path, ex.ToString());
            }
            catch { }
            System.Windows.MessageBox.Show("Failed to initialize window. See WindowInit.log for details.\n" + ex.Message, "Coffee Stock Widget", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }

        _store = new SqliteDataStore();
        _source = new Source
        {
            Name = "Black & White Roasters",
            RootUrl = new Uri("https://www.blackwhiteroasters.com/collections/all-coffee"),
            ParserType = "BlackAndWhite",
            PollIntervalSeconds = 300,
            Enabled = true
        };

        _scrapers = new Dictionary<string, ISiteScraper>(StringComparer.OrdinalIgnoreCase)
        {
            ["BlackAndWhite"] = new BlackAndWhiteScraper(_http),
            ["Brandywine"] = new BrandywineScraper(_http)
        };

        _source.DefaultColorHex = "#FF90CAF9"; // light blue

        _sources = new List<Source>
        {
            _source,
            new Source
            {
                Name = "Brandywine Coffee Roasters",
                RootUrl = new Uri("https://www.brandywinecoffeeroasters.com/collections/all-coffee-1"),
                ParserType = "Brandywine",
                PollIntervalSeconds = 300,
                Enabled = true,
                DefaultColorHex = "#FF80CBC4" // teal
            }
        };

        _notifyIcon = CreateTrayIcon();
        Loaded += async (_, __) =>
        {
            // Load settings
            var s = await _settings.LoadAsync();

            // Select persisted roaster if available
            if (!string.IsNullOrWhiteSpace(s.SelectedParserType))
            {
                var chosen = _sources.FirstOrDefault(x => string.Equals(x.ParserType, s.SelectedParserType, StringComparison.OrdinalIgnoreCase));
                if (chosen != null) _source = chosen;
            }

            // Apply poll interval
            if (s.PollIntervalSeconds >= 10)
            {
                _source.PollIntervalSeconds = s.PollIntervalSeconds;
            }

            // Apply visual settings and load acknowledgment time
            _lastAcknowledgedUtc = s.LastAcknowledgedUtc;
            ApplyVisualSettings(s);

            // Load custom roaster colors
            _customColors = s.CustomParserColors != null
                ? new Dictionary<string, string>(s.CustomParserColors, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Apply enabled sources from settings if present
            if (s.EnabledParsers is { Count: > 0 })
            {
                var set = new HashSet<string>(s.EnabledParsers, StringComparer.OrdinalIgnoreCase);
                foreach (var src in _sources)
                {
                    src.Enabled = set.Contains(src.ParserType!);
                }
            }

            // Initial prune based on retention (clamp days >= 1)
            var pruneDays = Math.Max(1, s.RetentionDays);
            _ = _store.PruneAsync(new RetentionPolicy { Days = pruneDays, ItemsPerSource = s.ItemsPerSource, EventsPerSource = s.EventsPerSource });

            // Set roaster label
            SourceLabel.Text = GetSourceDisplayName();

            // Ensure Source has a concrete Id so we can load cached items immediately
            await _store.UpsertSourceAsync(_source);
            await RefreshViewAsync();

            StartLoop();
            if (_attachToDesktop) ApplyDesktopAttachment();
            await RefreshEmptyStateAsync();
            ApplyRoundedCorners(12);
            UpdateRootClip(12);
        };
        Activated += (_, __) => { if (_attachToDesktop) ApplyDesktopAttachment(); };
        SourceInitialized += (_, __) =>
        {
            try
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                // Extend frame to enable transparent background composition for blur
                var margins = new MARGINS { cxLeftWidth = -1 };
                DwmExtendFrameIntoClientArea(hwnd, ref margins);
                // Hide from Alt-Tab
                int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TOOLWINDOW);

                var src = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
                src!.CompositionTarget.BackgroundColor = System.Windows.Media.Colors.Transparent;

                ApplyRoundedCorners(12);
                UpdateRootClip(12);
                // Apply Win11 rounded corner preference early (settings-based backdrop handled later)
                ApplyWin11WindowAttributes(false, false);
            }
            catch { }
        };
        SizeChanged += (_, __) => { ApplyRoundedCorners(12); UpdateRootClip(12); };
        Closed += (_, __) => { _notifyIcon.Visible = false; _notifyIcon.Dispose(); };
    }

    private async Task RunCurrentAsync()
    {
        try
        {
            await Dispatcher.InvokeAsync(() => StatusText.Text = "Running current source...", DispatcherPriority.Background);

            var src = _source;
            await _store.UpsertSourceAsync(src);
            var prev = await _store.GetItemsBySourceAsync(src.Id!.Value);
            var prevDict = prev.ToDictionary(i => i.ItemKey);
            var scraper = GetScraperFor(src);
            var items = await scraper.FetchAsync(src);
            // Optional notes enrichment (slower): only for new/missing notes, capped
            var s = await _settings.LoadAsync();
            if (s.FetchNotesEnabled)
            {
                await EnrichNotesAsync(src, items, prevDict, s);
            }
            var events = _detector.Compare(prev, items);
            await _store.UpsertItemsAsync(items);

            var newKeys = items.Where(i => !prevDict.ContainsKey(i.ItemKey)).Select(i => i.ItemKey);
            var backKeys = items.Where(i => prevDict.TryGetValue(i.ItemKey, out var old) && !old.InStock && i.InStock).Select(i => i.ItemKey);
            var unseenKeys = newKeys.Concat(backKeys).Distinct().ToList();
            if (unseenKeys.Count > 0) await _store.SetItemsUnseenAsync(src.Id!.Value, unseenKeys);

            if (_viewMode == ViewMode.Normal)
            {
                // Prefer freshly fetched items, but if fetch returned 0, fall back to cached DB items
                IReadOnlyList<CoffeeItem> used = items;
                if (used.Count == 0 && src.Id.HasValue)
                {
                    var cached = await _store.GetItemsBySourceAsync(src.Id.Value);
                    if (cached.Count > 0) used = cached;
                }
                var selectedUi = used.OrderBy(i => i.Title).Select(ToUiItemForView).ToList();
                var selectedInStock = used.Count(i => i.InStock);
                var label = $"{GetSourceDisplayName()} ({selectedInStock})";
                await Dispatcher.InvokeAsync(() =>
                {
                    ItemsList.ItemsSource = selectedUi;
                    SourceLabel.Text = label;
                    UpdateEmptyOverlay(selectedUi?.Count ?? 0);
                });
            }
            else
            {
                await RefreshViewAsync();
            }

            await UpdateUnseenUiAsync();
            var totalNew = events.Count(e => e.EventType is StockEventType.NewItem or StockEventType.BackInStock);
            if (totalNew > 0)
            {
                _notifyIcon.BalloonTipTitle = "Coffee Stock Updates";
                _notifyIcon.BalloonTipText = $"{totalNew} item(s) updated";
                _notifyIcon.ShowBalloonTip(3000);
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() => StatusText.Text = "Error: " + ex.Message);
        }
    }

    private string GetSourceShortNameById(int sourceId)
    {
        var src = _sources.FirstOrDefault(s => s.Id == sourceId);
        return src == null ? string.Empty : GetDisplayName(src);
    }

    private async Task RefreshViewAsync()
    {
        try
        {
            if (_viewMode == ViewMode.Normal)
            {
                if (_source.Id.HasValue)
                {
                    var items = await _store.GetItemsBySourceAsync(_source.Id.Value);
                    var ui = items.OrderBy(i => i.Title).Select(ToUiItemForView).ToList();
                    var label = $"{GetSourceDisplayName()} ({items.Count(i => i.InStock)})";
                    await Dispatcher.InvokeAsync(() =>
                    {
                        ItemsList.ItemsSource = ui;
                        SourceLabel.Text = label;
                        UpdateEmptyOverlay(ui.Count);
                    });
                }
            }
            else if (_viewMode == ViewMode.AllAggregated)
            {
                var all = new List<CoffeeItem>();
                foreach (var s in _sources)
                {
                    if (!s.Id.HasValue) continue;
                    var items = await _store.GetItemsBySourceAsync(s.Id.Value);
                    all.AddRange(items);
                }
                var ui = all
                    .OrderBy(i => GetSourceShortNameById(i.SourceId))
                    .ThenBy(i => i.Title)
                    .Select(ToUiItemForView)
                    .ToList();
                await Dispatcher.InvokeAsync(() =>
                {
                    ItemsList.ItemsSource = ui;
                    SourceLabel.Text = "All coffees";
                    UpdateEmptyOverlay(ui.Count);
                });
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() => StatusText.Text = "Error: " + ex.Message);
        }
    }

    // Win32 blur-behind (reliable background blur without blurring content)
    private void EnableBlurBehind(bool enable, bool acrylic)
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            // Derive gradient color (ABGR) from the last computed shell color
            // For ACCENT_ENABLE_BLURBEHIND, use a very low alpha so blur is clearly visible.
            // For acrylic, use a stronger alpha to emulate acrylic tint.
            uint ToAbgr(System.Windows.Media.Color c) => (uint)(c.A << 24 | c.B << 16 | c.G << 8 | c.R);
            byte alpha = acrylic ? (byte)0xCC : (byte)0x18; // acrylic ~80% tint; blur ~9% tint
            var tinted = System.Windows.Media.Color.FromArgb(alpha, _currentShellColor.R, _currentShellColor.G, _currentShellColor.B);
            uint gradient = ToAbgr(tinted);

            var accent = new ACCENT_POLICY
            {
                AccentState = enable ? (acrylic ? ACCENT_STATE.ACCENT_ENABLE_ACRYLICBLURBEHIND : ACCENT_STATE.ACCENT_ENABLE_BLURBEHIND) : ACCENT_STATE.ACCENT_DISABLED,
                AccentFlags = 2, // blur all borders
                GradientColor = gradient,
                AnimationId = 0
            };
            int size = Marshal.SizeOf(accent);
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(accent, ptr, false);
                var data = new WINDOWCOMPOSITIONATTRIBDATA
                {
                    Attribute = WINDOWCOMPOSITIONATTRIB.WCA_ACCENT_POLICY,
                    Data = ptr,
                    SizeOfData = size
                };
                SetWindowCompositionAttribute(hwnd, ref data);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
        catch { /* ignore blur failures */ }
    }

    private void UpdateRootClip(double radiusDip)
    {
        try
        {
            if (RootBorder.ActualWidth <= 0 || RootBorder.ActualHeight <= 0) return;
            var rect = new System.Windows.Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight);
            RootBorder.Clip = new System.Windows.Media.RectangleGeometry(rect, radiusDip, radiusDip);
        }
        catch { }
    }

    private enum WINDOWCOMPOSITIONATTRIB
    {
        WCA_ACCENT_POLICY = 19
    }

    private enum ACCENT_STATE
    {
        ACCENT_DISABLED = 0,
        ACCENT_ENABLE_GRADIENT = 1,
        ACCENT_ENABLE_TRANSPARENTGRADIENT = 2,
        ACCENT_ENABLE_BLURBEHIND = 3,
        ACCENT_ENABLE_ACRYLICBLURBEHIND = 4,
        ACCENT_INVALID_STATE = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ACCENT_POLICY
    {
        public ACCENT_STATE AccentState;
        public uint AccentFlags;
        public uint GradientColor;
        public uint AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WINDOWCOMPOSITIONATTRIBDATA
    {
        public WINDOWCOMPOSITIONATTRIB Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MARGINS
    {
        public int cxLeftWidth;
        public int cxRightWidth;
        public int cyTopHeight;
        public int cyBottomHeight;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRoundRectRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect, int nWidthEllipse, int nHeightEllipse);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hWnd, ref MARGINS pMarInset);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int GWL_STYLE = -16;
    private const int WS_SYSMENU = 0x80000;

    // Windows 11 DWM attributes
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
    private const int DWMWCP_DEFAULT = 0;
    private const int DWMWCP_DONOTROUND = 1;
    private const int DWMWCP_ROUND = 2;
    private const int DWMWCP_ROUNDSMALL = 3;
    private const int DWMSBT_NONE = 0; // disable system backdrop
    private const int DWMSBT_MAINWINDOW = 2; // Mica
    private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic-like

    private void ApplyWin11WindowAttributes(bool blur, bool acrylic)
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero) return;

            // Always ask for rounded corners on Win11
            int corner = DWMWCP_ROUND;
            _ = DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            
            // Avoid setting a system backdrop here to prevent conflicts with AccentPolicy blur.
            int backdrop = DWMSBT_NONE;
            _ = DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref backdrop, sizeof(int));
        }
        catch { /* ignore on unsupported systems */ }
    }

    private void ReloadBtn_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var cm = new ContextMenu
        {
            PlacementTarget = ReloadBtn,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom,
            Style = TryFindResource("TrayMenuStyle") as Style
        };
        var miStyle = TryFindResource("TrayMenuItemStyle") as Style;
        var runEnabled = new MenuItem { Header = "Run Enabled Now", Style = miStyle };
        runEnabled.Click += async (_, __) => await RunMassAsync(includeDisabled: false);
        var runAll = new MenuItem { Header = "Run ALL Now (ignore enabled)", Style = miStyle };
        runAll.Click += async (_, __) => await RunMassAsync(includeDisabled: true);
        var refreshNotes = new MenuItem { Header = "Refresh notes (current source)", Style = miStyle };
        refreshNotes.Click += async (_, __) => await RefreshNotesForCurrentSourceAsync();
        var markAll = new MenuItem { Header = "Mark all as seen", Style = miStyle };
        markAll.Click += async (_, __) => { await _store.MarkAllSeenAsync(DateTimeOffset.UtcNow); await UpdateUnseenUiAsync(); };
        cm.Items.Add(runEnabled);
        cm.Items.Add(runAll);
        cm.Items.Add(refreshNotes);
        cm.Items.Add(new Separator());
        cm.Items.Add(markAll);
        cm.IsOpen = true;
    }

    private Forms.NotifyIcon CreateTrayIcon()
    {
        var ni = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Visible = true,
            Text = "Coffee Stock Widget"
        };
        ni.DoubleClick += (_, __) => ToggleWindow();
        ni.MouseUp += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                Dispatcher.Invoke(ShowTrayMenu);
            }
            else if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                Dispatcher.Invoke(ToggleWindow);
            }
        };
        return ni;
    }

    private void ShowTrayMenu()
    {
        var cm = new ContextMenu { Style = TryFindResource("TrayMenuStyle") as Style };

        MenuItem NewItem(string header, RoutedEventHandler onClick, bool isCheckable = false, bool isChecked = false)
        {
            var mi = new MenuItem { Header = header, Style = TryFindResource("TrayMenuItemStyle") as Style, IsCheckable = isCheckable, IsChecked = isChecked };
            mi.Click += onClick;
            return mi;
        }

        cm.Items.Add(NewItem("Show/Hide", (_, __) => ToggleWindow()));
        cm.Items.Add(new Separator());
        cm.Items.Add(NewItem("Pause", (_, __) =>
        {
            _paused = !_paused;
            StatusText.Text = _paused ? "Paused" : "Running";
        }, isCheckable: true, isChecked: _paused));
        cm.Items.Add(NewItem("Attach to Desktop", (_, __) =>
        {
            _attachToDesktop = !_attachToDesktop;
            ApplyDesktopAttachment();
        }, isCheckable: true, isChecked: _attachToDesktop));
        cm.Items.Add(new Separator());
        cm.Items.Add(NewItem("Run Enabled Now", async (_, __) => await RunMassAsync(includeDisabled: false)));
        cm.Items.Add(NewItem("Run ALL Now (ignore enabled)", async (_, __) => await RunMassAsync(includeDisabled: true)));
        cm.Items.Add(NewItem("Mark All as Seen", async (_, __) => { await _store.MarkAllSeenAsync(DateTimeOffset.UtcNow); await UpdateUnseenUiAsync(); }));
        cm.Items.Add(new Separator());
        cm.Items.Add(NewItem("Exit", (_, __) => System.Windows.Application.Current.Shutdown()));

        cm.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        cm.IsOpen = true;
    }

    private void ToggleWindow()
    {
        if (IsVisible)
        {
            Hide();
        }
        else
        {
            Show();
            Activate();
            Topmost = true; // ensure on top after showing
        }
    }

    private void TogglePause(Forms.ToolStripMenuItem pauseItem)
    {
        _paused = !_paused;
        pauseItem.Checked = _paused;
        pauseItem.Text = _paused ? "Resume" : "Pause";
        StatusText.Text = _paused ? "Paused" : "Running";
    }

    private void ToggleAttach(Forms.ToolStripMenuItem attachItem)
    {
        _attachToDesktop = !_attachToDesktop;
        attachItem.Checked = _attachToDesktop;
        ApplyDesktopAttachment();
    }

    private void ApplyDesktopAttachment()
    {
        Topmost = false; // never on top when attached
        if (_attachToDesktop)
        {
            SendToBottom();
        }
    }

    private void StartLoop()
    {
        _loopCts?.Cancel();
        _loopCts = new CancellationTokenSource();
        _ = Task.Run(() => LoopAsync(_loopCts.Token));
    }

    private async Task LoopAsync(CancellationToken ct)
    {
        var jitter = new Random();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (!_paused)
                {
                    await Dispatcher.InvokeAsync(() => StatusText.Text = "Checking...", DispatcherPriority.Background);

                    int totalNew = 0;
                    List<UiItem>? selectedUi = null;
                    int selectedInStock = 0;

                    foreach (var src in _sources.Where(s => s.Enabled))
                    {
                        // Ensure Source exists and has a concrete Id before item upserts
                        await _store.UpsertSourceAsync(src, ct);
                        var prev = await _store.GetItemsBySourceAsync(src.Id!.Value, ct);
                        var prevDict = prev.ToDictionary(i => i.ItemKey);
                        var scraper = GetScraperFor(src);
                        var items = await scraper.FetchAsync(src, ct);
                        var events = _detector.Compare(prev, items);

                        await _store.UpsertItemsAsync(items, ct);

                        // Mark new/back-in-stock as unseen
                        var newKeys = items
                            .Where(i => !prevDict.ContainsKey(i.ItemKey))
                            .Select(i => i.ItemKey);
                        var backKeys = items
                            .Where(i => prevDict.TryGetValue(i.ItemKey, out var old) && !old.InStock && i.InStock)
                            .Select(i => i.ItemKey);
                        var unseenKeys = newKeys.Concat(backKeys).Distinct().ToList();
                        if (unseenKeys.Count > 0)
                        {
                            await _store.SetItemsUnseenAsync(src.Id!.Value, unseenKeys, ct);
                        }

                        if (ReferenceEquals(src, _source))
                        {
                            // Prefer freshly fetched items, but if the fetch returned 0, fall back to cached DB items to avoid flashing an empty view
                            IReadOnlyList<CoffeeItem> used = items;
                            if (used.Count == 0)
                            {
                                var cached = await _store.GetItemsBySourceAsync(src.Id!.Value, ct);
                                if (cached.Count > 0) used = cached;
                            }
                            selectedUi = used
                                .OrderBy(i => i.Title)
                                .Select(ToUiItem)
                                .ToList();
                            selectedInStock = used.Count(i => i.InStock);
                        }

                        var newCount = events.Count(e => e.EventType is StockEventType.NewItem or StockEventType.BackInStock);
                        totalNew += newCount;
                    }

                    if (_viewMode == ViewMode.Normal)
                    {
                        if (selectedUi != null)
                        {
                            var label = $"{GetSourceDisplayName()} ({selectedInStock})";
                            await Dispatcher.InvokeAsync(() =>
                            {
                                ItemsList.ItemsSource = selectedUi;
                                SourceLabel.Text = label;
                                UpdateEmptyOverlay(selectedUi.Count);
                            });
                        }
                    }
                    else
                    {
                        await RefreshViewAsync();
                    }

                    // Unseen count across all roasters controls the bubble
                    var unseenCount = await _store.GetUnseenCountAsync(ct);
                    // Avoid overriding the overlay state unless we have a freshly computed selection
                    if (_viewMode == ViewMode.Normal && selectedUi != null)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            UpdateEmptyOverlay(selectedUi.Count);
                        });
                    }

                    if (totalNew > 0)
                    {
                        _lastEventUtc = DateTimeOffset.UtcNow;
                        var showBubble = unseenCount > 0;
                        await Dispatcher.InvokeAsync(() =>
                        {
                            Bubble.Visibility = showBubble ? Visibility.Visible : Visibility.Collapsed;
                            StatusText.Text = unseenCount > 0 ? $"{unseenCount} updates" : $"Last check: {DateTime.Now:t}";
                        });
                        _notifyIcon.BalloonTipTitle = "Coffee Stock Updates";
                        _notifyIcon.BalloonTipText = $"{totalNew} item(s) updated across roasters";
                        _notifyIcon.ShowBalloonTip(3000);
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(async () =>
                        {
                            var count = await _store.GetUnseenCountAsync();
                            Bubble.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
                            StatusText.Text = count > 0 ? $"{count} updates" : $"Last check: {DateTime.Now:t}";
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() => StatusText.Text = "Error: " + ex.Message);
            }

            var baseDelay = TimeSpan.FromSeconds(_source.PollIntervalSeconds);
            var extraMs = jitter.Next(500, 2000);
            // Prune per retention setting after each cycle
            try
            {
                var s = await _settings.LoadAsync();
                var pruneDays3 = Math.Max(1, s.RetentionDays);
                await _store.PruneAsync(new RetentionPolicy { Days = pruneDays3, ItemsPerSource = s.ItemsPerSource, EventsPerSource = s.EventsPerSource }, ct);
            }
            catch { /* ignore prune errors */ }

            try { await Task.Delay(baseDelay + TimeSpan.FromMilliseconds(extraMs), ct); } catch { }
        }
    }

    // Allow click-drag to move the borderless window
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        try { DragMove(); } catch { }
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        System.Windows.Application.Current.Shutdown();
    }

    private async void SettingsBtn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow { Owner = this, Sources = _sources };
        var ok = dlg.ShowDialog();
        if (ok == true)
        {
            var s = await _settings.LoadAsync();
            if (s.PollIntervalSeconds >= 10)
            {
                _source.PollIntervalSeconds = s.PollIntervalSeconds;
            }
            var pruneDays2 = Math.Max(1, s.RetentionDays);
            _ = _store.PruneAsync(new RetentionPolicy { Days = pruneDays2, ItemsPerSource = s.ItemsPerSource, EventsPerSource = s.EventsPerSource });
            // Sync enabled roasters from settings
            if (s.EnabledParsers is { Count: > 0 })
            {
                var set = new HashSet<string>(s.EnabledParsers, StringComparer.OrdinalIgnoreCase);
                foreach (var src in _sources)
                {
                    src.Enabled = set.Contains(src.ParserType!);
                }
            }
            else
            {
                // If none specified, ensure at least current source remains enabled
                foreach (var src in _sources) src.Enabled = ReferenceEquals(src, _source);
            }
            // Refresh custom colors map
            _customColors = s.CustomParserColors != null
                ? new Dictionary<string, string>(s.CustomParserColors, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            await RefreshViewAsync();
            _loopCts?.Cancel();
            StartLoop();
            await RefreshEmptyStateAsync();
        }
    }

    // Single-click: show details dialog with notes and actions
    private void Item_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1) return; // double-click handled elsewhere
        if (sender is FrameworkElement fe && fe.DataContext is UiItem ui)
        {
            var dlg = new CoffeeDetailsWindow
            {
                Owner = this,
                DataContext = ui
            };
            dlg.ShowDialog();
        }
    }

    private async void ReloadBtn_Click(object? sender, RoutedEventArgs e)
    {
        StatusText.Text = "Refreshing...";
        _loopCts?.Cancel();
        await RunCurrentAsync();
        StartLoop();
    }

    public async void OnDatabaseCleared()
    {
        _loopCts?.Cancel();
        ItemsList.ItemsSource = null;
        Bubble.Visibility = Visibility.Collapsed;
        StatusText.Text = "Empty database — click ⟳ to fetch";
        await RefreshEmptyStateAsync();
    }

    private async void SourceButton_Click(object sender, RoutedEventArgs e)
    {
        var cm = new ContextMenu
        {
            PlacementTarget = SourceButton,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };
        cm.MinWidth = SourcePill.ActualWidth;
        cm.Width = SourcePill.ActualWidth;
        // Single aggregated entry
        var showAll = new MenuItem { Header = "Show all coffees" };
        showAll.Click += async (_, __) => { _viewMode = ViewMode.AllAggregated; await RefreshViewAsync(); };
        cm.Items.Add(showAll);
        cm.Items.Add(new Separator());

        // Per-roaster selection with unseen counts
        var unseenMap = await _store.GetUnseenCountsBySourceAsync();
        foreach (var src in _sources)
        {
            var implemented = IsParserImplemented(src.ParserType);
            var name = GetDisplayName(src);
            unseenMap.TryGetValue(src.Id ?? -1, out var unseen);
            var suffix = unseen > 0 ? $" ({unseen})" : string.Empty;
            var header = implemented ? name + suffix : name + " (coming soon)" + suffix;
            var mi = new MenuItem
            {
                Header = header,
                IsCheckable = true,
                IsChecked = ReferenceEquals(src, _source),
                Tag = src,
                IsEnabled = implemented
            };
            mi.Click += async (s, _) =>
            {
                if (s is MenuItem m && m.Tag is Source target)
                {
                    _viewMode = ViewMode.Normal;
                    await SwitchSourceAsync(target);
                }
            };
            cm.Items.Add(mi);
        }
        cm.IsOpen = true;
    }

    private async Task SwitchSourceAsync(Source s)
    {
        if (ReferenceEquals(_source, s)) return;
        _loopCts?.Cancel();
        _source = s;
        // Update label quickly with name; detailed count will be applied by RefreshViewAsync
        SourceLabel.Text = GetSourceDisplayName();
        // Ensure this source has a concrete Id so we can query cached items immediately
        await _store.UpsertSourceAsync(_source);
        // Load items from the database right away so the empty overlay and pill count are correct
        await RefreshViewAsync();
        StartLoop();
        await SaveSelectedRoasterAsync();
    }

    private static bool IsParserImplemented(string? parserType)
        => string.Equals(parserType, "BlackAndWhite", StringComparison.OrdinalIgnoreCase)
           || string.Equals(parserType, "Brandywine", StringComparison.OrdinalIgnoreCase);

    private static string GetDisplayName(Source s)
    {
        var name = s.Name ?? string.Empty;
        var shortName = name.Replace("Roasters", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(shortName) ? name : shortName;
    }

    private string GetSourceDisplayName()
    {
        var name = _source.Name ?? string.Empty;
        // Prefer a shorter display (remove trailing 'Roasters' if present)
        var shortName = name.Replace("Roasters", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        return string.IsNullOrWhiteSpace(shortName) ? name : shortName;
    }

    private ISiteScraper GetScraperFor(Source s)
    {
        if (s.ParserType != null && _scrapers.TryGetValue(s.ParserType, out var scraper))
        {
            return scraper;
        }
        return _scrapers["BlackAndWhite"]; // fallback
    }

    private void ApplyVisualSettings(AppSettings s)
    {
        // Clamp and convert percent to alpha (0-255)
        var p = Math.Max(0, Math.Min(100, s.TransparencyPercent));
        byte a = (byte)Math.Round(p * 2.55);
        var shell = System.Windows.Media.Color.FromArgb(a, 0x12, 0x12, 0x12);
        _currentShellColor = shell;
        RootBorder.Background = s.BlurEnabled
            ? System.Windows.Media.Brushes.Transparent
            : new System.Windows.Media.SolidColorBrush(shell);
        // Strengthen border when blur is on, to avoid it visually disappearing over bright backdrops
        RootBorder.BorderBrush = s.BlurEnabled
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0x00, 0x00, 0x00));
        // Keep content transparent per design
        RootBorder.Effect = null; // never blur content; use OS blur-behind instead
        EnableBlurBehind(s.BlurEnabled, acrylic: s.AcrylicEnabled);
        ApplyWin11WindowAttributes(s.BlurEnabled, s.AcrylicEnabled);
        // Bindable flag for hover tint
        AccentHoverTintEnabled = s.AccentHoverTintEnabled;
        // Ensure window region stays rounded when toggling blur/acrylic
        ApplyRoundedCorners(12);
        UpdateRootClip(12);
    }

    // Allow live preview from SettingsWindow without persisting yet
    public void ApplyAppearancePreview(int transparencyPercent, bool blur, bool acrylic, bool accentTint)
    {
        var s = new AppSettings
        {
            TransparencyPercent = transparencyPercent,
            BlurEnabled = blur,
            AcrylicEnabled = acrylic,
            AccentHoverTintEnabled = accentTint
        };
        ApplyVisualSettings(s);
    }

    private async Task SaveSelectedRoasterAsync()
    {
        var s = await _settings.LoadAsync();
        s.SelectedParserType = _source.ParserType;
        await _settings.SaveAsync(s);
    }

    private async Task UpdateUnseenUiAsync()
    {
        var count = await _store.GetUnseenCountAsync();
        await Dispatcher.InvokeAsync(() =>
        {
            Bubble.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = count > 0 ? $"{count} updates" : $"Last check: {DateTime.Now:t}";
        });
    }

    private async Task RunMassAsync(bool includeDisabled)
    {
        try
        {
            await Dispatcher.InvokeAsync(() => StatusText.Text = includeDisabled ? "Running ALL sources..." : "Running enabled sources...", DispatcherPriority.Background);

            int totalNew = 0;
            List<UiItem>? selectedUi = null;
            int selectedInStock = 0;

            var list = includeDisabled ? _sources : _sources.Where(s => s.Enabled).ToList();
            var settings = await _settings.LoadAsync();
            foreach (var src in list)
            {
                await _store.UpsertSourceAsync(src);
                var prev = await _store.GetItemsBySourceAsync(src.Id!.Value);
                var prevDict = prev.ToDictionary(i => i.ItemKey);
                var scraper = GetScraperFor(src);
                var items = await scraper.FetchAsync(src);
                if (settings.FetchNotesEnabled)
                {
                    await EnrichNotesAsync(src, items, prevDict, settings);
                }
                var events = _detector.Compare(prev, items);
                await _store.UpsertItemsAsync(items);

                var newKeys = items.Where(i => !prevDict.ContainsKey(i.ItemKey)).Select(i => i.ItemKey);
                var backKeys = items.Where(i => prevDict.TryGetValue(i.ItemKey, out var old) && !old.InStock && i.InStock).Select(i => i.ItemKey);
                var unseenKeys = newKeys.Concat(backKeys).Distinct().ToList();
                if (unseenKeys.Count > 0) await _store.SetItemsUnseenAsync(src.Id!.Value, unseenKeys);

                if (ReferenceEquals(src, _source))
                {
                    selectedUi = items.OrderBy(i => i.Title).Select(ToUiItemForView).ToList();
                    selectedInStock = items.Count(i => i.InStock);
                }
                totalNew += events.Count(e => e.EventType is StockEventType.NewItem or StockEventType.BackInStock);
            }

            if (selectedUi != null)
            {
                var label = $"{GetSourceDisplayName()} ({selectedInStock})";
                await Dispatcher.InvokeAsync(() =>
                {
                    ItemsList.ItemsSource = selectedUi;
                    SourceLabel.Text = label;
                    UpdateEmptyOverlay(selectedUi.Count);
                });
            }

            await UpdateUnseenUiAsync();
            if (totalNew > 0)
            {
                _notifyIcon.BalloonTipTitle = "Coffee Stock Updates";
                _notifyIcon.BalloonTipText = $"{totalNew} item(s) updated across roasters";
                _notifyIcon.ShowBalloonTip(3000);
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() => StatusText.Text = "Error: " + ex.Message);
        }
    }

    private void SendToBottom()
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private static System.Windows.Media.Color TryParseColor(string hex, System.Windows.Media.Color fallback)
    {
        try
        {
            var obj = System.Windows.Media.ColorConverter.ConvertFromString(hex);
            if (obj is System.Windows.Media.Color c) return c;
        }
        catch { }
        return fallback;
    }

    private string GetAccentHexFor(int sourceId)
    {
        var src = _sources.FirstOrDefault(s => s.Id == sourceId);
        if (src == null) return "#696969"; // DimGray
        if (!string.IsNullOrWhiteSpace(src.ParserType) && _customColors.TryGetValue(src.ParserType, out var custom))
        {
            return custom;
        }
        if (!string.IsNullOrWhiteSpace(src.CustomColorHex))
        {
            return src.CustomColorHex!;
        }
        return string.IsNullOrWhiteSpace(src.DefaultColorHex) ? "#696969" : src.DefaultColorHex!;
    }

    private UiItem ToUiItem(CoffeeItem i)
    {
        var price = i.PriceCents.HasValue ? ("$" + (i.PriceCents.Value / 100.0m).ToString("0.00")) : string.Empty;
        var stockText = i.InStock ? "In stock" : "Sold out";
        var accentHex = GetAccentHexFor(i.SourceId);
        var roaster = GetSourceShortNameById(i.SourceId);
        string? notes = null;
        if (i.Attributes != null && i.Attributes.TryGetValue("notes", out var n)) notes = n;
        return new UiItem { Roaster = roaster, Title = i.Title, Price = price, StockText = stockText, IsInStock = i.InStock, Url = i.Url, ItemKey = i.ItemKey, SourceId = i.SourceId, AccentHex = accentHex, Notes = notes };
    }

    private UiItem ToUiItemForView(CoffeeItem i)
    {
        var ui = ToUiItem(i);
        if (_viewMode == ViewMode.AllAggregated)
        {
            var prefix = GetSourceShortNameById(i.SourceId);
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                ui.Title = $"[{prefix}] {ui.Title}";
            }
        }
        return ui;
    }

    private class UiItem
    {
        public string Roaster { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string StockText { get; set; } = string.Empty;
        public bool IsInStock { get; set; }
        public Uri? Url { get; set; }
        public string ItemKey { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public string AccentHex { get; set; } = "#696969";
        public string? Notes { get; set; }

        // Create brushes on the UI thread when the binding reads these properties
        public System.Windows.Media.Brush StockBrush => IsInStock ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.OrangeRed;
        public System.Windows.Media.Brush AccentBrush => new System.Windows.Media.SolidColorBrush(TryParseColor(AccentHex, System.Windows.Media.Colors.DimGray));
    }

    // Open item in the default browser on double-click
    private void Item_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount < 2) return;
        if (sender is FrameworkElement fe && fe.DataContext is UiItem ui && ui.Url != null)
        {
            try
            {
                Process.Start(new ProcessStartInfo(ui.Url.ToString()) { UseShellExecute = true });
            }
            catch { /* ignore */ }
        }
    }

    // Acknowledge updates when hovering any item
    private async void Item_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is UiItem ui && !string.IsNullOrWhiteSpace(ui.ItemKey))
        {
            await _store.SetItemSeenAsync(ui.SourceId, ui.ItemKey, DateTimeOffset.UtcNow);
            var unseen = await _store.GetUnseenCountAsync();
            Bubble.Visibility = unseen > 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = unseen > 0 ? $"{unseen} updates" : $"Last check: {DateTime.Now:t}";
        }
    }

    private async Task RefreshNotesForCurrentSourceAsync()
    {
        try
        {
            StatusText.Text = "Refreshing notes...";
            if (!_source.Id.HasValue) return;
            var settings = await _settings.LoadAsync();
            var prev = await _store.GetItemsBySourceAsync(_source.Id!.Value);
            var prevDict = prev.ToDictionary(i => i.ItemKey);
            // Work from a copy of current DB items for enrichment
            var items = prev.Select(i => new CoffeeItem
            {
                Id = i.Id,
                SourceId = i.SourceId,
                ItemKey = i.ItemKey,
                Title = i.Title,
                Url = i.Url,
                PriceCents = i.PriceCents,
                InStock = i.InStock,
                FirstSeenUtc = i.FirstSeenUtc,
                LastSeenUtc = i.LastSeenUtc,
                Attributes = i.Attributes == null ? null : new Dictionary<string, string>(i.Attributes, StringComparer.OrdinalIgnoreCase)
            }).ToList();
            await EnrichNotesAsync(_source, items, prevDict, settings, force: true);
            await _store.UpsertItemsAsync(items);
            await RefreshViewAsync();
            await UpdateUnseenUiAsync();
            StatusText.Text = "Notes refreshed";
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() => StatusText.Text = "Error: " + ex.Message);
        }
    }

    private async Task EnrichNotesAsync(Source src, IReadOnlyList<CoffeeItem> items, Dictionary<string, CoffeeItem> prevDict, AppSettings settings, bool force = false)
    {
        var candidates = new List<CoffeeItem>();
        foreach (var i in items)
        {
            if (i.Url == null) continue;
            if (force)
            {
                candidates.Add(i);
                continue;
            }
            if (!prevDict.TryGetValue(i.ItemKey, out var prev))
            {
                candidates.Add(i);
            }
            else
            {
                var hasPrevNotes = prev.Attributes != null && prev.Attributes.TryGetValue("notes", out var pv) && !string.IsNullOrWhiteSpace(pv);
                if (!hasPrevNotes) candidates.Add(i);
            }
        }
        if (candidates.Count == 0) return;

        int cap = Math.Max(1, Math.Min(50, settings.MaxNotesFetchPerRun));
        foreach (var i in candidates.Take(cap))
        {
            try
            {
                var html = await _http.GetStringAsync(i.Url!, null, CancellationToken.None).ConfigureAwait(false);
                var notes = ExtractNotesFromHtml(html);
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    i.Attributes ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    i.Attributes["notes"] = notes!;
                }
            }
            catch { /* ignore individual failures */ }
        }
    }

    private static string? ExtractNotesFromHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;

        // Try common meta descriptions
        static string? ExtractMeta(string html, string name)
        {
            try
            {
                var pattern = $"<meta[^>]+(?:name|property)=[\"']{name}[\"'][^>]*content=[\"'](.*?)[\"']";
                var m = System.Text.RegularExpressions.Regex.Match(html, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                if (m.Success) return System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
            }
            catch { }
            return null;
        }
        var meta = ExtractMeta(html, "og:description") ?? ExtractMeta(html, "twitter:description") ?? ExtractMeta(html, "description");
        if (!string.IsNullOrWhiteSpace(meta))
        {
            var lm = meta!.ToLowerInvariant();
            if (lm.Contains("notes") || lm.Contains("tasting") || lm.Contains("flavor") || lm.Contains("flavour"))
            {
                return meta.Length > 300 ? meta.Substring(0, 300) : meta;
            }
        }

        // Fallback: basic visible text scan
        string text;
        try
        {
            text = System.Text.RegularExpressions.Regex.Replace(html, "<script[\\s\\S]*?</script>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, "<style[\\s\\S]*?</style>", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", "\n");
            text = System.Net.WebUtility.HtmlDecode(text);
        }
        catch { text = html; }

        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => s.Length > 0 && s.Length < 400)
                        .ToList();
        foreach (var line in lines)
        {
            var l = line.ToLowerInvariant();
            if (l.Contains("tasting notes") || l.StartsWith("notes:") || l.Contains("flavor notes") || l.Contains("flavour notes"))
            {
                var idx = line.IndexOf(':');
                if (idx >= 0 && idx + 1 < line.Length)
                {
                    var val = line.Substring(idx + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(val)) return val.Length > 300 ? val.Substring(0, 300) : val;
                }
                return line.Length > 300 ? line.Substring(0, 300) : line;
            }
        }
        return null;
    }

    private async Task SaveEnabledParsersAsync()
    {
        var s = await _settings.LoadAsync();
        s.EnabledParsers = _sources.Where(x => x.Enabled && x.ParserType != null).Select(x => x.ParserType!).ToList();
        await _settings.SaveAsync(s);
    }

    public async Task RefreshEmptyStateAsync()
    {
        var total = await _store.GetTotalItemsCountAsync();
        await Dispatcher.InvokeAsync(() =>
        {
            if (total == 0)
            {
                ItemsList.ItemsSource = null;
                StatusText.Text = "Empty database — click ⟳ to fetch";
                UpdateEmptyOverlay(0);
            }
        });
    }

    private void UpdateEmptyOverlay(int count)
    {
        // Adjust message based on current view
        if (count == 0)
        {
            var msg = _viewMode == ViewMode.Normal
                ? "No coffees for this roaster — click ⟳ to fetch"
                : "Empty database — click ⟳ to fetch";
            EmptyMessage.Text = msg;
            EmptyMessage.Visibility = Visibility.Visible;
        }
        else
        {
            EmptyMessage.Visibility = Visibility.Collapsed;
        }
    }
}