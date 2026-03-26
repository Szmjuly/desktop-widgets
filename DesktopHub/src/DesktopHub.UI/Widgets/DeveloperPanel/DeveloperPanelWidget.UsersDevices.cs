using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using DesktopHub.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using FontFamily = System.Windows.Media.FontFamily;
using Orientation = System.Windows.Controls.Orientation;

namespace DesktopHub.UI.Widgets;

public partial class DeveloperPanelWidget
{
    private sealed class UserDeviceDetail
    {
        public required string DeviceId { get; init; }
        public required string Username { get; init; }
        public string DeviceName { get; init; } = "";
        public string Status { get; init; } = "unknown";
        public string LastSeen { get; init; } = "";
        public string MacAddress { get; init; } = "";
        public string Platform { get; init; } = "";
        public string PlatformVersion { get; init; } = "";
        public string Machine { get; init; } = "";
        public string LicenseKey { get; init; } = "";
        public Dictionary<string, object>? Apps { get; init; }
    }

    private List<UserDeviceDetail> _allDeviceDetails = new();
    private string? _selectedDetailUser;

    private async Task RefreshUsersDevicesAsync()
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized) return;

        try
        {
            UsersDevicesList.Children.Clear();
            var devices = await _firebaseService.GetDevicesAsync();
            if (devices == null || devices.Count == 0)
            {
                UsersDevicesCountText.Text = "0 devices";
                _knownUsernames.Clear();
                _allDeviceDetails.Clear();
                return;
            }

            _allDeviceDetails = devices
                .Select(kvp =>
                {
                    var d = kvp.Value;
                    Dictionary<string, object>? apps = null;
                    if (d.TryGetValue("apps", out var appsObj) && appsObj is Dictionary<string, object> appsDict)
                        apps = appsDict;

                    return new UserDeviceDetail
                    {
                        DeviceId = kvp.Key,
                        Username = d.TryGetValue("username", out var u) ? u?.ToString() ?? "unknown" : "unknown",
                        DeviceName = d.TryGetValue("device_name", out var dn) ? dn?.ToString() ?? "" : "",
                        Status = d.TryGetValue("status", out var st) ? st?.ToString() ?? "unknown" : "unknown",
                        LastSeen = d.TryGetValue("last_seen", out var ls) ? ls?.ToString() ?? "" : "",
                        MacAddress = d.TryGetValue("mac_address", out var mac) ? mac?.ToString() ?? "" : "",
                        Platform = d.TryGetValue("platform", out var pl) ? pl?.ToString() ?? "" : "",
                        PlatformVersion = d.TryGetValue("platform_version", out var pv) ? pv?.ToString() ?? "" : "",
                        Machine = d.TryGetValue("machine", out var m) ? m?.ToString() ?? "" : "",
                        LicenseKey = d.TryGetValue("license_key", out var lk) ? lk?.ToString() ?? "" : "",
                        Apps = apps,
                    };
                })
                .ToList();

            var grouped = _allDeviceDetails.GroupBy(r => r.Username).OrderBy(g => g.Key).ToList();
            UsersDevicesCountText.Text = $"{devices.Count} devices · {grouped.Count} users";

            _knownUsernames = grouped.Select(g => g.Key).Distinct().OrderBy(x => x).ToList();

            foreach (var group in grouped)
                UsersDevicesList.Children.Add(CreateUserCard(group.Key, group.ToList()));

            LayoutUsersDevicesUniformGrid();

            PopulateUsernameDropdown();
            PopulateUpdateDropdowns();

            // Re-select detail panel if it was open
            if (_selectedDetailUser != null && _allDeviceDetails.Any(d => d.Username == _selectedDetailUser))
                await ShowUserDetailAsync(_selectedDetailUser);
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR loading users/devices: {ex.Message}");
        }
    }

    // ════════════════════════════════════════════════════════════
    // CARD GRID: compact user summary cards
    // ════════════════════════════════════════════════════════════

    private UIElement CreateUserCard(string username, IReadOnlyList<UserDeviceDetail> devices)
    {
        var anyActive = devices.Any(d => d.Status == "active");

        // Status dot
        var statusDot = new System.Windows.Shapes.Ellipse
        {
            Width = 8,
            Height = 8,
            Fill = FindBrush(anyActive ? "GreenBrush" : "OrangeBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
        };

        var nameBlock = new TextBlock
        {
            Text = username,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var topRow = new StackPanel { Orientation = Orientation.Horizontal };
        topRow.Children.Add(statusDot);
        topRow.Children.Add(nameBlock);

        // Device count + last seen
        var newestSeen = devices
            .Where(d => !string.IsNullOrWhiteSpace(d.LastSeen))
            .Select(d => d.LastSeen)
            .OrderByDescending(s => s)
            .FirstOrDefault();

        var metaText = $"{devices.Count} device{(devices.Count == 1 ? "" : "s")}";
        var ago = FormatTimeAgo(newestSeen);
        if (!string.IsNullOrEmpty(ago))
            metaText += $" · {ago}";

        var metaBlock = new TextBlock
        {
            Text = metaText,
            FontSize = 9,
            Foreground = FindBrush("TextTertiaryBrush"),
            Margin = new Thickness(0, 4, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        // Status badge
        var statusBadge = CreateRoleBadge(anyActive ? "active" : "inactive");
        statusBadge.Margin = new Thickness(0, 4, 0, 0);

        var stack = new StackPanel();
        stack.Children.Add(topRow);
        stack.Children.Add(metaBlock);
        stack.Children.Add(statusBadge);

        var border = new Border
        {
            Background = FindBrush("FaintOverlayBrush"),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(4),
            MinWidth = 128,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = stack,
            ToolTip = $"{username} — {devices.Count} device(s)",
        };

        border.MouseEnter += (_, _) => border.Background = FindBrush("HoverMediumBrush");
        border.MouseLeave += (_, _) =>
        {
            border.Background = _selectedDetailUser == username
                ? FindBrush("HoverMediumBrush")
                : FindBrush("FaintOverlayBrush");
        };
        border.MouseLeftButtonDown += async (_, _) => await ShowUserDetailAsync(username);

        return border;
    }

    // ════════════════════════════════════════════════════════════
    // DETAIL PANEL: full device info for selected user
    // ════════════════════════════════════════════════════════════

    private async Task ShowUserDetailAsync(string username)
    {
        _selectedDetailUser = username;
        UserDetailContent.Children.Clear();

        var userDevices = _allDeviceDetails
            .Where(d => d.Username == username)
            .OrderBy(d => d.DeviceName)
            .ThenBy(d => d.DeviceId)
            .ToList();

        // Header: username + close button
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var headerText = new TextBlock
        {
            Text = username,
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = FindBrush("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(headerText, 0);
        headerGrid.Children.Add(headerText);

        var closeBtn = new TextBlock
        {
            Text = "✕",
            FontSize = 14,
            Foreground = FindBrush("TextSecondaryBrush"),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        closeBtn.MouseLeftButtonDown += (_, _) => CloseUserDetail();
        Grid.SetColumn(closeBtn, 1);
        headerGrid.Children.Add(closeBtn);

        UserDetailContent.Children.Add(headerGrid);

        // Summary line
        var anyActive = userDevices.Any(d => d.Status == "active");
        var summaryPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 8) };
        summaryPanel.Children.Add(CreateRoleBadge(anyActive ? "active" : "inactive"));
        summaryPanel.Children.Add(new TextBlock
        {
            Text = $"{userDevices.Count} device{(userDevices.Count == 1 ? "" : "s")}",
            FontSize = 10,
            Foreground = FindBrush("TextTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        });
        UserDetailContent.Children.Add(summaryPanel);

        // Separator
        UserDetailContent.Children.Add(new Border
        {
            Height = 1,
            Background = FindBrush("BorderBrush"),
            Margin = new Thickness(0, 0, 0, 8),
        });

        // Device sub-cards
        foreach (var device in userDevices)
            UserDetailContent.Children.Add(CreateDeviceDetailCard(device));

        var enrichmentHost = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
        UserDetailContent.Children.Add(enrichmentHost);
        enrichmentHost.Children.Add(new TextBlock
        {
            Text = "Loading metrics and permissions...",
            FontSize = 10,
            Foreground = FindBrush("TextTertiaryBrush")
        });

        // Show the panel
        UserDetailPanel.Visibility = Visibility.Visible;
        DetailPanelColumn.Width = new GridLength(240);

        LayoutUsersDevicesUniformGrid();

        await LoadDetailEnrichmentAsync(username, userDevices, enrichmentHost);
    }

    private void UsersDevicesScroller_SizeChanged(object sender, SizeChangedEventArgs e) =>
        LayoutUsersDevicesUniformGrid();

    /// <summary>
    /// Fills available width with user cards (UniformGrid columns from viewport), avoiding empty space when the detail panel is closed.
    /// </summary>
    private void LayoutUsersDevicesUniformGrid()
    {
        if (UsersDevicesList is not UniformGrid grid)
            return;

        const double minCellWidth = 152;
        var available = UsersDevicesScroller?.ActualWidth ?? 0;
        if (double.IsNaN(available) || available < 8)
            return;

        var usable = Math.Max(0, available - 8);
        var cols = Math.Max(1, (int)Math.Floor(usable / minCellWidth));
        if (grid.Columns != cols)
            grid.Columns = cols;
    }

    private UIElement CreateDeviceDetailCard(UserDeviceDetail d)
    {
        var stack = new StackPanel();

        // Device name + status
        var nameRow = new Grid();
        nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nameRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var deviceName = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(d.DeviceName) ? "(unnamed device)" : d.DeviceName,
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("TextPrimaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(deviceName, 0);
        nameRow.Children.Add(deviceName);

        var statusBadge = CreateRoleBadge(d.Status);
        Grid.SetColumn(statusBadge, 1);
        nameRow.Children.Add(statusBadge);

        stack.Children.Add(nameRow);

        // Detail rows
        AddDetailRow(stack, "ID", d.DeviceId, monospace: true);

        var platformInfo = d.Platform;
        if (!string.IsNullOrEmpty(d.Machine)) platformInfo += $" {d.Machine}";
        if (!string.IsNullOrEmpty(d.PlatformVersion)) platformInfo += $" ({d.PlatformVersion})";
        if (!string.IsNullOrEmpty(platformInfo.Trim()))
            AddDetailRow(stack, "Platform", platformInfo.Trim());

        if (!string.IsNullOrEmpty(d.MacAddress) && d.MacAddress != "unknown")
            AddDetailRow(stack, "MAC", d.MacAddress, monospace: true);

        if (!string.IsNullOrEmpty(d.LicenseKey))
            AddDetailRow(stack, "License", d.LicenseKey, monospace: true);

        var ago = FormatTimeAgo(d.LastSeen);
        if (!string.IsNullOrEmpty(ago))
            AddDetailRow(stack, "Last seen", ago);

        // App state if available
        if (d.Apps != null)
        {
            foreach (var appKvp in d.Apps)
            {
                if (appKvp.Value is Dictionary<string, object> appData)
                {
                    var appVersion = appData.TryGetValue("version", out var v) ? v?.ToString() : null;
                    var appStatus = appData.TryGetValue("status", out var s) ? s?.ToString() : null;
                    var appLaunch = appData.TryGetValue("last_launch", out var ll) ? ll?.ToString() : null;

                    var appInfo = "";
                    if (!string.IsNullOrEmpty(appVersion)) appInfo += $"v{appVersion}";
                    if (!string.IsNullOrEmpty(appStatus)) appInfo += $" · {appStatus}";
                    var launchAgo = FormatTimeAgo(appLaunch);
                    if (!string.IsNullOrEmpty(launchAgo)) appInfo += $" · {launchAgo}";

                    if (!string.IsNullOrEmpty(appInfo.Trim()))
                        AddDetailRow(stack, appKvp.Key, appInfo.Trim());
                }
            }
        }

        return new Border
        {
            Background = FindBrush("CardBrush"),
            CornerRadius = new CornerRadius(6),
            BorderBrush = FindBrush("CardBorderBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 6),
            Child = stack,
        };
    }

    private void AddDetailRow(StackPanel parent, string label, string value, bool monospace = false)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 0) };
        row.Children.Add(new TextBlock
        {
            Text = $"{label}: ",
            FontSize = 10,
            Foreground = FindBrush("TextSecondaryBrush"),
        });
        var valueBlock = new TextBlock
        {
            Text = value,
            FontSize = 10,
            Foreground = FindBrush("TextPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap,
        };
        if (monospace)
            valueBlock.FontFamily = new FontFamily("Consolas");
        row.Children.Add(valueBlock);
        parent.Children.Add(row);
    }

    private void CloseUserDetail()
    {
        _selectedDetailUser = null;
        UserDetailContent.Children.Clear();
        UserDetailPanel.Visibility = Visibility.Collapsed;
        DetailPanelColumn.Width = new GridLength(0);
        LayoutUsersDevicesUniformGrid();
    }

    private async Task LoadDetailEnrichmentAsync(string username, List<UserDeviceDetail> userDevices, StackPanel target)
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
            return;

        try
        {
            target.Children.Clear();
            var roleBadges = await BuildPermissionsBadgesAsync(username);
            target.Children.Add(CreateSectionHeader("Roles"));
            target.Children.Add(roleBadges);

            var metrics = await LoadLatestMetricsForUserDevicesAsync(userDevices);
            target.Children.Add(CreateSectionHeader("Widgets Used"));
            target.Children.Add(BuildWidgetUsageSection(metrics));

            target.Children.Add(CreateSectionHeader("Key Metrics"));
            target.Children.Add(BuildKeyMetricsSection(metrics));
        }
        catch (Exception ex)
        {
            target.Children.Clear();
            target.Children.Add(new TextBlock
            {
                Text = $"Unable to load enrichment data: {ex.Message}",
                FontSize = 10,
                Foreground = FindBrush("TextTertiaryBrush")
            });
        }
    }

    private TextBlock CreateSectionHeader(string title) =>
        new()
        {
            Text = title,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("TextSecondaryBrush"),
            Margin = new Thickness(0, 8, 0, 4),
        };

    private async Task<UIElement> BuildPermissionsBadgesAsync(string username)
    {
        if (_firebaseService == null)
            return new TextBlock { Text = "No special roles", FontSize = 10, Foreground = FindBrush("TextTertiaryBrush") };

        var normalized = NormalizeUsername(username);
        var badges = new WrapPanel();

        // Same rules as Permissions tab: read each role *collection* and look up the username.
        // GetNodeAsync("admin_users/someone") fails for boolean leaves — RTDB stores true/false, not objects.
        try
        {
            var adminsTask = _firebaseService.GetNodeAsync("admin_users");
            var editorsTask = _firebaseService.GetNodeAsync("cheat_sheet_editors");
            var devsTask = _firebaseService.GetNodeAsync("dev_users");
            await Task.WhenAll(adminsTask, editorsTask, devsTask);

            if (UserHasRole(await adminsTask, normalized))
                badges.Children.Add(CreatePermissionRoleBadge("ADMIN", "OrangeBackgroundBrush", "OrangeBrush"));
            if (UserHasRole(await editorsTask, normalized))
                badges.Children.Add(CreatePermissionRoleBadge("EDITOR", "GreenBackgroundBrush", "GreenBrush"));
            if (UserHasRole(await devsTask, normalized))
                badges.Children.Add(CreatePermissionRoleBadge("DEV", "BlueBackgroundBrush", "BlueBrush"));
        }
        catch
        {
            return new TextBlock { Text = "Could not load roles", FontSize = 10, Foreground = FindBrush("TextTertiaryBrush") };
        }

        if (badges.Children.Count == 0)
            return new TextBlock { Text = "No special roles", FontSize = 10, Foreground = FindBrush("TextTertiaryBrush") };
        return badges;
    }

    private static readonly string[] AdditiveDailyMetricKeys =
    {
        "session_count",
        "total_session_duration_ms",
        "total_searches",
        "total_smart_searches",
        "total_doc_searches",
        "total_path_searches",
        "total_project_launches",
        "total_quick_launch_uses",
        "total_quick_launch_adds",
        "total_quick_launch_removes",
        "total_tasks_created",
        "total_tasks_completed",
        "total_doc_opens",
        "total_timer_uses",
        "total_cheat_sheet_views",
        "total_cheat_sheet_lookups",
        "total_cheat_sheet_copies",
        "total_cheat_sheet_searches",
        "total_hotkey_presses",
        "total_filter_changes",
        "total_clipboard_copies",
        "total_errors",
        "total_tags_created",
        "total_tags_updated",
        "total_tag_searches",
        "total_tag_carousel_clicks",
        "total_tasks_deleted",
        "total_setting_changes",
        "total_smart_search_filter_uses",
        "total_search_result_clicks",
    };

    private static readonly string[] AverageDailyMetricKeys =
    {
        "avg_startup_timing_ms",
        "avg_search_timing_ms",
        "avg_search_result_click_position",
    };

    /// <summary>
    /// Loads the latest daily snapshot per device and merges additive counters (and averages widget usage).
    /// Firebase often returns nested maps as <see cref="JObject"/>, not <see cref="Dictionary{TKey,TValue}"/>.
    /// </summary>
    private async Task<Dictionary<string, object>> LoadLatestMetricsForUserDevicesAsync(List<UserDeviceDetail> devices)
    {
        if (_firebaseService == null)
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var merged = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var widgetTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var avgBuckets = AverageDailyMetricKeys.ToDictionary(k => k, _ => new List<double>());

        foreach (var device in devices)
        {
            var root = await _firebaseService.GetNodeAsync($"metrics/{device.DeviceId}");
            if (root == null || root.Count == 0)
                continue;

            var latestDate = root.Keys.OrderByDescending(k => k).FirstOrDefault();
            if (latestDate == null)
                continue;
            if (!root.TryGetValue(latestDate, out var latestObj))
                continue;

            var day = NormalizeFirebaseMap(latestObj);
            if (day == null || day.Count == 0)
                continue;

            foreach (var key in AdditiveDailyMetricKeys)
            {
                if (!day.TryGetValue(key, out var val) || !TryToLong(val, out var n))
                    continue;
                merged[key] = (merged.TryGetValue(key, out var ex) && TryToLong(ex, out var exL) ? exL : 0) + n;
            }

            foreach (var key in AverageDailyMetricKeys)
            {
                if (!day.TryGetValue(key, out var val) || !TryToDouble(val, out var d))
                    continue;
                avgBuckets[key].Add(d);
            }

            foreach (var w in ParseWidgetUsageObject(day.GetValueOrDefault("widget_usage")))
                widgetTotals[w.Key] = widgetTotals.GetValueOrDefault(w.Key) + w.Value;

            if (day.TryGetValue("synced_at", out var sa) && sa != null)
                merged["synced_at"] = sa.ToString() ?? "";
            if (day.TryGetValue("date", out var dt) && dt != null)
                merged["date"] = dt.ToString() ?? "";
            if (day.TryGetValue("user_name", out var un) && un != null && !merged.ContainsKey("user_name"))
                merged["user_name"] = un.ToString() ?? "";
            if (day.TryGetValue("device_name", out var dn) && dn != null && !merged.ContainsKey("device_name"))
                merged["device_name"] = dn.ToString() ?? "";
        }

        if (widgetTotals.Count > 0)
        {
            foreach (var kvp in widgetTotals)
                merged[$"widget_usage::{kvp.Key}"] = (long)kvp.Value;
        }

        foreach (var kvp in avgBuckets)
        {
            if (kvp.Value.Count > 0)
                merged[kvp.Key] = kvp.Value.Average();
        }

        return merged;
    }

    private static Dictionary<string, object>? NormalizeFirebaseMap(object? o)
    {
        if (o == null)
            return null;
        if (o is Dictionary<string, object> d)
            return d;
        try
        {
            var json = JsonConvert.SerializeObject(o);
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, int> ParseWidgetUsageObject(object? usageObj)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (usageObj == null)
            return result;

        if (usageObj is Dictionary<string, object> od)
        {
            foreach (var kvp in od)
                result[kvp.Key] = CoerceToInt(kvp.Value);
            return result;
        }

        if (usageObj is JObject jobj)
        {
            foreach (var prop in jobj.Properties())
                result[prop.Name] = CoerceToInt(prop.Value);
            return result;
        }

        try
        {
            var json = usageObj as string ?? JsonConvert.SerializeObject(usageObj);
            var parsed = JsonConvert.DeserializeObject<Dictionary<string, long>>(json);
            if (parsed != null)
            {
                foreach (var kvp in parsed)
                    result[kvp.Key] = (int)Math.Min(int.MaxValue, kvp.Value);
            }
        }
        catch
        {
            // ignore
        }

        return result;
    }

    private static int CoerceToInt(object? v)
    {
        if (v == null)
            return 0;
        if (TryToLong(v, out var l))
            return (int)Math.Min(int.MaxValue, Math.Max(int.MinValue, l));
        return 0;
    }

    private static bool TryToLong(object? o, out long value)
    {
        value = 0;
        if (o == null)
            return false;
        switch (o)
        {
            case long l:
                value = l;
                return true;
            case int i:
                value = i;
                return true;
            case double d:
                value = (long)d;
                return true;
            case float f:
                value = (long)f;
                return true;
            case JValue jv when jv.Type == JTokenType.Integer:
                value = jv.Value<long>();
                return true;
            case JValue jv when jv.Type == JTokenType.Float:
                value = (long)jv.Value<double>();
                return true;
        }

        return long.TryParse(o.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryToDouble(object? o, out double value)
    {
        value = 0;
        if (o == null)
            return false;
        switch (o)
        {
            case double d:
                value = d;
                return true;
            case float f:
                value = f;
                return true;
            case long l:
                value = l;
                return true;
            case int i:
                value = i;
                return true;
            case JValue jv when jv.Type == JTokenType.Float || jv.Type == JTokenType.Integer:
                value = jv.Value<double>();
                return true;
        }

        return double.TryParse(o.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private UIElement BuildWidgetUsageSection(Dictionary<string, object> metrics)
    {
        var usage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var kvp in metrics)
        {
            if (kvp.Key.StartsWith("widget_usage::", StringComparison.Ordinal) && TryToLong(kvp.Value, out var c))
                usage[kvp.Key["widget_usage::".Length..]] = (int)Math.Min(int.MaxValue, c);
        }

        if (usage.Count == 0 && metrics.TryGetValue("widget_usage", out var embedded))
            foreach (var w in ParseWidgetUsageObject(embedded))
                usage[w.Key] = w.Value;

        var nonzero = usage.Where(k => k.Value > 0).OrderByDescending(k => k.Value).ToList();
        if (nonzero.Count == 0)
        {
            return new TextBlock
            {
                Text = "No widget usage in the latest synced day (counts are 0 or not synced yet).",
                FontSize = 10,
                Foreground = FindBrush("TextTertiaryBrush"),
                TextWrapping = TextWrapping.Wrap,
            };
        }

        var chips = new WrapPanel();
        foreach (var kvp in nonzero)
        {
            var id = kvp.Key;
            var label = WidgetIds.All.Contains(id) ? WidgetIds.DisplayName(id) : id;
            chips.Children.Add(new Border
            {
                Background = FindBrush("FaintOverlayBrush"),
                BorderBrush = FindBrush("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(8, 3, 8, 3),
                Child = new TextBlock
                {
                    Text = $"{label}: {kvp.Value.ToString("N0", CultureInfo.InvariantCulture)}",
                    FontSize = 9,
                    Foreground = FindBrush("TextPrimaryBrush")
                }
            });
        }

        return chips;
    }

    private UIElement BuildKeyMetricsSection(Dictionary<string, object> metrics)
    {
        var hasNumeric =
            AdditiveDailyMetricKeys.Any(metrics.ContainsKey)
            || AverageDailyMetricKeys.Any(metrics.ContainsKey)
            || metrics.Keys.Any(k => k.StartsWith("widget_usage::", StringComparison.Ordinal));

        if (!hasNumeric && !metrics.ContainsKey("date") && !metrics.ContainsKey("synced_at"))
        {
            return new TextBlock
            {
                Text = "No metrics synced for this user's devices yet.",
                FontSize = 10,
                Foreground = FindBrush("TextTertiaryBrush"),
                TextWrapping = TextWrapping.Wrap,
            };
        }

        var stack = new StackPanel();

        void AddCount(string label, string key)
        {
            if (!metrics.TryGetValue(key, out var raw) || !TryToLong(raw, out var n))
                return;
            AddDetailRow(stack, label, n.ToString("N0", CultureInfo.InvariantCulture));
        }

        void AddDurationMs(string label, string key)
        {
            if (!metrics.TryGetValue(key, out var raw) || !TryToLong(raw, out var ms) || ms <= 0)
                return;
            AddDetailRow(stack, label, FormatDurationMs(ms));
        }

        void AddAvgMs(string label, string key)
        {
            if (!metrics.TryGetValue(key, out var raw) || !TryToDouble(raw, out var ms) || ms <= 0)
                return;
            AddDetailRow(stack, label, $"{ms.ToString("F0", CultureInfo.InvariantCulture)} ms");
        }

        AddCount("Sessions", "session_count");
        AddDurationMs("Time in app (that day)", "total_session_duration_ms");
        AddCount("Searches", "total_searches");
        AddCount("Smart searches", "total_smart_searches");
        AddCount("Doc searches", "total_doc_searches");
        AddCount("Path searches", "total_path_searches");
        AddCount("Project launches", "total_project_launches");
        AddCount("Quick launch uses", "total_quick_launch_uses");
        AddCount("Cheat sheet views", "total_cheat_sheet_views");
        AddCount("Doc opens", "total_doc_opens");
        AddCount("Search result clicks", "total_search_result_clicks");
        AddAvgMs("Avg search time", "avg_search_timing_ms");
        AddAvgMs("Avg startup time", "avg_startup_timing_ms");
        if (metrics.TryGetValue("avg_search_result_click_position", out var posRaw) &&
            TryToDouble(posRaw, out var pos) && pos >= 0)
            AddDetailRow(stack, "Avg click position", pos.ToString("F1", CultureInfo.InvariantCulture));
        AddCount("Errors", "total_errors");

        if (metrics.TryGetValue("date", out var day) && day != null)
            AddDetailRow(stack, "Metrics day", day.ToString() ?? "");
        if (metrics.TryGetValue("synced_at", out var sync) && sync != null)
            AddDetailRow(stack, "Last sync", sync.ToString() ?? "");

        if (stack.Children.Count == 0)
        {
            return new TextBlock
            {
                Text = "Latest snapshot has no counters yet. Use the app on those devices and sync telemetry.",
                FontSize = 10,
                Foreground = FindBrush("TextTertiaryBrush"),
                TextWrapping = TextWrapping.Wrap,
            };
        }

        return stack;
    }

    private static string FormatDurationMs(long ms)
    {
        if (ms < 1000)
            return $"{ms} ms";
        var ts = TimeSpan.FromMilliseconds(ms);
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    private async void RefreshUsersDevices_Click(object sender, RoutedEventArgs e) => await RefreshUsersDevicesAsync();
}
