using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

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
            var devices = await _firebaseService.GetDevicesAsync();
            if (devices == null || devices.Count == 0)
            {
                HeartbeatCountText.Text = "0 online";
                _knownUsernames.Clear();
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
            HeartbeatCountText.Text = $"{onlineCount} online / {devices.Count} tracked";

            // Store usernames for permissions autocomplete
            _knownUsernames = grouped.Select(g => g.Key).ToList();

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

        var leftStack = new StackPanel { Orientation = Orientation.Horizontal };

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
            Cursor = System.Windows.Input.Cursors.Hand,
            Child = grid
        };

        // Click to navigate to permissions with this username
        border.MouseLeftButtonDown += (_, _) => NavigateToPermissions(username);

        // Hover effect
        border.MouseEnter += (_, _) => border.Background = FindBrush("HoverMediumBrush");
        border.MouseLeave += (_, _) => border.Background = FindBrush("FaintOverlayBrush");

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
        if (HeartbeatRowCountBox?.SelectedItem is ComboBoxItem item &&
            int.TryParse(item.Content?.ToString(), out var count))
        {
            HeartbeatScroller.MaxHeight = count * 38;
        }
    }

    // ════════════════════════════════════════════════════════════
    // EVENT HANDLERS
    // ════════════════════════════════════════════════════════════

    private async void RefreshHeartbeats_Click(object sender, RoutedEventArgs e) => await RefreshHeartbeatsAsync();
}
