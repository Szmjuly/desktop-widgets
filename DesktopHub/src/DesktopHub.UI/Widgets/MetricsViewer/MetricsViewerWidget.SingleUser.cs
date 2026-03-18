using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfPath = System.Windows.Shapes.Path;
using WpfLine = System.Windows.Shapes.Line;
using WpfPolyline = System.Windows.Shapes.Polyline;
using DesktopHub.Core.Models;
using DesktopHub.UI.Services;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DesktopHub.UI.Widgets;

// Single-user metrics: loading, rendering cards, enriched visualizations
public partial class MetricsViewerWidget
{
    private async System.Threading.Tasks.Task LoadMetricsAsync()
    {
        if (_isAdminView) { await LoadAdminMetricsAsync(); return; }

        try
        {
            var service = TelemetryAccessor.Service;
            if (service == null)
            {
                StatusText.Text = "Telemetry not initialized";
                return;
            }

            StatusText.Text = "Loading...";

            _currentSummary = await service.GetDailySummaryAsync(_selectedDate);

            UpdateDateLabel();

            if (_currentSummary == null)
            {
                ClearAll();
                StatusText.Text = "No data for this date";
                return;
            }

            RenderSessionCard(_currentSummary);
            RenderSearchCard(_currentSummary);
            RenderCheatSheetCard(_currentSummary);
            RenderActivityCard(_currentSummary);
            RenderTopQueries(_currentSummary);
            RenderWidgetUsage(_currentSummary);

            // New enriched cards — load in parallel
            var hourlyTask = service.GetHourlyBreakdownAsync(_selectedDate);
            var sessionsTask = service.GetSessionDetailsAsync(_selectedDate);
            var trendTask = service.GetMultiDaySummariesAsync(_selectedDate.AddDays(-6), _selectedDate);
            var projectsTask = service.GetTopProjectsAsync(_selectedDate.AddDays(-6), _selectedDate, 8);
            var transitionsTask = service.GetFeatureTransitionsAsync(_selectedDate.AddDays(-6), _selectedDate, 20);

            await System.Threading.Tasks.Task.WhenAll(hourlyTask, sessionsTask, trendTask, projectsTask, transitionsTask);

            var hourly = await hourlyTask;
            var sessions = await sessionsTask;
            var trend = await trendTask;
            var projects = await projectsTask;
            var transitions = await transitionsTask;

            // Cache for canvas redraws on window resize
            _lastHourlyBreakdown = hourly;
            _lastTransitions = transitions;
            _lastTrendData = trend;

            RenderHourlyHeatmap(hourly);
            RenderSessionTimeline(sessions);
            RenderTrendSparklines(trend);
            RenderTopProjects(projects);
            RenderFeatureDonut(_currentSummary);
            RenderActivityFlow(transitions);
            RenderUsageInsights(_currentSummary, trend, hourly);
            RenderPerformanceCard(_currentSummary);
            RenderDisciplinesCard(_currentSummary);
            RenderProjectTypesCard(_currentSummary);
            RenderFileTypesCard(_currentSummary);
            RenderWeeklyComparison(trend);
            RenderAllMetricsCard(_currentSummary);

            StatusText.Text = $"Updated {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error: {ex.Message}";
        }
    }

    private void UpdateDateLabel()
    {
        if (_selectedDate.Date == DateTime.Today)
            DateLabel.Text = "Today";
        else if (_selectedDate.Date == DateTime.Today.AddDays(-1))
            DateLabel.Text = "Yesterday";
        else
            DateLabel.Text = _selectedDate.ToString("MMM dd");
    }

    private void ClearAll()
    {
        SessionCountLabel.Text = "0";
        SessionDurationLabel.Text = "0m";
        ProjectSearchCount.Text = "0";
        SmartSearchCount.Text = "0";
        DocSearchCount.Text = "0";
        PathSearchCount.Text = "0";
        ActivityRows.Children.Clear();
        TopQueriesPanel.Children.Clear();
        WidgetUsageRows.Children.Clear();
        HourlyHeatmapCanvas.Children.Clear();
        HourlyPeakLabel.Text = "";
        TrendSparklines.Children.Clear();
        TopProjectsPanel.Children.Clear();
        FeatureDonutCanvas.Children.Clear();
        FeatureDonutLegend.Children.Clear();
        ActivityFlowCanvas.Children.Clear();
        SessionTimelinePanel.Children.Clear();
        InsightScoreLabel.Text = "--";
        InsightPeakLabel.Text = "--";
        InsightStreakLabel.Text = "--";
        InsightAvgDurLabel.Text = "--";
        InsightActiveDaysLabel.Text = "--";
        InsightTopFeatureLabel.Text = "--";
        CheatSheetViewsCount.Text = "0";
        CheatSheetLookupsCount.Text = "0";
        CheatSheetCopiesCount.Text = "0";
        CheatSheetSearchesCount.Text = "0";
        CheatSheetTopSheetsPanel.Children.Clear();
        CheatSheetTopSheetsPanel.Visibility = Visibility.Collapsed;

        PerformanceRows.Children.Clear();
        PerformanceCard.Visibility = Visibility.Collapsed;

        DisciplinePills.Children.Clear();
        DisciplinesCard.Visibility = Visibility.Collapsed;

        ProjectTypePills.Children.Clear();
        ProjectTypesCard.Visibility = Visibility.Collapsed;

        FileTypePills.Children.Clear();
        FileTypesCard.Visibility = Visibility.Collapsed;

        WeeklyComparisonPanel.Children.Clear();
        AllMetricsRows.Children.Clear();
    }

    private void RenderSessionCard(DailyMetricsSummary summary)
    {
        SessionCountLabel.Text = summary.SessionCount.ToString();
        var totalMinutes = summary.TotalSessionDurationMs / 60_000;
        if (totalMinutes >= 60)
            SessionDurationLabel.Text = $"{totalMinutes / 60}h {totalMinutes % 60}m";
        else
            SessionDurationLabel.Text = $"{totalMinutes}m";
    }

    private void RenderSearchCard(DailyMetricsSummary summary)
    {
        ProjectSearchCount.Text = summary.TotalSearches.ToString();
        SmartSearchCount.Text = summary.TotalSmartSearches.ToString();
        DocSearchCount.Text = summary.TotalDocSearches.ToString();
        PathSearchCount.Text = summary.TotalPathSearches.ToString();
    }

    private void RenderCheatSheetCard(DailyMetricsSummary summary)
    {
        CheatSheetViewsCount.Text = summary.TotalCheatSheetViews.ToString();
        CheatSheetLookupsCount.Text = summary.TotalCheatSheetLookups.ToString();
        CheatSheetCopiesCount.Text = summary.TotalCheatSheetCopies.ToString();
        CheatSheetSearchesCount.Text = summary.TotalCheatSheetSearches.ToString();

        CheatSheetTopSheetsPanel.Children.Clear();

        // Build a unified per-sheet breakdown from all frequency dictionaries
        var allSheets = new HashSet<string>();
        if (summary.CheatSheetUsageFrequency != null)
            foreach (var k in summary.CheatSheetUsageFrequency.Keys) allSheets.Add(k);
        if (summary.CheatSheetLookupFrequency != null)
            foreach (var k in summary.CheatSheetLookupFrequency.Keys) allSheets.Add(k);
        if (summary.CheatSheetCopyFrequency != null)
            foreach (var k in summary.CheatSheetCopyFrequency.Keys) allSheets.Add(k);
        if (summary.CheatSheetInteractions != null)
            foreach (var k in summary.CheatSheetInteractions.Keys) allSheets.Add(k);

        if (allSheets.Count == 0)
        {
            CheatSheetTopSheetsPanel.Visibility = Visibility.Collapsed;
            return;
        }

        CheatSheetTopSheetsPanel.Visibility = Visibility.Visible;

        // Sort by views descending
        var sorted = allSheets
            .Select(id => new
            {
                Id = id,
                Views = summary.CheatSheetUsageFrequency?.GetValueOrDefault(id, 0) ?? 0,
                Lookups = summary.CheatSheetLookupFrequency?.GetValueOrDefault(id, 0) ?? 0,
                Copies = summary.CheatSheetCopyFrequency?.GetValueOrDefault(id, 0) ?? 0,
                Interactions = summary.CheatSheetInteractions?.GetValueOrDefault(id)
            })
            .OrderByDescending(x => x.Views + x.Lookups + x.Copies)
            .ToList();

        var maxTotal = Math.Max(sorted.Max(s => s.Views + s.Lookups + s.Copies), 1);

        // Section header
        CheatSheetTopSheetsPanel.Children.Add(new TextBlock
        {
            Text = $"PER-SHEET BREAKDOWN ({sorted.Count} sheets)",
            FontSize = 8, FontWeight = FontWeights.SemiBold,
            Foreground = Helpers.ThemeHelper.TextTertiary,
            Margin = new Thickness(0, 2, 0, 4)
        });

        int ci = 0;
        foreach (var sheet in sorted)
        {
            var sheetTotal = sheet.Views + sheet.Lookups + sheet.Copies;
            var shortId = sheet.Id.Length > 22 ? sheet.Id[..19] + "..." : sheet.Id;
            var colorIdx = ci % Palette.Length;

            // Sheet header row: name + bar + total
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 1) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameTb = new TextBlock
            {
                Text = shortId, FontSize = 9, FontWeight = FontWeights.SemiBold,
                Foreground = PaletteBrush(colorIdx),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = sheet.Id
            };
            Grid.SetColumn(nameTb, 0);
            headerGrid.Children.Add(nameTb);

            // Mini usage bar
            var barBg = new Border
            {
                Background = Helpers.ThemeHelper.FaintOverlay,
                CornerRadius = new CornerRadius(2), Height = 5,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 4, 0)
            };
            var barFill = new Border
            {
                Background = PaletteBrush(colorIdx),
                CornerRadius = new CornerRadius(2), Height = 5,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Width = Math.Max(3, sheetTotal / (double)maxTotal * 46)
            };
            barBg.Child = barFill;
            Grid.SetColumn(barBg, 1);
            headerGrid.Children.Add(barBg);

            var totalTb = new TextBlock
            {
                Text = sheetTotal.ToString(), FontSize = 9, FontWeight = FontWeights.SemiBold,
                Foreground = Helpers.ThemeHelper.TextPrimary, VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(totalTb, 2);
            headerGrid.Children.Add(totalTb);

            CheatSheetTopSheetsPanel.Children.Add(headerGrid);

            // Sub-metrics row: views / lookups / copies + interactions
            var detailPanel = new WrapPanel { Margin = new Thickness(8, 0, 0, 4), Orientation = System.Windows.Controls.Orientation.Horizontal };

            if (sheet.Views > 0) AddCsDetailPill(detailPanel, "views", sheet.Views, Palette[9]);
            if (sheet.Lookups > 0) AddCsDetailPill(detailPanel, "lookups", sheet.Lookups, Palette[6]);
            if (sheet.Copies > 0) AddCsDetailPill(detailPanel, "copies", sheet.Copies, Palette[4]);

            // Interaction sub-items (MCA lookup, CES copy, view mode changed)
            if (sheet.Interactions != null && sheet.Interactions.Count > 0)
            {
                foreach (var kv in sheet.Interactions.OrderByDescending(x => x.Value))
                {
                    var displayName = kv.Key switch
                    {
                        "mca_lookup" => "MCA",
                        "ces_copy" => "CES",
                        "view_mode_changed" => "mode\u0394",
                        _ => kv.Key
                    };
                    AddCsDetailPill(detailPanel, displayName, kv.Value, Palette[2]);
                }
            }

            if (detailPanel.Children.Count > 0)
                CheatSheetTopSheetsPanel.Children.Add(detailPanel);

            ci++;
        }
    }

    private static void AddCsDetailPill(WrapPanel panel, string label, int value, string colorHex)
    {
        var pill = new Border
        {
            Background = new WpfSolidColorBrush(WpfColor.FromArgb(0x20,
                ((WpfColor)WpfColorConverter.ConvertFromString(colorHex)).R,
                ((WpfColor)WpfColorConverter.ConvertFromString(colorHex)).G,
                ((WpfColor)WpfColorConverter.ConvertFromString(colorHex)).B)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(4, 1, 4, 1),
            Margin = new Thickness(0, 0, 3, 2)
        };
        pill.Child = new TextBlock
        {
            Text = $"{label} {value}",
            FontSize = 7,
            Foreground = new WpfSolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(colorHex))
        };
        panel.Children.Add(pill);
    }

    private void RenderActivityCard(DailyMetricsSummary summary)
    {
        ActivityRows.Children.Clear();

        AddActivityRow("Project launches", summary.TotalProjectLaunches, Palette[1],
            "Times a project folder was opened from search results or frequent projects");
        AddActivityRow("Doc opens", summary.TotalDocOpens, Palette[4],
            "Times a document was opened via Doc Quick Open");
        AddActivityRow("Doc searches", summary.TotalDocSearches, Palette[4],
            "Number of search queries in Doc Quick Open");
        AddActivityRow("Quick launch uses", summary.TotalQuickLaunchUses, Palette[2],
            "Times an item was launched from the Quick Launch widget");
        AddActivityRow("Tasks created", summary.TotalTasksCreated, Palette[3],
            "Quick Tasks items created");
        AddActivityRow("Tasks completed", summary.TotalTasksCompleted, Palette[1],
            "Quick Tasks items marked as done");
        AddActivityRow("Timer uses", summary.TotalTimerUses, Palette[9],
            "Times the timer was started");
        AddActivityRow("Cheat sheet views", summary.TotalCheatSheetViews, Palette[9],
            "Times a specific cheat sheet was opened inside the Cheat Sheets widget");
        AddActivityRow("Hotkey presses", summary.TotalHotkeyPresses, Palette[10],
            "Global hotkey activations (Ctrl+Alt+Space, close shortcut, etc.)");
        AddActivityRow("Clipboard copies", summary.TotalClipboardCopies, Palette[6],
            "Times a path or value was copied to the clipboard from any widget");
        AddActivityRow("Filter changes", summary.TotalFilterChanges, Palette[7],
            "Times a filter (year, drive, discipline) was changed");
        AddActivityRow("Tags created", summary.TotalTagsCreated, Palette[14],
            "New project tag entries created");
        AddActivityRow("Tags updated", summary.TotalTagsUpdated, Palette[14],
            "Existing project tag entries updated");
        AddActivityRow("Tag searches", summary.TotalTagSearches, Palette[10],
            "Tag-based search queries executed (e.g. voltage:208)");
        AddActivityRow("Tag carousel clicks", summary.TotalTagCarouselClicks, Palette[10],
            "Tag carousel chip clicks for quick filtering");
        AddActivityRow("Errors", summary.TotalErrors, Palette[5],
            "Application or widget errors logged");

        if (ActivityRows.Children.Count == 0)
        {
            ActivityRows.Children.Add(new TextBlock
            {
                Text = "No activity yet",
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                Foreground = Helpers.ThemeHelper.TextTertiary
            });
        }
    }

    private void AddActivityRow(string label, int count, string colorHex, string? tooltip = null)
    {
        if (count == 0) return;

        var grid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
        if (tooltip != null)
            grid.ToolTip = tooltip;

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var labelBlock = new TextBlock
        {
            Text = label,
            FontSize = 11,
            Foreground = Helpers.ThemeHelper.TextSecondary
        };
        Grid.SetColumn(labelBlock, 0);
        grid.Children.Add(labelBlock);

        var countBlock = new TextBlock
        {
            Text = count.ToString(),
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = new WpfSolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(colorHex))
        };
        Grid.SetColumn(countBlock, 1);
        grid.Children.Add(countBlock);

        ActivityRows.Children.Add(grid);
    }

    private void RenderTopQueries(DailyMetricsSummary summary)
    {
        TopQueriesPanel.Children.Clear();

        if (summary.TopSearchQueries.Count == 0)
        {
            TopQueriesPanel.Children.Add(new TextBlock
            {
                Text = "No searches yet",
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                Foreground = Helpers.ThemeHelper.TextTertiary
            });
            return;
        }

        for (var i = 0; i < Math.Min(summary.TopSearchQueries.Count, 8); i++)
        {
            var queryItem = summary.TopSearchQueries[i];
            var row = new Border
            {
                Background = Helpers.ThemeHelper.FaintOverlay,
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 0, 2)
            };

            // Build tooltip with source breakdown
            var tooltipText = queryItem.Query;
            if (queryItem.SourceBreakdown.Count > 0)
            {
                tooltipText += "\n\nSource breakdown:";
                foreach (var src in queryItem.SourceBreakdown.OrderByDescending(s => s.Value))
                    tooltipText += $"\n  {QuerySources.DisplayName(src.Key)}: {src.Value}x";
            }
            row.ToolTip = tooltipText;

            var outerStack = new StackPanel();

            // Top line: rank + query + count
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var rankBlock = new TextBlock
            {
                Text = $"{i + 1}.",
                FontSize = 10,
                Foreground = Helpers.ThemeHelper.TextTertiary,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(rankBlock, 0);
            grid.Children.Add(rankBlock);

            var queryBlock = new TextBlock
            {
                Text = queryItem.Query,
                FontSize = 11,
                Foreground = Helpers.ThemeHelper.TextPrimary,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(queryBlock, 1);
            grid.Children.Add(queryBlock);

            var countBlock = new TextBlock
            {
                Text = $"{queryItem.Count}x",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Helpers.ThemeHelper.Accent,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(countBlock, 2);
            grid.Children.Add(countBlock);

            outerStack.Children.Add(grid);

            // Source tags row (compact colored pills)
            if (queryItem.SourceBreakdown.Count > 0)
            {
                var sourcePanel = new WrapPanel { Margin = new Thickness(16, 1, 0, 0) };
                foreach (var src in queryItem.SourceBreakdown.OrderByDescending(s => s.Value))
                {
                    var sourceColor = src.Key switch
                    {
                        "typed" => Palette[4],
                        "pasted" => Palette[2],
                        "history" => Palette[3],
                        "frequent_project" => Palette[1],
                        "smart_search" => Palette[6],
                        "path_search" => Palette[9],
                        "doc_search" => Palette[10],
                        _ => Palette[9]
                    };

                    var pill = new Border
                    {
                        Background = new WpfSolidColorBrush(WpfColor.FromArgb(0x25,
                            ((WpfColor)WpfColorConverter.ConvertFromString(sourceColor)).R,
                            ((WpfColor)WpfColorConverter.ConvertFromString(sourceColor)).G,
                            ((WpfColor)WpfColorConverter.ConvertFromString(sourceColor)).B)),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 0, 4, 0),
                        Margin = new Thickness(0, 0, 3, 0)
                    };
                    pill.Child = new TextBlock
                    {
                        Text = $"{QuerySources.DisplayName(src.Key)} {src.Value}",
                        FontSize = 8,
                        Foreground = new WpfSolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(sourceColor))
                    };
                    sourcePanel.Children.Add(pill);
                }
                outerStack.Children.Add(sourcePanel);
            }

            row.Child = outerStack;
            TopQueriesPanel.Children.Add(row);
        }
    }

    private void RenderWidgetUsage(DailyMetricsSummary summary)
    {
        WidgetUsageRows.Children.Clear();

        if (summary.WidgetUsageCounts.Count == 0)
        {
            WidgetUsageRows.Children.Add(new TextBlock
            {
                Text = "No widget events yet",
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                Foreground = Helpers.ThemeHelper.TextTertiary
            });
            return;
        }

        foreach (var kvp in summary.WidgetUsageCounts.OrderByDescending(x => x.Value))
        {
            var displayName = WidgetIds.All.Contains(kvp.Key)
                ? WidgetIds.DisplayName(kvp.Key)
                : kvp.Key;

            var grid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelBlock = new TextBlock
            {
                Text = displayName,
                FontSize = 11,
                Foreground = Helpers.ThemeHelper.TextSecondary
            };
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            var countBlock = new TextBlock
            {
                Text = kvp.Value.ToString(),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = Helpers.ThemeHelper.Accent
            };
            Grid.SetColumn(countBlock, 1);
            grid.Children.Add(countBlock);

            WidgetUsageRows.Children.Add(grid);
        }
    }

    // ===== Enriched single-user cards =====

    private void RenderHourlyHeatmap(HourlyBreakdown breakdown)
    {
        HourlyHeatmapCanvas.Children.Clear();
        HourlyHeatmapCanvas.UpdateLayout();
        var w = HourlyHeatmapCanvas.ActualWidth > 0 ? HourlyHeatmapCanvas.ActualWidth : 200;
        var h = HourlyHeatmapCanvas.ActualHeight > 0 ? HourlyHeatmapCanvas.ActualHeight : 50;

        var max = Math.Max(breakdown.PeakCount, 1);
        var cellW = (w - 2) / 24.0;
        var barAreaH = h - 14;

        for (int i = 0; i < 24; i++)
        {
            var val = breakdown.EventCounts[i];
            var intensity = (byte)(30 + (int)(val / (double)max * 200));
            var barH = Math.Max(2, val / (double)max * barAreaH);

            var rect = new WpfRectangle
            {
                Width = Math.Max(2, cellW - 1),
                Height = barH,
                Fill = Helpers.ThemeHelper.BrushFrom(Helpers.ThemeHelper.AccentColor, intensity),
                RadiusX = 1, RadiusY = 1,
                ToolTip = $"{i:00}:00 — {val} events"
            };
            Canvas.SetLeft(rect, 1 + i * cellW);
            Canvas.SetTop(rect, barAreaH - barH);
            HourlyHeatmapCanvas.Children.Add(rect);

            // Hour label every 4 hours
            if (i % 4 == 0)
            {
                var tb = new TextBlock { Text = $"{i}", FontSize = 7, Foreground = Helpers.ThemeHelper.TextTertiary };
                Canvas.SetLeft(tb, 1 + i * cellW);
                Canvas.SetTop(tb, barAreaH + 1);
                HourlyHeatmapCanvas.Children.Add(tb);
            }
        }

        if (breakdown.PeakCount > 0)
            HourlyPeakLabel.Text = $"Peak: {breakdown.PeakHour:00}:00 ({breakdown.PeakCount} events)";
        else
            HourlyPeakLabel.Text = "No activity";
    }

    private void RenderSessionTimeline(List<SessionDetail> sessions)
    {
        SessionTimelinePanel.Children.Clear();

        if (sessions.Count == 0)
        {
            SessionTimelinePanel.Children.Add(new TextBlock
            {
                Text = "No sessions", FontSize = 10, FontStyle = FontStyles.Italic,
                Foreground = Helpers.ThemeHelper.TextTertiary
            });
            return;
        }

        foreach (var sess in sessions.Take(6))
        {
            var durMin = sess.DurationMs / 60_000;
            var durStr = durMin >= 60 ? $"{durMin / 60}h{durMin % 60}m" : $"{durMin}m";
            var startStr = sess.StartTime.ToLocalTime().ToString("HH:mm");

            var border = new Border
            {
                Background = Helpers.ThemeHelper.FaintOverlay,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(0, 0, 0, 4)
            };

            var stack = new StackPanel();

            // Header: time + duration + event count
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var timeBlock = new TextBlock
            {
                Text = $"{startStr}  ({durStr})",
                FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = Helpers.ThemeHelper.TextPrimary
            };
            Grid.SetColumn(timeBlock, 0);
            headerGrid.Children.Add(timeBlock);

            var evtBlock = new TextBlock
            {
                Text = $"{sess.EventCount} events",
                FontSize = 9, Foreground = Helpers.ThemeHelper.TextTertiary
            };
            Grid.SetColumn(evtBlock, 1);
            headerGrid.Children.Add(evtBlock);
            stack.Children.Add(headerGrid);

            // Action breakdown pills
            if (sess.ActionBreakdown.Count > 0)
            {
                var pillPanel = new WrapPanel { Margin = new Thickness(0, 2, 0, 0) };
                int ci = 0;
                foreach (var ab in sess.ActionBreakdown.OrderByDescending(a => a.Value).Take(5))
                {
                    var pill = new Border
                    {
                        Background = PaletteBrushAlpha(ci, 0x25),
                        CornerRadius = new CornerRadius(3),
                        Padding = new Thickness(4, 1, 4, 1),
                        Margin = new Thickness(0, 0, 3, 0)
                    };
                    pill.Child = new TextBlock
                    {
                        Text = $"{ab.Key} {ab.Value}",
                        FontSize = 8,
                        Foreground = PaletteBrush(ci)
                    };
                    pillPanel.Children.Add(pill);
                    ci++;
                }
                stack.Children.Add(pillPanel);
            }

            border.Child = stack;
            SessionTimelinePanel.Children.Add(border);
        }
    }

    private void RenderTrendSparklines(List<DailyMetricsSummary> trendData)
    {
        TrendSparklines.Children.Clear();

        if (trendData.Count == 0)
        {
            TrendSparklines.Children.Add(new TextBlock
            {
                Text = "Not enough data", FontSize = 10, FontStyle = FontStyles.Italic,
                Foreground = Helpers.ThemeHelper.TextTertiary
            });
            return;
        }

        // Show sparklines for key metrics
        var sparkMetrics = new (string Label, Func<DailyMetricsSummary, int> Sel, string Color)[]
        {
            ("Searches", s => s.TotalSearches + s.TotalSmartSearches + s.TotalDocSearches + s.TotalPathSearches, Palette[4]),
            ("Launches", s => s.TotalProjectLaunches, Palette[1]),
            ("Sessions", s => s.SessionCount, Palette[2]),
            ("Duration", s => (int)(s.TotalSessionDurationMs / 60_000), Palette[3]),
        };

        foreach (var (label, sel, color) in sparkMetrics)
        {
            var values = trendData.Select(sel).ToList();
            var total = values.Sum();

            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(58) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var labelTb = new TextBlock
            {
                Text = label, FontSize = 9, Foreground = Brush(color),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelTb, 0);
            row.Children.Add(labelTb);

            // Mini sparkline canvas
            var sparkCanvas = new Canvas { Height = 18, ClipToBounds = true };
            Grid.SetColumn(sparkCanvas, 1);
            row.Children.Add(sparkCanvas);

            sparkCanvas.Loaded += (s, e) =>
            {
                sparkCanvas.Children.Clear();
                var sw = sparkCanvas.ActualWidth > 0 ? sparkCanvas.ActualWidth : 80;
                var sh = sparkCanvas.ActualHeight;
                var max = Math.Max(values.Max(), 1);
                var points = new System.Windows.Media.PointCollection();

                for (int i = 0; i < values.Count; i++)
                {
                    var px = values.Count > 1 ? i * sw / (values.Count - 1) : sw / 2;
                    var py = sh - 2 - (values[i] / (double)max) * (sh - 4);
                    points.Add(new WpfPoint(px, py));
                }

                if (points.Count >= 2)
                {
                    var polyline = new WpfPolyline
                    {
                        Points = points,
                        Stroke = Brush(color),
                        StrokeThickness = 1.5
                    };
                    sparkCanvas.Children.Add(polyline);
                }

                // Dots at start and end
                if (points.Count > 0)
                {
                    var endDot = new WpfEllipse { Width = 4, Height = 4, Fill = Brush(color) };
                    Canvas.SetLeft(endDot, points.Last().X - 2);
                    Canvas.SetTop(endDot, points.Last().Y - 2);
                    sparkCanvas.Children.Add(endDot);
                }
            };

            var totalTb = new TextBlock
            {
                Text = total.ToString(), FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = Helpers.ThemeHelper.TextPrimary, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 0, 0)
            };
            Grid.SetColumn(totalTb, 2);
            row.Children.Add(totalTb);

            TrendSparklines.Children.Add(row);
        }
    }

    private void RenderTopProjects(List<TopProjectInfo> projects)
    {
        TopProjectsPanel.Children.Clear();

        if (projects.Count == 0)
        {
            TopProjectsPanel.Children.Add(new TextBlock
            {
                Text = "No project activity", FontSize = 10, FontStyle = FontStyles.Italic,
                Foreground = Helpers.ThemeHelper.TextTertiary
            });
            return;
        }

        var maxTotal = Math.Max(projects.Max(p => p.TotalInteractions), 1);

        for (int i = 0; i < projects.Count; i++)
        {
            var proj = projects[i];
            var border = new Border
            {
                Margin = new Thickness(0, 0, 0, 3)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var projLabel = new TextBlock
            {
                Text = proj.ProjectNumber,
                FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = Helpers.ThemeHelper.TextPrimary,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 100,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(projLabel, 0);
            grid.Children.Add(projLabel);

            // Mini bar showing relative usage
            var barBg = new Border
            {
                Background = Helpers.ThemeHelper.Hover,
                CornerRadius = new CornerRadius(2),
                Height = 6,
                Margin = new Thickness(6, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            var barFill = new Border
            {
                Background = PaletteBrush(i),
                CornerRadius = new CornerRadius(2),
                Height = 6,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                Width = Math.Max(4, proj.TotalInteractions / (double)maxTotal * 60)
            };
            barBg.Child = barFill;
            Grid.SetColumn(barBg, 1);
            grid.Children.Add(barBg);

            var countLabel = new TextBlock
            {
                Text = proj.TotalInteractions.ToString(),
                FontSize = 10, Foreground = PaletteBrush(i),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(countLabel, 2);
            grid.Children.Add(countLabel);

            var tooltipParts = new List<string> { proj.ProjectNumber };
            if (proj.ProjectType != null) tooltipParts.Add($"Type: {proj.ProjectType}");
            tooltipParts.Add($"Launches: {proj.LaunchCount}");
            tooltipParts.Add($"Searches: {proj.SearchCount}");
            tooltipParts.Add($"Doc opens: {proj.DocOpenCount}");
            grid.ToolTip = string.Join("\n", tooltipParts);

            border.Child = grid;
            TopProjectsPanel.Children.Add(border);
        }
    }

    private void RenderFeatureDonut(DailyMetricsSummary summary)
    {
        FeatureDonutCanvas.Children.Clear();
        FeatureDonutLegend.Children.Clear();
        FeatureDonutCanvas.UpdateLayout();
        var w = FeatureDonutCanvas.ActualWidth > 0 ? FeatureDonutCanvas.ActualWidth : 160;
        var h = FeatureDonutCanvas.ActualHeight > 0 ? FeatureDonutCanvas.ActualHeight : 130;

        var featureData = new (string Label, int Value, string Color)[]
        {
            ("Search", summary.TotalSearches, Palette[4]),
            ("Smart", summary.TotalSmartSearches, Palette[6]),
            ("Doc", summary.TotalDocSearches + summary.TotalDocOpens, Palette[10]),
            ("Path", summary.TotalPathSearches, Palette[9]),
            ("Launches", summary.TotalProjectLaunches, Palette[1]),
            ("Tasks", summary.TotalTasksCreated + summary.TotalTasksCompleted, Palette[3]),
            ("Timer", summary.TotalTimerUses, Palette[2]),
            ("Quick Launch", summary.TotalQuickLaunchUses, Palette[7]),
        };

        var slices = featureData.Where(f => f.Value > 0).ToList();
        if (slices.Count == 0)
        {
            FeatureDonutLegend.Children.Add(new TextBlock
            {
                Text = "No feature activity", FontSize = 10, FontStyle = FontStyles.Italic,
                Foreground = Helpers.ThemeHelper.TextTertiary
            });
            return;
        }

        var total = slices.Sum(s => s.Value);
        var cx = w / 2; var cy = h / 2;
        var r = Math.Min(w, h) / 2 - 8;
        double startAngle = -90;

        foreach (var (label, value, color) in slices)
        {
            var sweepAngle = (value / (double)total) * 360;
            if (sweepAngle < 0.5) continue;
            var endAngle = startAngle + sweepAngle;
            var isLargeArc = sweepAngle > 180;

            var startRad = startAngle * Math.PI / 180;
            var endRad = endAngle * Math.PI / 180;
            var x1 = cx + r * Math.Cos(startRad);
            var y1 = cy + r * Math.Sin(startRad);
            var x2 = cx + r * Math.Cos(endRad);
            var y2 = cy + r * Math.Sin(endRad);

            var pg = new PathGeometry();
            var pf = new PathFigure { StartPoint = new WpfPoint(cx, cy), IsClosed = true, IsFilled = true };
            pf.Segments.Add(new LineSegment(new WpfPoint(x1, y1), true));
            pf.Segments.Add(new ArcSegment(new WpfPoint(x2, y2), new WpfSize(r, r), 0, isLargeArc, SweepDirection.Clockwise, true));
            pg.Figures.Add(pf);

            var slice = new WpfPath { Data = pg, Fill = Brush(color), Stroke = new WpfSolidColorBrush(WpfColor.FromArgb(0x20, 0, 0, 0)), StrokeThickness = 1 };
            slice.ToolTip = $"{label}: {value} ({(value * 100.0 / total):F0}%)";
            FeatureDonutCanvas.Children.Add(slice);

            startAngle = endAngle;
        }

        // Donut hole
        var holeR = r * 0.5;
        var holeColor = Helpers.ThemeHelper.GetColor("WindowBackgroundColor");
        var hole = new WpfEllipse { Width = holeR * 2, Height = holeR * 2, Fill = new WpfSolidColorBrush(WpfColor.FromArgb(0xF0, holeColor.R, holeColor.G, holeColor.B)) };
        Canvas.SetLeft(hole, cx - holeR);
        Canvas.SetTop(hole, cy - holeR);
        FeatureDonutCanvas.Children.Add(hole);

        // Center total
        var totalTb = new TextBlock { Text = total.ToString(), FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Helpers.ThemeHelper.TextPrimary };
        totalTb.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(totalTb, cx - totalTb.DesiredSize.Width / 2);
        Canvas.SetTop(totalTb, cy - totalTb.DesiredSize.Height / 2);
        FeatureDonutCanvas.Children.Add(totalTb);

        // Legend
        foreach (var (label, value, color) in slices)
        {
            var legendRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 1) };
            legendRow.Children.Add(new WpfRectangle { Width = 8, Height = 8, Fill = Brush(color), RadiusX = 1, RadiusY = 1, Margin = new Thickness(0, 1, 4, 0) });
            legendRow.Children.Add(new TextBlock { Text = $"{label} {value}", FontSize = 8, Foreground = Helpers.ThemeHelper.TextSecondary });
            FeatureDonutLegend.Children.Add(legendRow);
        }
    }

    private void RenderActivityFlow(List<FeatureTransition> transitions)
    {
        ActivityFlowCanvas.Children.Clear();
        ActivityFlowCanvas.UpdateLayout();
        var w = ActivityFlowCanvas.ActualWidth > 0 ? ActivityFlowCanvas.ActualWidth : 200;
        var h = ActivityFlowCanvas.ActualHeight > 0 ? ActivityFlowCanvas.ActualHeight : 120;

        if (transitions.Count == 0)
        {
            var tb = new TextBlock { Text = "No flow data yet", FontSize = 10, FontStyle = FontStyles.Italic, Foreground = Helpers.ThemeHelper.TextTertiary };
            Canvas.SetLeft(tb, w / 2 - 30);
            Canvas.SetTop(tb, h / 2 - 6);
            ActivityFlowCanvas.Children.Add(tb);
            return;
        }

        // Build unique categories and position them
        var cats = transitions.SelectMany(t => new[] { t.From, t.To }).Distinct().Take(8).ToList();
        var catPositions = new Dictionary<string, WpfPoint>();
        var cx = w / 2; var cy = h / 2;
        var r = Math.Min(w, h) / 2 - 20;

        for (int i = 0; i < cats.Count; i++)
        {
            var angle = -Math.PI / 2 + 2 * Math.PI * i / cats.Count;
            catPositions[cats[i]] = new WpfPoint(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle));
        }

        var maxCount = Math.Max(transitions.Max(t => t.Count), 1);

        // Draw flow arrows (lines with width proportional to count)
        foreach (var t in transitions.Take(15))
        {
            if (!catPositions.ContainsKey(t.From) || !catPositions.ContainsKey(t.To)) continue;
            var from = catPositions[t.From];
            var to = catPositions[t.To];
            var thickness = 0.5 + t.Count / (double)maxCount * 3;
            var alpha = (byte)(40 + t.Count / (double)maxCount * 160);

            var line = new WpfLine
            {
                X1 = from.X, Y1 = from.Y,
                X2 = to.X, Y2 = to.Y,
                Stroke = new WpfSolidColorBrush(WpfColor.FromArgb(alpha, 0x42, 0xA5, 0xF5)),
                StrokeThickness = thickness,
                ToolTip = $"{t.From} → {t.To}: {t.Count}x"
            };
            ActivityFlowCanvas.Children.Add(line);
        }

        // Draw nodes
        for (int i = 0; i < cats.Count; i++)
        {
            var pos = catPositions[cats[i]];
            var totalForCat = transitions.Where(t => t.From == cats[i] || t.To == cats[i]).Sum(t => t.Count);
            var nodeSize = 8 + Math.Min(16, totalForCat / (double)maxCount * 12);

            var node = new WpfEllipse
            {
                Width = nodeSize, Height = nodeSize,
                Fill = PaletteBrushAlpha(i, 0xC0),
                Stroke = PaletteBrush(i),
                StrokeThickness = 1.5,
                ToolTip = $"{cats[i]}: {totalForCat} transitions"
            };
            Canvas.SetLeft(node, pos.X - nodeSize / 2);
            Canvas.SetTop(node, pos.Y - nodeSize / 2);
            ActivityFlowCanvas.Children.Add(node);

            // Short label
            var shortLabel = cats[i].Length > 6 ? cats[i][..6] : cats[i];
            var labelTb = new TextBlock { Text = shortLabel, FontSize = 7, Foreground = Helpers.ThemeHelper.TextSecondary };
            Canvas.SetLeft(labelTb, pos.X - 12);
            Canvas.SetTop(labelTb, pos.Y + nodeSize / 2 + 1);
            ActivityFlowCanvas.Children.Add(labelTb);
        }
    }

    private void RenderPerformanceCard(DailyMetricsSummary summary)
    {
        PerformanceRows.Children.Clear();

        var hasData = summary.AvgStartupTimingMs > 0
            || summary.AvgSearchTimingMs > 0
            || summary.AvgSearchResultClickPosition > 0;

        PerformanceCard.Visibility = hasData ? Visibility.Visible : Visibility.Collapsed;
        if (!hasData) return;

        if (summary.AvgStartupTimingMs > 0)
            AddPerfRow("Avg startup", $"{summary.AvgStartupTimingMs:F0} ms", Palette[2]);
        if (summary.AvgSearchTimingMs > 0)
            AddPerfRow("Avg search", $"{summary.AvgSearchTimingMs:F0} ms", Palette[4]);
        if (summary.AvgSearchResultClickPosition > 0)
            AddPerfRow("Avg click pos", $"#{summary.AvgSearchResultClickPosition:F1}", Palette[1],
                "Average position (0-based) in results list where user clicked");
        if (summary.TotalSearchResultClicks > 0)
            AddPerfRow("Search clicks", $"{summary.TotalSearchResultClicks}", Palette[6]);
    }

    private void AddPerfRow(string label, string value, string colorHex, string? tooltip = null)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
        if (tooltip != null) grid.ToolTip = tooltip;
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var lbl = new TextBlock { Text = label, FontSize = 11, Foreground = Helpers.ThemeHelper.TextSecondary };
        Grid.SetColumn(lbl, 0);
        grid.Children.Add(lbl);

        var val = new TextBlock
        {
            Text = value, FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = new WpfSolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(colorHex))
        };
        Grid.SetColumn(val, 1);
        grid.Children.Add(val);

        PerformanceRows.Children.Add(grid);
    }

    private void RenderDisciplinesCard(DailyMetricsSummary summary)
    {
        DisciplinePills.Children.Clear();
        if (summary.DisciplineFrequency == null || summary.DisciplineFrequency.Count == 0)
        {
            DisciplinesCard.Visibility = Visibility.Collapsed;
            return;
        }

        DisciplinesCard.Visibility = Visibility.Visible;
        int ci = 0;
        foreach (var kv in summary.DisciplineFrequency.OrderByDescending(x => x.Value))
        {
            DisciplinePills.Children.Add(MakeFrequencyPill(kv.Key, kv.Value, ci++));
        }
    }

    private void RenderProjectTypesCard(DailyMetricsSummary summary)
    {
        ProjectTypePills.Children.Clear();
        if (summary.ProjectTypeFrequency == null || summary.ProjectTypeFrequency.Count == 0)
        {
            ProjectTypesCard.Visibility = Visibility.Collapsed;
            return;
        }

        ProjectTypesCard.Visibility = Visibility.Visible;
        int ci = 0;
        foreach (var kv in summary.ProjectTypeFrequency.OrderByDescending(x => x.Value).Take(12))
        {
            ProjectTypePills.Children.Add(MakeFrequencyPill(kv.Key, kv.Value, ci++));
        }
    }

    private void RenderFileTypesCard(DailyMetricsSummary summary)
    {
        FileTypePills.Children.Clear();
        if (summary.FileExtensionFrequency == null || summary.FileExtensionFrequency.Count == 0)
        {
            FileTypesCard.Visibility = Visibility.Collapsed;
            return;
        }

        FileTypesCard.Visibility = Visibility.Visible;
        int ci = 0;
        foreach (var kv in summary.FileExtensionFrequency.OrderByDescending(x => x.Value).Take(12))
        {
            var ext = kv.Key.StartsWith('.') ? kv.Key : $".{kv.Key}";
            FileTypePills.Children.Add(MakeFrequencyPill(ext, kv.Value, ci++));
        }
    }

    private static Border MakeFrequencyPill(string label, int count, int colorIndex)
    {
        var pill = new Border
        {
            Background = PaletteBrushAlpha(colorIndex, 0x22),
            BorderBrush = PaletteBrush(colorIndex),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2, 6, 2),
            Margin = new Thickness(0, 0, 4, 4),
            ToolTip = $"{label}: {count}"
        };
        var sp = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        sp.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9,
            Foreground = PaletteBrush(colorIndex),
            VerticalAlignment = VerticalAlignment.Center
        });
        sp.Children.Add(new TextBlock
        {
            Text = $" {count}",
            FontSize = 8,
            Foreground = Helpers.ThemeHelper.TextTertiary,
            VerticalAlignment = VerticalAlignment.Center
        });
        pill.Child = sp;
        return pill;
    }

    private void RenderWeeklyComparison(List<DailyMetricsSummary> trendData)
    {
        WeeklyComparisonPanel.Children.Clear();

        if (trendData.Count < 2)
        {
            WeeklyComparisonPanel.Children.Add(new TextBlock
            {
                Text = "Need at least 2 days of data",
                FontSize = 9, FontStyle = FontStyles.Italic,
                Foreground = Helpers.ThemeHelper.TextTertiary
            });
            return;
        }

        // Split: today / selected day = last entry, rest = prior window
        var current = trendData.Last();
        var prior = trendData.Take(trendData.Count - 1).ToList();
        if (prior.Count == 0) return;

        var compareMetrics = new (string Label, Func<DailyMetricsSummary, int> Sel)[]
        {
            ("Searches",   s => s.TotalSearches + s.TotalSmartSearches + s.TotalDocSearches + s.TotalPathSearches),
            ("Launches",   s => s.TotalProjectLaunches),
            ("Doc opens",  s => s.TotalDocOpens),
            ("Tasks +/-",  s => s.TotalTasksCreated - s.TotalTasksDeleted),
            ("Timer",      s => s.TotalTimerUses),
            ("Cheat Sheet",s => s.TotalCheatSheetViews + s.TotalCheatSheetLookups),
            ("Hotkeys",    s => s.TotalHotkeyPresses),
            ("Clipboard",  s => s.TotalClipboardCopies),
        };

        // Average the prior days so comparison is fair regardless of window size
        var priorDays = Math.Max(prior.Count, 1);

        foreach (var (label, sel) in compareMetrics)
        {
            var cur = sel(current);
            var prevAvg = prior.Sum(sel) / (double)priorDays;
            if (cur == 0 && prevAvg < 0.5) continue;

            var delta = prevAvg > 0 ? ((cur - prevAvg) / prevAvg * 100) : (cur > 0 ? 100.0 : 0.0);
            var isUp = delta >= 0;
            var deltaStr = $"{(isUp ? "▲" : "▼")} {Math.Abs(delta):F0}%";

            var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            row.ToolTip = $"{label}: today {cur} vs {prevAvg:F1} avg ({priorDays}d)";

            var lbl = new TextBlock { Text = label, FontSize = 9, Foreground = Helpers.ThemeHelper.TextSecondary };
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);

            var curTb = new TextBlock
            {
                Text = cur.ToString(), FontSize = 9, FontWeight = FontWeights.SemiBold,
                Foreground = Helpers.ThemeHelper.TextPrimary,
                Margin = new Thickness(0, 0, 6, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            Grid.SetColumn(curTb, 1);
            row.Children.Add(curTb);

            var deltaTb = new TextBlock
            {
                Text = deltaStr, FontSize = 9,
                Foreground = isUp ? Helpers.ThemeHelper.Green : Helpers.ThemeHelper.Red,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            Grid.SetColumn(deltaTb, 2);
            row.Children.Add(deltaTb);

            WeeklyComparisonPanel.Children.Add(row);
        }

        if (WeeklyComparisonPanel.Children.Count == 0)
        {
            WeeklyComparisonPanel.Children.Add(new TextBlock
            {
                Text = "No activity to compare",
                FontSize = 9, FontStyle = FontStyles.Italic,
                Foreground = Helpers.ThemeHelper.TextTertiary
            });
        }
    }

    private void RenderAllMetricsCard(DailyMetricsSummary summary)
    {
        AllMetricsRows.Children.Clear();

        var allMetrics = new (string Label, int Value, string Color)[]
        {
            ("Sessions",           summary.SessionCount,                                    Palette[0]),
            ("Duration (min)",     (int)(summary.TotalSessionDurationMs / 60_000),          Palette[3]),
            ("Project Search",     summary.TotalSearches,                                   Palette[4]),
            ("Smart Search",       summary.TotalSmartSearches,                              Palette[6]),
            ("Doc Search",         summary.TotalDocSearches,                                Palette[10]),
            ("Path Search",        summary.TotalPathSearches,                               Palette[9]),
            ("Project Launches",   summary.TotalProjectLaunches,                            Palette[1]),
            ("Doc Opens",          summary.TotalDocOpens,                                   Palette[4]),
            ("Quick Launch",       summary.TotalQuickLaunchUses,                            Palette[7]),
            ("QL Adds",            summary.TotalQuickLaunchAdds,                            Palette[7]),
            ("QL Removes",         summary.TotalQuickLaunchRemoves,                         Palette[7]),
            ("Tasks Created",      summary.TotalTasksCreated,                               Palette[3]),
            ("Tasks Completed",    summary.TotalTasksCompleted,                             Palette[1]),
            ("Tasks Deleted",      summary.TotalTasksDeleted,                               Palette[5]),
            ("Timer Uses",         summary.TotalTimerUses,                                  Palette[2]),
            ("CS Views",           summary.TotalCheatSheetViews,                            Palette[9]),
            ("CS Lookups",         summary.TotalCheatSheetLookups,                          Palette[9]),
            ("CS Copies",          summary.TotalCheatSheetCopies,                           Palette[6]),
            ("CS Searches",        summary.TotalCheatSheetSearches,                         Palette[6]),
            ("Hotkey Presses",     summary.TotalHotkeyPresses,                              Palette[10]),
            ("Clipboard Copies",   summary.TotalClipboardCopies,                            Palette[6]),
            ("Filter Changes",     summary.TotalFilterChanges,                              Palette[7]),
            ("Setting Changes",    summary.TotalSettingChanges,                              Palette[8]),
            ("Tags Created",       summary.TotalTagsCreated,                                Palette[14]),
            ("Tags Updated",       summary.TotalTagsUpdated,                                Palette[14]),
            ("Tag Searches",       summary.TotalTagSearches,                                Palette[10]),
            ("Tag Carousel",       summary.TotalTagCarouselClicks,                          Palette[10]),
            ("Smart Filters",      summary.TotalSmartSearchFilterUses,                      Palette[6]),
            ("Search Clicks",      summary.TotalSearchResultClicks,                         Palette[4]),
            ("Errors",             summary.TotalErrors,                                     Palette[5]),
        };

        // Two-column layout
        for (int i = 0; i < allMetrics.Length; i += 2)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            AddAllMetricCell(row, 0, 1, allMetrics[i].Label, allMetrics[i].Value, allMetrics[i].Color);
            if (i + 1 < allMetrics.Length)
                AddAllMetricCell(row, 3, 4, allMetrics[i + 1].Label, allMetrics[i + 1].Value, allMetrics[i + 1].Color);

            AllMetricsRows.Children.Add(row);
        }
    }

    private static void AddAllMetricCell(Grid row, int labelCol, int valueCol, string label, int value, string colorHex)
    {
        var lbl = new TextBlock
        {
            Text = label, FontSize = 9,
            Foreground = Helpers.ThemeHelper.TextTertiary,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(lbl, labelCol);
        row.Children.Add(lbl);

        var val = new TextBlock
        {
            Text = value.ToString(), FontSize = 9, FontWeight = FontWeights.SemiBold,
            Foreground = value > 0
                ? new WpfSolidColorBrush((WpfColor)WpfColorConverter.ConvertFromString(colorHex))
                : Helpers.ThemeHelper.TextTertiary,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(val, valueCol);
        row.Children.Add(val);
    }

    private void RenderUsageInsights(DailyMetricsSummary summary, List<DailyMetricsSummary> trendData, HourlyBreakdown hourly)
    {
        // Productivity score: weighted composite of actions
        var weights = new Dictionary<string, double>
        {
            ["searches"] = 1.0, ["smart_searches"] = 1.5, ["doc_searches"] = 1.2,
            ["launches"] = 2.0, ["doc_opens"] = 1.5, ["tasks_created"] = 3.0,
            ["tasks_done"] = 4.0, ["timer"] = 2.0
        };

        double score = summary.TotalSearches * weights["searches"]
            + summary.TotalSmartSearches * weights["smart_searches"]
            + summary.TotalDocSearches * weights["doc_searches"]
            + summary.TotalProjectLaunches * weights["launches"]
            + summary.TotalDocOpens * weights["doc_opens"]
            + summary.TotalTasksCreated * weights["tasks_created"]
            + summary.TotalTasksCompleted * weights["tasks_done"]
            + summary.TotalTimerUses * weights["timer"];

        // Normalize to 0-100 (cap at 200 raw points = 100 score)
        var normalizedScore = Math.Min(100, (int)(score / 2));
        InsightScoreLabel.Text = normalizedScore.ToString();
        InsightScoreLabel.Foreground = normalizedScore >= 70 ? Helpers.ThemeHelper.Green
            : normalizedScore >= 40 ? Helpers.ThemeHelper.Orange : Helpers.ThemeHelper.Red;

        // Peak hour
        InsightPeakLabel.Text = hourly.PeakCount > 0 ? $"{hourly.PeakHour:00}:00" : "--";

        // Streak: consecutive days with activity counting back from today
        int streak = 0;
        var sortedDates = trendData.OrderByDescending(d => d.Date).ToList();
        foreach (var day in sortedDates)
        {
            var totalActions = day.TotalSearches + day.TotalSmartSearches + day.TotalProjectLaunches + day.SessionCount;
            if (totalActions > 0) streak++;
            else break;
        }
        InsightStreakLabel.Text = $"{streak}d";

        // Avg session duration
        var totalSessions = trendData.Sum(d => d.SessionCount);
        var totalDurMin = trendData.Sum(d => d.TotalSessionDurationMs) / 60_000;
        if (totalSessions > 0)
        {
            var avg = totalDurMin / totalSessions;
            InsightAvgDurLabel.Text = avg >= 60 ? $"{avg / 60}h{avg % 60}m" : $"{avg}m";
        }
        else
        {
            InsightAvgDurLabel.Text = "--";
        }

        // Active days out of 7
        var activeDays = trendData.Count(d => d.SessionCount > 0);
        InsightActiveDaysLabel.Text = $"{activeDays}/7";

        // Most used feature
        var features = new (string Name, int Count)[]
        {
            ("Search", summary.TotalSearches),
            ("Smart", summary.TotalSmartSearches),
            ("Doc", summary.TotalDocSearches + summary.TotalDocOpens),
            ("Launch", summary.TotalProjectLaunches),
            ("Tasks", summary.TotalTasksCreated + summary.TotalTasksCompleted),
            ("Timer", summary.TotalTimerUses),
        };
        var topFeature = features.OrderByDescending(f => f.Count).FirstOrDefault();
        InsightTopFeatureLabel.Text = topFeature.Count > 0 ? topFeature.Name : "--";
        InsightTopFeatureLabel.ToolTip = topFeature.Count > 0 ? $"{topFeature.Name}: {topFeature.Count} uses today" : null;
    }
}
