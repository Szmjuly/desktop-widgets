using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json;
using WpfFontFamily = System.Windows.Media.FontFamily;

namespace DesktopHub.UI.Widgets;

public partial class DeveloperPanelWidget
{
    private const string UpdatesAppId = "desktophub";

    // ════════════════════════════════════════════════════════════
    // UPDATES TAB
    // ════════════════════════════════════════════════════════════

    private void InitUpdatesTab()
    {
        UpdateVersionBox.Text = DateTime.Now.ToString("yyyy.M.d", CultureInfo.InvariantCulture);
        UpdateNotesBox.Text = "";
        PopulateUpdateDropdowns();
    }

    private void PopulateUpdateDropdowns()
    {
        UpdateAppBox.ItemsSource = new[] { "desktophub" };
        if (UpdateAppBox.SelectedIndex < 0)
            UpdateAppBox.SelectedIndex = 0;

        var users = _allDeviceDetails.Select(d => d.Username).Distinct().OrderBy(x => x).ToList();
        users.Insert(0, "(all users)");

        PushUserFilterBox.ItemsSource = users;

        if (PushUserFilterBox.SelectedIndex < 0)
            PushUserFilterBox.SelectedIndex = 0;

        RebuildPushDeviceDropdown();
    }

    private void RebuildPushDeviceDropdown()
    {
        var selectedUser = PushUserFilterBox.SelectedItem?.ToString() ?? "(all users)";
        var deviceIds = _allDeviceDetails
            .Where(d => selectedUser == "(all users)" || d.Username.Equals(selectedUser, StringComparison.OrdinalIgnoreCase))
            .Select(d => d.DeviceId)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        PushDeviceIdBox.ItemsSource = deviceIds;
        if (deviceIds.Count > 0)
            PushDeviceIdBox.SelectedIndex = 0;
    }

    private void DismissUpdateResults_Click(object sender, RoutedEventArgs e)
    {
        UpdateStructuredResultsPanel.Children.Clear();
        UpdateStructuredResultsBorder.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Shows the structured panel above OUTPUT. Caller fills <see cref="UpdateStructuredResultsPanel"/>.
    /// </summary>
    private void PresentStructuredResults(string title, Action populateBody)
    {
        UpdateStructuredResultsTitle.Text = title;
        UpdateStructuredResultsPanel.Children.Clear();
        populateBody();
        UpdateStructuredResultsBorder.Visibility = Visibility.Visible;
    }

    // ════════════════════════════════════════════════════════════
    // VERSION MANAGEMENT
    // ════════════════════════════════════════════════════════════

    private async void PublishVersion_Click(object sender, RoutedEventArgs e)
    {
        var appId = UpdateAppBox.SelectedItem?.ToString() ?? "desktophub";
        var version = UpdateVersionBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(version))
        {
            AppendOutput("Version is required.");
            return;
        }

        var notes = UpdateNotesBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(notes))
            notes = "New version available";

        // Replace spaces with underscores for PowerShell arg safety
        var safeNotes = notes.Replace(" ", "_");

        if (!ConfirmDangerous($"Publish {appId} version {version}?")) return;

        await RunScriptAsync("admin.ps1", "-Action", "version-update", "-Version", version, "-ReleaseNotes", safeNotes);
    }

    // ════════════════════════════════════════════════════════════
    // PUSH UPDATES
    // ════════════════════════════════════════════════════════════

    private async void ListDevices_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDeviceInventoryUiAsync();
        await RunScriptWithOutputAsync("admin.ps1", false, "-Action", "update-list");
    }

    private async void PushUpdateAll_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDangerous("Push update to all outdated devices?")) return;
        await RunScriptAsync("admin.ps1", "-Action", "update-push-all");
    }

    private async void PushUpdateDevice_Click(object sender, RoutedEventArgs e)
    {
        var deviceId = PushDeviceIdBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            AppendOutput("Device ID is required.");
            return;
        }

        if (!ConfirmDangerous($"Push update to device '{deviceId}'?")) return;
        await RunScriptAsync("admin.ps1", "-Action", "update-push", "-DeviceId", deviceId);
    }

    private void PushUserFilterBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PushDeviceIdBox == null)
            return;
        RebuildPushDeviceDropdown();
    }

    private async void CheckUpdateStatus_Click(object sender, RoutedEventArgs e)
    {
        await RefreshForceUpdateStatusUiAsync();
        await RunScriptWithOutputAsync("admin.ps1", false, "-Action", "update-status");
    }

    private async void ClearUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDangerous("Clear completed/failed push update entries?")) return;
        await RunScriptAsync("admin.ps1", "-Action", "update-clear");
    }

    // ════════════════════════════════════════════════════════════
    // STRUCTURED RESULTS (Firebase — same source as push-update.ps1)
    // ════════════════════════════════════════════════════════════

    private async Task RefreshForceUpdateStatusUiAsync()
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase not connected; cannot build force-update table.");
            return;
        }

        try
        {
            var forceTask = _firebaseService.GetNodeAsync("force_update");
            var devicesTask = _firebaseService.GetDevicesAsync();
            await Task.WhenAll(forceTask, devicesTask);
            var force = await forceTask;
            var devices = await devicesTask;

            PresentStructuredResults("Force-update queue", () =>
            {
                if (force == null || force.Count == 0)
                {
                    UpdateStructuredResultsPanel.Children.Add(new TextBlock
                    {
                        Text = "No force-update entries in Firebase.",
                        FontSize = 10,
                        Foreground = FindBrush("TextTertiaryBrush"),
                        TextWrapping = TextWrapping.Wrap,
                    });
                    return;
                }

                var rows = new List<(string DeviceId, Dictionary<string, object> Map)>();
                foreach (var kvp in force)
                {
                    if (!TryGetForceUpdateMap(kvp.Value, out var map) || map == null)
                        continue;
                    var appId = GetMapStr(map, "app_id");
                    if (!string.IsNullOrEmpty(appId) &&
                        !string.Equals(appId, UpdatesAppId, StringComparison.OrdinalIgnoreCase))
                        continue;
                    rows.Add((kvp.Key, map));
                }

                if (rows.Count == 0)
                {
                    UpdateStructuredResultsPanel.Children.Add(new TextBlock
                    {
                        Text = "No desktophub force-update entries (other app_id values are hidden).",
                        FontSize = 10,
                        Foreground = FindBrush("TextTertiaryBrush"),
                        TextWrapping = TextWrapping.Wrap,
                    });
                    return;
                }

                rows = rows
                    .OrderBy(r => ForceStatusSortKey(GetMapStr(r.Map, "status")))
                    .ThenBy(r => ResolveDeviceUsername(devices, r.DeviceId))
                    .ToList();

                UpdateStructuredResultsPanel.Children.Add(BuildForceUpdateHeaderRow());
                for (var i = 0; i < rows.Count; i++)
                    UpdateStructuredResultsPanel.Children.Add(BuildForceUpdateDataRow(rows[i].DeviceId, rows[i].Map, devices, i));

                var pending = rows.Count(r => string.Equals(GetMapStr(r.Map, "status"), "pending", StringComparison.OrdinalIgnoreCase));
                UpdateStructuredResultsPanel.Children.Add(new TextBlock
                {
                    Text = $"{rows.Count} entr(y/ies) · {pending} pending",
                    FontSize = 9,
                    Foreground = FindBrush("TextTertiaryBrush"),
                    Margin = new Thickness(0, 8, 0, 0),
                });
            });
        }
        catch (Exception ex)
        {
            AppendOutput($"Force-update table: {ex.Message}");
        }
    }

    private async Task RefreshDeviceInventoryUiAsync()
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
        {
            AppendOutput("Firebase not connected; cannot build device list.");
            return;
        }

        try
        {
            var devicesTask = _firebaseService.GetDevicesAsync();
            var verTask = _firebaseService.GetNodeAsync($"app_versions/{UpdatesAppId}");
            await Task.WhenAll(devicesTask, verTask);
            var devices = await devicesTask;
            var verNode = await verTask;
            var latest = verNode?.TryGetValue("latest_version", out var lv) == true ? lv?.ToString()?.Trim() : null;
            if (string.IsNullOrWhiteSpace(latest))
                latest = "—";

            PresentStructuredResults("Devices & versions", () =>
            {
                UpdateStructuredResultsPanel.Children.Add(new TextBlock
                {
                    Text = $"Published latest: {latest}",
                    FontSize = 10,
                    Foreground = FindBrush("TextSecondaryBrush"),
                    Margin = new Thickness(0, 0, 0, 8),
                });

                if (devices == null || devices.Count == 0)
                {
                    UpdateStructuredResultsPanel.Children.Add(new TextBlock
                    {
                        Text = "No devices in Firebase.",
                        FontSize = 10,
                        Foreground = FindBrush("TextTertiaryBrush"),
                    });
                    return;
                }

                var rows = devices
                    .Select(kvp => BuildDeviceInvRow(kvp.Key, kvp.Value, latest))
                    .OrderByDescending(r => r.Outdated)
                    .ThenBy(r => r.Username, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(r => r.DeviceName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                UpdateStructuredResultsPanel.Children.Add(BuildDeviceInventoryHeaderRow());
                for (var i = 0; i < rows.Count; i++)
                    UpdateStructuredResultsPanel.Children.Add(BuildDeviceInventoryDataRow(rows[i], i));

                var outdated = rows.Count(r => r.Outdated);
                UpdateStructuredResultsPanel.Children.Add(new TextBlock
                {
                    Text = $"{rows.Count} devices · {outdated} outdated vs {latest}",
                    FontSize = 9,
                    Foreground = FindBrush("TextTertiaryBrush"),
                    Margin = new Thickness(0, 8, 0, 0),
                });
            });
        }
        catch (Exception ex)
        {
            AppendOutput($"Device list table: {ex.Message}");
        }
    }

    private sealed record DeviceInvRow(
        string DeviceIdFull,
        string DeviceIdShort,
        string Username,
        string DeviceName,
        string InstalledDisplay,
        bool Outdated,
        string LastSeen);

    private DeviceInvRow BuildDeviceInvRow(string deviceId, Dictionary<string, object> d, string latestPublished)
    {
        var user = d.TryGetValue("username", out var u) ? u?.ToString() ?? "?" : "?";
        var name = d.TryGetValue("device_name", out var dn) ? dn?.ToString() ?? "" : "";
        var lastSeen = d.TryGetValue("last_seen", out var ls) ? FormatDeviceLastSeen(ls?.ToString()) : "—";
        var installed = GetInstalledVersionForApp(d);
        var outdated = IsInstalledOutdated(installed, latestPublished);
        var display = string.IsNullOrWhiteSpace(installed) ? "?" : installed;
        if (outdated && latestPublished != "—")
            display += "  (behind)";

        return new DeviceInvRow(
            deviceId,
            ShortenDeviceId(deviceId),
            user,
            name,
            display,
            outdated,
            lastSeen);
    }

    private static string ShortenDeviceId(string id) =>
        id.Length <= 14 ? id : id[..12] + "…";

    private static string FormatDeviceLastSeen(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return "—";
        if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            return iso;
        var span = DateTime.UtcNow - dt.ToUniversalTime();
        if (span.TotalMinutes < 1) return "now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return dt.ToString("MM/dd HH:mm", CultureInfo.InvariantCulture);
    }

    private static string? GetInstalledVersionForApp(Dictionary<string, object> device)
    {
        if (!device.TryGetValue("apps", out var appsObj) || appsObj == null)
            return null;
        var apps = NormalizeToStringObjectMap(appsObj);
        if (apps == null || !apps.TryGetValue(UpdatesAppId, out var appEntry))
            return null;
        var appMap = NormalizeToStringObjectMap(appEntry);
        if (appMap == null)
            return null;
        return appMap.TryGetValue("installed_version", out var v) ? v?.ToString() : null;
    }

    private static Dictionary<string, object>? NormalizeToStringObjectMap(object? o)
    {
        if (o == null)
            return null;
        if (o is Dictionary<string, object> d)
            return d;
        try
        {
            return JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(o));
        }
        catch
        {
            return null;
        }
    }

    private static bool IsInstalledOutdated(string? installed, string? latest)
    {
        latest = latest?.Trim();
        if (string.IsNullOrWhiteSpace(latest) || latest == "—")
            return false;
        installed = installed?.Trim();
        if (string.IsNullOrWhiteSpace(installed))
            return true;
        if (!TryParseLooseVersion(installed, out var vi))
            return true;
        if (!TryParseLooseVersion(latest, out var vl))
            return false;
        return vi < vl;
    }

    private static bool TryParseLooseVersion(string s, out Version v)
    {
        v = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(s))
            return false;
        var t = s.Trim().TrimStart('v', 'V');
        var sp = t.IndexOf(' ', StringComparison.Ordinal);
        if (sp > 0)
            t = t[..sp];
        if (!Version.TryParse(t, out var parsed))
            return false;
        v = parsed;
        return true;
    }

    private static bool TryGetForceUpdateMap(object? entry, out Dictionary<string, object>? map)
    {
        map = null;
        if (entry == null)
            return false;
        if (entry is Dictionary<string, object> d)
        {
            map = d;
            return true;
        }

        try
        {
            var json = JsonConvert.SerializeObject(entry);
            var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            map = parsed;
            return parsed != null;
        }
        catch
        {
            return false;
        }
    }

    private static string? GetMapStr(Dictionary<string, object> m, string key) =>
        m.TryGetValue(key, out var v) ? v?.ToString() : null;

    private static int ForceStatusSortKey(string? s) => s?.ToLowerInvariant() switch
    {
        "pending" => 0,
        "downloading" => 1,
        "installing" => 2,
        "failed" => 3,
        "completed" => 4,
        _ => 9,
    };

    private static string ResolveDeviceUsername(Dictionary<string, Dictionary<string, object>>? devices, string deviceId)
    {
        if (devices == null || !devices.TryGetValue(deviceId, out var d))
            return "?";
        return d.TryGetValue("username", out var u) ? u?.ToString() ?? "?" : "?";
    }

    private UIElement BuildForceUpdateHeaderRow()
    {
        var grid = CreateUpdatesTableGrid(forceTable: true);
        grid.Children.Add(UpdatesHeaderCell("Device", 0));
        grid.Children.Add(UpdatesHeaderCell("User", 1));
        grid.Children.Add(UpdatesHeaderCell("Status", 2));
        grid.Children.Add(UpdatesHeaderCell("Target", 3));
        grid.Children.Add(UpdatesHeaderCell("Pushed", 4));
        return WrapUpdatesTableRow(grid, isHeader: true, rowIndex: 0);
    }

    private UIElement BuildForceUpdateDataRow(string deviceId, Dictionary<string, object> map,
        Dictionary<string, Dictionary<string, object>>? devices, int rowIndex)
    {
        var grid = CreateUpdatesTableGrid(forceTable: true);
        var user = ResolveDeviceUsername(devices, deviceId);
        var status = GetMapStr(map, "status") ?? "?";
        var target = GetMapStr(map, "target_version") ?? "—";
        var pushed = GetMapStr(map, "pushed_at") ?? "—";
        if (pushed.Length > 16)
            pushed = pushed[..16];

        var idCell = UpdatesDataCell(ShortenDeviceId(deviceId), 0, monospace: true);
        if (idCell is TextBlock idTb)
            idTb.ToolTip = deviceId;
        grid.Children.Add(idCell);
        grid.Children.Add(UpdatesDataCell(user, 1));
        grid.Children.Add(UpdatesStatusBadge(status, 2));
        grid.Children.Add(UpdatesDataCell(target, 3));
        grid.Children.Add(UpdatesDataCell(pushed, 4));

        var row = WrapUpdatesTableRow(grid, isHeader: false, rowIndex);
        var err = GetMapStr(map, "error");
        if (!string.IsNullOrWhiteSpace(err))
        {
            var stack = new StackPanel();
            stack.Children.Add(row);
            stack.Children.Add(new TextBlock
            {
                Text = err,
                FontSize = 9,
                Foreground = FindBrush("RedBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8, 2, 8, 4),
            });
            return stack;
        }

        return row;
    }

    private UIElement BuildDeviceInventoryHeaderRow()
    {
        var grid = CreateUpdatesTableGrid(forceTable: false);
        grid.Children.Add(UpdatesHeaderCell("Device", 0));
        grid.Children.Add(UpdatesHeaderCell("User", 1));
        grid.Children.Add(UpdatesHeaderCell("Host", 2));
        grid.Children.Add(UpdatesHeaderCell("Installed", 3));
        grid.Children.Add(UpdatesHeaderCell("Seen", 4));
        return WrapUpdatesTableRow(grid, isHeader: true, rowIndex: 0);
    }

    private UIElement BuildDeviceInventoryDataRow(DeviceInvRow r, int rowIndex)
    {
        var grid = CreateUpdatesTableGrid(forceTable: false);
        var idCell = UpdatesDataCell(r.DeviceIdShort, 0, monospace: true);
        if (idCell is TextBlock idTb)
            idTb.ToolTip = r.DeviceIdFull;
        grid.Children.Add(idCell);
        grid.Children.Add(UpdatesDataCell(r.Username, 1));
        grid.Children.Add(UpdatesDataCell(string.IsNullOrWhiteSpace(r.DeviceName) ? "—" : r.DeviceName, 2));
        grid.Children.Add(UpdatesDataCell(r.InstalledDisplay, 3, warn: r.Outdated));
        grid.Children.Add(UpdatesDataCell(r.LastSeen, 4));
        return WrapUpdatesTableRow(grid, isHeader: false, rowIndex);
    }

    private Grid CreateUpdatesTableGrid(bool forceTable)
    {
        var grid = new Grid { MinHeight = 26 };
        if (forceTable)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.75, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.65, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
        }
        else
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.75, GridUnitType.Star) });
        }

        return grid;
    }

    private UIElement UpdatesHeaderCell(string text, int col)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 10,
            FontWeight = FontWeights.SemiBold,
            Foreground = FindBrush("TextSecondaryBrush"),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(block, col);
        return block;
    }

    private UIElement UpdatesDataCell(string text, int col, bool monospace = false, bool warn = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = warn ? FindBrush("OrangeBrush") : FindBrush("TextPrimaryBrush"),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            FontFamily = monospace ? new WpfFontFamily("Consolas") : new WpfFontFamily("Segoe UI"),
        };
        Grid.SetColumn(block, col);
        return block;
    }

    private UIElement UpdatesStatusBadge(string status, int col)
    {
        var s = status.ToLowerInvariant();
        var (bg, fg) = s switch
        {
            "pending" => ("OrangeBackgroundBrush", "OrangeBrush"),
            "downloading" or "installing" => ("BlueBackgroundBrush", "BlueBrush"),
            "completed" => ("GreenBackgroundBrush", "GreenBrush"),
            "failed" => ("RedBackgroundBrush", "RedBrush"),
            _ => ("FaintOverlayBrush", "TextSecondaryBrush"),
        };

        var border = new Border
        {
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Background = FindBrush(bg),
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = status,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = FindBrush(fg),
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
            },
        };
        Grid.SetColumn(border, col);
        return border;
    }

    private UIElement WrapUpdatesTableRow(Grid grid, bool isHeader, int rowIndex)
    {
        return new Border
        {
            Background = isHeader
                ? FindBrush("SurfaceBrush")
                : (rowIndex % 2 == 0 ? System.Windows.Media.Brushes.Transparent : FindBrush("FaintOverlayBrush")),
            BorderBrush = FindBrush("BorderBrush"),
            BorderThickness = new Thickness(1, isHeader ? 1 : 0, 1, 1),
            Padding = new Thickness(8, isHeader ? 6 : 4, 8, isHeader ? 6 : 4),
            Child = grid,
        };
    }

    // ════════════════════════════════════════════════════════════
    // BUILD TOOLS
    // ════════════════════════════════════════════════════════════

    private async void Build_Click(object sender, RoutedEventArgs e)
    {
        await RunScriptAsync("admin.ps1", "-Action", "build");
    }

    private async void BuildInstaller_Click(object sender, RoutedEventArgs e)
    {
        await RunScriptAsync("admin.ps1", "-Action", "build-installer");
    }
}
