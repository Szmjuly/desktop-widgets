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
    private DateTimeOffset? _lastAcknowledgedUtc;
    private DateTimeOffset _lastEventUtc;

    private enum ViewMode
    {
        Normal,
        UnseenAll,
        UnseenCurrent
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
            else if (_viewMode == ViewMode.UnseenAll)
            {
                var items = await _store.GetUnseenItemsAsync();
                var ui = items.Select(ToUiItemForView).ToList();
                await Dispatcher.InvokeAsync(() =>
                {
                    ItemsList.ItemsSource = ui;
                    EmptyMessage.Visibility = ui.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                });
            }
            else if (_viewMode == ViewMode.UnseenCurrent)
            {
                if (_source.Id.HasValue)
                {
                    var items = await _store.GetUnseenItemsBySourceAsync(_source.Id.Value);
                    var ui = items.Select(ToUiItemForView).ToList();
                    await Dispatcher.InvokeAsync(() =>
                    {
                        ItemsList.ItemsSource = ui;
                        EmptyMessage.Visibility = ui.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                    });
                }
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() => StatusText.Text = "Error: " + ex.Message);
        }
    }

    private void ReloadBtn_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var cm = new ContextMenu
        {
            PlacementTarget = ReloadBtn,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };
        var runEnabled = new MenuItem { Header = "Run Enabled Now" };
        runEnabled.Click += async (_, __) => await RunMassAsync(includeDisabled: false);
        var runAll = new MenuItem { Header = "Run ALL Now (ignore enabled)" };
        runAll.Click += async (_, __) => await RunMassAsync(includeDisabled: true);
        var markAll = new MenuItem { Header = "Mark all as seen" };
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
        InitializeComponent();

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

        _sources = new List<Source>
        {
            _source,
            new Source
            {
                Name = "Brandywine Coffee Roasters",
                RootUrl = new Uri("https://www.brandywinecoffeeroasters.com/collections/all-coffee-1"),
                ParserType = "Brandywine",
                PollIntervalSeconds = 300,
                Enabled = true
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

        var menu = new Forms.ContextMenuStrip();
        var showItem = new Forms.ToolStripMenuItem("Show/Hide", null, (_, __) => ToggleWindow());
        var pauseItem = new Forms.ToolStripMenuItem("Pause") { CheckOnClick = true };
        pauseItem.Click += (_, __) => TogglePause(pauseItem);
        var attachItem = new Forms.ToolStripMenuItem("Attach to Desktop") { CheckOnClick = true };
        attachItem.Click += (_, __) => ToggleAttach(attachItem);
        var runEnabledItem = new Forms.ToolStripMenuItem("Run Enabled Now", null, async (_, __) => await RunMassAsync(includeDisabled: false));
        var runAllItem = new Forms.ToolStripMenuItem("Run ALL Now (ignore enabled)", null, async (_, __) => await RunMassAsync(includeDisabled: true));
        var markAllSeenItem = new Forms.ToolStripMenuItem("Mark All as Seen", null, async (_, __) =>
        {
            await _store.MarkAllSeenAsync(DateTimeOffset.UtcNow);
            await UpdateUnseenUiAsync();
        });
        var exitItem = new Forms.ToolStripMenuItem("Exit", null, (_, __) => System.Windows.Application.Current.Shutdown());
        menu.Items.Add(showItem);
        menu.Items.Add(pauseItem);
        menu.Items.Add(attachItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(runEnabledItem);
        menu.Items.Add(runAllItem);
        menu.Items.Add(markAllSeenItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);
        ni.ContextMenuStrip = menu;
        ni.DoubleClick += (_, __) => ToggleWindow();
        return ni;
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
        var dlg = new SettingsWindow { Owner = this };
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

        // Enable/disable sources submenu
        var enableRoot = new MenuItem { Header = "Enable sources" };
        foreach (var src in _sources)
        {
            var toggle = new MenuItem
            {
                Header = GetDisplayName(src),
                IsCheckable = true,
                IsChecked = src.Enabled,
                Tag = src
            };
            toggle.Click += async (s, _) =>
            {
                if (s is MenuItem m && m.Tag is Source target)
                {
                    // Ensure at least one source remains enabled
                    var currentlyEnabled = _sources.Count(x => x.Enabled);
                    if (currentlyEnabled == 1 && target.Enabled)
                    {
                        // Prevent disabling the last enabled source
                        m.IsChecked = true;
                        return;
                    }

                    target.Enabled = m.IsChecked;
                    await SaveEnabledParsersAsync();
                    // Restart loop so change takes effect immediately
                    _loopCts?.Cancel();
                    StartLoop();
                    await UpdateUnseenUiAsync();
                }
            };
            enableRoot.Items.Add(toggle);
        }
        cm.Items.Add(enableRoot);
        cm.Items.Add(new Separator());

        // Mark seen actions
        var markCurrent = new MenuItem { Header = "Mark current source as seen" };
        markCurrent.Click += async (_, __) =>
        {
            if (_source.Id.HasValue)
            {
                await _store.MarkSourceSeenAsync(_source.Id.Value, DateTimeOffset.UtcNow);
                await UpdateUnseenUiAsync();
            }
        };
        var markAll = new MenuItem { Header = "Mark all as seen" };
        markAll.Click += async (_, __) =>
        {
            await _store.MarkAllSeenAsync(DateTimeOffset.UtcNow);
            await UpdateUnseenUiAsync();
        };
        cm.Items.Add(markCurrent);
        cm.Items.Add(markAll);
        cm.Items.Add(new Separator());

        // View filters
        var viewAll = new MenuItem { Header = "Show all items (current roaster)" };
        viewAll.Click += async (_, __) => { _viewMode = ViewMode.Normal; await RefreshViewAsync(); };
        var viewUnseenCurrent = new MenuItem { Header = "Show unseen for current roaster" };
        viewUnseenCurrent.Click += async (_, __) => { _viewMode = ViewMode.UnseenCurrent; await RefreshViewAsync(); };
        var viewUnseenAll = new MenuItem { Header = "Show unseen across all roasters" };
        viewUnseenAll.Click += async (_, __) => { _viewMode = ViewMode.UnseenAll; await RefreshViewAsync(); };
        cm.Items.Add(viewAll);
        cm.Items.Add(viewUnseenCurrent);
        cm.Items.Add(viewUnseenAll);
        cm.Items.Add(new Separator());

        // Unseen counts by source
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

        if (s.BlurEnabled)
        {
            RootBorder.Effect = new BlurEffect { Radius = 8 };
        }
        else
        {
            RootBorder.Effect = null;
        }
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

    private static UiItem ToUiItem(CoffeeItem i)
    {
        var price = i.PriceCents.HasValue ? ("$" + (i.PriceCents.Value / 100.0m).ToString("0.00")) : string.Empty;
        var stockText = i.InStock ? "In stock" : "Sold out";
        var stockBrush = i.InStock ? System.Windows.Media.Brushes.LightGreen : System.Windows.Media.Brushes.OrangeRed;
        return new UiItem { Title = i.Title, Price = price, StockText = stockText, StockBrush = stockBrush, Url = i.Url, ItemKey = i.ItemKey, SourceId = i.SourceId };
    }

    private UiItem ToUiItemForView(CoffeeItem i)
    {
        var ui = ToUiItem(i);
        if (_viewMode == ViewMode.UnseenAll)
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
        public System.Windows.Media.Brush StockBrush { get; set; } = System.Windows.Media.Brushes.Gray;
        public Uri? Url { get; set; }
        public string ItemKey { get; set; } = string.Empty;
        public int SourceId { get; set; }
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