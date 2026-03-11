using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using DesktopHub.Core;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.UI.Services;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DesktopHub.UI.Widgets;

public partial class MetricsViewerWidget : System.Windows.Controls.UserControl
{
    private DateTime _selectedDate = DateTime.Today;
    private DailyMetricsSummary? _currentSummary;
    private DispatcherTimer? _pollTimer;
    private ISettingsService? _settings;
    private bool _isPolling;

    // Admin view state
    private bool _isAdminView;
    private int _adminRangeDays = 7;
    private DateTime _adminRangeStart = DateTime.Today;
    private DateTime _adminRangeEnd = DateTime.Today;
    private List<DailyMetricsSummary> _adminSummaries = new();
    private List<DailyMetricsSummary> _filteredAdminSummaries = new();
    private HashSet<string> _enabledUsers = new();
    private string _selectedChartType = string.Empty;
    private string _selectedXMetric = "searches";
    private string _selectedYMetric = "launches";
    private HashSet<string> _selectedCategoryMetrics = new() { "searches", "smart_searches", "doc_searches", "launches", "tasks_created", "timer" };

    // Dynamic axis panels (built in code, not XAML)
    private WrapPanel? _xAxisButtons;
    private WrapPanel? _yAxisButtons;
    private WrapPanel? _categoryButtons;

    // Chart metric modes
    private enum ChartMetricMode { Single, Dual, Category }

    private static ChartMetricMode GetChartMode(string chartType) => chartType switch
    {
        "bar" or "line" or "area" => ChartMetricMode.Single,
        "stacked_bar" or "scatter" or "bubble" => ChartMetricMode.Dual,
        _ => ChartMetricMode.Category
    };

    // Selectable metric definitions
    private static readonly (string Id, string Label, Func<DailyMetricsSummary, int> Selector)[] MetricDefs = new (string, string, Func<DailyMetricsSummary, int>)[]
    {
        ("sessions",        "Sessions",         s => s.SessionCount),
        ("duration",        "Duration (min)",   s => (int)(s.TotalSessionDurationMs / 60_000)),
        ("searches",        "Searches",         s => s.TotalSearches),
        ("smart_searches",  "Smart Search",     s => s.TotalSmartSearches),
        ("doc_searches",    "Doc Search",       s => s.TotalDocSearches),
        ("path_searches",   "Path Search",      s => s.TotalPathSearches),
        ("launches",        "Launches",         s => s.TotalProjectLaunches),
        ("doc_opens",       "Doc Opens",        s => s.TotalDocOpens),
        ("quick_launch",    "Quick Launch",     s => s.TotalQuickLaunchUses),
        ("tasks_created",   "Tasks Created",    s => s.TotalTasksCreated),
        ("tasks_done",      "Tasks Done",       s => s.TotalTasksCompleted),
        ("timer",           "Timer",            s => s.TotalTimerUses),
        ("cheatsheet",      "Cheat Sheet",      s => s.TotalCheatSheetViews),
        ("hotkeys",         "Hotkeys",          s => s.TotalHotkeyPresses),
        ("clipboard",       "Clipboard",        s => s.TotalClipboardCopies),
        ("errors",          "Errors",           s => s.TotalErrors),
    };

    // 15 chart types
    private static readonly (string Id, string Label, string Icon)[] ChartTypes = new[]
    {
        ("bar",             "Bar",              "\u2587"),
        ("stacked_bar",     "Stacked Bar",      "\u2593"),
        ("horizontal_bar",  "H-Bar",            "\u2590"),
        ("line",            "Line",             "\u2571"),
        ("area",            "Area",             "\u25E2"),
        ("pie",             "Pie",              "\u25CF"),
        ("donut",           "Donut",            "\u25CB"),
        ("heatmap",         "Heat Map",         "\u25A3"),
        ("scatter",         "Scatter",          "\u2022"),
        ("bubble",          "Bubble",           "\u25EF"),
        ("radar",           "Radar",            "\u2731"),
        ("matrix",          "Matrix",           "\u25A6"),
        ("treemap",         "Treemap",          "\u25A4"),
        ("waterfall",       "Waterfall",        "\u2502"),
        ("node_graph",      "Node Graph",       "\u2B2E"),
    };

    // Palette for chart rendering — resolved from theme at runtime
    private static string[] GetPalette()
    {
        var keys = new[]
        {
            "AccentColor", "ChartGreenColor", "ChartOrangeColor", "ChartPurpleColor", "ChartBlueColor",
            "ChartRedColor", "ChartCyanColor", "ChartDeepOrangeColor", "ChartLimeColor", "ChartSlateColor",
            "ChartIndigoColor", "ChartPinkColor", "ChartSkyBlueColor", "ChartAmberColor", "ChartBrownColor"
        };
        var result = new string[keys.Length];
        for (int i = 0; i < keys.Length; i++)
        {
            if (System.Windows.Application.Current?.TryFindResource(keys[i]) is System.Windows.Media.Color c)
                result[i] = $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
            else
                result[i] = "#007ACC"; // fallback
        }
        return result;
    }
    private static string[] _palette = null!;
    private static string[] Palette => _palette ??= GetPalette();

    public MetricsViewerWidget()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnVisibleChanged;
    }

    public void SetSettingsService(ISettingsService settings)
    {
        _settings = settings;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateDeviceInfo();
        InitializeAdminDateRange();

        // Check Firebase for admin privileges based on Windows username
        await CheckAdminStatusAsync();

        BuildChartTypeButtons();
        BuildAxisButtons();
        await LoadMetricsAsync();
        StartPolling();
    }

    private void InitializeAdminDateRange()
    {
        var weekStartDay = (DayOfWeek)(_settings?.GetMetricsWeekStartDay() ?? 1);
        var rangeWeeks = _settings?.GetMetricsRangeWeeks() ?? 1;
        _adminRangeDays = rangeWeeks * 7;

        // Compute the most recent start-of-week boundary
        var today = DateTime.Today;
        var daysSinceStart = ((int)today.DayOfWeek - (int)weekStartDay + 7) % 7;
        _adminRangeStart = today.AddDays(-daysSinceStart);
        _adminRangeEnd = _adminRangeStart.AddDays(_adminRangeDays - 1);

        // Clamp end to today so we don't show future dates
        if (_adminRangeEnd > today)
            _adminRangeEnd = today;
    }

    public void RefreshAdminDateRange()
    {
        InitializeAdminDateRange();
        if (_isAdminView)
            _ = LoadAdminMetricsAsync();
    }

    private bool _isAdmin;

    private async System.Threading.Tasks.Task CheckAdminStatusAsync()
    {
        try
        {
            var app = System.Windows.Application.Current as App;
            var firebaseManager = app?.FirebaseManager;
            var firebaseService = firebaseManager?.FirebaseService;

            if (firebaseService != null && firebaseService.IsInitialized)
            {
                _isAdmin = await firebaseService.IsUserAdminAsync();
            }
            else
            {
                _isAdmin = false;
            }
        }
        catch
        {
            _isAdmin = false;
        }

        AdminToggleButton.Visibility = _isAdmin ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopPolling();
    }

    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            _ = LoadMetricsAsync();
            StartPolling();
        }
        else
        {
            StopPolling();
        }
    }

    private void StartPolling()
    {
        if (_isPolling) return;
        _isPolling = true;

        var intervalSeconds = _settings?.GetMetricsRefreshIntervalSeconds() ?? 30;
        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(intervalSeconds)
        };
        _pollTimer.Tick += async (_, _) => await LoadMetricsAsync();
        _pollTimer.Start();

        PollingIndicator.Visibility = Visibility.Visible;
        PollingIndicator.ToolTip = $"Auto-refreshing every {intervalSeconds}s";
    }

    private void StopPolling()
    {
        _isPolling = false;
        _pollTimer?.Stop();
        _pollTimer = null;
        PollingIndicator.Visibility = Visibility.Collapsed;
    }

    public void UpdatePollingInterval()
    {
        if (!_isPolling) return;
        StopPolling();
        StartPolling();
    }

    private void UpdateDeviceInfo()
    {
        var machineName = Environment.MachineName;
        var userName = Environment.UserName;
        DeviceInfoLabel.Text = $"{userName} @ {machineName}";
        DeviceInfoLabel.ToolTip = $"User: {userName}\nDevice: {machineName}\nMetrics are tracked per-device and synced to Firebase for admin visibility.";
    }

    // ===== Admin view toggle =====

    private void AdminToggle_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        _isAdminView = !_isAdminView;

        if (_isAdminView)
        {
            SingleViewPanel.Visibility = Visibility.Collapsed;
            AdminViewPanel.Visibility = Visibility.Visible;
            TitleText.Text = "Metrics (Admin)";
            AdminToggleIcon.Text = "\U0001F465"; // multiple people
            AdminToggleIcon.ToolTip = "All users view";
            _ = LoadAdminMetricsAsync();
        }
        else
        {
            SingleViewPanel.Visibility = Visibility.Visible;
            AdminViewPanel.Visibility = Visibility.Collapsed;
            TitleText.Text = "Metrics";
            AdminToggleIcon.Text = "\U0001F464"; // single person
            AdminToggleIcon.ToolTip = "Single user view";
            _ = LoadMetricsAsync();
        }
    }

    // ===== Event handlers =====

    private void RefreshButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isAdminView)
            _ = LoadAdminMetricsAsync();
        else
            _ = LoadMetricsAsync();
    }

    private void CloseButton_Click(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        var parentWindow = Window.GetWindow(this);
        if (parentWindow != null)
        {
            parentWindow.Visibility = Visibility.Hidden;
            parentWindow.Tag = null;
        }
    }

    private void PrevDay_Click(object sender, MouseButtonEventArgs e)
    {
        _selectedDate = _selectedDate.AddDays(-1);
        _ = LoadMetricsAsync();
    }

    private void NextDay_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedDate.Date < DateTime.Today)
        {
            _selectedDate = _selectedDate.AddDays(1);
            _ = LoadMetricsAsync();
        }
    }

    private void AdminPrevRange_Click(object sender, MouseButtonEventArgs e)
    {
        _adminRangeStart = _adminRangeStart.AddDays(-_adminRangeDays);
        _adminRangeEnd = _adminRangeStart.AddDays(_adminRangeDays - 1);
        _ = LoadAdminMetricsAsync();
    }

    private void AdminNextRange_Click(object sender, MouseButtonEventArgs e)
    {
        var today = DateTime.Today;
        if (_adminRangeEnd < today)
        {
            _adminRangeStart = _adminRangeStart.AddDays(_adminRangeDays);
            _adminRangeEnd = _adminRangeStart.AddDays(_adminRangeDays - 1);
            if (_adminRangeEnd > today) _adminRangeEnd = today;
            _ = LoadAdminMetricsAsync();
        }
    }

    // ===== Shared helpers =====

    private static WpfSolidColorBrush Brush(string hex) =>
        new((WpfColor)WpfColorConverter.ConvertFromString(hex));

    private static WpfSolidColorBrush PaletteBrush(int index) =>
        Brush(Palette[index % Palette.Length]);

    private static WpfSolidColorBrush PaletteBrushAlpha(int index, byte alpha)
    {
        var c = (WpfColor)WpfColorConverter.ConvertFromString(Palette[index % Palette.Length]);
        return new WpfSolidColorBrush(WpfColor.FromArgb(alpha, c.R, c.G, c.B));
    }
}
