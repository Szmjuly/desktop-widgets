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

    private enum ViewMode
    {
        Normal,
        AllAggregated
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
            var events = _detector.Compare(prev, items);
            await _store.UpsertItemsAsync(items);

            var newKeys = items.Where(i => !prevDict.ContainsKey(i.ItemKey)).Select(i => i.ItemKey);
            var backKeys = items.Where(i => prevDict.TryGetValue(i.ItemKey, out var old) && !old.InStock && i.InStock).Select(i => i.ItemKey);
            var unseenKeys = newKeys.Concat(backKeys).Distinct().ToList();
            if (unseenKeys.Count > 0) await _store.SetItemsUnseenAsync(src.Id!.Value, unseenKeys);

            if (_viewMode == ViewMode.Normal)
            {
                var selectedUi = items.OrderBy(i => i.Title).Select(ToUiItemForView).ToList();
                var selectedInStock = items.Count(i => i.InStock);
                var label = $"{GetSourceDisplayName()} ({selectedInStock})";
                await Dispatcher.InvokeAsync(() =>
                {
                    ItemsList.ItemsSource = selectedUi;
                    SourceLabel.Text = label;
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
                        EmptyMessage.Visibility = ui.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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
                    EmptyMessage.Visibility = ui.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

            var accent = new ACCENT_POLICY
            {
                AccentState = enable ? (acrylic ? ACCENT_STATE.ACCENT_ENABLE_ACRYLICBLURBEHIND : ACCENT_STATE.ACCENT_ENABLE_BLURBEHIND) : ACCENT_STATE.ACCENT_DISABLED,
                AccentFlags = 2, // blur all borders
                GradientColor = 0x00000000,
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

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WINDOWCOMPOSITIONATTRIBDATA data);


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
    var markAll = new MenuItem { Header = "Mark all as seen", Style = miStyle };
    markAll.Click += async (_, __) => { await _store.MarkAllSeenAsync(DateTimeOffset.UtcNow); await UpdateUnseenUiAsync(); };
    cm.Items.Add(runEnabled);
    cm.Items.Add(runAll);
    cm.Items.Add(new Separator());
    cm.Items.Add(markAll);
    cm.IsOpen = true;
}

private ViewMode _viewMode = ViewMode.Normal;

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
        Id = 1,
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

            // Initial prune based on retention
            _ = _store.PruneAsync(new RetentionPolicy { Days = s.RetentionDays, ItemsPerSource = s.ItemsPerSource, EventsPerSource = s.EventsPerSource });

            // Set roaster label
            SourceLabel.Text = GetSourceDisplayName();

            StartLoop();
            if (_attachToDesktop) ApplyDesktopAttachment();
        };
        Activated += (_, __) => { if (_attachToDesktop) ApplyDesktopAttachment(); };
        Closed += (_, __) => { _notifyIcon.Visible = false; _notifyIcon.Dispose(); };
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
                            selectedUi = items
                                .OrderBy(i => i.Title)
                                .Select(ToUiItem)
                                .ToList();
                            selectedInStock = items.Count(i => i.InStock);
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
                            });
                        }
                    }
                    else
                    {
                        await RefreshViewAsync();
                    }

                    // Unseen count across all roasters controls the bubble
                    var unseenCount = await _store.GetUnseenCountAsync(ct);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        EmptyMessage.Visibility = (selectedUi == null || selectedUi.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
                    });

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
                await _store.PruneAsync(new RetentionPolicy { Days = s.RetentionDays, ItemsPerSource = s.ItemsPerSource, EventsPerSource = s.EventsPerSource }, ct);
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
            ApplyVisualSettings(s);
            _ = _store.PruneAsync(new RetentionPolicy { Days = s.RetentionDays, ItemsPerSource = s.ItemsPerSource, EventsPerSource = s.EventsPerSource });
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
            mi.Click += (s, _) =>
            {
                if (s is MenuItem m && m.Tag is Source target)
                {
                    _viewMode = ViewMode.Normal;
                    SwitchSource(target);
                }
            };
            cm.Items.Add(mi);
        }
        cm.IsOpen = true;
    }

    private void SwitchSource(Source s)
    {
        if (ReferenceEquals(_source, s)) return;
        _loopCts?.Cancel();
        _source = s;
        SourceLabel.Text = GetSourceDisplayName();
        StartLoop();
        _ = SaveSelectedRoasterAsync();
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
        RootBorder.Background = new System.Windows.Media.SolidColorBrush(shell);
        // Keep content transparent per design
        RootBorder.Effect = null; // never blur content; use OS blur-behind instead
        EnableBlurBehind(s.BlurEnabled, acrylic: s.AcrylicEnabled);
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
            foreach (var src in list)
            {
                await _store.UpsertSourceAsync(src);
                var prev = await _store.GetItemsBySourceAsync(src.Id!.Value);
                var prevDict = prev.ToDictionary(i => i.ItemKey);
                var scraper = GetScraperFor(src);
                var items = await scraper.FetchAsync(src);
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
        return new UiItem { Title = i.Title, Price = price, StockText = stockText, IsInStock = i.InStock, Url = i.Url, ItemKey = i.ItemKey, SourceId = i.SourceId, AccentHex = accentHex };
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
        public string Title { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string StockText { get; set; } = string.Empty;
        public bool IsInStock { get; set; }
        public Uri? Url { get; set; }
        public string ItemKey { get; set; } = string.Empty;
        public int SourceId { get; set; }
        public string AccentHex { get; set; } = "#696969";

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
                EmptyMessage.Visibility = Visibility.Visible;
                StatusText.Text = "Empty database — click ⟳ to fetch";
            }
            else
            {
                EmptyMessage.Visibility = Visibility.Collapsed;
            }
        });
    }
}