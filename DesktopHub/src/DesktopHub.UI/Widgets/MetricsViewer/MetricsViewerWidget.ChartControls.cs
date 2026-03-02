using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using DesktopHub.Core.Models;
using WpfColor = System.Windows.Media.Color;
using WpfColorConverter = System.Windows.Media.ColorConverter;
using WpfSolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DesktopHub.UI.Widgets;

// Chart type buttons, metric selectors (axis pills), highlighting
public partial class MetricsViewerWidget
{
    private void BuildChartTypeButtons()
    {
        ChartTypeButtons.Children.Clear();
        foreach (var (id, label, icon) in ChartTypes)
        {
            var capturedId = id;
            var btn = new Border
            {
                Background = new WpfSolidColorBrush(WpfColor.FromArgb(0x10, 0xF5, 0xF7, 0xFA)),
                BorderBrush = new WpfSolidColorBrush(WpfColor.FromArgb(0x30, 0xF5, 0xF7, 0xFA)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(10, 5, 10, 5),
                Margin = new Thickness(0, 0, 5, 5),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = label,
                Tag = id
            };

            var tb = new TextBlock
            {
                Text = $"{icon} {label}",
                FontSize = 11,
                Foreground = Brush("#B6C3CA")
            };
            btn.Child = tb;

            btn.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                _selectedChartType = capturedId;
                HighlightSelectedChartButton(capturedId);
                BuildMetricSelector(capturedId);
                RenderChart(capturedId);
            };

            ChartTypeButtons.Children.Add(btn);
        }
    }

    private void BuildAxisButtons()
    {
        // Initial build — show a prompt until a chart is selected
        BuildMetricSelector(string.Empty);
    }

    private void BuildMetricSelector(string chartType)
    {
        MetricSelectorPanel.Children.Clear();

        if (string.IsNullOrEmpty(chartType))
        {
            MetricSelectorPanel.Children.Add(new TextBlock
            {
                Text = "Select a chart type to configure metrics",
                FontSize = 10, FontStyle = FontStyles.Italic,
                Foreground = Brush("#6B7A85")
            });
            return;
        }

        var mode = GetChartMode(chartType);

        switch (mode)
        {
            case ChartMetricMode.Single:
                BuildSingleMetricRow();
                break;
            case ChartMetricMode.Dual:
                BuildDualMetricRows();
                break;
            case ChartMetricMode.Category:
                BuildCategoryMetricRow(chartType);
                break;
        }
    }

    private void BuildSingleMetricRow()
    {
        var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        row.Children.Add(new TextBlock
        {
            Text = "Metric:",
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#B6C3CA"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        _xAxisButtons = new WrapPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        foreach (var (id, label, _) in MetricDefs)
            _xAxisButtons.Children.Add(MakeAxisPill(id, label, true));
        row.Children.Add(_xAxisButtons);

        MetricSelectorPanel.Children.Add(row);
        HighlightAxisPanel(_xAxisButtons, _selectedXMetric, "#42A5F5");
    }

    private void BuildDualMetricRows()
    {
        // X Axis row
        var xRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
        xRow.Children.Add(new TextBlock
        {
            Text = "X Axis:",
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#42A5F5"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        _xAxisButtons = new WrapPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        foreach (var (id, label, _) in MetricDefs)
            _xAxisButtons.Children.Add(MakeAxisPill(id, label, true));
        xRow.Children.Add(_xAxisButtons);
        MetricSelectorPanel.Children.Add(xRow);

        // Y Axis row
        var yRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        yRow.Children.Add(new TextBlock
        {
            Text = "Y Axis:",
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#66BB6A"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        _yAxisButtons = new WrapPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        foreach (var (id, label, _) in MetricDefs)
            _yAxisButtons.Children.Add(MakeAxisPill(id, label, false));
        yRow.Children.Add(_yAxisButtons);
        MetricSelectorPanel.Children.Add(yRow);

        HighlightAxisPanel(_xAxisButtons, _selectedXMetric, "#42A5F5");
        HighlightAxisPanel(_yAxisButtons, _selectedYMetric, "#66BB6A");
    }

    private void BuildCategoryMetricRow(string chartType)
    {
        var desc = chartType switch
        {
            "pie" or "donut" => "Select metrics to include in slices:",
            "radar" => "Select metrics for radar axes:",
            "node_graph" => "Select metrics to show as nodes:",
            "heatmap" => "Select metrics for heat map rows:",
            "matrix" => "Select metrics for matrix columns:",
            "treemap" => "Select metrics for treemap areas:",
            "waterfall" => "Select metrics for waterfall steps:",
            _ => "Select metrics to include:"
        };

        MetricSelectorPanel.Children.Add(new TextBlock
        {
            Text = desc,
            FontSize = 10,
            Foreground = Brush("#8899A6"),
            Margin = new Thickness(0, 0, 0, 6)
        });

        _categoryButtons = new WrapPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        foreach (var (id, label, _) in MetricDefs)
        {
            var capturedId = id;
            var btn = new Border
            {
                Background = _selectedCategoryMetrics.Contains(id)
                    ? new WpfSolidColorBrush(WpfColor.FromArgb(0x30, 0x00, 0x7A, 0xCC))
                    : new WpfSolidColorBrush(WpfColor.FromArgb(0x10, 0xF5, 0xF7, 0xFA)),
                BorderBrush = _selectedCategoryMetrics.Contains(id)
                    ? Brush("#007ACC")
                    : new WpfSolidColorBrush(WpfColor.FromArgb(0x20, 0xF5, 0xF7, 0xFA)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(0, 0, 5, 5),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = id
            };
            btn.Child = new TextBlock
            {
                Text = label,
                FontSize = 10,
                Foreground = _selectedCategoryMetrics.Contains(id) ? Brush("#42A5F5") : Brush("#B6C3CA")
            };
            btn.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                if (_selectedCategoryMetrics.Contains(capturedId))
                {
                    if (_selectedCategoryMetrics.Count > 1)
                        _selectedCategoryMetrics.Remove(capturedId);
                }
                else
                {
                    _selectedCategoryMetrics.Add(capturedId);
                }
                HighlightCategoryButtons();
                if (!string.IsNullOrEmpty(_selectedChartType))
                    RenderChart(_selectedChartType);
            };
            _categoryButtons.Children.Add(btn);
        }
        MetricSelectorPanel.Children.Add(_categoryButtons);
    }

    private Border MakeAxisPill(string metricId, string label, bool isXAxis)
    {
        var btn = new Border
        {
            Background = new WpfSolidColorBrush(WpfColor.FromArgb(0x10, 0xF5, 0xF7, 0xFA)),
            BorderBrush = new WpfSolidColorBrush(WpfColor.FromArgb(0x20, 0xF5, 0xF7, 0xFA)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(8, 4, 8, 4),
            Margin = new Thickness(0, 0, 5, 5),
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = metricId
        };
        btn.Child = new TextBlock { Text = label, FontSize = 10, Foreground = Brush("#B6C3CA") };
        btn.MouseLeftButtonDown += (s, e) =>
        {
            e.Handled = true;
            if (isXAxis) _selectedXMetric = metricId;
            else _selectedYMetric = metricId;
            HighlightActiveAxisButtons();
            if (!string.IsNullOrEmpty(_selectedChartType))
                RenderChart(_selectedChartType);
        };
        return btn;
    }

    private void HighlightActiveAxisButtons()
    {
        if (_xAxisButtons != null)
            HighlightAxisPanel(_xAxisButtons, _selectedXMetric, "#42A5F5");
        if (_yAxisButtons != null)
            HighlightAxisPanel(_yAxisButtons, _selectedYMetric, "#66BB6A");
    }

    private void HighlightCategoryButtons()
    {
        if (_categoryButtons == null) return;
        var accent = (WpfColor)WpfColorConverter.ConvertFromString("#007ACC");
        foreach (var child in _categoryButtons.Children)
        {
            if (child is Border b && b.Tag is string id)
            {
                var isOn = _selectedCategoryMetrics.Contains(id);
                b.Background = isOn
                    ? new WpfSolidColorBrush(WpfColor.FromArgb(0x30, accent.R, accent.G, accent.B))
                    : new WpfSolidColorBrush(WpfColor.FromArgb(0x10, 0xF5, 0xF7, 0xFA));
                b.BorderBrush = isOn
                    ? new WpfSolidColorBrush(accent)
                    : new WpfSolidColorBrush(WpfColor.FromArgb(0x20, 0xF5, 0xF7, 0xFA));
                if (b.Child is TextBlock tb)
                    tb.Foreground = isOn ? Brush("#42A5F5") : Brush("#B6C3CA");
            }
        }
    }

    private static void HighlightAxisPanel(WrapPanel panel, string selectedId, string accentHex)
    {
        var accent = (WpfColor)WpfColorConverter.ConvertFromString(accentHex);
        foreach (var child in panel.Children)
        {
            if (child is Border b && b.Tag is string id)
            {
                var isSelected = id == selectedId;
                b.Background = isSelected
                    ? new WpfSolidColorBrush(WpfColor.FromArgb(0x30, accent.R, accent.G, accent.B))
                    : new WpfSolidColorBrush(WpfColor.FromArgb(0x10, 0xF5, 0xF7, 0xFA));
                b.BorderBrush = isSelected
                    ? new WpfSolidColorBrush(accent)
                    : new WpfSolidColorBrush(WpfColor.FromArgb(0x20, 0xF5, 0xF7, 0xFA));
                if (b.Child is TextBlock tb)
                    tb.Foreground = isSelected ? new WpfSolidColorBrush(accent) : Brush("#B6C3CA");
            }
        }
    }

    private Func<DailyMetricsSummary, int> GetMetricSelector(string metricId)
    {
        foreach (var (id, _, sel) in MetricDefs)
            if (id == metricId) return sel;
        return s => s.TotalSearches;
    }

    private string GetMetricLabel(string metricId)
    {
        foreach (var (id, label, _) in MetricDefs)
            if (id == metricId) return label;
        return metricId;
    }

    private void HighlightSelectedChartButton(string selectedId)
    {
        foreach (var child in ChartTypeButtons.Children)
        {
            if (child is Border b && b.Tag is string id)
            {
                b.Background = id == selectedId
                    ? new WpfSolidColorBrush(WpfColor.FromArgb(0x40, 0x00, 0x7A, 0xCC))
                    : new WpfSolidColorBrush(WpfColor.FromArgb(0x10, 0xF5, 0xF7, 0xFA));
                b.BorderBrush = id == selectedId
                    ? Brush("#007ACC")
                    : new WpfSolidColorBrush(WpfColor.FromArgb(0x30, 0xF5, 0xF7, 0xFA));
            }
        }
    }
}
