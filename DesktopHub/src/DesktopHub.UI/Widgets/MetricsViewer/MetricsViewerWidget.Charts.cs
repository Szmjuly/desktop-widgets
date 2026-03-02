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
using WpfPolygon = System.Windows.Shapes.Polygon;
using DesktopHub.Core.Models;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DesktopHub.UI.Widgets;

// Admin chart rendering: 15 chart types and their data helpers
public partial class MetricsViewerWidget
{
    private void RenderChart(string chartType)
    {
        ChartCanvas.Children.Clear();
        ChartEmptyLabel.Visibility = Visibility.Collapsed;

        if (_filteredAdminSummaries.Count == 0)
        {
            ChartEmptyLabel.Text = "No data to chart";
            ChartEmptyLabel.Visibility = Visibility.Visible;
            return;
        }

        // Ensure canvas has a rendered size
        ChartCanvas.UpdateLayout();
        var w = ChartCanvas.ActualWidth > 0 ? ChartCanvas.ActualWidth : 280;
        var h = ChartCanvas.ActualHeight > 0 ? ChartCanvas.ActualHeight : 200;

        switch (chartType)
        {
            case "bar":             DrawBarChart(w, h, false); break;
            case "stacked_bar":     DrawStackedBarChart(w, h); break;
            case "horizontal_bar":  DrawHorizontalBarChart(w, h); break;
            case "line":            DrawLineChart(w, h, false); break;
            case "area":            DrawLineChart(w, h, true); break;
            case "pie":             DrawPieChart(w, h, false); break;
            case "donut":           DrawPieChart(w, h, true); break;
            case "heatmap":         DrawHeatMap(w, h); break;
            case "scatter":         DrawScatterChart(w, h, false); break;
            case "bubble":          DrawScatterChart(w, h, true); break;
            case "radar":           DrawRadarChart(w, h); break;
            case "matrix":          DrawMatrixChart(w, h); break;
            case "treemap":         DrawTreemapChart(w, h); break;
            case "waterfall":       DrawWaterfallChart(w, h); break;
            case "node_graph":      DrawNodeGraph(w, h); break;
            default:
                ChartEmptyLabel.Text = "Unknown chart type";
                ChartEmptyLabel.Visibility = Visibility.Visible;
                break;
        }
    }

    // --- Data helpers ---

    private Dictionary<string, int> GetDailyTotals(Func<DailyMetricsSummary, int> selector)
    {
        return _filteredAdminSummaries
            .GroupBy(s => s.Date)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => g.Sum(selector));
    }

    private Dictionary<string, int> GetCategoryTotals()
    {
        var totals = new Dictionary<string, int>();
        foreach (var (id, label, selector) in MetricDefs)
        {
            if (_selectedCategoryMetrics.Contains(id))
            {
                var val = _filteredAdminSummaries.Sum(selector);
                if (val > 0) totals[label] = val;
            }
        }
        return totals;
    }

    private void AddLabel(double x, double y, string text, double fontSize = 8, string color = "#B6C3CA")
    {
        var tb = new TextBlock { Text = text, FontSize = fontSize, Foreground = Brush(color) };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        ChartCanvas.Children.Add(tb);
    }

    // --- 1. Bar Chart (uses X metric over time) ---
    private void DrawBarChart(double w, double h, bool _unused)
    {
        var data = GetDailyTotals(GetMetricSelector(_selectedXMetric));
        if (data.Count == 0) return;

        var max = Math.Max(data.Values.Max(), 1);
        var barW = Math.Max(4, (w - 20) / data.Count - 4);
        double x = 10;

        foreach (var kvp in data)
        {
            var barH = (kvp.Value / (double)max) * (h - 30);
            var rect = new WpfRectangle
            {
                Width = barW, Height = Math.Max(2, barH),
                Fill = PaletteBrush(0),
                RadiusX = 2, RadiusY = 2
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, h - 20 - barH);
            ChartCanvas.Children.Add(rect);
            AddLabel(x, h - 14, kvp.Key.Length > 5 ? kvp.Key[5..] : kvp.Key, 7);
            x += barW + 4;
        }
    }

    // --- 2. Stacked Bar Chart (stacks X metric and Y metric per day) ---
    private void DrawStackedBarChart(double w, double h)
    {
        var xSel = GetMetricSelector(_selectedXMetric);
        var ySel = GetMetricSelector(_selectedYMetric);
        var days = _filteredAdminSummaries.GroupBy(s => s.Date).OrderBy(g => g.Key).ToList();
        if (days.Count == 0) return;

        var barW = Math.Max(4, (w - 20) / days.Count - 4);
        double x = 10;
        var maxTotal = days.Max(g => g.Sum(xSel) + g.Sum(ySel));
        if (maxTotal == 0) maxTotal = 1;

        foreach (var day in days)
        {
            var valX = day.Sum(xSel);
            var valY = day.Sum(ySel);
            double y = h - 20;

            void DrawSegment(int val, int colorIdx)
            {
                var segH = (val / (double)maxTotal) * (h - 30);
                if (segH < 1) return;
                var rect = new WpfRectangle { Width = barW, Height = segH, Fill = PaletteBrush(colorIdx), RadiusX = 1, RadiusY = 1 };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y - segH);
                ChartCanvas.Children.Add(rect);
                y -= segH;
            }

            DrawSegment(valX, 0);
            DrawSegment(valY, 1);

            AddLabel(x, h - 14, day.Key.Length > 5 ? day.Key[5..] : day.Key, 7);
            x += barW + 4;
        }

        // Legend
        AddLabel(w - 120, 2, $"\u25A0 {GetMetricLabel(_selectedXMetric)}", 8, Palette[0]);
        AddLabel(w - 120, 14, $"\u25A0 {GetMetricLabel(_selectedYMetric)}", 8, Palette[1]);
    }

    // --- 3. Horizontal Bar Chart ---
    private void DrawHorizontalBarChart(double w, double h)
    {
        var cats = GetCategoryTotals();
        if (cats.Count == 0) return;

        var max = Math.Max(cats.Values.Max(), 1);
        var barH = Math.Max(8, (h - 10) / cats.Count - 6);
        double y = 5;
        int ci = 0;

        foreach (var kvp in cats.OrderByDescending(k => k.Value))
        {
            var barW2 = (kvp.Value / (double)max) * (w - 80);
            var rect = new WpfRectangle { Width = Math.Max(2, barW2), Height = barH, Fill = PaletteBrush(ci++), RadiusX = 2, RadiusY = 2 };
            Canvas.SetLeft(rect, 60);
            Canvas.SetTop(rect, y);
            ChartCanvas.Children.Add(rect);
            AddLabel(2, y + barH / 2 - 5, kvp.Key, 8);
            AddLabel(62 + barW2, y + barH / 2 - 5, kvp.Value.ToString(), 8, "#F5F7FA");
            y += barH + 6;
        }
    }

    // --- 4 & 5. Line / Area Chart (uses X metric over time) ---
    private void DrawLineChart(double w, double h, bool fillArea)
    {
        var data = GetDailyTotals(GetMetricSelector(_selectedXMetric));
        if (data.Count < 2) { DrawBarChart(w, h, false); return; }

        var max = Math.Max(data.Values.Max(), 1);
        var points = new List<WpfPoint>();
        double stepX = (w - 20) / (data.Count - 1);
        double x = 10;

        foreach (var kvp in data)
        {
            var yVal = h - 20 - (kvp.Value / (double)max) * (h - 30);
            points.Add(new WpfPoint(x, yVal));
            x += stepX;
        }

        if (fillArea)
        {
            var pg = new PathGeometry();
            var pf = new PathFigure { StartPoint = new WpfPoint(points[0].X, h - 20), IsClosed = true, IsFilled = true };
            pf.Segments.Add(new LineSegment(points[0], false));
            for (int i = 1; i < points.Count; i++)
                pf.Segments.Add(new LineSegment(points[i], true));
            pf.Segments.Add(new LineSegment(new WpfPoint(points.Last().X, h - 20), true));
            pg.Figures.Add(pf);
            var areaPath = new WpfPath { Data = pg, Fill = PaletteBrushAlpha(0, 0x40), Stroke = PaletteBrush(0), StrokeThickness = 1.5 };
            ChartCanvas.Children.Add(areaPath);
        }
        else
        {
            var polyline = new WpfPolyline { Stroke = PaletteBrush(0), StrokeThickness = 2 };
            foreach (var p in points) polyline.Points.Add(p);
            ChartCanvas.Children.Add(polyline);
        }

        foreach (var p in points)
        {
            var dot = new WpfEllipse { Width = 5, Height = 5, Fill = PaletteBrush(0) };
            Canvas.SetLeft(dot, p.X - 2.5);
            Canvas.SetTop(dot, p.Y - 2.5);
            ChartCanvas.Children.Add(dot);
        }
    }

    // --- 6 & 7. Pie / Donut Chart ---
    private void DrawPieChart(double w, double h, bool isDonut)
    {
        var cats = GetCategoryTotals();
        if (cats.Count == 0) return;

        var total = cats.Values.Sum();
        if (total == 0) return;

        var cx = w / 2; var cy = h / 2;
        var r = Math.Min(w, h) / 2 - 20;
        double startAngle = 0;
        int ci = 0;

        foreach (var kvp in cats.OrderByDescending(k => k.Value))
        {
            var sweepAngle = (kvp.Value / (double)total) * 360;
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

            var slice = new WpfPath { Data = pg, Fill = PaletteBrush(ci), Stroke = new WpfSolidColorBrush(WpfColor.FromArgb(0x40, 0, 0, 0)), StrokeThickness = 1 };
            slice.ToolTip = $"{kvp.Key}: {kvp.Value}";
            ChartCanvas.Children.Add(slice);

            // Label
            var midAngle = (startAngle + sweepAngle / 2) * Math.PI / 180;
            var labelR = r * 0.65;
            AddLabel(cx + labelR * Math.Cos(midAngle) - 10, cy + labelR * Math.Sin(midAngle) - 5, kvp.Key, 7, "#F5F7FA");

            startAngle = endAngle;
            ci++;
        }

        if (isDonut)
        {
            var hole = new WpfEllipse { Width = r, Height = r, Fill = new WpfSolidColorBrush(WpfColor.FromArgb(0xF0, 0x12, 0x12, 0x12)) };
            Canvas.SetLeft(hole, cx - r / 2);
            Canvas.SetTop(hole, cy - r / 2);
            ChartCanvas.Children.Add(hole);
            AddLabel(cx - 15, cy - 6, total.ToString(), 12, "#F5F7FA");
        }
    }

    // --- 8. Heat Map ---
    private void DrawHeatMap(double w, double h)
    {
        var days = _filteredAdminSummaries.GroupBy(s => s.Date).OrderBy(g => g.Key).ToList();
        if (days.Count == 0) return;

        var selectedDefs = MetricDefs.Where(m => _selectedCategoryMetrics.Contains(m.Id)).ToArray();
        if (selectedDefs.Length == 0) return;

        var categories = selectedDefs.Select(m => m.Label).ToArray();
        var selectors = selectedDefs.Select(m => m.Selector).ToArray();

        var cellW = Math.Max(8, (w - 60) / days.Count);
        var cellH = Math.Max(8, (h - 20) / categories.Length);
        var allVals = days.SelectMany(d => selectors.Select(sel => d.Sum(sel))).ToList();
        var maxVal = Math.Max(allVals.Max(), 1);

        for (int row = 0; row < categories.Length; row++)
        {
            AddLabel(2, 10 + row * cellH + cellH / 2 - 5, categories[row], 7);
            for (int col = 0; col < days.Count; col++)
            {
                var val = days[col].Sum(selectors[row]);
                var intensity = (byte)(40 + (int)(val / (double)maxVal * 200));
                var rect = new WpfRectangle
                {
                    Width = cellW - 1, Height = cellH - 1,
                    Fill = new WpfSolidColorBrush(WpfColor.FromArgb(intensity, 0x00, 0x7A, 0xCC)),
                    RadiusX = 2, RadiusY = 2,
                    ToolTip = $"{categories[row]}: {val}"
                };
                Canvas.SetLeft(rect, 55 + col * cellW);
                Canvas.SetTop(rect, 10 + row * cellH);
                ChartCanvas.Children.Add(rect);
            }
        }
    }

    // --- 9 & 10. Scatter / Bubble (X metric vs Y metric) ---
    private void DrawScatterChart(double w, double h, bool isBubble)
    {
        if (_filteredAdminSummaries.Count == 0) return;

        var xSel = GetMetricSelector(_selectedXMetric);
        var ySel = GetMetricSelector(_selectedYMetric);
        var maxX = Math.Max(_filteredAdminSummaries.Max(xSel), 1);
        var maxY = Math.Max(_filteredAdminSummaries.Max(ySel), 1);
        var maxSession = isBubble ? Math.Max(_filteredAdminSummaries.Max(s => s.SessionCount), 1) : 1;

        int ci = 0;
        foreach (var s in _filteredAdminSummaries)
        {
            var x = 20 + xSel(s) / (double)maxX * (w - 40);
            var y = h - 20 - ySel(s) / (double)maxY * (h - 40);
            var size = isBubble ? 6 + s.SessionCount / (double)maxSession * 20 : 5;

            var dot = new WpfEllipse
            {
                Width = size, Height = size,
                Fill = isBubble ? PaletteBrushAlpha(ci, 0x80) : PaletteBrush(ci),
                Stroke = PaletteBrush(ci), StrokeThickness = 1,
                ToolTip = $"{s.UserName}: {GetMetricLabel(_selectedXMetric)}={xSel(s)}, {GetMetricLabel(_selectedYMetric)}={ySel(s)}"
            };
            Canvas.SetLeft(dot, x - size / 2);
            Canvas.SetTop(dot, y - size / 2);
            ChartCanvas.Children.Add(dot);
            ci++;
        }
        AddLabel(w / 2 - 30, h - 12, $"{GetMetricLabel(_selectedXMetric)} \u2192", 8);
        AddLabel(2, 2, $"\u2191 {GetMetricLabel(_selectedYMetric)}", 8);
    }

    // --- 11. Radar Chart ---
    private void DrawRadarChart(double w, double h)
    {
        var cats = GetCategoryTotals();
        if (cats.Count < 3) { DrawBarChart(w, h, false); return; }

        var keys = cats.Keys.ToList();
        var vals = cats.Values.ToList();
        var max = Math.Max(vals.Max(), 1);
        var cx = w / 2; var cy = h / 2;
        var r = Math.Min(w, h) / 2 - 25;
        var n = keys.Count;

        // Grid lines
        for (int ring = 1; ring <= 3; ring++)
        {
            var ringR = r * ring / 3.0;
            var ringPoly = new WpfPolygon { Stroke = new WpfSolidColorBrush(WpfColor.FromArgb(0x20, 0xFF, 0xFF, 0xFF)), StrokeThickness = 0.5 };
            for (int i = 0; i < n; i++)
            {
                var angle = -Math.PI / 2 + 2 * Math.PI * i / n;
                ringPoly.Points.Add(new WpfPoint(cx + ringR * Math.Cos(angle), cy + ringR * Math.Sin(angle)));
            }
            ChartCanvas.Children.Add(ringPoly);
        }

        // Data polygon
        var poly = new WpfPolygon { Stroke = PaletteBrush(0), StrokeThickness = 2, Fill = PaletteBrushAlpha(0, 0x40) };
        for (int i = 0; i < n; i++)
        {
            var angle = -Math.PI / 2 + 2 * Math.PI * i / n;
            var valR = r * vals[i] / max;
            poly.Points.Add(new WpfPoint(cx + valR * Math.Cos(angle), cy + valR * Math.Sin(angle)));

            // Axis label
            var labelR2 = r + 12;
            AddLabel(cx + labelR2 * Math.Cos(angle) - 12, cy + labelR2 * Math.Sin(angle) - 5, keys[i], 7);
        }
        ChartCanvas.Children.Add(poly);
    }

    // --- 12. Matrix Chart ---
    private void DrawMatrixChart(double w, double h)
    {
        // User x Category matrix
        var users = _filteredAdminSummaries.Select(s => s.UserName).Where(u => !string.IsNullOrEmpty(u)).Distinct().Take(8).ToList();
        if (users.Count == 0) users.Add(Environment.UserName);

        var selectedDefs = MetricDefs.Where(m => _selectedCategoryMetrics.Contains(m.Id)).ToArray();
        if (selectedDefs.Length == 0) return;

        var categories = selectedDefs.Select(m => m.Label).ToArray();
        var selectors = selectedDefs.Select(m => m.Selector).ToArray();
        var cellW = Math.Max(20, (w - 60) / categories.Length);
        var cellH = Math.Max(14, (h - 30) / (users.Count + 1));

        // Column headers
        for (int c = 0; c < categories.Length; c++)
            AddLabel(60 + c * cellW + 2, 2, categories[c], 7, "#80B6C3CA");

        for (int row = 0; row < users.Count; row++)
        {
            var user = users[row];
            var userSummaries = _filteredAdminSummaries.Where(s => s.UserName == user).ToList();
            AddLabel(2, 20 + row * cellH + 2, user, 8, "#F5F7FA");

            for (int c = 0; c < selectors.Length; c++)
            {
                var val = userSummaries.Sum(selectors[c]);
                var rect = new WpfRectangle
                {
                    Width = cellW - 2, Height = cellH - 2,
                    Fill = PaletteBrushAlpha(c, (byte)(40 + Math.Min(200, val * 20))),
                    RadiusX = 3, RadiusY = 3,
                    ToolTip = $"{user}: {categories[c]} = {val}"
                };
                Canvas.SetLeft(rect, 60 + c * cellW);
                Canvas.SetTop(rect, 20 + row * cellH);
                ChartCanvas.Children.Add(rect);
                AddLabel(60 + c * cellW + 4, 20 + row * cellH + 2, val.ToString(), 8, "#F5F7FA");
            }
        }
    }

    // --- 13. Treemap Chart ---
    private void DrawTreemapChart(double w, double h)
    {
        var cats = GetCategoryTotals().OrderByDescending(k => k.Value).ToList();
        if (cats.Count == 0) return;

        var total = cats.Sum(c => c.Value);
        if (total == 0) return;

        double x = 0, y = 0;
        bool horizontal = true;
        double remainW = w, remainH = h;
        int ci = 0;

        foreach (var kvp in cats)
        {
            var fraction = kvp.Value / (double)total;
            double rw, rh;

            if (horizontal)
            {
                rw = remainW * fraction * cats.Count / (cats.Count - ci); rh = remainH;
                if (rw > remainW) rw = remainW;
            }
            else
            {
                rw = remainW; rh = remainH * fraction * cats.Count / (cats.Count - ci);
                if (rh > remainH) rh = remainH;
            }

            rw = Math.Max(4, Math.Min(rw, remainW));
            rh = Math.Max(4, Math.Min(rh, remainH));

            var rect = new WpfRectangle
            {
                Width = rw - 2, Height = rh - 2,
                Fill = PaletteBrushAlpha(ci, 0xA0),
                RadiusX = 3, RadiusY = 3,
                ToolTip = $"{kvp.Key}: {kvp.Value}"
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            ChartCanvas.Children.Add(rect);

            if (rw > 30 && rh > 12)
                AddLabel(x + 3, y + 2, $"{kvp.Key}\n{kvp.Value}", 7, "#F5F7FA");

            if (horizontal) { x += rw; remainW -= rw; }
            else { y += rh; remainH -= rh; }

            horizontal = !horizontal;
            ci++;
        }
    }

    // --- 14. Waterfall Chart ---
    private void DrawWaterfallChart(double w, double h)
    {
        // Sum all selected category metrics per day
        var selectedSelectors = MetricDefs
            .Where(m => _selectedCategoryMetrics.Contains(m.Id))
            .Select(m => m.Selector)
            .ToArray();
        var data = _filteredAdminSummaries
            .GroupBy(s => s.Date)
            .OrderBy(g => g.Key)
            .ToDictionary(g => g.Key, g => selectedSelectors.Sum(sel => g.Sum(sel)));
        if (data.Count == 0) return;

        var vals = data.Values.ToList();
        var cumulative = new List<int> { 0 };
        foreach (var v in vals) cumulative.Add(cumulative.Last() + v);

        var maxCum = Math.Max(cumulative.Max(), 1);
        var barW = Math.Max(4, (w - 20) / data.Count - 4);
        double x = 10;
        int i = 0;

        foreach (var kvp in data)
        {
            var bottom = cumulative[i] / (double)maxCum * (h - 30);
            var top = cumulative[i + 1] / (double)maxCum * (h - 30);

            var rect = new WpfRectangle
            {
                Width = barW, Height = Math.Max(2, top - bottom),
                Fill = PaletteBrush(i % Palette.Length),
                RadiusX = 2, RadiusY = 2
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, h - 20 - top);
            ChartCanvas.Children.Add(rect);

            // Connector line
            if (i > 0)
            {
                var line = new WpfLine
                {
                    X1 = x - 4, Y1 = h - 20 - bottom,
                    X2 = x, Y2 = h - 20 - bottom,
                    Stroke = Brush("#40FFFFFF"), StrokeThickness = 1,
                    StrokeDashArray = new DoubleCollection { 2, 2 }
                };
                ChartCanvas.Children.Add(line);
            }

            AddLabel(x, h - 14, kvp.Key.Length > 5 ? kvp.Key[5..] : kvp.Key, 7);
            x += barW + 4;
            i++;
        }
    }

    // --- 15. Node Graph ---
    private void DrawNodeGraph(double w, double h)
    {
        var cats = GetCategoryTotals();
        if (cats.Count == 0) return;

        var max = Math.Max(cats.Values.Max(), 1);
        var cx = w / 2; var cy = h / 2;
        var r = Math.Min(w, h) / 2 - 30;
        var keys = cats.Keys.ToList();
        var vals = cats.Values.ToList();
        var n = keys.Count;

        // Position nodes around center
        var nodePositions = new List<WpfPoint>();
        for (int i = 0; i < n; i++)
        {
            var angle = -Math.PI / 2 + 2 * Math.PI * i / n;
            nodePositions.Add(new WpfPoint(cx + r * Math.Cos(angle), cy + r * Math.Sin(angle)));
        }

        // Draw edges (weight = value correlation)
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                var weight = Math.Min(vals[i], vals[j]);
                var opacity = (byte)(20 + weight / (double)max * 80);
                var line = new WpfLine
                {
                    X1 = nodePositions[i].X, Y1 = nodePositions[i].Y,
                    X2 = nodePositions[j].X, Y2 = nodePositions[j].Y,
                    Stroke = new WpfSolidColorBrush(WpfColor.FromArgb(opacity, 0xF5, 0xF7, 0xFA)),
                    StrokeThickness = 0.5 + weight / (double)max * 2
                };
                ChartCanvas.Children.Add(line);
            }
        }

        // Draw nodes
        for (int i = 0; i < n; i++)
        {
            var nodeSize = 10 + vals[i] / (double)max * 20;
            var node = new WpfEllipse
            {
                Width = nodeSize, Height = nodeSize,
                Fill = PaletteBrushAlpha(i, 0xC0),
                Stroke = PaletteBrush(i), StrokeThickness = 2,
                ToolTip = $"{keys[i]}: {vals[i]}"
            };
            Canvas.SetLeft(node, nodePositions[i].X - nodeSize / 2);
            Canvas.SetTop(node, nodePositions[i].Y - nodeSize / 2);
            ChartCanvas.Children.Add(node);

            AddLabel(nodePositions[i].X - 12, nodePositions[i].Y + nodeSize / 2 + 2, keys[i], 7);
        }

        // Center node
        var center = new WpfEllipse { Width = 12, Height = 12, Fill = Brush("#F5F7FA"), Stroke = Brush("#007ACC"), StrokeThickness = 2 };
        Canvas.SetLeft(center, cx - 6);
        Canvas.SetTop(center, cy - 6);
        ChartCanvas.Children.Add(center);
    }
}
