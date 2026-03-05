using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfPoint = System.Windows.Point;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfLine = System.Windows.Shapes.Line;
using DesktopHub.Core.Models;
using DesktopHub.UI.Services;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DesktopHub.UI.Widgets;

// Admin view: multi-user metrics, user toggles, summary table, insights
public partial class MetricsViewerWidget
{
    private async System.Threading.Tasks.Task LoadAdminMetricsAsync()
    {
        try
        {
            var service = TelemetryAccessor.Service;
            if (service == null) { AdminStatusText.Text = "Telemetry not initialized"; return; }

            AdminStatusText.Text = "Loading...";

            _adminSummaries = await service.GetAllUsersSummariesAsync(_adminRangeStart, _adminRangeEnd);

            AdminDateRangeLabel.Text = $"{_adminRangeStart:MMM dd} - {_adminRangeEnd:MMM dd}";

            // Build user list and toggles
            var users = _adminSummaries
                .Where(s => !string.IsNullOrEmpty(s.UserName))
                .Select(s => s.UserName)
                .Distinct()
                .ToList();
            AdminUserCountLabel.Text = users.Count > 0 ? $"{users.Count} user(s)" : "All users";

            // Initialize enabled users if empty (enable all by default)
            if (_enabledUsers.Count == 0)
                foreach (var u in users) _enabledUsers.Add(u);

            BuildUserToggles(users);
            ApplyUserFilter();

            RenderAdminSummaryTable();
            RenderAdminInsights(_adminRangeStart);

            if (!string.IsNullOrEmpty(_selectedChartType))
                RenderChart(_selectedChartType);

            AdminStatusText.Text = $"Updated {DateTime.Now:HH:mm:ss} ({_filteredAdminSummaries.Count}/{_adminSummaries.Count} records)";
        }
        catch (Exception ex)
        {
            AdminStatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void BuildUserToggles(List<string> users)
    {
        UserTogglePanel.Children.Clear();
        int ci = 0;
        foreach (var user in users)
        {
            var capturedUser = user;
            var colorIdx = ci;
            var isOn = _enabledUsers.Contains(user);

            var pill = new Border
            {
                Background = isOn ? PaletteBrushAlpha(colorIdx, 0x30) : new WpfSolidColorBrush(WpfColor.FromArgb(0x10, 0xF5, 0xF7, 0xFA)),
                BorderBrush = isOn ? PaletteBrush(colorIdx) : new WpfSolidColorBrush(WpfColor.FromArgb(0x20, 0xF5, 0xF7, 0xFA)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 5, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = user
            };
            pill.Child = new TextBlock
            {
                Text = user,
                FontSize = 10,
                Foreground = isOn ? PaletteBrush(colorIdx) : Brush("#6B7A85")
            };
            pill.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (_enabledUsers.Contains(capturedUser))
                {
                    if (_enabledUsers.Count > 1) _enabledUsers.Remove(capturedUser);
                }
                else
                {
                    _enabledUsers.Add(capturedUser);
                }
                // Refresh toggles and views
                var allUsers = _adminSummaries
                    .Where(s2 => !string.IsNullOrEmpty(s2.UserName))
                    .Select(s2 => s2.UserName).Distinct().ToList();
                BuildUserToggles(allUsers);
                ApplyUserFilter();
                RenderAdminSummaryTable();
                RenderAdminInsights(_adminRangeStart);
                if (!string.IsNullOrEmpty(_selectedChartType))
                    RenderChart(_selectedChartType);
                AdminStatusText.Text = $"Showing {_enabledUsers.Count} user(s) — {_filteredAdminSummaries.Count} records";
            };
            UserTogglePanel.Children.Add(pill);
            ci++;
        }
    }

    private void ApplyUserFilter()
    {
        _filteredAdminSummaries = _adminSummaries
            .Where(s => _enabledUsers.Contains(string.IsNullOrEmpty(s.UserName) ? s.DeviceName : s.UserName))
            .ToList();
    }

    private void RenderAdminInsights(DateTime from)
    {
        // Admin flow
        AdminFlowCanvas.Children.Clear();
        AdminTopProjectsPanel.Children.Clear();
        AdminFeatureRows.Children.Clear();

        if (_filteredAdminSummaries.Count == 0) return;

        // Feature usage breakdown (horizontal bars)
        var featureData = new (string Label, int Value, string Color)[]
        {
            ("Searches", _filteredAdminSummaries.Sum(s => s.TotalSearches), "#42A5F5"),
            ("Smart Search", _filteredAdminSummaries.Sum(s => s.TotalSmartSearches), "#26C6DA"),
            ("Doc Search", _filteredAdminSummaries.Sum(s => s.TotalDocSearches), "#5C6BC0"),
            ("Path Search", _filteredAdminSummaries.Sum(s => s.TotalPathSearches), "#78909C"),
            ("Launches", _filteredAdminSummaries.Sum(s => s.TotalProjectLaunches), "#66BB6A"),
            ("Doc Opens", _filteredAdminSummaries.Sum(s => s.TotalDocOpens), "#42A5F5"),
            ("Tasks", _filteredAdminSummaries.Sum(s => s.TotalTasksCreated + s.TotalTasksCompleted), "#AB47BC"),
            ("Timer", _filteredAdminSummaries.Sum(s => s.TotalTimerUses), "#FFA726"),
            ("Quick Launch", _filteredAdminSummaries.Sum(s => s.TotalQuickLaunchUses), "#FF7043"),
            ("Hotkeys", _filteredAdminSummaries.Sum(s => s.TotalHotkeyPresses), "#5C6BC0"),
            ("Clipboard", _filteredAdminSummaries.Sum(s => s.TotalClipboardCopies), "#26C6DA"),
        };

        var nonZero = featureData.Where(f => f.Value > 0).OrderByDescending(f => f.Value).ToArray();
        var maxFeat = nonZero.Length > 0 ? nonZero[0].Value : 1;

        foreach (var (label, value, color) in nonZero)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lbl = new TextBlock { Text = label, FontSize = 9, Foreground = Brush(color), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);

            var barW = Math.Max(4, value / (double)maxFeat * 60);
            var bar = new Border
            {
                Background = Brush(color),
                CornerRadius = new CornerRadius(2),
                Height = 6, Width = barW,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(bar, 1);
            row.Children.Add(bar);

            var val = new TextBlock { Text = value.ToString(), FontSize = 9, Foreground = Brush("#B6C3CA"), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(val, 2);
            row.Children.Add(val);

            AdminFeatureRows.Children.Add(row);
        }

        // Admin flow — aggregate transitions from all users' project types
        AdminFlowCanvas.UpdateLayout();
        var fw = AdminFlowCanvas.ActualWidth > 0 ? AdminFlowCanvas.ActualWidth : 160;
        var fh = AdminFlowCanvas.ActualHeight > 0 ? AdminFlowCanvas.ActualHeight : 120;

        // Build user→metric connections for flow visualization
        var userGroups = _filteredAdminSummaries
            .GroupBy(s => string.IsNullOrEmpty(s.UserName) ? s.DeviceName : s.UserName)
            .ToList();

        if (userGroups.Count > 0)
        {
            var fcx = fw / 2; var fcy = fh / 2;
            var fr = Math.Min(fw, fh) / 2 - 20;
            var userNames = userGroups.Select(g => g.Key).Take(6).ToList();

            // Place users around a circle
            for (int i = 0; i < userNames.Count; i++)
            {
                var angle = -Math.PI / 2 + 2 * Math.PI * i / userNames.Count;
                var px = fcx + fr * Math.Cos(angle);
                var py = fcy + fr * Math.Sin(angle);
                var totalActivity = userGroups.First(g => g.Key == userNames[i])
                    .Sum(s => s.TotalSearches + s.TotalProjectLaunches + s.TotalDocOpens);
                var nodeSize = 8 + Math.Min(14, totalActivity / 10.0);

                // Line to center
                var line = new WpfLine
                {
                    X1 = fcx, Y1 = fcy, X2 = px, Y2 = py,
                    Stroke = PaletteBrushAlpha(i, 0x40),
                    StrokeThickness = 1 + totalActivity / 50.0
                };
                AdminFlowCanvas.Children.Add(line);

                var node = new WpfEllipse
                {
                    Width = nodeSize, Height = nodeSize,
                    Fill = PaletteBrushAlpha(i, 0xC0),
                    Stroke = PaletteBrush(i), StrokeThickness = 1.5,
                    ToolTip = $"{userNames[i]}: {totalActivity} actions"
                };
                Canvas.SetLeft(node, px - nodeSize / 2);
                Canvas.SetTop(node, py - nodeSize / 2);
                AdminFlowCanvas.Children.Add(node);

                var lbl = new TextBlock { Text = userNames[i].Length > 8 ? userNames[i][..8] : userNames[i], FontSize = 7, Foreground = Brush("#B6C3CA") };
                Canvas.SetLeft(lbl, px - 14);
                Canvas.SetTop(lbl, py + nodeSize / 2 + 1);
                AdminFlowCanvas.Children.Add(lbl);
            }

            // Center hub
            var hub = new WpfEllipse { Width = 10, Height = 10, Fill = Brush("#F5F7FA"), Stroke = Brush("#007ACC"), StrokeThickness = 2 };
            Canvas.SetLeft(hub, fcx - 5);
            Canvas.SetTop(hub, fcy - 5);
            AdminFlowCanvas.Children.Add(hub);
        }

        // Admin top projects — aggregate across all filtered users
        var projFreq = _filteredAdminSummaries
            .SelectMany(s => s.ProjectTypeFrequency)
            .GroupBy(p => p.Key)
            .Select(g => new { Name = g.Key, Count = g.Sum(x => x.Value) })
            .OrderByDescending(p => p.Count)
            .Take(8)
            .ToList();

        if (projFreq.Count == 0)
        {
            AdminTopProjectsPanel.Children.Add(new TextBlock { Text = "No project data", FontSize = 10, FontStyle = FontStyles.Italic, Foreground = Brush("#6B7A85") });
        }
        else
        {
            var maxProj = projFreq[0].Count;
            for (int i = 0; i < projFreq.Count; i++)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var lbl = new TextBlock { Text = projFreq[i].Name, FontSize = 10, Foreground = Brush("#F5F7FA"), TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(lbl, 0);
                row.Children.Add(lbl);
                var cnt = new TextBlock { Text = projFreq[i].Count.ToString(), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = PaletteBrush(i) };
                Grid.SetColumn(cnt, 1);
                row.Children.Add(cnt);
                AdminTopProjectsPanel.Children.Add(row);
            }
        }
    }

    private void RenderAdminSummaryTable()
    {
        AdminSummaryRows.Children.Clear();

        if (_filteredAdminSummaries.Count == 0)
        {
            AdminSummaryRows.Children.Add(new TextBlock
            {
                Text = "No data for this range",
                FontSize = 10, FontStyle = FontStyles.Italic,
                Foreground = Brush("#6B7A85")
            });
            return;
        }

        // Aggregate per user (filtered)
        var byUser = _filteredAdminSummaries
            .GroupBy(s => string.IsNullOrEmpty(s.UserName) ? s.DeviceName : s.UserName)
            .Select(g => new
            {
                User = g.Key,
                Sessions = g.Sum(x => x.SessionCount),
                Searches = g.Sum(x => x.TotalSearches + x.TotalSmartSearches + x.TotalPathSearches),
                Launches = g.Sum(x => x.TotalProjectLaunches),
                DurationMin = g.Sum(x => x.TotalSessionDurationMs) / 60_000
            })
            .OrderByDescending(x => x.Sessions)
            .ToList();

        // Header
        AddAdminRow("User", "Sess", "Srch", "Proj", "Time", true);

        foreach (var u in byUser)
        {
            var timeStr = u.DurationMin >= 60 ? $"{u.DurationMin / 60}h{u.DurationMin % 60}m" : $"{u.DurationMin}m";
            AddAdminRow(u.User, u.Sessions.ToString(), u.Searches.ToString(), u.Launches.ToString(), timeStr, false);
        }
    }

    private void AddAdminRow(string col1, string col2, string col3, string col4, string col5, bool isHeader)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, isHeader ? 4 : 2) };
        for (int i = 0; i < 5; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = i == 0 ? new GridLength(1, GridUnitType.Star) : GridLength.Auto });

        var vals = new[] { col1, col2, col3, col4, col5 };
        for (int i = 0; i < 5; i++)
        {
            var tb = new TextBlock
            {
                Text = vals[i],
                FontSize = isHeader ? 9 : 10,
                FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isHeader ? Brush("#80B6C3CA") : Brush(i == 0 ? "#F5F7FA" : "#B6C3CA"),
                Margin = new Thickness(i > 0 ? 8 : 0, 0, 0, 0),
                TextTrimming = i == 0 ? TextTrimming.CharacterEllipsis : TextTrimming.None
            };
            Grid.SetColumn(tb, i);
            grid.Children.Add(tb);
        }

        AdminSummaryRows.Children.Add(grid);
    }
}
