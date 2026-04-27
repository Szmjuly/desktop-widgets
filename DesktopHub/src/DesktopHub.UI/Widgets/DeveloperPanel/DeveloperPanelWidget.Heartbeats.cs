using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using WpfBrushes = System.Windows.Media.Brushes;

namespace DesktopHub.UI.Widgets;

public partial class DeveloperPanelWidget
{
    // ════════════════════════════════════════════════════════════
    // HEARTBEATS
    // ════════════════════════════════════════════════════════════

    private async Task RefreshHeartbeatsAsync()
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized) return;

        try
        {
            HeartbeatList.Children.Clear();

            // Populate the user_id -> username map before rendering so each
            // row can show the real display name instead of the hash. Uses
            // EnsureTenantUsersLoadedAsync (tab-agnostic -- works without
            // the Permissions tab's UI being wired yet).
            await EnsureTenantUsersLoadedAsync();
            var userIdToUsername = _tenantUsers.ToDictionary(
                u => u.UserId, u => u.Username, StringComparer.OrdinalIgnoreCase);

            var devices = await _firebaseService.GetDevicesAsync();
            if (devices == null || devices.Count == 0)
            {
                HeartbeatCountText.Text = "0 online";
                _knownUsernames.Clear();
                return;
            }

            var lines = devices
                .Select(kvp =>
                {
                    var d = kvp.Value;
                    // Read user_id (new schema); resolve to decrypted name.
                    var uid = d.TryGetValue("user_id", out var u) ? u?.ToString() ?? "" : "";
                    var displayName = userIdToUsername.TryGetValue(uid, out var un) && !string.IsNullOrEmpty(un)
                        ? un
                        : (string.IsNullOrEmpty(uid) ? "unknown" : uid);
                    return new HeartbeatDeviceLine
                    {
                        DeviceId = kvp.Key,
                        Username = displayName,
                        DeviceName = d.TryGetValue("device_name", out var dn) ? dn?.ToString() ?? "" : "",
                        Status = d.TryGetValue("status", out var st) ? st?.ToString() ?? "unknown" : "unknown",
                        LastSeen = d.TryGetValue("last_seen", out var ls) ? ls?.ToString() ?? "" : "",
                    };
                })
                .ToList();

            var grouped = lines.GroupBy(x => x.Username).OrderBy(g => g.Key).ToList();

            int onlineCount = lines.Count(d => d.Status == "active");
            HeartbeatCountText.Text = $"{onlineCount} online / {devices.Count} tracked";

            _knownUsernames = grouped.Select(g => g.Key).ToList();
            PopulateUsernameDropdown();

            RenderHeartbeatTable(grouped);
        }
        catch (Exception ex)
        {
            AppendOutput($"ERROR refreshing heartbeats: {ex.Message}");
        }
    }

    private sealed class HeartbeatDeviceLine
    {
        public required string DeviceId { get; init; }
        public required string Username { get; init; }
        public string DeviceName { get; init; } = "";
        public string Status { get; init; } = "unknown";
        public string LastSeen { get; init; } = "";
    }

    private void RenderHeartbeatTable(System.Collections.Generic.IEnumerable<System.Linq.IGrouping<string, HeartbeatDeviceLine>> grouped)
    {
        HeartbeatList.Children.Clear();
        HeartbeatList.Children.Add(CreateHeartbeatHeaderRow());
        var rowIndex = 0;

        foreach (var group in grouped.OrderBy(g => g.Key))
        {
            var orderedDevices = group.OrderBy(d => d.DeviceName).ThenBy(d => d.DeviceId).ToList();
            for (var i = 0; i < orderedDevices.Count; i++)
            {
                HeartbeatList.Children.Add(CreateHeartbeatDataRow(
                    i == 0 ? group.Key : string.Empty,
                    orderedDevices[i],
                    rowIndex++));
            }
        }
    }

    private UIElement CreateHeartbeatHeaderRow()
    {
        var grid = CreateHeartbeatRowGrid();
        grid.Children.Add(CreateCell("User", 0, true));
        grid.Children.Add(CreateCell("Device", 1, true));
        grid.Children.Add(CreateCell("Status", 2, true));
        grid.Children.Add(CreateCell("Last Seen", 3, true));
        return new Border
        {
            Background = FindBrush("SurfaceBrush"),
            BorderBrush = FindBrush("BorderBrush"),
            BorderThickness = new Thickness(1, 0, 1, 1),
            Padding = new Thickness(8, 6, 8, 6),
            Child = grid
        };
    }

    private UIElement CreateHeartbeatDataRow(string usernameCell, HeartbeatDeviceLine d, int rowIndex)
    {
        var grid = CreateHeartbeatRowGrid();
        var deviceText = string.IsNullOrWhiteSpace(d.DeviceName) ? d.DeviceId : d.DeviceName;
        grid.Children.Add(CreateCell(usernameCell, 0, false));
        grid.Children.Add(CreateCell(deviceText, 1, false));
        grid.Children.Add(CreateStatusCell(d.Status, 2));
        grid.Children.Add(CreateCell(FormatTimeAgo(d.LastSeen), 3, false));

        return new Border
        {
            Background = rowIndex % 2 == 0 ? WpfBrushes.Transparent : FindBrush("FaintOverlayBrush"),
            BorderBrush = FindBrush("BorderBrush"),
            BorderThickness = new Thickness(1, 0, 1, 1),
            Padding = new Thickness(8, 4, 8, 4),
            Child = grid
        };
    }

    private Grid CreateHeartbeatRowGrid()
    {
        var grid = new Grid { MinHeight = 28 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
        return grid;
    }

    private UIElement CreateCell(string text, int column, bool isHeader)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = isHeader ? 10 : 10,
            FontWeight = isHeader ? FontWeights.SemiBold : FontWeights.Normal,
            Foreground = isHeader ? FindBrush("TextSecondaryBrush") : FindBrush("TextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(block, column);
        return block;
    }

    private UIElement CreateStatusCell(string status, int column)
    {
        var isActive = status == "active";
        var dot = new Ellipse
        {
            Width = 7,
            Height = 7,
            Fill = FindBrush(isActive ? "GreenBrush" : "OrangeBrush"),
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        var label = new TextBlock
        {
            Text = isActive ? "active" : status,
            FontSize = 10,
            Foreground = FindBrush(isActive ? "GreenBrush" : "OrangeBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        var panel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        panel.Children.Add(dot);
        panel.Children.Add(label);
        Grid.SetColumn(panel, column);
        return panel;
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
    // AUTO-REFRESH TIMER
    // ════════════════════════════════════════════════════════════

    private void StartHeartbeatTimer()
    {
        if (_heartbeatTimer == null)
        {
            _heartbeatTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _heartbeatTimer.Tick += async (_, _) => await RefreshHeartbeatsAsync();
        }
        _heartbeatTimer.Start();
    }

    // ════════════════════════════════════════════════════════════
    // ROW COUNT
    // ════════════════════════════════════════════════════════════

    private void HeartbeatRowCount_Changed(object sender, SelectionChangedEventArgs e)
    {
        // SelectionChanged can fire during InitializeComponent when SelectedIndex is applied,
        // before later named elements (e.g. HeartbeatScroller) are wired up.
        if (HeartbeatScroller == null)
            return;

        if (HeartbeatRowCountBox?.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Content?.ToString(), out var count))
        {
            HeartbeatScroller.MaxHeight = count * 30;
        }
    }

    // ════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ════════════════════════════════════════════════════════════

    private async void RefreshHeartbeats_Click(object sender, RoutedEventArgs e) => await RefreshHeartbeatsAsync();
}
