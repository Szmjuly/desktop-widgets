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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
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
            // Set roaster label (single source for now)
            SourceLabel.Text = GetSourceDisplayName();

            // Load settings and apply
            var s = await _settings.LoadAsync();
            if (s.PollIntervalSeconds >= 10)
            {
                _source.PollIntervalSeconds = s.PollIntervalSeconds;
            }

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
        var exitItem = new Forms.ToolStripMenuItem("Exit", null, (_, __) => System.Windows.Application.Current.Shutdown());
        menu.Items.Add(showItem);
        menu.Items.Add(pauseItem);
        menu.Items.Add(attachItem);
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

                    // Ensure Source exists and has a concrete Id before item upserts
                    await _store.UpsertSourceAsync(_source, ct);
                    var previous = await _store.GetItemsBySourceAsync(_source.Id!.Value, ct);
                    var scraper = GetScraperFor(_source);
                    var items = await scraper.FetchAsync(_source, ct);
                    var events = _detector.Compare(previous, items);

                    await _store.UpsertItemsAsync(items, ct);

                    // Update UI list
                    var uiItems = items
                        .OrderBy(i => i.Title)
                        .Select(ToUiItem)
                        .ToList();
                    var inStockCount = items.Count(i => i.InStock);
                    var label = $"{GetSourceDisplayName()} ({inStockCount})";
                    await Dispatcher.InvokeAsync(() =>
                    {
                        ItemsList.ItemsSource = uiItems;
                        SourceLabel.Text = label;
                    });

                    var newStock = events.Where(e => e.EventType is StockEventType.NewItem or StockEventType.BackInStock).ToList();
                    if (newStock.Count > 0)
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            Bubble.Visibility = Visibility.Visible;
                            StatusText.Text = $"{newStock.Count} updates";
                        });
                        _notifyIcon.BalloonTipTitle = "Coffee Stock Updates";
                        _notifyIcon.BalloonTipText = $"{newStock.Count} item(s) updated";
                        _notifyIcon.ShowBalloonTip(3000);
                    }
                    else
                    {
                        await Dispatcher.InvokeAsync(() =>
                        {
                            Bubble.Visibility = Visibility.Collapsed;
                            StatusText.Text = $"Last check: {DateTime.Now:t}";
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
        }
    }

    private void SourceButton_Click(object sender, RoutedEventArgs e)
    {
        var cm = new ContextMenu
        {
            PlacementTarget = SourceButton,
            Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
        };
        cm.MinWidth = SourcePill.ActualWidth;
        cm.Width = SourcePill.ActualWidth;

        foreach (var src in _sources)
        {
            var implemented = IsParserImplemented(src.ParserType);
            var header = implemented ? GetDisplayName(src) : GetDisplayName(src) + " (coming soon)";
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
        return new UiItem { Title = i.Title, Price = price, StockText = stockText, StockBrush = stockBrush };
    }

    private class UiItem
    {
        public string Title { get; set; } = string.Empty;
        public string Price { get; set; } = string.Empty;
        public string StockText { get; set; } = string.Empty;
        public System.Windows.Media.Brush StockBrush { get; set; } = System.Windows.Media.Brushes.Gray;
    }
}