using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopHub.Infrastructure.Firebase;

namespace DesktopHub.UI.Widgets;

public partial class DeveloperPanelWidget : UserControl
{
    private static readonly HashSet<string> AllowedScripts = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin.ps1",
        "manage-admin.ps1",
        "manage-cheatsheet-editors.ps1",
        "manage-dev.ps1",
        "dump-database.ps1",
        "backup-database.ps1",
        "push-update.ps1",
        "build-single-file.ps1",
        "build-installer.ps1",
        "Update-FirebaseVersion.ps1",
        "tag-manager.ps1",
        "wipe-tags.ps1",
        "cleanup-auth-users.ps1",
        "Reset-Metrics.ps1",
        "wipe-devices.ps1"
    };

    internal static readonly string[] KnownNodes =
    {
        "devices", "users", "licenses", "app_versions", "admin_users",
        "project_tags", "cheat_sheet_data", "events", "errors",
        "feature_flags", "dev_users", "cheat_sheet_editors", "metrics",
        "tag_registry", "pending_updates"
    };

    private static readonly (string Id, string Label)[] TabDefs =
    {
        ("dashboard",   "Dashboard"),
        ("database",    "Database"),
        ("heartbeats",  "Heartbeats"),
        ("permissions", "Permissions"),
        ("updates",     "Updates"),
    };

    internal record ScriptTile(string Label, string Abbrev, string Color, Func<Task> Action, bool IsDanger = false);

    internal readonly IFirebaseService? _firebaseService;
    private bool _isDev;
    internal readonly string _scriptsDir;
    internal readonly string? _serviceAccountPath;
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
    internal bool _suppressSuggestion;

    public DeveloperPanelWidget(IFirebaseService? firebaseService)
    {
        InitializeComponent();
        _firebaseService = firebaseService;
        _scriptsDir = ResolveScriptsDir();
        _serviceAccountPath = ResolveServiceAccountPath();

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
        TryFindResource(key) as SolidColorBrush ?? Brushes.Gray;

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
            var fgBrush = isActive ? Brushes.White : FindBrush("TextPrimaryBrush");
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
        PermissionsTab.Visibility = tabId == "permissions" ? Visibility.Visible : Visibility.Collapsed;
        UpdatesTab.Visibility = tabId == "updates" ? Visibility.Visible : Visibility.Collapsed;

        // Lazy-load tab data on first visit
        if (_loadedTabs.Add(tabId))
            _ = LoadTabAsync(tabId);

        // Start/stop heartbeat timer based on active tab
        if (tabId == "heartbeats")
            StartHeartbeatTimer();
        else
            _heartbeatTimer?.Stop();
    }

    private async Task LoadTabAsync(string tabId)
    {
        switch (tabId)
        {
            case "dashboard":
                BuildScriptTiles();
                await RefreshVersionInfoAsync();
                break;
            case "database":
                await DiscoverNodePillsAsync();
                break;
            case "heartbeats":
                await RefreshHeartbeatsAsync();
                break;
            case "permissions":
                break;
            case "updates":
                InitUpdatesTab();
                break;
        }
    }

    // ════════════════════════════════════════════════════════════
    // PATH RESOLUTION
    // ════════════════════════════════════════════════════════════

    private static string ResolveScriptsDir()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidate = System.IO.Path.Combine(baseDir, "scripts");
        if (System.IO.Directory.Exists(candidate))
            return candidate;

        var dir = new System.IO.DirectoryInfo(baseDir);
        while (dir?.Parent != null)
        {
            dir = dir.Parent;
            candidate = System.IO.Path.Combine(dir.FullName, "scripts");
            if (System.IO.Directory.Exists(candidate) &&
                System.IO.File.Exists(System.IO.Path.Combine(candidate, "admin.ps1")))
                return candidate;
        }

        return System.IO.Path.Combine(baseDir, "scripts");
    }

    private static string? ResolveServiceAccountPath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            System.IO.Path.Combine(baseDir, "secrets", "firebase-license.json"),
            System.IO.Path.Combine(baseDir, "firebase-license.json"),
        };

        foreach (var c in candidates)
            if (System.IO.File.Exists(c)) return System.IO.Path.GetFullPath(c);

        var dir = new System.IO.DirectoryInfo(baseDir);
        while (dir?.Parent != null)
        {
            dir = dir.Parent;
            var candidate = System.IO.Path.Combine(dir.FullName, "secrets", "firebase-license.json");
            if (System.IO.File.Exists(candidate))
                return System.IO.Path.GetFullPath(candidate);
        }

        return null;
    }

    // ════════════════════════════════════════════════════════════
    // SCRIPT EXECUTION
    // ════════════════════════════════════════════════════════════

    internal static string NormalizeUsername(string? raw) =>
        (raw ?? string.Empty).Trim().ToLowerInvariant();

    internal string ResolveUsername()
    {
        var normalized = NormalizeUsername(RoleUsernameBox.Text);
        _suppressSuggestion = true;
        RoleUsernameBox.Text = normalized;
        _suppressSuggestion = false;
        return normalized;
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    internal async Task RunScriptAsync(string fileName, params string[] args)
    {
        await RunScriptWithOutputAsync(fileName, skipServiceAccount: false, args);
    }

    internal async Task<(string Stdout, string Stderr, int ExitCode)> RunScriptWithOutputAsync(
        string fileName, bool skipServiceAccount = false, params string[] args)
    {
        if (!EnsureDevForAction($"run {fileName}"))
            return ("", "DENIED", -1);

        if (!AllowedScripts.Contains(fileName))
        {
            AppendOutput($"DENIED: Script '{fileName}' is not in the allowlist.");
            return ("", "DENIED", -1);
        }

        var scriptPath = System.IO.Path.Combine(_scriptsDir, fileName);
        if (!System.IO.File.Exists(scriptPath))
        {
            AppendOutput($"ERROR: Script not found: {scriptPath}");
            return ("", "NOT_FOUND", -1);
        }

        var allArgs = new List<string>(args);
        if (!skipServiceAccount && _serviceAccountPath != null)
        {
            allArgs.Add("-ServiceAccountPath");
            allArgs.Add(Quote(_serviceAccountPath));
        }

        var psi = new ProcessStartInfo
        {
            FileName = "powershell",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -File {Quote(scriptPath)} {string.Join(" ", allArgs)}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
        };

        AppendOutput($"> {fileName} {string.Join(" ", args)}");

        try
        {
            using var proc = Process.Start(psi);
            if (proc == null)
            {
                AppendOutput("ERROR: Failed to start script process.");
                return ("", "FAILED_TO_START", -1);
            }

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(stdout))
                AppendOutput(stdout.TrimEnd());
            if (!string.IsNullOrWhiteSpace(stderr))
                AppendOutput("STDERR: " + stderr.TrimEnd());

            AppendOutput($"Exit code: {proc.ExitCode}");
            return (stdout, stderr, proc.ExitCode);
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR running script: {ex.Message}");
            return ("", ex.Message, -1);
        }
    }

    internal void AppendOutput(string text)
    {
        OutputBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        OutputBox.ScrollToEnd();
    }

    internal async Task ExecuteRoleActionAsync(string script, string action, bool confirmDangerous = false)
    {
        var username = ResolveUsername();
        if (string.IsNullOrWhiteSpace(username))
        {
            AppendOutput("Please enter a username first.");
            return;
        }

        if (confirmDangerous)
        {
            var result = MessageBox.Show(
                $"Confirm role action '{action}' for '{username}'?",
                "Confirm Role Action",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                AppendOutput("Cancelled.");
                return;
            }
        }

        await RunScriptAsync(script, "-Action", action, "-Username", username);
    }

    internal bool ConfirmDangerous(string message)
    {
        return MessageBox.Show(message, "Confirm", MessageBoxButton.YesNo,
            MessageBoxImage.Warning) == MessageBoxResult.Yes;
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
    private void NormalizeUsername_Click(object sender, RoutedEventArgs e) => _ = ResolveUsername();

    internal void NavigateToPermissions(string username)
    {
        _suppressSuggestion = true;
        RoleUsernameBox.Text = username;
        _suppressSuggestion = false;
        SwitchTab("permissions");
    }
}
