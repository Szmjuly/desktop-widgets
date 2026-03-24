using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopHub.Infrastructure.Firebase;

namespace DesktopHub.UI.Widgets;

public partial class DeveloperPanelWidget : System.Windows.Controls.UserControl
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

    private static readonly string[] KnownNodes =
    {
        "devices", "users", "licenses", "app_versions", "admin_users",
        "project_tags", "cheat_sheet_data", "events", "errors",
        "feature_flags", "dev_users", "cheat_sheet_editors", "metrics",
        "tag_registry", "pending_updates"
    };

    private record ScriptTile(string Label, string Abbrev, string Color, Func<Task> Action, bool IsDanger = false);

    private readonly IFirebaseService? _firebaseService;
    private bool _isDev;
    private readonly string _scriptsDir;
    private readonly string? _serviceAccountPath;
    private readonly DispatcherTimer _clockTimer;
    private string? _selectedNodePill;

    public DeveloperPanelWidget(IFirebaseService? firebaseService)
    {
        InitializeComponent();
        _firebaseService = firebaseService;
        _scriptsDir = ResolveScriptsDir();
        _serviceAccountPath = ResolveServiceAccountPath();

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => ClockText.Text = DateTime.Now.ToString("h:mm:ss tt");

        Loaded += OnLoaded;
        Unloaded += (_, _) => _clockTimer.Stop();
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
            BuildScriptTiles();
            await RefreshHeartbeatsAsync();
            await RefreshVersionInfoAsync();
            await DiscoverNodePillsAsync();
        }
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
        MainDashboard.IsEnabled = _isDev;
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

    private bool EnsureDevForAction(string action)
    {
        if (_isDev) return true;
        AppendOutput($"DENIED: {action} requires DEV role.");
        return false;
    }

    private SolidColorBrush FindBrush(string key) =>
        TryFindResource(key) as SolidColorBrush ?? System.Windows.Media.Brushes.Gray;

    // ════════════════════════════════════════════════════════════
    // SCRIPT TILES
    // ════════════════════════════════════════════════════════════

    private void BuildScriptTiles()
    {
        var tiles = new List<ScriptTile>
        {
            new("Dump DB",       "db",  "#42A5F5", () => RunScriptAsync("admin.ps1", "-Action", "db-dump")),
            new("Backup",        "bk",  "#66BB6A", () => RunScriptAsync("admin.ps1", "-Action", "db-backup")),
            new("List Devices",  "ls",  "#42A5F5", () => RunScriptAsync("admin.ps1", "-Action", "update-list")),
            new("Push Update",   "pu",  "#FFA726", () => RunPushUpdateAll()),
            new("Build",         "bl",  "#AB47BC", () => RunScriptAsync("admin.ps1", "-Action", "build")),
            new("Version",       "vr",  "#26C6DA", () => RunVersionUpdate()),
            new("List Tags",     "tg",  "#66BB6A", () => RunScriptAsync("admin.ps1", "-Action", "tags-list")),
            new("Auth Users",    "au",  "#FFA726", () => RunScriptAsync("cleanup-auth-users.ps1")),
            new("Console",       "ac",  "#42A5F5", () => RunScriptAsync("admin.ps1")),
            new("Decrypt Tags",  "dt",  "#66BB6A", () => RunScriptAsync("admin.ps1", "-Action", "tags-decrypt")),
            new("Metrics Reset", "mr",  "#EF5350", () => RunScriptAsync("admin.ps1", "-Action", "metrics-reset")),
            new("HMAC Secret",   "hm",  "#78909C", () => RunScriptAsync("admin.ps1", "-Action", "show-secret")),
        };

        ScriptTileGrid.Children.Clear();
        foreach (var tile in tiles)
            ScriptTileGrid.Children.Add(CreateScriptTile(tile));
    }

    private UIElement CreateScriptTile(ScriptTile tile)
    {
        var accentColor = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(tile.Color);
        var accentBrush = new SolidColorBrush(accentColor);
        var bgBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(30, accentColor.R, accentColor.G, accentColor.B));
        var borderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, accentColor.R, accentColor.G, accentColor.B));

        var abbrevBlock = new TextBlock
        {
            Text = tile.Abbrev,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = accentBrush,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        };

        var labelBlock = new TextBlock
        {
            Text = tile.Label,
            FontSize = 9,
            Foreground = FindBrush("TextSecondaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 2, 0, 0)
        };

        var statusDot = new System.Windows.Shapes.Ellipse
        {
            Width = 6, Height = 6,
            Fill = FindBrush("TextTertiaryBrush"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top
        };

        var innerGrid = new Grid();
        innerGrid.Children.Add(statusDot);
        var stack = new StackPanel();
        stack.Children.Add(abbrevBlock);
        stack.Children.Add(labelBlock);
        innerGrid.Children.Add(stack);

        var border = new Border
        {
            Background = bgBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6, 8, 6),
            Margin = new Thickness(0, 0, 4, 4),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = innerGrid,
            ToolTip = tile.Label
        };

        var hoverBg = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, accentColor.R, accentColor.G, accentColor.B));
        border.MouseEnter += (_, _) => border.Background = hoverBg;
        border.MouseLeave += (_, _) => border.Background = bgBrush;
        border.MouseLeftButtonDown += async (_, _) =>
        {
            statusDot.Fill = FindBrush("GreenBrush");
            try { await tile.Action(); }
            catch (Exception ex) { AppendOutput($"ERROR: {ex.Message}"); }
        };

        return border;
    }

    // ════════════════════════════════════════════════════════════
    // HEARTBEATS
    // ════════════════════════════════════════════════════════════

    private async Task RefreshHeartbeatsAsync()
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized) return;

        try
        {
            HeartbeatList.Children.Clear();
            var devices = await _firebaseService.GetDevicesAsync();
            if (devices == null || devices.Count == 0)
            {
                HeartbeatCountText.Text = "0 online";
                return;
            }

            var grouped = devices
                .Select(kvp =>
                {
                    var d = kvp.Value;
                    return new
                    {
                        Username = d.TryGetValue("username", out var u) ? u?.ToString() ?? "unknown" : "unknown",
                        Device = d.TryGetValue("device_name", out var dn) ? dn?.ToString() ?? "" : "",
                        Status = d.TryGetValue("status", out var st) ? st?.ToString() ?? "unknown" : "unknown",
                        LastSeen = d.TryGetValue("last_seen", out var ls) ? ls?.ToString() ?? "" : "",
                    };
                })
                .GroupBy(x => x.Username)
                .OrderBy(g => g.Key)
                .ToList();

            int onlineCount = grouped.SelectMany(g => g).Count(d => d.Status == "active");
            HeartbeatCountText.Text = $"{onlineCount} online · {devices.Count} tracked";

            foreach (var group in grouped)
            {
                var first = group.First();
                var row = CreateHeartbeatRow(first.Username, first.Status, first.LastSeen);
                HeartbeatList.Children.Add(row);
            }
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR refreshing heartbeats: {ex.Message}");
        }
    }

    private UIElement CreateHeartbeatRow(string username, string status, string lastSeen)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leftStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };

        var nameBlock = new TextBlock
        {
            Text = username,
            FontSize = 12,
            Foreground = FindBrush("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
        leftStack.Children.Add(nameBlock);

        var roleBadge = CreateRoleBadge(status);
        leftStack.Children.Add(roleBadge);

        Grid.SetColumn(leftStack, 0);
        grid.Children.Add(leftStack);

        var timeAgo = FormatTimeAgo(lastSeen);
        var timeBlock = new TextBlock
        {
            Text = timeAgo,
            FontSize = 10,
            Foreground = FindBrush("TextTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(timeBlock, 1);
        grid.Children.Add(timeBlock);

        var border = new Border
        {
            Background = FindBrush("FaintOverlayBrush"),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 0, 2),
            Child = grid
        };

        return border;
    }

    private Border CreateRoleBadge(string status)
    {
        var isActive = status == "active";
        var bgKey = isActive ? "GreenBackgroundBrush" : "OrangeBackgroundBrush";
        var fgKey = isActive ? "GreenBrush" : "OrangeBrush";
        var text = isActive ? "active" : status;

        return new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Background = FindBrush(bgKey),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = text,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = FindBrush(fgKey)
            }
        };
    }

    private static string FormatTimeAgo(string? isoTimestamp)
    {
        if (string.IsNullOrWhiteSpace(isoTimestamp)) return "";
        if (!DateTime.TryParse(isoTimestamp, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return isoTimestamp;
        var span = DateTime.UtcNow - dt.ToUniversalTime();
        if (span.TotalMinutes < 1) return "now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }

    // ════════════════════════════════════════════════════════════
    // DATABASE EXPLORER — NODE PILLS
    // ════════════════════════════════════════════════════════════

    private async Task DiscoverNodePillsAsync()
    {
        NodePillsPanel.Children.Clear();

        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            NodeCountText.Text = "Firebase unavailable";
            return;
        }

        int found = 0;
        foreach (var nodeName in KnownNodes)
        {
            try
            {
                var data = await _firebaseService.GetNodeAsync(nodeName);
                if (data == null) continue;

                int count = data.Count;
                found++;
                var pill = CreateNodePill(nodeName, count);
                NodePillsPanel.Children.Add(pill);
            }
            catch { }
        }

        NodeCountText.Text = $"{found} nodes";
    }

    private UIElement CreateNodePill(string nodeName, int childCount)
    {
        var nameBlock = new TextBlock
        {
            Text = nodeName,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };

        var countBlock = new TextBlock
        {
            Text = childCount.ToString("N0"),
            FontSize = 10,
            Foreground = FindBrush("TextTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var stack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        stack.Children.Add(nameBlock);
        stack.Children.Add(countBlock);

        var isSelected = _selectedNodePill == nodeName;
        var bgBrush = isSelected ? FindBrush("SelectedAccentBrush") : FindBrush("SurfaceBrush");
        var borderBrush = isSelected ? FindBrush("AccentBrush") : FindBrush("BorderBrush");

        var border = new Border
        {
            Background = bgBrush,
            BorderBrush = borderBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 0, 6, 6),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = stack
        };

        border.MouseEnter += (_, _) => border.Background = FindBrush("HoverMediumBrush");
        border.MouseLeave += (_, _) => border.Background = isSelected ? FindBrush("SelectedAccentBrush") : FindBrush("SurfaceBrush");
        border.MouseLeftButtonDown += async (_, _) =>
        {
            _selectedNodePill = nodeName;
            NodePathBox.Text = nodeName;
            await GetNodeAsync();
            await DiscoverNodePillsAsync();
        };

        return border;
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

    private static string NormalizeUsername(string? raw) =>
        (raw ?? string.Empty).Trim().ToLowerInvariant();

    private string ResolveUsername()
    {
        var normalized = NormalizeUsername(RoleUsernameBox.Text);
        RoleUsernameBox.Text = normalized;
        return normalized;
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";

    private async Task RunScriptAsync(string fileName, params string[] args)
    {
        if (!EnsureDevForAction($"run {fileName}")) return;
        if (!AllowedScripts.Contains(fileName))
        {
            AppendOutput($"DENIED: Script '{fileName}' is not in the allowlist.");
            return;
        }

        var scriptPath = System.IO.Path.Combine(_scriptsDir, fileName);
        if (!System.IO.File.Exists(scriptPath))
        {
            AppendOutput($"ERROR: Script not found: {scriptPath}");
            return;
        }

        var allArgs = new List<string>(args);
        if (_serviceAccountPath != null)
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
                return;
            }

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(stdout))
                AppendOutput(stdout.TrimEnd());
            if (!string.IsNullOrWhiteSpace(stderr))
                AppendOutput("STDERR: " + stderr.TrimEnd());

            AppendOutput($"Exit code: {proc.ExitCode}");
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR running script: {ex.Message}");
        }
    }

    private void AppendOutput(string text)
    {
        OutputBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}{Environment.NewLine}");
        OutputBox.ScrollToEnd();
    }

    private async Task ExecuteRoleActionAsync(string script, string action, bool confirmDangerous = false)
    {
        var username = ResolveUsername();
        if (string.IsNullOrWhiteSpace(username))
        {
            AppendOutput("Please enter a username first.");
            return;
        }

        if (confirmDangerous)
        {
            var result = System.Windows.MessageBox.Show(
                $"Confirm role action '{action}' for '{username}'?",
                "Confirm Role Action",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);
            if (result != System.Windows.MessageBoxResult.Yes)
            {
                AppendOutput("Cancelled.");
                return;
            }
        }

        await RunScriptAsync(script, "-Action", action, "-Username", username);
    }

    private bool ConfirmDangerous(string message)
    {
        return System.Windows.MessageBox.Show(message, "Confirm", System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning) == System.Windows.MessageBoxResult.Yes;
    }

    // ════════════════════════════════════════════════════════════
    // VERSION INFO
    // ════════════════════════════════════════════════════════════

    private async Task RefreshVersionInfoAsync()
    {
        if (!EnsureDevForAction("refresh version info")) return;
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            VersionInfoText.Text = "Firebase service unavailable.";
            return;
        }

        try
        {
            var node = await _firebaseService.GetNodeAsync("app_versions/desktophub");
            if (node == null)
            {
                VersionInfoText.Text = "No version data found at app_versions/desktophub.";
                return;
            }

            var latest = node.TryGetValue("latest_version", out var lv) ? lv?.ToString() : "n/a";
            var notes = node.TryGetValue("release_notes", out var rn) ? rn?.ToString() : "";
            var updatedAt = node.TryGetValue("updated_at", out var ua) ? ua?.ToString() : "n/a";
            var required = node.TryGetValue("required_update", out var ru) ? ru?.ToString() : "false";

            VersionInfoText.Text = $"Latest: {latest} | Required: {required} | Updated: {updatedAt}{Environment.NewLine}{notes}";
        }
        catch (Exception ex)
        {
            VersionInfoText.Text = $"Failed to load version info: {ex.Message}";
        }
    }

    // ════════════════════════════════════════════════════════════
    // NODE BROWSER
    // ════════════════════════════════════════════════════════════

    private async Task GetNodeAsync()
    {
        if (!EnsureDevForAction("get firebase node")) return;
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase service unavailable.");
            return;
        }

        var path = (NodePathBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            AppendOutput("Node path is required.");
            return;
        }

        var node = await _firebaseService.GetNodeAsync(path);
        if (node == null)
        {
            NodeJsonBox.Text = "{}";
            AppendOutput($"No data found for '{path}'.");
            return;
        }

        NodeJsonBox.Text = JsonSerializer.Serialize(node, new JsonSerializerOptions { WriteIndented = true });
        AppendOutput($"Fetched node '{path}'.");
    }

    private async Task SetNodeAsync()
    {
        if (!EnsureDevForAction("set firebase node")) return;
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase service unavailable.");
            return;
        }

        var path = (NodePathBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            AppendOutput("Node path is required.");
            return;
        }

        object? payload;
        var raw = (NodeJsonBox.Text ?? string.Empty).Trim();
        try
        {
            using var doc = JsonDocument.Parse(raw);
            payload = JsonElementToObject(doc.RootElement);
        }
        catch (Exception ex)
        {
            AppendOutput($"Invalid JSON payload: {ex.Message}");
            return;
        }

        var result = await _firebaseService.SetNodeAsync(path, payload ?? new Dictionary<string, object>());
        AppendOutput(result ? $"Updated node '{path}'." : $"Failed to update node '{path}'.");
    }

    private async Task DeleteNodeAsync()
    {
        if (!EnsureDevForAction("delete firebase node")) return;
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase service unavailable.");
            return;
        }

        var path = (NodePathBox.Text ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            AppendOutput("Node path is required.");
            return;
        }

        if (!ConfirmDangerous($"Delete Firebase node '{path}'?"))
        {
            AppendOutput("Delete cancelled.");
            return;
        }

        var result = await _firebaseService.DeleteNodeAsync(path);
        AppendOutput(result ? $"Deleted node '{path}'." : $"Failed to delete node '{path}'.");
    }

    private static object? JsonElementToObject(JsonElement element) => element.ValueKind switch
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
    // SCRIPT TILE HELPERS (with confirmation)
    // ════════════════════════════════════════════════════════════

    private async Task RunPushUpdateAll()
    {
        if (!ConfirmDangerous("Push update to all outdated devices?")) return;
        await RunScriptAsync("admin.ps1", "-Action", "update-push-all");
    }

    private async Task RunVersionUpdate()
    {
        var version = DateTime.Now.ToString("yyyy.M.d", CultureInfo.InvariantCulture);
        await RunScriptAsync("admin.ps1", "-Action", "version-update", "-Version", version, "-ReleaseNotes", "Updated_from_Developer_Panel");
    }

    // ════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ════════════════════════════════════════════════════════════

    private void ClearOutput_Click(object sender, RoutedEventArgs e) => OutputBox.Clear();

    private void NormalizeUsername_Click(object sender, RoutedEventArgs e) => _ = ResolveUsername();

    private async void AddAdmin_Click(object sender, RoutedEventArgs e) => await ExecuteRoleActionAsync("manage-admin.ps1", "add");
    private async void RemoveAdmin_Click(object sender, RoutedEventArgs e) => await ExecuteRoleActionAsync("manage-admin.ps1", "remove", confirmDangerous: true);
    private async void AddEditor_Click(object sender, RoutedEventArgs e) => await ExecuteRoleActionAsync("manage-cheatsheet-editors.ps1", "add");
    private async void RemoveEditor_Click(object sender, RoutedEventArgs e) => await ExecuteRoleActionAsync("manage-cheatsheet-editors.ps1", "remove", confirmDangerous: true);
    private async void AddDev_Click(object sender, RoutedEventArgs e) => await ExecuteRoleActionAsync("manage-dev.ps1", "add");
    private async void RemoveDev_Click(object sender, RoutedEventArgs e) => await ExecuteRoleActionAsync("manage-dev.ps1", "remove", confirmDangerous: true);

    private async void ListAdmins_Click(object sender, RoutedEventArgs e) => await RunScriptAsync("manage-admin.ps1", "-Action", "list");
    private async void ListEditors_Click(object sender, RoutedEventArgs e) => await RunScriptAsync("manage-cheatsheet-editors.ps1", "-Action", "list");
    private async void ListDevs_Click(object sender, RoutedEventArgs e) => await RunScriptAsync("manage-dev.ps1", "-Action", "list");

    private async void GetNode_Click(object sender, RoutedEventArgs e) => await GetNodeAsync();
    private async void SetNode_Click(object sender, RoutedEventArgs e) => await SetNodeAsync();
    private async void DeleteNode_Click(object sender, RoutedEventArgs e) => await DeleteNodeAsync();

    private async void RefreshVersionInfo_Click(object sender, RoutedEventArgs e) => await RefreshVersionInfoAsync();
}
