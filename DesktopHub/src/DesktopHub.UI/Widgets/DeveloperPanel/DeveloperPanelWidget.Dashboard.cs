using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Newtonsoft.Json;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using WpfPath = System.Windows.Shapes.Path;

namespace DesktopHub.UI.Widgets;

public partial class DeveloperPanelWidget
{
    private const string QuickPeekDesktopHubAppId = "desktophub";

    private sealed record HealthOverviewDevice(string DeviceId, string Username, string Status, string Platform);
    private DispatcherTimer? _dashboardTimer;

    private List<(string Section, List<ScriptTile> Tiles)>? _scriptTileGroups;
    private int _activeScriptGroupIndex;
    private WrapPanel? _scriptCategoryBar;
    private UniformGrid? _scriptTilesGrid;
    // ════════════════════════════════════════════════════════════
    // HEALTH OVERVIEW
    // ════════════════════════════════════════════════════════════

    private async Task BuildHealthOverviewAsync()
    {
        HealthOverviewPanel.Children.Clear();

        var devices = await _firebaseService?.GetDevicesAsync()!;
        if (devices == null || devices.Count == 0)
        {
            HealthOverviewPanel.Children.Add(new TextBlock
            {
                Text = "No device data available",
                FontSize = 11,
                Foreground = FindBrush("TextSecondaryBrush"),
            });
            return;
        }

        // Resolve user_id hashes to display names via the tenant directory.
        await EnsureTenantUsersLoadedAsync();
        var userIdToUsername = _tenantUsers.ToDictionary(
            u => u.UserId, u => u.Username, StringComparer.OrdinalIgnoreCase);

        var rows = devices.Select(kvp =>
        {
            var d = kvp.Value;
            var plat = d.TryGetValue("platform", out var pl) ? pl?.ToString()?.Trim() ?? "" : "";
            var uid = d.TryGetValue("user_id", out var u) ? u?.ToString() ?? "" : "";
            var display = userIdToUsername.TryGetValue(uid, out var un) && !string.IsNullOrEmpty(un)
                ? un
                : (string.IsNullOrEmpty(uid) ? "unknown" : uid);
            return new HealthOverviewDevice(
                kvp.Key,
                display,
                d.TryGetValue("status", out var st) ? st?.ToString() ?? "unknown" : "unknown",
                plat);
        }).ToList();

        int totalDevices = rows.Count;
        int activeDevices = rows.Count(d => d.Status == "active");
        int inactiveDevices = totalDevices - activeDevices;
        var uniqueUsers = rows.Select(d => d.Username).Distinct().ToList();
        int totalUsers = uniqueUsers.Count;
        int onlineUsers = rows.Where(d => d.Status == "active").Select(d => d.Username).Distinct().Count();

        // Ring | stats | right region (two panes split the remaining width)
        var mainGrid = new Grid
        {
            Margin = new Thickness(0, 2, 0, 2),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
        };
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var chartContainer = CreateRingChart(activeDevices, totalDevices);
        Grid.SetColumn(chartContainer, 0);
        mainGrid.Children.Add(chartContainer);

        var statsPanel = new StackPanel { Margin = new Thickness(10, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center };
        AddStatRow(statsPanel, "Total Users", totalUsers.ToString(), "TextPrimaryBrush");
        AddStatRow(statsPanel, "Online Now", onlineUsers.ToString(), "GreenBrush");
        AddStatRow(statsPanel, "Total Devices", totalDevices.ToString(), "TextPrimaryBrush");
        AddStatRow(statsPanel, "Active", activeDevices.ToString(), "GreenBrush");
        AddStatRow(statsPanel, "Inactive", inactiveDevices.ToString(), "OrangeBrush");
        Grid.SetColumn(statsPanel, 1);
        mainGrid.Children.Add(statsPanel);

        var rightSplit = new Grid { HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
        rightSplit.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rightSplit.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var quickPeek = await BuildQuickPeekPanelAsync();
        var quickWrap = new Border
        {
            BorderBrush = FindBrush("BorderBrush"),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Margin = new Thickness(4, 0, 0, 0),
            Padding = new Thickness(12, 4, 8, 4),
            Child = quickPeek,
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
        };
        Grid.SetColumn(quickWrap, 0);
        rightSplit.Children.Add(quickWrap);

        var fleetWrap = new Border
        {
            BorderBrush = FindBrush("BorderBrush"),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Margin = new Thickness(0, 0, 0, 0),
            Padding = new Thickness(12, 4, 0, 4),
            Child = BuildFleetSnapshotPanel(rows),
            VerticalAlignment = VerticalAlignment.Stretch,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
        };
        Grid.SetColumn(fleetWrap, 1);
        rightSplit.Children.Add(fleetWrap);

        Grid.SetColumn(rightSplit, 2);
        mainGrid.Children.Add(rightSplit);

        Grid.SetRow(mainGrid, 0);
        Grid.SetColumn(mainGrid, 0);
        HealthOverviewPanel.Children.Add(mainGrid);
    }

    private StackPanel BuildFleetSnapshotPanel(IReadOnlyList<HealthOverviewDevice> rows)
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = "Fleet snapshot",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("TextTertiaryBrush"),
            Margin = new Thickness(0, 0, 0, 6),
        });

        var byPlatform = rows
            .Select(d => string.IsNullOrWhiteSpace(d.Platform) ? "unknown" : d.Platform)
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(6)
            .ToList();

        if (byPlatform.Count == 0)
            AddQuickPeekRow(panel, "Platforms", "—", "TextTertiaryBrush");
        else
        {
            foreach (var g in byPlatform)
                AddQuickPeekRow(panel, g.Key, g.Count().ToString(), "TextPrimaryBrush");
        }

        panel.Children.Add(new TextBlock
        {
            Text = "Users (most devices)",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("TextTertiaryBrush"),
            Margin = new Thickness(0, 8, 0, 4),
        });

        var topUsers = rows
            .GroupBy(d => d.Username, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Take(4)
            .ToList();

        if (topUsers.Count == 0)
            AddQuickPeekRow(panel, "Top users", "—", "TextTertiaryBrush");
        else
        {
            foreach (var g in topUsers)
                AddQuickPeekRow(panel, g.Key, $"{g.Count()} device(s)", "TextSecondaryBrush");
        }

        return panel;
    }

    /// <summary>
    /// Pushed updates live under <c>force_update/{deviceId}</c> with <c>status: pending</c> (see push-update.ps1).
    /// The legacy <c>pending_updates</c> path is not used by the current push flow.
    /// </summary>
    private static int CountPendingForceUpdateEntries(Dictionary<string, object>? forceRoot)
    {
        if (forceRoot == null || forceRoot.Count == 0)
            return 0;

        var n = 0;
        foreach (var kvp in forceRoot)
        {
            if (!TryReadForceUpdateFields(kvp.Value, out var status, out var appId))
                continue;
            if (!string.Equals(status, "pending", StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrEmpty(appId) &&
                !string.Equals(appId, QuickPeekDesktopHubAppId, StringComparison.OrdinalIgnoreCase))
                continue;
            n++;
        }

        return n;
    }

    private static bool TryReadForceUpdateFields(object? entry, out string? status, out string? appId)
    {
        status = null;
        appId = null;
        if (entry == null)
            return false;

        if (entry is Dictionary<string, object> d)
        {
            ExtractStatusAppId(d, out status, out appId);
            return true;
        }

        try
        {
            var json = JsonConvert.SerializeObject(entry);
            var map = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            if (map == null)
                return false;
            ExtractStatusAppId(map, out status, out appId);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ExtractStatusAppId(Dictionary<string, object> map, out string? status, out string? appId)
    {
        status = map.TryGetValue("status", out var s) ? s?.ToString() : null;
        appId = map.TryGetValue("app_id", out var a) ? a?.ToString() : null;
    }

    private async Task<StackPanel> BuildQuickPeekPanelAsync()
    {
        var panel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        panel.Children.Add(new TextBlock
        {
            Text = "Quick peek",
            FontSize = 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("TextTertiaryBrush"),
            Margin = new Thickness(0, 0, 0, 6),
        });

        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AddQuickPeekRow(panel, "Firebase", "Not connected", "TextTertiaryBrush");
            return panel;
        }

        try
        {
            var verTask = _firebaseService.GetNodeAsync("app_versions/desktophub");
            // Pushes write status under force_update/{deviceId} (see push-update.ps1), not pending_updates.
            var forceTask = _firebaseService.GetNodeAsync("force_update");
            var licTask = _firebaseService.GetNodeAsync("licenses");
            await Task.WhenAll(verTask, forceTask, licTask);

            var ver = await verTask;
            var latest = ver?.TryGetValue("latest_version", out var lv) == true ? lv?.ToString() : null;
            if (string.IsNullOrWhiteSpace(latest)) latest = "—";
            var req = ver?.TryGetValue("required_update", out var ru) == true &&
                      string.Equals(ru?.ToString(), "true", StringComparison.OrdinalIgnoreCase);
            var verNote = req ? "required" : "optional";
            AddQuickPeekRow(panel, "Published", $"{latest}  ({verNote})", "TextPrimaryBrush");

            var forceRoot = await forceTask;
            var n = CountPendingForceUpdateEntries(forceRoot);
            var pendingMsg = n == 0 ? "None pending" : $"{n} pending";
            AddQuickPeekRow(panel, "Update queue", pendingMsg, n > 0 ? "OrangeBrush" : "GreenBrush");
            if (n > 0)
            {
                panel.Children.Add(new TextBlock
                {
                    Text = "Pending rows are force_update entries. If one never leaves pending, the device build may not support forced updates yet (e.g. before 1.8.0)—that user may need one manual install.",
                    FontSize = 9,
                    Foreground = FindBrush("TextTertiaryBrush"),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0),
                });
            }

            var lic = await licTask;
            var lc = lic?.Count ?? 0;
            AddQuickPeekRow(panel, "License keys", lc.ToString(), "TextPrimaryBrush");
        }
        catch (Exception ex)
        {
            AddQuickPeekRow(panel, "Quick peek", ex.Message, "TextTertiaryBrush");
        }

        return panel;
    }

    private void AddQuickPeekRow(StackPanel parent, string label, string value, string valueBrushKey)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 3) };
        row.Children.Add(new TextBlock
        {
            Text = $"{label}: ",
            FontSize = 10,
            Foreground = FindBrush("TextSecondaryBrush"),
            Width = 92,
        });
        row.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush(valueBrushKey),
            TextWrapping = TextWrapping.Wrap,
        });
        parent.Children.Add(row);
    }

    private void StartDashboardTimer()
    {
        if (_dashboardTimer == null)
        {
            _dashboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _dashboardTimer.Tick += async (_, _) =>
            {
                await BuildHealthOverviewAsync();
                BuildSystemStatus();
            };
        }
        _dashboardTimer.Start();
    }

    private void StopDashboardTimer()
    {
        _dashboardTimer?.Stop();
    }

    private UIElement CreateRingChart(int active, int total)
    {
        double size = 106;
        double cx = size / 2, cy = size / 2;
        // Thinner ring stroke (~5px) — was outerR−innerR ≈ 14
        double outerR = 46, innerR = 41;

        var canvas = new Canvas { Width = size, Height = size, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

        if (total == 0)
        {
            // Empty ring
            canvas.Children.Add(new Ellipse
            {
                Width = outerR * 2, Height = outerR * 2,
                Stroke = FindBrush("BorderBrush"),
                StrokeThickness = outerR - innerR,
                Fill = WpfBrushes.Transparent,
            });
            Canvas.SetLeft(canvas.Children[0], cx - outerR);
            Canvas.SetTop(canvas.Children[0], cy - outerR);
        }
        else
        {
            double activeRatio = (double)active / total;
            double midR = (outerR + innerR) / 2;
            double thickness = outerR - innerR;

            // Background ring (inactive)
            var bgRing = new Ellipse
            {
                Width = midR * 2, Height = midR * 2,
                Stroke = FindBrush("OrangeBackgroundBrush"),
                StrokeThickness = thickness,
                Fill = WpfBrushes.Transparent,
            };
            Canvas.SetLeft(bgRing, cx - midR);
            Canvas.SetTop(bgRing, cy - midR);
            canvas.Children.Add(bgRing);

            // Active arc
            if (active > 0 && active < total)
            {
                double angle = activeRatio * 360;
                var arc = CreateArc(cx, cy, midR, thickness, 0, angle, FindBrush("GreenBrush"));
                canvas.Children.Add(arc);
            }
            else if (active == total)
            {
                var fullRing = new Ellipse
                {
                    Width = midR * 2, Height = midR * 2,
                    Stroke = FindBrush("GreenBrush"),
                    StrokeThickness = thickness,
                    Fill = WpfBrushes.Transparent,
                };
                Canvas.SetLeft(fullRing, cx - midR);
                Canvas.SetTop(fullRing, cy - midR);
                canvas.Children.Add(fullRing);
            }
        }

        var centerOverlay = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Margin = new Thickness(0, -2, 0, 0),
        };
        centerOverlay.Children.Add(new TextBlock
        {
            Text = $"{active}/{total}",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = FindBrush("TextPrimaryBrush"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        });
        centerOverlay.Children.Add(new TextBlock
        {
            Text = "devices",
            FontSize = 10,
            Foreground = FindBrush("TextTertiaryBrush"),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, -2, 0, 0),
        });

        var container = new Grid
        {
            Width = 118,
            Height = 118,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        container.Children.Add(canvas);
        container.Children.Add(centerOverlay);
        return container;
    }

    private WpfPath CreateArc(double cx, double cy, double r, double thickness, double startAngle, double sweepAngle, System.Windows.Media.Brush stroke)
    {
        double startRad = (startAngle - 90) * Math.PI / 180;
        double endRad = (startAngle + sweepAngle - 90) * Math.PI / 180;

        double x1 = cx + r * Math.Cos(startRad);
        double y1 = cy + r * Math.Sin(startRad);
        double x2 = cx + r * Math.Cos(endRad);
        double y2 = cy + r * Math.Sin(endRad);

        bool isLargeArc = sweepAngle > 180;

        var pg = new PathGeometry();
        var pf = new PathFigure { StartPoint = new WpfPoint(x1, y1), IsClosed = false, IsFilled = false };
        pf.Segments.Add(new ArcSegment(new WpfPoint(x2, y2), new WpfSize(r, r), 0, isLargeArc, SweepDirection.Clockwise, true));
        pg.Figures.Add(pf);

        return new WpfPath
        {
            Data = pg,
            Stroke = stroke,
            StrokeThickness = thickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
        };
    }

    private void AddStatRow(StackPanel parent, string label, string value, string valueBrushKey)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(new TextBlock
        {
            Text = $"{label}: ",
            FontSize = 10,
            Foreground = FindBrush("TextSecondaryBrush"),
            Width = 85,
        });
        row.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush(valueBrushKey),
        });
        parent.Children.Add(row);
    }

    // ════════════════════════════════════════════════════════════
    // SYSTEM STATUS
    // ════════════════════════════════════════════════════════════

    private void BuildSystemStatus()
    {
        SystemStatusPanel.Children.Clear();

        var fbStatus = _firebaseService?.IsInitialized == true ? "Connected" : "Disconnected";
        var fbColor = _firebaseService?.IsInitialized == true ? "GreenBrush" : "RedBrush";

        AddSystemRow(SystemStatusPanel, "Firebase", fbStatus, fbColor);
        AddSystemRow(SystemStatusPanel, "User", Environment.UserName?.ToLowerInvariant() ?? "unknown", "TextPrimaryBrush");
        AddSystemRow(SystemStatusPanel, "Role", _isDev ? "DEV" : "DENIED", _isDev ? "BlueBrush" : "RedBrush");
        AddSystemRow(SystemStatusPanel, "AdminOps", "Native C#", "GreenBrush");
    }

    private void AddSystemRow(StackPanel parent, string label, string value, string valueBrushKey)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
        row.Children.Add(new TextBlock
        {
            Text = $"{label}: ",
            FontSize = 10,
            Foreground = FindBrush("TextSecondaryBrush"),
            Width = 60,
        });
        row.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush(valueBrushKey),
        });
        parent.Children.Add(row);
    }

    // ════════════════════════════════════════════════════════════
    // GROUPED SCRIPT TILES
    // ════════════════════════════════════════════════════════════

    private void BuildScriptTiles()
    {
        _scriptTileGroups = new List<(string Section, List<ScriptTile> Tiles)>
        {
            ("Database", new List<ScriptTile>
            {
                new("Dump DB",      "db", "#42A5F5", () => DumpDatabaseAsync()),
                new("Backup",       "bk", "#66BB6A", () => BackupDatabaseAsync()),
                new("Decrypt Tags", "dt", "#66BB6A", () => DecryptTagsAsync()),
            }),
            ("Deployment", new List<ScriptTile>
            {
                new("Push Update",  "pu", "#FFA726", () => RunPushUpdateAll()),
                new("Version",      "vr", "#26C6DA", () => RunVersionUpdate()),
            }),
            ("Management", new List<ScriptTile>
            {
                new("List Devices", "ls", "#42A5F5", () => ListDevicesAndVersionsAsync()),
                new("List Tags",    "tg", "#66BB6A", () => ListTagsAsync()),
                new("Cleanup Devs", "cd", "#EF5350", () => CleanupDuplicateDevicesAsync(), IsDanger: true),
                new("HMAC Secret",  "hm", "#78909C", () => { ShowHmacSecret(); return Task.CompletedTask; }),
                new("Metrics Reset","mr", "#EF5350", () => { ResetLocalMetrics(); return Task.CompletedTask; }),
            }),
        };

        _activeScriptGroupIndex = 0;
        ScriptTileContainer.Children.Clear();

        _scriptCategoryBar = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
        ScriptTileContainer.Children.Add(_scriptCategoryBar);

        // Single grid directly under pills — no extra bordered "inner tray" (section card is enough chrome).
        _scriptTilesGrid = new UniformGrid { Columns = 3, Margin = new Thickness(0, 0, 0, 0) };
        ScriptTileContainer.Children.Add(_scriptTilesGrid);

        RefreshScriptCategoryUI();
    }

    private void SelectScriptGroup(int index)
    {
        if (_scriptTileGroups == null || index < 0 || index >= _scriptTileGroups.Count)
            return;
        _activeScriptGroupIndex = index;
        RefreshScriptCategoryUI();
    }

    private void RefreshScriptCategoryUI()
    {
        if (_scriptCategoryBar == null || _scriptTilesGrid == null || _scriptTileGroups == null)
            return;

        _scriptCategoryBar.Children.Clear();
        for (var i = 0; i < _scriptTileGroups.Count; i++)
        {
            var idx = i;
            var pill = CreateScriptCategoryPill(_scriptTileGroups[i].Section, i == _activeScriptGroupIndex);
            pill.MouseLeftButtonDown += (_, _) => SelectScriptGroup(idx);
            _scriptCategoryBar.Children.Add(pill);
        }

        _scriptTilesGrid.Children.Clear();
        foreach (var tile in _scriptTileGroups[_activeScriptGroupIndex].Tiles)
            _scriptTilesGrid.Children.Add(CreateScriptTile(tile));
    }

    private Border CreateScriptCategoryPill(string label, bool selected)
    {
        var bg = selected ? FindBrush("AccentBrush") : FindBrush("SurfaceBrush");
        var fg = selected ? System.Windows.Media.Brushes.White : FindBrush("TextPrimaryBrush");
        var bd = selected ? FindBrush("AccentBrush") : FindBrush("BorderBrush");

        var text = new TextBlock
        {
            Text = label,
            FontSize = 10,
            FontWeight = selected ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = fg,
        };

        var border = new Border
        {
            Background = bg,
            BorderBrush = bd,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(12, 5, 12, 5),
            Margin = new Thickness(0, 0, 6, 4),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = text,
        };

        var hover = FindBrush("HoverMediumBrush");
        var normalBg = bg;
        border.MouseEnter += (_, _) =>
        {
            if (!selected)
                border.Background = hover;
        };
        border.MouseLeave += (_, _) => { border.Background = normalBg; };

        return border;
    }

    private UIElement CreateScriptTile(ScriptTile tile)
    {
        var accentColor = (Color)ColorConverter.ConvertFromString(tile.Color);
        var accentBrush = new SolidColorBrush(accentColor);
        var neutralBorder = FindBrush("BorderBrush");
        var normalBg = FindBrush("FaintOverlayBrush");
        var hoverBg = FindBrush("HoverMediumBrush");

        var abbrevBlock = new TextBlock
        {
            Text = tile.Abbrev,
            FontSize = 12,
            FontWeight = FontWeights.Bold,
            Foreground = accentBrush,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left
        };

        var labelBlock = new TextBlock
        {
            Text = tile.Label,
            FontSize = 7.5,
            Foreground = FindBrush("TextSecondaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 0, 0, 0)
        };

        var statusDot = new Ellipse
        {
            Width = 4, Height = 4,
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
            Background = normalBg,
            BorderBrush = neutralBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(5, 3, 5, 3),
            Margin = new Thickness(0, 0, 3, 3),
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = innerGrid,
            ToolTip = tile.Label
        };

        border.MouseEnter += (_, _) => border.Background = hoverBg;
        border.MouseLeave += (_, _) => border.Background = normalBg;
        border.MouseLeftButtonDown += async (_, _) =>
        {
            statusDot.Fill = FindBrush("GreenBrush");
            try { await tile.Action(); }
            catch (Exception ex) { AppendOutput($"ERROR: {ex.Message}"); }
        };

        return border;
    }

    // ════════════════════════════════════════════════════════════
    // VERSION INFO
    // ════════════════════════════════════════════════════════════

    private async Task RefreshVersionInfoAsync()
    {
        if (!EnsureDevForAction("refresh version info")) return;
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            ShowVersionInfoError("Firebase unavailable.");
            return;
        }

        try
        {
            var node = await _firebaseService.GetNodeAsync("app_versions/desktophub");
            if (node == null)
            {
                ShowVersionInfoError("No version data.");
                return;
            }

            var latest = node.TryGetValue("latest_version", out var lv) ? lv?.ToString() : "n/a";
            var notes = node.TryGetValue("release_notes", out var rn) ? rn?.ToString() : "";
            var updatedAt = node.TryGetValue("updated_at", out var ua) ? ua?.ToString() : "n/a";
            var required = node.TryGetValue("required_update", out var ru) ? ru?.ToString() : "false";
            var isRequired = required?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;

            BuildVersionInfoDisplay(latest ?? "n/a", isRequired, updatedAt ?? "n/a", notes ?? "");
        }
        catch (Exception ex)
        {
            ShowVersionInfoError($"Error: {ex.Message}");
        }
    }

    private void BuildVersionInfoDisplay(string version, bool isRequired, string updatedAt, string notes)
    {
        VersionInfoPanel.Children.Clear();

        var versionRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        versionRow.Children.Add(new TextBlock
        {
            Text = version,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = FindBrush("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        });

        var badgeBg = isRequired ? "OrangeBackgroundBrush" : "GreenBackgroundBrush";
        var badgeFg = isRequired ? "OrangeBrush" : "GreenBrush";
        versionRow.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            Background = FindBrush(badgeBg),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = isRequired ? "REQUIRED" : "OPTIONAL",
                FontSize = 8,
                FontWeight = FontWeights.Bold,
                Foreground = FindBrush(badgeFg),
            }
        });
        VersionInfoPanel.Children.Add(versionRow);

        AddSystemRow(VersionInfoPanel, "Updated", updatedAt, "TextPrimaryBrush");

        if (!string.IsNullOrWhiteSpace(notes))
        {
            VersionInfoPanel.Children.Add(new TextBlock
            {
                Text = notes,
                FontSize = 9,
                Foreground = FindBrush("TextSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0),
            });
        }
    }

    private void ShowVersionInfoError(string message)
    {
        VersionInfoPanel.Children.Clear();
        VersionInfoPanel.Children.Add(new TextBlock
        {
            Text = message, FontSize = 10, Foreground = FindBrush("TextSecondaryBrush"), TextWrapping = TextWrapping.Wrap,
        });
    }

    private async Task RunPushUpdateAll()
    {
        if (!await ConfirmDangerousAsync("Push update to all outdated devices?")) return;
        await PushUpdateToAllAsync();
    }

    private async Task RunVersionUpdate()
    {
        var version = DateTime.Now.ToString("yyyy.M.d", CultureInfo.InvariantCulture);
        await PublishVersionAsync("desktophub", version, "Updated from Developer Panel");
    }

    private async void RefreshVersionInfo_Click(object sender, RoutedEventArgs e) => await RefreshVersionInfoAsync();
}
