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

            var prevStart = _adminRangeStart.AddDays(-_adminRangeDays);
            var prevEnd = _adminRangeEnd.AddDays(-_adminRangeDays);
            _previousPeriodSummaries = await service.GetAllUsersSummariesAsync(prevStart, prevEnd);

            // Resolve user_id hashes to decrypted usernames before rendering.
            // TelemetryService stores user_id in the UserName field; we swap
            // in the real name here so toggles, charts, and tables all show
            // something human-readable. Admins never see other tenants --
            // listTenantUsers enforces tenant isolation server-side.
            await EnsureUserDirectoryLoadedAsync();
            foreach (var s in _adminSummaries) s.UserName = ResolveUserLabel(s.UserName);
            foreach (var s in _previousPeriodSummaries) s.UserName = ResolveUserLabel(s.UserName);

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
            RenderAdminInsightCallouts();
            RenderPeriodComparison();
            PopulateAdminCompareUsers(users);
            UpdateAdminUserComparison();

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
                Background = isOn ? PaletteBrushAlpha(colorIdx, 0x30) : Helpers.ThemeHelper.Hover,
                BorderBrush = isOn ? PaletteBrush(colorIdx) : Helpers.ThemeHelper.HoverMedium,
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
                Foreground = isOn ? PaletteBrush(colorIdx) : Helpers.ThemeHelper.TextTertiary
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
        // Admin flow and panels
        AdminFlowCanvas.Children.Clear();
        AdminTopProjectsPanel.Children.Clear();
        AdminFeatureRows.Children.Clear();
        AdminCheatSheetPanel.Children.Clear();

        if (_filteredAdminSummaries.Count == 0) return;

        // Feature usage breakdown (horizontal bars) — includes cheat sheet and other metrics
        var featureData = new (string Label, int Value, string Color)[]
        {
            ("Searches", _filteredAdminSummaries.Sum(s => s.TotalSearches), Palette[4]),
            ("Smart Search", _filteredAdminSummaries.Sum(s => s.TotalSmartSearches), Palette[6]),
            ("Doc Search", _filteredAdminSummaries.Sum(s => s.TotalDocSearches), Palette[10]),
            ("Path Search", _filteredAdminSummaries.Sum(s => s.TotalPathSearches), Palette[9]),
            ("Launches", _filteredAdminSummaries.Sum(s => s.TotalProjectLaunches), Palette[1]),
            ("Doc Opens", _filteredAdminSummaries.Sum(s => s.TotalDocOpens), Palette[4]),
            ("Tasks", _filteredAdminSummaries.Sum(s => s.TotalTasksCreated + s.TotalTasksCompleted), Palette[3]),
            ("Timer", _filteredAdminSummaries.Sum(s => s.TotalTimerUses), Palette[2]),
            ("Quick Launch", _filteredAdminSummaries.Sum(s => s.TotalQuickLaunchUses), Palette[7]),
            ("Cheat Sheet (views)", _filteredAdminSummaries.Sum(s => s.TotalCheatSheetViews), Palette[9]),
            ("Cheat Sheet (lookups)", _filteredAdminSummaries.Sum(s => s.TotalCheatSheetLookups), Palette[9]),
            ("Cheat Sheet (copies)", _filteredAdminSummaries.Sum(s => s.TotalCheatSheetCopies), Palette[6]),
            ("Cheat Sheet (searches)", _filteredAdminSummaries.Sum(s => s.TotalCheatSheetSearches), Palette[6]),
            ("Hotkeys", _filteredAdminSummaries.Sum(s => s.TotalHotkeyPresses), Palette[10]),
            ("Clipboard", _filteredAdminSummaries.Sum(s => s.TotalClipboardCopies), Palette[6]),
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

            var val = new TextBlock { Text = value.ToString(), FontSize = 9, Foreground = Helpers.ThemeHelper.TextSecondary, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(val, 2);
            row.Children.Add(val);

            AdminFeatureRows.Children.Add(row);
        }

        // Cheat sheet metrics (aggregate)
        var csViews = _filteredAdminSummaries.Sum(s => s.TotalCheatSheetViews);
        var csLookups = _filteredAdminSummaries.Sum(s => s.TotalCheatSheetLookups);
        var csCopies = _filteredAdminSummaries.Sum(s => s.TotalCheatSheetCopies);
        var csSearches = _filteredAdminSummaries.Sum(s => s.TotalCheatSheetSearches);
        AdminCheatSheetPanel.Children.Add(AddAdminCheatSheetRow("Views", csViews));
        AdminCheatSheetPanel.Children.Add(AddAdminCheatSheetRow("Lookups", csLookups));
        AdminCheatSheetPanel.Children.Add(AddAdminCheatSheetRow("Copies", csCopies));
        AdminCheatSheetPanel.Children.Add(AddAdminCheatSheetRow("Searches", csSearches));
        var usageBySheet = _filteredAdminSummaries
            .SelectMany(s => s.CheatSheetUsageFrequency ?? new Dictionary<string, int>())
            .GroupBy(kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Value));
        foreach (var kv in usageBySheet.OrderByDescending(x => x.Value).Take(5))
        {
            var shortId = kv.Key.Length > 18 ? kv.Key[..15] + "..." : kv.Key;
            AdminCheatSheetPanel.Children.Add(AddAdminCheatSheetRow(shortId, kv.Value, isSubItem: true));
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

                var lbl = new TextBlock { Text = userNames[i].Length > 8 ? userNames[i][..8] : userNames[i], FontSize = 7, Foreground = Helpers.ThemeHelper.TextSecondary };
                Canvas.SetLeft(lbl, px - 14);
                Canvas.SetTop(lbl, py + nodeSize / 2 + 1);
                AdminFlowCanvas.Children.Add(lbl);
            }

            // Center hub
            var hub = new WpfEllipse { Width = 10, Height = 10, Fill = Helpers.ThemeHelper.TextPrimary, Stroke = Helpers.ThemeHelper.Accent, StrokeThickness = 2 };
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
            AdminTopProjectsPanel.Children.Add(new TextBlock { Text = "No project data", FontSize = 10, FontStyle = FontStyles.Italic, Foreground = Helpers.ThemeHelper.TextTertiary });
        }
        else
        {
            var maxProj = projFreq[0].Count;
            for (int i = 0; i < projFreq.Count; i++)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var lbl = new TextBlock { Text = projFreq[i].Name, FontSize = 10, Foreground = Helpers.ThemeHelper.TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis };
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
                Foreground = Helpers.ThemeHelper.TextTertiary
            });
            return;
        }

        // Aggregate per user — 11 columns
        var byUser = _filteredAdminSummaries
            .GroupBy(s => string.IsNullOrEmpty(s.UserName) ? s.DeviceName : s.UserName)
            .Select(g => new
            {
                User       = g.Key,
                Sessions   = g.Sum(x => x.SessionCount),
                Searches   = g.Sum(x => x.TotalSearches + x.TotalSmartSearches + x.TotalPathSearches),
                Launches   = g.Sum(x => x.TotalProjectLaunches),
                Docs       = g.Sum(x => x.TotalDocOpens),
                Tasks      = g.Sum(x => x.TotalTasksCreated + x.TotalTasksCompleted),
                QL         = g.Sum(x => x.TotalQuickLaunchUses),
                Timer      = g.Sum(x => x.TotalTimerUses),
                CS         = g.Sum(x => x.TotalCheatSheetViews + x.TotalCheatSheetLookups),
                Errors     = g.Sum(x => x.TotalErrors),
                DurationMin = g.Sum(x => x.TotalSessionDurationMs) / 60_000,
                Summaries  = g.ToList()
            })
            .OrderByDescending(x => x.Sessions)
            .ToList();

        // Header
        AddAdminRow11("User", "Sess", "Srch", "Proj", "Docs", "Tasks", "QL", "Tmr", "CS", "Err", "Time",
            isHeader: true, isHighlight: false);

        foreach (var u in byUser)
        {
            var timeStr = FormatDuration(u.DurationMin);
            var userRow = AddAdminRow11(u.User, u.Sessions.ToString(), u.Searches.ToString(),
                u.Launches.ToString(), u.Docs.ToString(), u.Tasks.ToString(),
                u.QL.ToString(), u.Timer.ToString(), u.CS.ToString(),
                u.Errors.ToString(), timeStr, isHeader: false, isHighlight: false);

            // Clickable expansion for per-day detail
            var capturedUser = u;
            var expansionPanel = new StackPanel { Visibility = Visibility.Collapsed, Margin = new Thickness(4, 0, 0, 6) };
            userRow.Cursor = System.Windows.Input.Cursors.Hand;
            userRow.ToolTip = "Click to expand daily breakdown";
            userRow.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (expansionPanel.Visibility == Visibility.Visible)
                {
                    expansionPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    expansionPanel.Children.Clear();
                    var dailySorted = capturedUser.Summaries.OrderByDescending(d => d.Date).ToList();
                    foreach (var day in dailySorted)
                    {
                        var daySearches = day.TotalSearches + day.TotalSmartSearches + day.TotalPathSearches;
                        var dayTasks = day.TotalTasksCreated + day.TotalTasksCompleted;
                        var dayCS = day.TotalCheatSheetViews + day.TotalCheatSheetLookups;
                        var dayDur = FormatDuration(day.TotalSessionDurationMs / 60_000);
                        AddAdminRow11(day.Date, day.SessionCount.ToString(), daySearches.ToString(),
                            day.TotalProjectLaunches.ToString(), day.TotalDocOpens.ToString(),
                            dayTasks.ToString(), day.TotalQuickLaunchUses.ToString(),
                            day.TotalTimerUses.ToString(), dayCS.ToString(),
                            day.TotalErrors.ToString(), dayDur,
                            isHeader: false, isHighlight: false, target: expansionPanel, fontSize: 7);
                    }
                    expansionPanel.Visibility = Visibility.Visible;
                }
            };
            AdminSummaryRows.Children.Add(expansionPanel);
        }

        // Total row
        var totSess     = byUser.Sum(u => u.Sessions);
        var totSearch   = byUser.Sum(u => u.Searches);
        var totLaunch   = byUser.Sum(u => u.Launches);
        var totDocs     = byUser.Sum(u => u.Docs);
        var totTasks    = byUser.Sum(u => u.Tasks);
        var totQL       = byUser.Sum(u => u.QL);
        var totTimer    = byUser.Sum(u => u.Timer);
        var totCS       = byUser.Sum(u => u.CS);
        var totErr      = byUser.Sum(u => u.Errors);
        var totDur      = byUser.Sum(u => u.DurationMin);

        AddAdminRow11("TOTAL", totSess.ToString(), totSearch.ToString(),
            totLaunch.ToString(), totDocs.ToString(), totTasks.ToString(),
            totQL.ToString(), totTimer.ToString(), totCS.ToString(),
            totErr.ToString(), FormatDuration(totDur),
            isHeader: false, isHighlight: true);

        // Avg/day row
        var totalDays = Math.Max(_filteredAdminSummaries.Select(s => s.Date).Distinct().Count(), 1);
        AddAdminRow11("Avg/day",
            $"{totSess / (double)totalDays:F1}", $"{totSearch / (double)totalDays:F1}",
            $"{totLaunch / (double)totalDays:F1}", $"{totDocs / (double)totalDays:F1}",
            $"{totTasks / (double)totalDays:F1}", $"{totQL / (double)totalDays:F1}",
            $"{totTimer / (double)totalDays:F1}", $"{totCS / (double)totalDays:F1}",
            $"{totErr / (double)totalDays:F1}", $"{totDur / totalDays}m",
            isHeader: false, isHighlight: true);
    }

    private static string FormatDuration(long minutes)
    {
        return minutes >= 60 ? $"{minutes / 60}h{minutes % 60}m" : $"{minutes}m";
    }

    private Border AddAdminRow11(string c0, string c1, string c2, string c3,
        string c4, string c5, string c6, string c7, string c8, string c9, string c10,
        bool isHeader, bool isHighlight, StackPanel? target = null, int fontSize = 0)
    {
        target ??= AdminSummaryRows;
        var baseFontSize = fontSize > 0 ? fontSize : (isHeader ? 7 : 8);

        var border = new Border
        {
            Background = isHighlight
                ? Helpers.ThemeHelper.FaintOverlay
                : System.Windows.Media.Brushes.Transparent,
            CornerRadius = new CornerRadius(isHighlight ? 3 : 0),
            Padding = new Thickness(0, isHighlight ? 2 : 0, 0, isHighlight ? 2 : 0),
            Margin = new Thickness(0, 0, 0, isHeader ? 3 : 1)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 1; i <= 10; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var vals = new[] { c0, c1, c2, c3, c4, c5, c6, c7, c8, c9, c10 };
        for (int i = 0; i < 11; i++)
        {
            var tb = new TextBlock
            {
                Text = vals[i],
                FontSize = baseFontSize,
                FontWeight = (isHeader || isHighlight) ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isHeader
                    ? Helpers.ThemeHelper.TextTertiary
                    : (i == 0 ? Helpers.ThemeHelper.TextPrimary : Helpers.ThemeHelper.TextSecondary),
                Margin = new Thickness(i > 0 ? 6 : 0, 0, 0, 0),
                TextTrimming = i == 0 ? TextTrimming.CharacterEllipsis : TextTrimming.None,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(tb, i);
            grid.Children.Add(tb);
        }

        border.Child = grid;
        target.Children.Add(border);
        return border;
    }

    private void AddAdminRow(string col1, string col2, string col3, string col4, string col5, bool isHeader)
    {
        AddAdminRow11(col1, col2, col3, col4, "", "", "", "", "", "", col5, isHeader, false);
    }

    private static Grid AddAdminCheatSheetRow(string label, int value, bool isSubItem = false)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var lbl = new TextBlock
        {
            Text = label,
            FontSize = isSubItem ? 8 : 9,
            Foreground = Helpers.ThemeHelper.TextSecondary,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(lbl, 0);
        grid.Children.Add(lbl);
        var val = new TextBlock
        {
            Text = value.ToString(),
            FontSize = isSubItem ? 8 : 9,
            FontWeight = FontWeights.SemiBold,
            Foreground = Helpers.ThemeHelper.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(val, 1);
        grid.Children.Add(val);
        return grid;
    }

    private void RenderAdminInsightCallouts()
    {
        AdminInsightCalloutsPanel.Children.Clear();
        if (_filteredAdminSummaries.Count == 0)
        {
            AdminInsightCalloutsPanel.Children.Add(new TextBlock
            {
                Text = "No data — select users and range.",
                FontSize = 9,
                FontStyle = FontStyles.Italic,
                Foreground = Helpers.ThemeHelper.TextTertiary
            });
            return;
        }

        var byUser = _filteredAdminSummaries
            .GroupBy(s => string.IsNullOrEmpty(s.UserName) ? s.DeviceName : s.UserName)
            .Select(g => new { User = g.Key, Summaries = g.ToList() })
            .ToList();
        var totalSearches = _filteredAdminSummaries.Sum(s => s.TotalSearches + s.TotalSmartSearches + s.TotalPathSearches);
        var totalSessions = _filteredAdminSummaries.Sum(s => s.SessionCount);
        var totalCsViews = _filteredAdminSummaries.Sum(s => s.TotalCheatSheetViews);

        if (byUser.Count > 0)
        {
            var topUser = byUser.OrderByDescending(u => u.Summaries.Sum(x => x.TotalSearches + x.TotalProjectLaunches)).First();
            var uSearches = topUser.Summaries.Sum(s => s.TotalSearches + s.TotalSmartSearches);
            var uSessions = topUser.Summaries.Sum(s => s.SessionCount);
            AdminInsightCalloutsPanel.Children.Add(AddInsightLine($"Top user: {topUser.User} (searches: {uSearches}, sessions: {uSessions})"));
        }
        if (totalCsViews > 0)
            AdminInsightCalloutsPanel.Children.Add(AddInsightLine($"Cheat sheet usage: {totalCsViews} views across {byUser.Count} user(s)."));
        var byDay = _filteredAdminSummaries.GroupBy(s => s.Date).OrderByDescending(g => g.Sum(x => x.TotalSearches + x.SessionCount)).FirstOrDefault();
        if (byDay != null)
            AdminInsightCalloutsPanel.Children.Add(AddInsightLine($"Peak day: {byDay.Key:ddd MMM dd} ({byDay.Sum(s => s.TotalSearches + s.SessionCount)} events)."));
        if (totalSessions > 0)
            AdminInsightCalloutsPanel.Children.Add(AddInsightLine($"Total: {totalSearches} searches, {totalSessions} sessions in range."));
    }

    private static TextBlock AddInsightLine(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 9,
            Foreground = Helpers.ThemeHelper.TextSecondary,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 2)
        };
    }

    private void RenderPeriodComparison()
    {
        AdminPeriodComparisonPanel.Children.Clear();
        if (_filteredAdminSummaries.Count == 0 || _previousPeriodSummaries.Count == 0)
        {
            AdminPeriodComparisonPanel.Children.Add(new TextBlock
            {
                Text = "This vs previous period (need both).",
                FontSize = 8,
                Foreground = Helpers.ThemeHelper.TextTertiary,
                Margin = new Thickness(0, 0, 0, 2)
            });
            return;
        }

        int Cur(string key) => _filteredAdminSummaries.Sum(GetMetricSelector(key));
        int Prev(string key) => _previousPeriodSummaries.Sum(GetMetricSelector(key));
        var metrics = new[]
        {
            ("searches",        "Searches"),
            ("smart_searches",  "Smart Search"),
            ("sessions",        "Sessions"),
            ("launches",        "Launches"),
            ("doc_opens",       "Doc Opens"),
            ("tasks_created",   "Tasks Created"),
            ("tasks_done",      "Tasks Done"),
            ("cheatsheet",      "Cheat Sheet"),
            ("hotkeys",         "Hotkeys"),
            ("errors",          "Errors"),
        };
        foreach (var (id, label) in metrics)
        {
            var c = Cur(id);
            var p = Prev(id);
            if (c == 0 && p == 0) continue;
            var delta = p > 0 ? (c - p) * 100.0 / p : (c > 0 ? 100 : 0);
            var isUp = delta >= 0;
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 1) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });

            var lblTb = new TextBlock { Text = label, FontSize = 8, Foreground = Helpers.ThemeHelper.TextSecondary };
            Grid.SetColumn(lblTb, 0);
            grid.Children.Add(lblTb);

            var valTb = new TextBlock
            {
                Text = $"{c} / {p}",
                FontSize = 8, Foreground = Helpers.ThemeHelper.TextSecondary,
                Margin = new Thickness(0, 0, 4, 0), HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            Grid.SetColumn(valTb, 1);
            grid.Children.Add(valTb);

            var deltaTb = new TextBlock
            {
                Text = $"{(isUp ? "▲" : "▼")}{Math.Abs(delta):F0}%",
                FontSize = 8,
                Foreground = isUp ? Helpers.ThemeHelper.Green : Helpers.ThemeHelper.Red,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            Grid.SetColumn(deltaTb, 2);
            grid.Children.Add(deltaTb);

            AdminPeriodComparisonPanel.Children.Add(grid);
        }
    }

    private void PopulateAdminCompareUsers(List<string> users)
    {
        var list = new List<string> { "" };
        list.AddRange(users);
        AdminCompareUserA.ItemsSource = list;
        AdminCompareUserB.ItemsSource = list;
        AdminCompareUserA.SelectedIndex = 0;
        AdminCompareUserB.SelectedIndex = 0;
        AdminCompareUserA.SelectionChanged -= AdminCompareUser_SelectionChanged;
        AdminCompareUserB.SelectionChanged -= AdminCompareUser_SelectionChanged;
        AdminCompareUserA.SelectionChanged += AdminCompareUser_SelectionChanged;
        AdminCompareUserB.SelectionChanged += AdminCompareUser_SelectionChanged;
    }

    private void AdminCompareUser_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateAdminUserComparison();

    private void UpdateAdminUserComparison()
    {
        AdminUserComparisonResultPanel.Children.Clear();
        var userA = AdminCompareUserA.SelectedItem as string;
        var userB = AdminCompareUserB.SelectedItem as string;
        if (string.IsNullOrEmpty(userA) || string.IsNullOrEmpty(userB) || userA == userB)
            return;
        var listA = _filteredAdminSummaries.Where(s => (string.IsNullOrEmpty(s.UserName) ? s.DeviceName : s.UserName) == userA).ToList();
        var listB = _filteredAdminSummaries.Where(s => (string.IsNullOrEmpty(s.UserName) ? s.DeviceName : s.UserName) == userB).ToList();
        if (listA.Count == 0 || listB.Count == 0) return;
        int Sum(List<DailyMetricsSummary> list, string key) => list.Sum(GetMetricSelector(key));
        var metrics = new[]
        {
            ("searches",        "Searches"),
            ("smart_searches",  "Smart Search"),
            ("sessions",        "Sessions"),
            ("launches",        "Launches"),
            ("doc_opens",       "Doc Opens"),
            ("tasks_created",   "Tasks Created"),
            ("tasks_done",      "Tasks Done"),
            ("cheatsheet",      "Cheat Sheet"),
            ("hotkeys",         "Hotkeys"),
            ("errors",          "Errors"),
        };
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var h0 = new TextBlock { Text = "", FontSize = 8, FontWeight = FontWeights.SemiBold, Foreground = Helpers.ThemeHelper.TextTertiary };
        var h1 = new TextBlock { Text = userA, FontSize = 8, FontWeight = FontWeights.SemiBold, Foreground = Helpers.ThemeHelper.TextTertiary, TextTrimming = TextTrimming.CharacterEllipsis };
        var h2 = new TextBlock { Text = userB, FontSize = 8, FontWeight = FontWeights.SemiBold, Foreground = Helpers.ThemeHelper.TextTertiary, TextTrimming = TextTrimming.CharacterEllipsis };
        headerGrid.Children.Add(h0);
        headerGrid.Children.Add(h1);
        headerGrid.Children.Add(h2);
        Grid.SetColumn(h0, 0);
        Grid.SetColumn(h1, 1);
        Grid.SetColumn(h2, 2);
        AdminUserComparisonResultPanel.Children.Add(headerGrid);
        foreach (var (id, label) in metrics)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var tbLabel = new TextBlock { Text = label, FontSize = 8, Foreground = Helpers.ThemeHelper.TextTertiary, VerticalAlignment = VerticalAlignment.Center };
            var tbA = new TextBlock { Text = Sum(listA, id).ToString(), FontSize = 9, Foreground = Helpers.ThemeHelper.TextPrimary, VerticalAlignment = VerticalAlignment.Center };
            var tbB = new TextBlock { Text = Sum(listB, id).ToString(), FontSize = 9, Foreground = Helpers.ThemeHelper.TextPrimary, VerticalAlignment = VerticalAlignment.Center };
            grid.Children.Add(tbLabel);
            grid.Children.Add(tbA);
            grid.Children.Add(tbB);
            Grid.SetColumn(tbLabel, 0);
            Grid.SetColumn(tbA, 1);
            Grid.SetColumn(tbB, 2);
            AdminUserComparisonResultPanel.Children.Add(grid);
        }
    }

    // ===== Admin right-panel tabs =====

    private void BuildAdminRightTabs()
    {
        AdminRightTabBar.Children.Clear();

        var tabs = new[] { ("overview", "Overview"), ("category", "Category Detail") };
        foreach (var (id, label) in tabs)
        {
            var capturedId = id;
            var isActive = id == _adminRightTab;
            var pill = new Border
            {
                Background = isActive ? Helpers.ThemeHelper.AccentLight : Helpers.ThemeHelper.Hover,
                BorderBrush = isActive ? Helpers.ThemeHelper.Accent : Helpers.ThemeHelper.HoverMedium,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 5, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = id
            };
            pill.Child = new TextBlock
            {
                Text = label, FontSize = 10,
                Foreground = isActive ? Helpers.ThemeHelper.Accent : Helpers.ThemeHelper.TextSecondary
            };
            pill.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                SwitchAdminRightTab(capturedId);
            };
            AdminRightTabBar.Children.Add(pill);
        }
    }

    private void SwitchAdminRightTab(string tabId)
    {
        _adminRightTab = tabId;
        BuildAdminRightTabs();

        if (tabId == "overview")
        {
            AdminOverviewTab.Visibility = Visibility.Visible;
            AdminCategoryDetailTab.Visibility = Visibility.Collapsed;
        }
        else
        {
            AdminOverviewTab.Visibility = Visibility.Collapsed;
            AdminCategoryDetailTab.Visibility = Visibility.Visible;
            RenderCategoryDetail(_selectedCategoryDetailId);
        }
    }

    // ===== Category detail tab =====

    private static readonly (string Id, string Label)[] CategoryDefs = new[]
    {
        ("search",       "Search"),
        ("smart_search", "Smart Search"),
        ("cheat_sheet",  "Cheat Sheet"),
        ("tasks",        "Tasks"),
        ("timer",        "Timer"),
        ("quick_launch", "Quick Launch"),
        ("doc_access",   "Doc Access"),
        ("tags",         "Tags"),
        ("hotkeys",      "Hotkeys"),
        ("clipboard",    "Clipboard"),
        ("errors",       "Errors"),
        ("performance",  "Performance"),
        ("settings",     "Settings"),
    };

    private void BuildCategorySelectorPills()
    {
        CategorySelectorPills.Children.Clear();
        foreach (var (id, label) in CategoryDefs)
        {
            var capturedId = id;
            var isActive = id == _selectedCategoryDetailId;
            var pill = new Border
            {
                Background = isActive ? Helpers.ThemeHelper.AccentLight : Helpers.ThemeHelper.Hover,
                BorderBrush = isActive ? Helpers.ThemeHelper.Accent : Helpers.ThemeHelper.HoverMedium,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(7, 3, 7, 3),
                Margin = new Thickness(0, 0, 4, 4),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = id
            };
            pill.Child = new TextBlock
            {
                Text = label, FontSize = 9,
                Foreground = isActive ? Helpers.ThemeHelper.Accent : Helpers.ThemeHelper.TextSecondary
            };
            pill.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                _selectedCategoryDetailId = capturedId;
                BuildCategorySelectorPills();
                RenderCategoryDetail(capturedId);
            };
            CategorySelectorPills.Children.Add(pill);
        }
    }

    private void RenderCategoryDetail(string categoryId)
    {
        CategoryDetailContent.Children.Clear();

        if (_filteredAdminSummaries.Count == 0)
        {
            CategoryDetailContent.Children.Add(new TextBlock
            {
                Text = "No data for current selection.",
                FontSize = 10, FontStyle = FontStyles.Italic,
                Foreground = Helpers.ThemeHelper.TextTertiary
            });
            return;
        }

        var byUser = _filteredAdminSummaries
            .GroupBy(s => string.IsNullOrEmpty(s.UserName) ? s.DeviceName : s.UserName)
            .ToDictionary(g => g.Key, g => g.ToList());

        switch (categoryId)
        {
            case "cheat_sheet":
                RenderCategoryCheatSheet(byUser);
                break;
            case "search":
                RenderCategorySearch(byUser);
                break;
            case "smart_search":
                RenderCategorySmartSearch(byUser);
                break;
            case "tasks":
                RenderCategoryTasks(byUser);
                break;
            default:
                RenderCategoryGeneric(categoryId, byUser);
                break;
        }
    }

    private void RenderCategoryCheatSheet(Dictionary<string, List<DailyMetricsSummary>> byUser)
    {
        // Aggregate totals
        var totalViews   = _filteredAdminSummaries.Sum(s => s.TotalCheatSheetViews);
        var totalLookups = _filteredAdminSummaries.Sum(s => s.TotalCheatSheetLookups);
        var totalCopies  = _filteredAdminSummaries.Sum(s => s.TotalCheatSheetCopies);
        var totalSearches = _filteredAdminSummaries.Sum(s => s.TotalCheatSheetSearches);

        AddCategoryHeader("CHEAT SHEET TOTALS");
        AddCategoryMetricRow("Views", totalViews, Palette[9]);
        AddCategoryMetricRow("Lookups", totalLookups, Palette[6]);
        AddCategoryMetricRow("Copies", totalCopies, Palette[4]);
        AddCategoryMetricRow("Searches", totalSearches, Palette[10]);

        // Per-user breakdown
        AddCategoryHeader("PER USER");
        foreach (var (user, summaries) in byUser.OrderByDescending(kv => kv.Value.Sum(s => s.TotalCheatSheetViews)))
        {
            var uViews = summaries.Sum(s => s.TotalCheatSheetViews);
            var uLookups = summaries.Sum(s => s.TotalCheatSheetLookups);
            var uCopies = summaries.Sum(s => s.TotalCheatSheetCopies);
            if (uViews + uLookups + uCopies == 0) continue;

            var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var lbl = new TextBlock
            {
                Text = user, FontSize = 9, Foreground = Helpers.ThemeHelper.TextPrimary,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);

            var pills = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            if (uViews > 0) AddMiniPill(pills, $"V:{uViews}", Palette[9]);
            if (uLookups > 0) AddMiniPill(pills, $"L:{uLookups}", Palette[6]);
            if (uCopies > 0) AddMiniPill(pills, $"C:{uCopies}", Palette[4]);
            Grid.SetColumn(pills, 1);
            row.Children.Add(pills);
            CategoryDetailContent.Children.Add(row);
        }

        // Per-sheet aggregate across all users
        var usageBySheet = _filteredAdminSummaries
            .SelectMany(s => s.CheatSheetUsageFrequency ?? new Dictionary<string, int>())
            .GroupBy(kv => kv.Key)
            .Select(g => new { Sheet = g.Key, Views = g.Sum(x => x.Value) })
            .OrderByDescending(x => x.Views)
            .ToList();

        if (usageBySheet.Count > 0)
        {
            AddCategoryHeader($"PER SHEET ({usageBySheet.Count})");
            var maxSheet = usageBySheet[0].Views;
            int ci = 0;
            foreach (var sheet in usageBySheet.Take(15))
            {
                var shortId = sheet.Sheet.Length > 20 ? sheet.Sheet[..17] + "..." : sheet.Sheet;
                var row = new Grid { Margin = new Thickness(0, 0, 0, 2), ToolTip = sheet.Sheet };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var lbl = new TextBlock { Text = shortId, FontSize = 8, Foreground = PaletteBrush(ci), TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(lbl, 0);
                row.Children.Add(lbl);

                var bar = new Border
                {
                    Background = PaletteBrush(ci), CornerRadius = new CornerRadius(2), Height = 5,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    Width = Math.Max(3, sheet.Views / (double)maxSheet * 36),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(bar, 1);
                row.Children.Add(bar);

                var val = new TextBlock { Text = sheet.Views.ToString(), FontSize = 8, Foreground = Helpers.ThemeHelper.TextSecondary, Margin = new Thickness(4, 0, 0, 0) };
                Grid.SetColumn(val, 2);
                row.Children.Add(val);
                CategoryDetailContent.Children.Add(row);
                ci++;
            }
        }
    }

    private void RenderCategorySearch(Dictionary<string, List<DailyMetricsSummary>> byUser)
    {
        var total = _filteredAdminSummaries.Sum(s => s.TotalSearches);
        var totalSmart = _filteredAdminSummaries.Sum(s => s.TotalSmartSearches);
        var totalDoc = _filteredAdminSummaries.Sum(s => s.TotalDocSearches);
        var totalPath = _filteredAdminSummaries.Sum(s => s.TotalPathSearches);
        var totalClicks = _filteredAdminSummaries.Sum(s => s.TotalSearchResultClicks);

        AddCategoryHeader("SEARCH TOTALS");
        AddCategoryMetricRow("Project Search", total, Palette[4]);
        AddCategoryMetricRow("Smart Search", totalSmart, Palette[6]);
        AddCategoryMetricRow("Doc Search", totalDoc, Palette[10]);
        AddCategoryMetricRow("Path Search", totalPath, Palette[9]);
        AddCategoryMetricRow("Result Clicks", totalClicks, Palette[1]);

        var withTiming = _filteredAdminSummaries.Where(s => s.AvgSearchTimingMs > 0).ToList();
        if (withTiming.Count > 0)
        {
            var avgTiming = withTiming.Average(s => s.AvgSearchTimingMs);
            AddCategoryMetricRow("Avg Search Time", (int)avgTiming, Palette[2], suffix: "ms");
        }

        AddCategoryHeader("PER USER");
        foreach (var (user, summaries) in byUser.OrderByDescending(kv => kv.Value.Sum(s => s.TotalSearches + s.TotalSmartSearches)))
        {
            var uTotal = summaries.Sum(s => s.TotalSearches + s.TotalSmartSearches + s.TotalDocSearches + s.TotalPathSearches);
            if (uTotal == 0) continue;
            AddCategoryMetricRow(user, uTotal, Palette[4]);
        }
    }

    private void RenderCategorySmartSearch(Dictionary<string, List<DailyMetricsSummary>> byUser)
    {
        var total = _filteredAdminSummaries.Sum(s => s.TotalSmartSearches);
        var filters = _filteredAdminSummaries.Sum(s => s.TotalSmartSearchFilterUses);

        AddCategoryHeader("SMART SEARCH TOTALS");
        AddCategoryMetricRow("Searches", total, Palette[6]);
        AddCategoryMetricRow("Filter Uses", filters, Palette[7]);

        AddCategoryHeader("PER USER");
        foreach (var (user, summaries) in byUser.OrderByDescending(kv => kv.Value.Sum(s => s.TotalSmartSearches)))
        {
            var u = summaries.Sum(s => s.TotalSmartSearches);
            var uf = summaries.Sum(s => s.TotalSmartSearchFilterUses);
            if (u == 0) continue;

            var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var lbl = new TextBlock { Text = user, FontSize = 9, Foreground = Helpers.ThemeHelper.TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);
            var pills = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            AddMiniPill(pills, $"srch:{u}", Palette[6]);
            if (uf > 0) AddMiniPill(pills, $"flt:{uf}", Palette[7]);
            Grid.SetColumn(pills, 1);
            row.Children.Add(pills);
            CategoryDetailContent.Children.Add(row);
        }
    }

    private void RenderCategoryTasks(Dictionary<string, List<DailyMetricsSummary>> byUser)
    {
        var created = _filteredAdminSummaries.Sum(s => s.TotalTasksCreated);
        var completed = _filteredAdminSummaries.Sum(s => s.TotalTasksCompleted);
        var deleted = _filteredAdminSummaries.Sum(s => s.TotalTasksDeleted);

        AddCategoryHeader("TASKS TOTALS");
        AddCategoryMetricRow("Created", created, Palette[3]);
        AddCategoryMetricRow("Completed", completed, Palette[1]);
        AddCategoryMetricRow("Deleted", deleted, Palette[5]);

        AddCategoryHeader("PER USER");
        foreach (var (user, summaries) in byUser.OrderByDescending(kv => kv.Value.Sum(s => s.TotalTasksCreated)))
        {
            var uc = summaries.Sum(s => s.TotalTasksCreated);
            var ud = summaries.Sum(s => s.TotalTasksCompleted);
            var udel = summaries.Sum(s => s.TotalTasksDeleted);
            if (uc + ud + udel == 0) continue;

            var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var lbl = new TextBlock { Text = user, FontSize = 9, Foreground = Helpers.ThemeHelper.TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);
            var pills = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            if (uc > 0) AddMiniPill(pills, $"+{uc}", Palette[3]);
            if (ud > 0) AddMiniPill(pills, $"\u2713{ud}", Palette[1]);
            if (udel > 0) AddMiniPill(pills, $"\u2212{udel}", Palette[5]);
            Grid.SetColumn(pills, 1);
            row.Children.Add(pills);
            CategoryDetailContent.Children.Add(row);
        }
    }

    private void RenderCategoryGeneric(string categoryId, Dictionary<string, List<DailyMetricsSummary>> byUser)
    {
        // Map category to metric selector(s)
        var metricMap = categoryId switch
        {
            "timer"        => new (string Label, Func<DailyMetricsSummary, int> Sel)[] { ("Timer Uses", s => s.TotalTimerUses) },
            "quick_launch" => new[] { ("Launches", (Func<DailyMetricsSummary, int>)(s => s.TotalQuickLaunchUses)), ("Adds", s => s.TotalQuickLaunchAdds), ("Removes", s => s.TotalQuickLaunchRemoves) },
            "doc_access"   => new[] { ("Doc Opens", (Func<DailyMetricsSummary, int>)(s => s.TotalDocOpens)), ("Doc Searches", s => s.TotalDocSearches) },
            "tags"         => new[] { ("Created", (Func<DailyMetricsSummary, int>)(s => s.TotalTagsCreated)), ("Updated", s => s.TotalTagsUpdated), ("Searches", s => s.TotalTagSearches), ("Carousel", s => s.TotalTagCarouselClicks) },
            "hotkeys"      => new[] { ("Presses", (Func<DailyMetricsSummary, int>)(s => s.TotalHotkeyPresses)) },
            "clipboard"    => new[] { ("Copies", (Func<DailyMetricsSummary, int>)(s => s.TotalClipboardCopies)) },
            "errors"       => new[] { ("Errors", (Func<DailyMetricsSummary, int>)(s => s.TotalErrors)) },
            "performance"  => new[] { ("Avg Startup (ms)", (Func<DailyMetricsSummary, int>)(s => (int)s.AvgStartupTimingMs)), ("Avg Search (ms)", s => (int)s.AvgSearchTimingMs) },
            "settings"     => new[] { ("Changes", (Func<DailyMetricsSummary, int>)(s => s.TotalSettingChanges)) },
            _              => new[] { ("Events", (Func<DailyMetricsSummary, int>)(s => s.SessionCount)) }
        };

        var displayLabel = CategoryDefs.FirstOrDefault(c => c.Id == categoryId).Label ?? categoryId;
        AddCategoryHeader($"{displayLabel.ToUpper()} TOTALS");
        foreach (var (label, sel) in metricMap)
        {
            var v = _filteredAdminSummaries.Sum(sel);
            AddCategoryMetricRow(label, v, Palette[4]);
        }

        AddCategoryHeader("PER USER");
        foreach (var (user, summaries) in byUser.OrderByDescending(kv => metricMap.Sum(m => kv.Value.Sum(m.Item2))))
        {
            var pills = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            bool anyNonZero = false;
            int ci = 0;
            foreach (var (label, sel) in metricMap)
            {
                var v = summaries.Sum(sel);
                if (v > 0) { AddMiniPill(pills, $"{label}: {v}", Palette[ci % Palette.Length]); anyNonZero = true; }
                ci++;
            }
            if (!anyNonZero) continue;

            var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var lbl = new TextBlock { Text = user, FontSize = 9, Foreground = Helpers.ThemeHelper.TextPrimary, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);
            Grid.SetColumn(pills, 1);
            row.Children.Add(pills);
            CategoryDetailContent.Children.Add(row);
        }
    }

    // ===== Category detail helpers =====

    private void AddCategoryHeader(string text)
    {
        CategoryDetailContent.Children.Add(new TextBlock
        {
            Text = text, FontSize = 8, FontWeight = FontWeights.SemiBold,
            Foreground = Helpers.ThemeHelper.TextTertiary,
            Margin = new Thickness(0, 6, 0, 4)
        });
    }

    private void AddCategoryMetricRow(string label, int value, string colorHex, string suffix = "")
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var lbl = new TextBlock { Text = label, FontSize = 9, Foreground = Helpers.ThemeHelper.TextSecondary, TextTrimming = TextTrimming.CharacterEllipsis };
        Grid.SetColumn(lbl, 0);
        grid.Children.Add(lbl);

        var val = new TextBlock
        {
            Text = string.IsNullOrEmpty(suffix) ? value.ToString() : $"{value} {suffix}",
            FontSize = 9, FontWeight = FontWeights.SemiBold,
            Foreground = value > 0 ? Brush(colorHex) : Helpers.ThemeHelper.TextTertiary
        };
        Grid.SetColumn(val, 1);
        grid.Children.Add(val);

        CategoryDetailContent.Children.Add(grid);
    }

    private static void AddMiniPill(StackPanel panel, string text, string colorHex)
    {
        var color = (WpfColor)WpfColorConverter.ConvertFromString(colorHex);
        var pill = new Border
        {
            Background = new WpfSolidColorBrush(WpfColor.FromArgb(0x22, color.R, color.G, color.B)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(0, 0, 3, 0)
        };
        pill.Child = new TextBlock
        {
            Text = text, FontSize = 7,
            Foreground = new WpfSolidColorBrush(color)
        };
        panel.Children.Add(pill);
    }
}
