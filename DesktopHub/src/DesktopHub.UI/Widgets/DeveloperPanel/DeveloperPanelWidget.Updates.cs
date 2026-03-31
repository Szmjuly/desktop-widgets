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

    private bool _suppressUpdatesAppBoxEvent;

    // ════════════════════════════════════════════════════════════
    // UPDATES TAB
    // ════════════════════════════════════════════════════════════

    private bool _publishTabPrimed;

    private void InitPublishTab()
    {
        if (_publishTabPrimed)
            return;
        _publishTabPrimed = true;
        UpdateVersionBox.Text = DateTime.Now.ToString("yyyy.M.d", CultureInfo.InvariantCulture);
        UpdateNotesBox.Text = "";
        UpdateAppBox.ItemsSource = new[] { UpdatesAppId };
        UpdateAppBox.SelectedIndex = 0;
    }

    private async void InitUpdatesTab()
    {
        if (_allDeviceDetails.Count == 0)
            await LoadDeviceDetailsAsync();
        PopulatePushUpdateDropdowns();
        SyncPushTargetVersionCustomVisibility();
        _ = InitializeUpdatesAppPickerAndLatestAsync();
    }

    private async Task InitializeUpdatesAppPickerAndLatestAsync()
    {
        await RefreshUpdatesAppPickerOnInitAsync();
        await LoadPublishedLatestForPushLabelAsync();
    }

    private string GetSelectedUpdatesAppId()
    {
        var s = UpdatesPushAppBox?.SelectedItem?.ToString()?.Trim();
        return string.IsNullOrWhiteSpace(s) ? UpdatesAppId : s;
    }

    private void UpdatesPushAppBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressUpdatesAppBoxEvent || UpdatesPushAppBox?.SelectedItem == null)
            return;
        _ = OnUpdatesPushAppSelectionChangedAsync();
    }

    private async Task OnUpdatesPushAppSelectionChangedAsync()
    {
        await LoadPublishedLatestForPushLabelAsync();
        if (string.Equals(UpdateStructuredResultsTitle?.Text, "Devices & versions", StringComparison.Ordinal))
            await RefreshDeviceInventoryUiAsync();
    }

    private static List<string> OrderAppIds(IEnumerable<string> ids)
    {
        var list = ids.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        static int Rank(string x)
        {
            if (string.Equals(x, "desktophub", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(x, "hapextractor", StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }

        return list.OrderBy(Rank).ThenBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void MergeAppKeysFromDevice(Dictionary<string, object> device, HashSet<string> set)
    {
        if (!device.TryGetValue("apps", out var appsObj) || appsObj == null)
            return;
        var apps = NormalizeToStringObjectMap(appsObj);
        if (apps == null)
            return;
        foreach (var k in apps.Keys)
        {
            if (!string.IsNullOrWhiteSpace(k))
                set.Add(k);
        }
    }

    private static List<string> BuildAppIdListFromRootAndDevices(Dictionary<string, object>? versionsRoot,
        Dictionary<string, Dictionary<string, object>>? devices)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "desktophub", "hapextractor" };
        if (versionsRoot != null)
        {
            foreach (var k in versionsRoot.Keys)
            {
                if (!string.IsNullOrWhiteSpace(k))
                    set.Add(k);
            }
        }

        if (devices != null)
        {
            foreach (var d in devices.Values)
                MergeAppKeysFromDevice(d, set);
        }

        return OrderAppIds(set);
    }

    private void ApplyAppPickerItemsPreservingSelection(List<string> ordered)
    {
        if (UpdatesPushAppBox == null || ordered.Count == 0)
            return;
        var sel = UpdatesPushAppBox.SelectedItem?.ToString()?.Trim();
        _suppressUpdatesAppBoxEvent = true;
        try
        {
            UpdatesPushAppBox.ItemsSource = ordered;
            var pick = (!string.IsNullOrEmpty(sel) &&
                        ordered.Exists(x => string.Equals(x, sel, StringComparison.OrdinalIgnoreCase))
                ? ordered.First(x => string.Equals(x, sel, StringComparison.OrdinalIgnoreCase))
                : null)
                       ?? ordered.FirstOrDefault(x => string.Equals(x, UpdatesAppId, StringComparison.OrdinalIgnoreCase))
                       ?? ordered[0];
            UpdatesPushAppBox.SelectedItem = pick;
        }
        finally
        {
            _suppressUpdatesAppBoxEvent = false;
        }
    }

    private async Task RefreshUpdatesAppPickerOnInitAsync()
    {
        if (UpdatesPushAppBox == null || _firebaseService == null || !_firebaseService.IsInitialized)
            return;
        try
        {
            var root = await _firebaseService.GetNodeAsync("app_versions");
            var merged = BuildAppIdListFromRootAndDevices(root, devices: null);
            await Dispatcher.InvokeAsync(() => ApplyAppPickerItemsPreservingSelection(merged));
        }
        catch
        {
            await Dispatcher.InvokeAsync(() =>
                ApplyAppPickerItemsPreservingSelection(OrderAppIds(new[] { UpdatesAppId, "hapextractor" })));
        }
    }

    private void SyncPushTargetVersionCustomVisibility()
    {
        if (PushTargetVersionCustomBox == null || PushTargetVersionModeBox == null)
            return;
        PushTargetVersionCustomBox.Visibility = PushTargetVersionModeBox.SelectedIndex == 1
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async Task LoadPublishedLatestForPushLabelAsync()
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized)
            return;
        try
        {
            var appId = Dispatcher.Invoke(GetSelectedUpdatesAppId);
            var ver = await _firebaseService.GetNodeAsync($"app_versions/{appId}");
            var latest = ver?.TryGetValue("latest_version", out var lv) == true ? lv?.ToString()?.Trim() : null;
            Dispatcher.Invoke(() => UpdatePushLatestVersionLabel(latest));
        }
        catch
        {
            /* ignore */
        }
    }

    private void UpdatePushLatestVersionLabel(string? latestPublished)
    {
        var box = PushTargetVersionModeBox;
        if (box == null || box.Items.Count < 1 || box.Items[0] is not ComboBoxItem first)
            return;
        first.Content = string.IsNullOrWhiteSpace(latestPublished) || latestPublished == "—"
            ? "Latest published (Firebase)"
            : $"Latest published ({latestPublished})";
    }

    private void PushTargetVersionMode_Changed(object sender, SelectionChangedEventArgs e) =>
        SyncPushTargetVersionCustomVisibility();

    private void PopulatePushUpdateDropdowns()
    {
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
    /// Shows the structured panel in the Updates tab. Caller fills <see cref="UpdateStructuredResultsPanel"/>.
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

        if (!await ConfirmDangerousAsync($"Publish {appId} version {version}?")) return;

        await PublishVersionAsync(appId, version, notes);
    }

    // ════════════════════════════════════════════════════════════
    // PUSH UPDATES
    // ════════════════════════════════════════════════════════════

    private async void ListDevices_Click(object sender, RoutedEventArgs e)
    {
        await RefreshDeviceInventoryUiAsync();
        var app = GetSelectedUpdatesAppId();
        await ListDevicesAndVersionsAsync(app);
    }

    private async void PushUpdateAll_Click(object sender, RoutedEventArgs e)
    {
        var app = GetSelectedUpdatesAppId();
        if (!await ConfirmDangerousAsync($"Push update to all outdated devices for app '{app}'?")) return;
        await PushUpdateToAllAsync(app);
    }

    private async void PushUpdateDevice_Click(object sender, RoutedEventArgs e) => await RunSingleDevicePushAsync();

    private async void PushDeviceFromTable_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn || btn.Tag is not string tag || string.IsNullOrWhiteSpace(tag))
            return;

        string deviceId;
        string? username = null;
        var sep = tag.IndexOf('\x1F', StringComparison.Ordinal);
        if (sep >= 0)
        {
            username = tag[..sep];
            deviceId = tag[(sep + 1)..];
        }
        else
        {
            deviceId = tag;
        }

        if (!string.IsNullOrEmpty(username))
        {
            foreach (var o in PushUserFilterBox.Items)
            {
                if (string.Equals(o?.ToString(), username, StringComparison.OrdinalIgnoreCase))
                {
                    PushUserFilterBox.SelectedItem = o;
                    break;
                }
            }

            RebuildPushDeviceDropdown();
        }

        PushDeviceIdBox.Text = deviceId;
        foreach (var o in PushDeviceIdBox.Items)
        {
            if (string.Equals(o?.ToString(), deviceId, StringComparison.OrdinalIgnoreCase))
            {
                PushDeviceIdBox.SelectedItem = o;
                break;
            }
        }

        if (PushDeviceIdBox.SelectedItem == null)
            PushDeviceIdBox.Text = deviceId;

        await RunSingleDevicePushAsync();
    }

    private async Task RunSingleDevicePushAsync()
    {
        var deviceId = PushDeviceIdBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            AppendOutput("Device ID is required.");
            return;
        }

        string? targetVersion = null;
        if (PushTargetVersionModeBox?.SelectedIndex == 1)
        {
            var v = PushTargetVersionCustomBox?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(v))
            {
                AppendOutput("Enter a version (e.g. 1.8.1) or switch Push target to Latest published.");
                return;
            }

            if (!await ConfirmDangerousAsync($"Push version {v} to device '{deviceId}' (app '{GetSelectedUpdatesAppId()}')? (download URL stays Firebase latest for that app)"))
                return;
            targetVersion = v;
        }
        else
        {
            if (!await ConfirmDangerousAsync($"Push latest published update to device '{deviceId}' (app '{GetSelectedUpdatesAppId()}')?"))
                return;
        }

        var force = PushForceReinstallBox?.IsChecked == true;
        await PushUpdateToDeviceAsync(deviceId, GetSelectedUpdatesAppId(), targetVersion, force);
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
        var app = GetSelectedUpdatesAppId();
        await GetUpdateStatusAsync(app);
    }

    private async void ClearUpdates_Click(object sender, RoutedEventArgs e)
    {
        var app = GetSelectedUpdatesAppId();
        if (!await ConfirmDangerousAsync($"Clear completed/failed push update entries for app '{app}'?")) return;
        await ClearCompletedUpdatesAsync(app);
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
            var appFilter = Dispatcher.Invoke(GetSelectedUpdatesAppId);

            await Dispatcher.InvokeAsync(() =>
            {
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
                        var rowApp = GetMapStr(map, "app_id");
                        if (!string.IsNullOrEmpty(rowApp) &&
                            !string.Equals(rowApp, appFilter, StringComparison.OrdinalIgnoreCase))
                            continue;
                        rows.Add((kvp.Key, map));
                    }

                    if (rows.Count == 0)
                    {
                        UpdateStructuredResultsPanel.Children.Add(new TextBlock
                        {
                            Text = $"No force-update entries for app '{appFilter}' (other app_id values are hidden).",
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
            var versionsRootTask = _firebaseService.GetNodeAsync("app_versions");
            await Task.WhenAll(devicesTask, versionsRootTask);
            var devices = await devicesTask;
            var versionsRoot = await versionsRootTask;

            var mergedAppIds = BuildAppIdListFromRootAndDevices(versionsRoot, devices);
            await Dispatcher.InvokeAsync(() => ApplyAppPickerItemsPreservingSelection(mergedAppIds));

            var appId = Dispatcher.Invoke(GetSelectedUpdatesAppId);
            var verNode = await _firebaseService.GetNodeAsync($"app_versions/{appId}");
            var latest = verNode?.TryGetValue("latest_version", out var lv) == true ? lv?.ToString()?.Trim() : null;
            if (string.IsNullOrWhiteSpace(latest))
                latest = "—";

            await Dispatcher.InvokeAsync(() => UpdatePushLatestVersionLabel(latest));

            await Dispatcher.InvokeAsync(() =>
            {
                PresentStructuredResults("Devices & versions", () =>
                {
                    UpdateStructuredResultsPanel.Children.Add(new TextBlock
                    {
                        Text = $"Published latest ({appId}): {latest}",
                        FontSize = 10,
                        Foreground = FindBrush("TextSecondaryBrush"),
                        Margin = new Thickness(0, 0, 0, 4),
                    });
                    UpdateStructuredResultsPanel.Children.Add(new TextBlock
                    {
                        Text = $"Only devices with Firebase apps/{appId}. Machines that only report other apps are hidden. (Legacy: no apps node → still listed for desktophub.)",
                        FontSize = 9,
                        Foreground = FindBrush("TextTertiaryBrush"),
                        TextWrapping = TextWrapping.Wrap,
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

                    var relevant = devices
                        .Where(kvp => DeviceRelevantForSelectedApp(kvp.Value, appId))
                        .ToList();

                    if (relevant.Count == 0)
                    {
                        UpdateStructuredResultsPanel.Children.Add(new TextBlock
                        {
                            Text = $"No devices have apps/{appId} ({devices.Count} in Firebase may only report other apps). Change App above or use User/Device to push.",
                            FontSize = 10,
                            Foreground = FindBrush("TextTertiaryBrush"),
                            TextWrapping = TextWrapping.Wrap,
                        });
                        return;
                    }

                    var rows = relevant
                        .Select(kvp => BuildDeviceInvRow(kvp.Key, kvp.Value, latest, appId))
                        .OrderByDescending(r => r.Outdated)
                        .ThenByDescending(r => r.MissingVersionReport)
                        .ThenBy(r => r.Username, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(r => r.DeviceName, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    UpdateStructuredResultsPanel.Children.Add(BuildDeviceInventoryHeaderRow());
                    for (var i = 0; i < rows.Count; i++)
                        UpdateStructuredResultsPanel.Children.Add(BuildDeviceInventoryDataRow(rows[i], i));

                    var outdated = rows.Count(r => r.Outdated);
                    var missing = rows.Count(r => r.MissingVersionReport);
                    UpdateStructuredResultsPanel.Children.Add(new TextBlock
                    {
                        Text = $"{rows.Count} devices · app {appId} · {outdated} behind {latest} · {missing} not reporting this app",
                        FontSize = 9,
                        Foreground = FindBrush("TextTertiaryBrush"),
                        Margin = new Thickness(0, 8, 0, 0),
                    });
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
        bool MissingVersionReport,
        string? InstalledCellToolTip,
        string LastSeen);

    private DeviceInvRow BuildDeviceInvRow(string deviceId, Dictionary<string, object> d, string latestPublished, string appId)
    {
        var user = d.TryGetValue("username", out var u) ? u?.ToString() ?? "?" : "?";
        var name = d.TryGetValue("device_name", out var dn) ? dn?.ToString() ?? "" : "";
        var lastSeen = d.TryGetValue("last_seen", out var ls) ? FormatDeviceLastSeen(ls?.ToString()) : "—";
        var installed = GetInstalledVersionForApp(d, appId);
        var missing = string.IsNullOrWhiteSpace(installed);
        var outdated = !missing && IsInstalledOutdated(installed, latestPublished);
        string display;
        string? tip = null;
        if (missing)
        {
            display = "not reported";
            tip = BuildMissingVersionToolTip(d, appId);
        }
        else
        {
            display = installed!;
            if (outdated && latestPublished != "—")
                display += "  (behind)";
        }

        return new DeviceInvRow(
            deviceId,
            ShortenDeviceId(deviceId),
            user,
            name,
            display,
            outdated,
            missing,
            tip,
            lastSeen);
    }

    private static string BuildMissingVersionToolTip(Dictionary<string, object> d, string appId)
    {
        var others = new List<string>();
        if (d.TryGetValue("apps", out var appsObj) && appsObj != null)
        {
            var apps = NormalizeToStringObjectMap(appsObj);
            if (apps != null)
            {
                foreach (var kvp in apps)
                {
                    if (string.Equals(kvp.Key, appId, StringComparison.OrdinalIgnoreCase))
                        continue;
                    var v = NormalizeToStringObjectMap(kvp.Value);
                    var iv = v?.TryGetValue("installed_version", out var ivs) == true ? ivs?.ToString()?.Trim() : null;
                    if (!string.IsNullOrWhiteSpace(iv))
                        others.Add($"{kvp.Key}: {iv}");
                    else
                        others.Add($"{kvp.Key}: (no version)");
                }
            }
        }

        if (others.Count > 0)
            return $"This device has not reported installed_version for '{appId}'. Other apps: {string.Join(", ", others.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}.";

        return $"No '{appId}' entry under devices/…/apps. The app may not be installed, the client may be too old to report per-app versions, or the device has not heartbeated yet.";
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

    /// <summary>
    /// Inventory is scoped per app: only devices that have heartbeated with this app id under <c>devices/.../apps/{appId}</c>.
    /// Exception: if <c>apps</c> is missing/empty, include the row only when viewing desktophub (legacy DesktopHub).
    /// </summary>
    private static bool DeviceRelevantForSelectedApp(Dictionary<string, object> device, string appId)
    {
        if (!device.TryGetValue("apps", out var appsObj) || appsObj == null)
            return string.Equals(appId, UpdatesAppId, StringComparison.OrdinalIgnoreCase);

        var apps = NormalizeToStringObjectMap(appsObj);
        if (apps == null || apps.Count == 0)
            return string.Equals(appId, UpdatesAppId, StringComparison.OrdinalIgnoreCase);

        return apps.Keys.Any(k => string.Equals(k, appId, StringComparison.OrdinalIgnoreCase));
    }

    private static string? GetInstalledVersionForApp(Dictionary<string, object> device, string appId)
    {
        if (!device.TryGetValue("apps", out var appsObj) || appsObj == null)
            return null;
        var apps = NormalizeToStringObjectMap(appsObj);
        if (apps == null)
            return null;
        Dictionary<string, object>? appEntry = null;
        foreach (var kvp in apps)
        {
            if (string.Equals(kvp.Key, appId, StringComparison.OrdinalIgnoreCase))
            {
                appEntry = NormalizeToStringObjectMap(kvp.Value);
                break;
            }
        }

        if (appEntry == null)
            return null;
        return appEntry.TryGetValue("installed_version", out var v) ? v?.ToString()?.Trim() : null;
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
        // Unknown installed version is not "behind" — handled as "not reported" in the grid.
        if (string.IsNullOrWhiteSpace(installed))
            return false;
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
        grid.Children.Add(UpdatesHeaderCell("Installed (selected app)", 3));
        grid.Children.Add(UpdatesHeaderCell("Seen", 4));
        grid.Children.Add(UpdatesHeaderCell("Push", 5));
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
        grid.Children.Add(UpdatesHostCell(string.IsNullOrWhiteSpace(r.DeviceName) ? "—" : r.DeviceName, 2));
        var installedCell = UpdatesDataCell(r.InstalledDisplay, 3, warn: r.Outdated, missingReport: r.MissingVersionReport);
        if (installedCell is TextBlock instTb && r.InstalledCellToolTip != null)
            instTb.ToolTip = r.InstalledCellToolTip;
        grid.Children.Add(installedCell);
        grid.Children.Add(UpdatesDataCell(r.LastSeen, 4));

        var pushBtn = new System.Windows.Controls.Button
        {
            Content = "Push",
            Style = TryFindResource("ActionBtn") as Style,
            Padding = new Thickness(8, 2, 8, 2),
            FontSize = 9,
            Margin = new Thickness(4, 0, 0, 0),
            Tag = $"{r.Username}\x1F{r.DeviceIdFull}",
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            ToolTip = "Push to this device (toolbar target + version rules apply)",
        };
        pushBtn.Click += PushDeviceFromTable_Click;
        Grid.SetColumn(pushBtn, 5);
        grid.Children.Add(pushBtn);

        return WrapUpdatesTableRow(grid, isHeader: false, rowIndex, minInnerHeight: 32);
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
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.75, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        return grid;
    }

    private UIElement UpdatesHostCell(string text, int col)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = FindBrush("TextPrimaryBrush"),
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
        };
        Grid.SetColumn(block, col);
        return block;
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

    private UIElement UpdatesDataCell(string text, int col, bool monospace = false, bool warn = false, bool missingReport = false)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = warn
                ? FindBrush("OrangeBrush")
                : missingReport
                    ? FindBrush("TextTertiaryBrush")
                    : FindBrush("TextPrimaryBrush"),
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

    private UIElement WrapUpdatesTableRow(Grid grid, bool isHeader, int rowIndex, double minInnerHeight = 26)
    {
        grid.MinHeight = Math.Max(grid.MinHeight, minInnerHeight);
        return new Border
        {
            Background = isHeader
                ? FindBrush("SurfaceBrush")
                : (rowIndex % 2 == 0 ? System.Windows.Media.Brushes.Transparent : FindBrush("FaintOverlayBrush")),
            BorderBrush = FindBrush("BorderBrush"),
            BorderThickness = new Thickness(1, isHeader ? 1 : 0, 1, 1),
            Padding = new Thickness(8, isHeader ? 6 : 6, 8, isHeader ? 6 : 6),
            Child = grid,
        };
    }

    // ════════════════════════════════════════════════════════════
    // BUILD TOOLS
    // ════════════════════════════════════════════════════════════

    private void Build_Click(object sender, RoutedEventArgs e)
    {
        AppendOutput("Build operations are only available from the development environment (dotnet CLI).");
    }

    private void BuildInstaller_Click(object sender, RoutedEventArgs e)
    {
        AppendOutput("Installer build is only available from the development environment.");
    }
}
