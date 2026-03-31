using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopHub.Infrastructure.Firebase;

namespace DesktopHub.UI.Widgets;

public partial class DeveloperPanelWidget : System.Windows.Controls.UserControl
{
    internal static readonly string[] KnownNodes =
    {
        "devices", "users", "licenses", "app_versions", "admin_users",
        "project_tags", "cheat_sheet_data", "events", "errors",
        "feature_flags", "dev_users", "cheat_sheet_editors", "metrics",
        "tag_registry", "force_update"
    };

    private static readonly (string Id, string Label)[] TabDefs =
    {
        ("dashboard",     "Dashboard"),
        ("database",      "Database"),
        ("heartbeats",    "Heartbeats"),
        ("users_devices", "Users / devices"),
        ("permissions",   "Permissions"),
        ("licensing",     "Licensing"),
        ("publish",       "Publish"),
        ("updates",       "Updates"),
    };

    internal record ScriptTile(string Label, string Abbrev, string Color, Func<Task> Action, bool IsDanger = false);

    internal readonly IFirebaseService? _firebaseService;
    private bool _isDev;
    private readonly DispatcherTimer _clockTimer;

    // Tab state
    private string _activeTab = "dashboard";
    private readonly HashSet<string> _loadedTabs = new();

    // Shared state across tabs
    internal string? _selectedNodePill;
    internal Dictionary<string, Dictionary<string, object>?>? _nodeCache;
    internal Dictionary<string, object>? _lastNodeData;
    internal bool _dbShowJson;
    internal List<string> _knownUsernames = new();
    internal DispatcherTimer? _heartbeatTimer;

    public DeveloperPanelWidget(IFirebaseService? firebaseService)
    {
        InitializeComponent();
        _firebaseService = firebaseService;

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("h:mm:ss tt");

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    // ════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ════════════════════════════════════════════════════════════

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _clockTimer.Start();
        ClockText.Text = DateTime.Now.ToString("h:mm:ss tt");
        SetCurrentUsername();
        await EnsureDevAccessAsync();
        if (_isDev)
        {
            BuildTabBar();
            SwitchTab("dashboard");
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _clockTimer.Stop();
        _heartbeatTimer?.Stop();
        StopDashboardTimer();
    }

    private void SetCurrentUsername()
    {
        var username = Environment.UserName?.ToLowerInvariant() ?? "unknown";
        UsernameText.Text = username;
        UserInitialText.Text = username.Length > 0 ? username[0].ToString().ToUpperInvariant() : "?";
    }

    private async Task EnsureDevAccessAsync()
    {
        try
        {
            _isDev = _firebaseService != null && _firebaseService.IsInitialized && await _firebaseService.IsUserDevAsync();
        }
        catch
        {
            _isDev = false;
        }

        AccessDeniedPanel.Visibility = _isDev ? Visibility.Collapsed : Visibility.Visible;
        MainContent.IsEnabled = _isDev;
        DevBadgeText.Text = _isDev ? "DEV" : "DENIED";

        if (_isDev)
        {
            LiveDot.Fill = FindBrush("GreenBrush");
            LiveStatusText.Text = "LIVE";
            LiveStatusText.Foreground = FindBrush("GreenBrush");
        }
        else
        {
            LiveDot.Fill = FindBrush("RedBrush");
            LiveStatusText.Text = "DENIED";
            LiveStatusText.Foreground = FindBrush("RedBrush");
        }
    }

    internal bool EnsureDevForAction(string action)
    {
        if (_isDev) return true;
        AppendOutput($"DENIED: {action} requires DEV role.");
        return false;
    }

    internal SolidColorBrush FindBrush(string key) =>
        TryFindResource(key) as SolidColorBrush ?? System.Windows.Media.Brushes.Gray;

    // ════════════════════════════════════════════════════════════
    // TAB SWITCHING
    // ════════════════════════════════════════════════════════════

    private void BuildTabBar()
    {
        TabBar.Children.Clear();
        foreach (var (id, label) in TabDefs)
        {
            var isActive = _activeTab == id;
            var bgBrush = isActive ? FindBrush("AccentBrush") : FindBrush("SurfaceBrush");
            var fgBrush = isActive ? System.Windows.Media.Brushes.White : FindBrush("TextPrimaryBrush");
            var borderBrush = isActive ? FindBrush("AccentBrush") : FindBrush("BorderBrush");

            var text = new TextBlock
            {
                Text = label,
                FontSize = 11,
                FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = fgBrush
            };

            var pill = new Border
            {
                Background = bgBrush,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(0, 0, 6, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Child = text
            };

            var tabId = id;
            var hoverBrush = isActive ? bgBrush : FindBrush("HoverMediumBrush");
            pill.MouseEnter += (_, _) => { if (_activeTab != tabId) pill.Background = hoverBrush; };
            pill.MouseLeave += (_, _) => { if (_activeTab != tabId) pill.Background = FindBrush("SurfaceBrush"); };
            pill.MouseLeftButtonDown += (_, _) => SwitchTab(tabId);

            TabBar.Children.Add(pill);
        }
    }

    internal void SwitchTab(string tabId)
    {
        _activeTab = tabId;
        BuildTabBar();

        DashboardTab.Visibility = tabId == "dashboard" ? Visibility.Visible : Visibility.Collapsed;
        DatabaseTab.Visibility = tabId == "database" ? Visibility.Visible : Visibility.Collapsed;
        HeartbeatsTab.Visibility = tabId == "heartbeats" ? Visibility.Visible : Visibility.Collapsed;
        UsersDevicesTab.Visibility = tabId == "users_devices" ? Visibility.Visible : Visibility.Collapsed;
        PermissionsTab.Visibility = tabId == "permissions" ? Visibility.Visible : Visibility.Collapsed;
        LicensingTab.Visibility = tabId == "licensing" ? Visibility.Visible : Visibility.Collapsed;
        PublishTab.Visibility = tabId == "publish" ? Visibility.Visible : Visibility.Collapsed;
        UpdatesTab.Visibility = tabId == "updates" ? Visibility.Visible : Visibility.Collapsed;

        // Lazy-load tab data on first visit
        if (_loadedTabs.Add(tabId))
            _ = LoadTabAsync(tabId);

        // Start/stop heartbeat timer based on active tab
        if (tabId == "heartbeats")
            StartHeartbeatTimer();
        else
            _heartbeatTimer?.Stop();

        if (tabId == "dashboard")
            StartDashboardTimer();
        else
            StopDashboardTimer();
    }

    private async Task LoadTabAsync(string tabId)
    {
        switch (tabId)
        {
            case "dashboard":
                await BuildHealthOverviewAsync();
                BuildScriptTiles();
                BuildSystemStatus();
                await RefreshVersionInfoAsync();
                break;
            case "database":
                await DiscoverNodePillsAsync();
                break;
            case "heartbeats":
                await RefreshHeartbeatsAsync();
                break;
            case "users_devices":
                await RefreshUsersDevicesAsync();
                break;
            case "permissions":
                WirePermissionTabEventsOnce();
                PopulateUsernameDropdown();
                await RefreshPermissionTabStateAsync();
                break;
            case "licensing":
                await RefreshLicensesAsync();
                break;
            case "publish":
                InitPublishTab();
                break;
            case "updates":
                InitUpdatesTab();
                break;
        }
    }

    // ════════════════════════════════════════════════════════════
    // SHARED UTILITIES
    // ════════════════════════════════════════════════════════════

    internal static string NormalizeUsername(string? raw) =>
        (raw ?? string.Empty).Trim().ToLowerInvariant();

    internal string ResolveUsername()
    {
        var normalized = NormalizeUsername(RoleUsernameBox.Text);
        RoleUsernameBox.Text = normalized;
        return normalized;
    }

    internal void AppendOutput(string text)
    {
        OutputBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        OutputBox.ScrollToEnd();
    }

    internal async Task<bool> ConfirmDangerousAsync(string message)
    {
        ConfirmOverlayMessage.Text = message;
        ConfirmOverlay.Visibility = Visibility.Visible;

        var tcs = new TaskCompletionSource<bool>();
        void OnYes(object s, RoutedEventArgs e) => tcs.TrySetResult(true);
        void OnNo(object s, RoutedEventArgs e) => tcs.TrySetResult(false);

        ConfirmYesBtn.Click += OnYes;
        ConfirmNoBtn.Click += OnNo;
        try
        {
            return await tcs.Task;
        }
        finally
        {
            ConfirmYesBtn.Click -= OnYes;
            ConfirmNoBtn.Click -= OnNo;
            ConfirmOverlay.Visibility = Visibility.Collapsed;
        }
    }

    internal static object? JsonElementToObject(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(JsonElementToObject).ToList(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var i) => i,
        JsonValueKind.Number => element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => element.ToString()
    };

    // ════════════════════════════════════════════════════════════
    // SHARED EVENT HANDLERS
    // ════════════════════════════════════════════════════════════

    private void ClearOutput_Click(object sender, RoutedEventArgs e) => OutputBox.Clear();
    private async void NormalizeUsername_Click(object sender, RoutedEventArgs e)
    {
        _ = ResolveUsername();
        await RefreshPermissionTabStateAsync();
    }

    private void ClosePanel_Click(object sender, RoutedEventArgs e)
    {
        var w = Window.GetWindow(this);
        if (w != null)
            w.Hide();
    }

    internal void NavigateToPermissions(string username)
    {
        RoleUsernameBox.Text = username;
        SwitchTab("permissions");
    }
}
