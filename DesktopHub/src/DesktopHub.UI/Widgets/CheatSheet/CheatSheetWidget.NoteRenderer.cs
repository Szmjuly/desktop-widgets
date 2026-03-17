using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using DesktopHub.Core.Models;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace DesktopHub.UI.Widgets;

public partial class CheatSheetWidget
{
    private enum NoteMode { Text, Interactive, Visual }
    private NoteMode _noteMode = NoteMode.Text;
    private readonly Dictionary<string, double> _guideValues = new();
    private readonly Dictionary<string, TextBlock> _guideOutputLabels = new();
    private readonly Dictionary<string, System.Windows.Controls.TextBox> _guideInputBoxes = new();

    // ───────────────────── Tab switching ─────────────────────

    private void NoteTabText_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => SwitchNoteMode(NoteMode.Text);
    private void NoteTabInteractive_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => SwitchNoteMode(NoteMode.Interactive);
    private void NoteTabVisual_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) => SwitchNoteMode(NoteMode.Visual);

    private void SwitchNoteMode(NoteMode mode)
    {
        _noteMode = mode;
        var sheet = _activeSheet;
        if (sheet == null) return;

        var accent = FindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue;
        var surface = FindResource("SurfaceBrush") as Brush ?? Brushes.Transparent;
        var textSec = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;

        // Update tab visuals
        NoteTabText.Background = mode == NoteMode.Text ? accent : surface;
        NoteTabTextLabel.Foreground = mode == NoteMode.Text ? Brushes.White : textSec;

        NoteTabInteractive.Background = mode == NoteMode.Interactive ? accent : surface;
        NoteTabInteractiveLabel.Foreground = mode == NoteMode.Interactive ? Brushes.White : textSec;

        NoteTabVisual.Background = mode == NoteMode.Visual ? accent : surface;
        NoteTabVisualLabel.Foreground = mode == NoteMode.Visual ? Brushes.White : textSec;

        // Show/hide the default content (table or note) vs steps-based panels
        var showDefault = mode == NoteMode.Text;
        if (sheet.SheetType == CheatSheetType.Note)
        {
            NoteView.Visibility = showDefault ? Visibility.Visible : Visibility.Collapsed;
        }
        else
        {
            // Table/Calculator sheets: restore based on the sheet's resolved layout.
            // CompactLookup supports Lookup/Table toggle; FullTable/SimpleList are always Table-only.
            var hasLookupMode = _activeLayout == CheatSheetLayout.CompactLookup;

            ViewModeToggle.Visibility = showDefault && hasLookupMode ? Visibility.Visible : Visibility.Collapsed;
            LookupModePanel.Visibility = showDefault && hasLookupMode && _isLookupMode ? Visibility.Visible : Visibility.Collapsed;

            var showTable = showDefault && (!hasLookupMode || !_isLookupMode);
            TableModePanel.Visibility = showTable ? Visibility.Visible : Visibility.Collapsed;

            // When returning to Text mode, the DataGrid can be empty because it was collapsed while
            // in Interactive/Visual. Re-render to restore content.
            if (showTable)
                RenderDataGrid(sheet, _tableSystemFilter == null ? null : GetFilteredRowIndices());
        }

        NoteInteractivePanel.Visibility = mode == NoteMode.Interactive ? Visibility.Visible : Visibility.Collapsed;
        NoteVisualPanel.Visibility = mode == NoteMode.Visual ? Visibility.Visible : Visibility.Collapsed;

        // Render if switching to Interactive or Visual for the first time
        if (mode == NoteMode.Interactive && NoteInteractivePanel.Children.Count == 0)
            RenderNoteInteractive(sheet);
        if (mode == NoteMode.Visual && NoteVisualPanel.Children.Count == 0)
            RenderNoteVisual(sheet);

        // Update footer
        var baseLabel = sheet.SheetType == CheatSheetType.Note ? "Note" : "Table";
        DetailFooterLabel.Text = mode switch
        {
            NoteMode.Text => baseLabel,
            NoteMode.Interactive => $"{baseLabel} \u2014 Interactive",
            NoteMode.Visual => $"{baseLabel} \u2014 Visual",
            _ => baseLabel
        };
    }

    // ───────────────────── INTERACTIVE MODE ─────────────────────

    private void RenderNoteInteractive(CheatSheet sheet)
    {
        NoteInteractivePanel.Children.Clear();
        _guideValues.Clear();
        _guideOutputLabels.Clear();
        _guideInputBoxes.Clear();

        var accent = FindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue;
        var surface = FindResource("SurfaceBrush") as Brush ?? Brushes.Transparent;
        var textBrush = FindResource("TextBrush") as Brush ?? Brushes.White;
        var textSec = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;
        var borderSub = FindResource("BorderSubtleBrush") as Brush ?? Brushes.DimGray;

        // Seed default values
        foreach (var step in sheet.Steps)
            foreach (var f in step.Fields)
                if (!string.IsNullOrEmpty(f.Default) && double.TryParse(f.Default, out var dv))
                    _guideValues[f.Id] = dv;

        foreach (var step in sheet.Steps)
        {
            // Step card
            var card = new Border
            {
                Background = surface,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 8)
            };
            var cardStack = new StackPanel();
            card.Child = cardStack;

            // Header: step number badge + title
            var header = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            header.Children.Add(new Border
            {
                Background = accent,
                CornerRadius = new CornerRadius(10),
                Width = 20, Height = 20,
                Margin = new Thickness(0, 0, 8, 0),
                Child = new TextBlock
                {
                    Text = step.Number.ToString(),
                    FontSize = 10, FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                }
            });
            header.Children.Add(new TextBlock
            {
                Text = step.Title,
                FontSize = 12, FontWeight = FontWeights.SemiBold,
                Foreground = textBrush,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            });
            cardStack.Children.Add(header);

            // Description
            if (!string.IsNullOrEmpty(step.Description))
            {
                cardStack.Children.Add(new TextBlock
                {
                    Text = step.Description,
                    FontSize = 10.5, Foreground = textSec,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(28, 0, 0, 6)
                });
            }

            // Fields
            foreach (var field in step.Fields)
            {
                var row = new Grid { Margin = new Thickness(28, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                // Label
                var label = new TextBlock
                {
                    Text = field.Label,
                    FontSize = 11, Foreground = textBrush,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                Grid.SetColumn(label, 0);
                row.Children.Add(label);

                if (field.IsOutput)
                {
                    // Computed output
                    var outputLabel = new TextBlock
                    {
                        FontSize = 11, FontWeight = FontWeights.SemiBold,
                        Foreground = accent,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center
                    };
                    Grid.SetColumn(outputLabel, 1);
                    row.Children.Add(outputLabel);
                    _guideOutputLabels[field.Id] = outputLabel;
                }
                else
                {
                    // User input
                    var tb = new System.Windows.Controls.TextBox
                    {
                        Text = field.Default ?? "",
                        FontSize = 11,
                        Width = 80,
                        Background = new SolidColorBrush(Color.FromArgb(30, 100, 160, 255)),
                        Foreground = textBrush,
                        CaretBrush = textBrush,
                        BorderBrush = borderSub,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Padding = new Thickness(4, 2, 4, 2),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                        HorizontalContentAlignment = System.Windows.HorizontalAlignment.Right,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center
                    };
                    if (!string.IsNullOrEmpty(field.Hint))
                        tb.ToolTip = field.Hint;

                    var fieldId = field.Id;
                    tb.TextChanged += (_, _) =>
                    {
                        if (double.TryParse(tb.Text, out var val))
                            _guideValues[fieldId] = val;
                        else
                            _guideValues.Remove(fieldId);
                        RecalculateGuideOutputs();
                    };

                    Grid.SetColumn(tb, 1);
                    row.Children.Add(tb);
                    _guideInputBoxes[field.Id] = tb;
                }

                // Unit label
                if (!string.IsNullOrEmpty(field.Unit))
                {
                    var unit = new TextBlock
                    {
                        Text = field.Unit,
                        FontSize = 9, Foreground = textSec,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 0, 0)
                    };
                    Grid.SetColumn(unit, 2);
                    row.Children.Add(unit);
                }

                cardStack.Children.Add(row);
            }

            // Tip callout
            if (!string.IsNullOrEmpty(step.Tip))
            {
                var tipBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(20, 255, 200, 50)),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 4, 8, 4),
                    Margin = new Thickness(28, 6, 0, 0)
                };
                tipBorder.Child = new TextBlock
                {
                    Text = "\U0001F4A1 " + step.Tip,
                    FontSize = 10, Foreground = textSec,
                    TextWrapping = TextWrapping.Wrap,
                    FontStyle = FontStyles.Italic
                };
                cardStack.Children.Add(tipBorder);
            }

            // Reference link
            if (!string.IsNullOrEmpty(step.Reference))
            {
                var refId = step.Reference;
                var refLink = new TextBlock
                {
                    Text = "\u2192 See related sheet",
                    FontSize = 10, Foreground = accent,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Margin = new Thickness(28, 4, 0, 0)
                };
                refLink.MouseLeftButtonDown += (_, _) =>
                {
                    var target = _service.GetSheets(_currentDiscipline).FirstOrDefault(s => s.Id == refId);
                    if (target != null) OpenSheet(target);
                };
                cardStack.Children.Add(refLink);
            }

            NoteInteractivePanel.Children.Add(card);
        }

        // Initial calculation
        RecalculateGuideOutputs();
    }

    private void RecalculateGuideOutputs()
    {
        if (_activeSheet == null) return;

        // First pass: compute all outputs in step order
        foreach (var step in _activeSheet.Steps)
        {
            foreach (var field in step.Fields)
            {
                if (!field.IsOutput || string.IsNullOrEmpty(field.Formula)) continue;
                var result = EvaluateFormula(field.Formula, _guideValues);
                _guideValues[field.Id] = result;
            }
        }

        // Second pass: update labels
        foreach (var step in _activeSheet.Steps)
        {
            foreach (var field in step.Fields)
            {
                if (!field.IsOutput) continue;
                if (!_guideOutputLabels.TryGetValue(field.Id, out var label)) continue;

                if (_guideValues.TryGetValue(field.Id, out var val))
                {
                    label.Text = val.ToString("F2");

                    // Highlight
                    if (field.Highlight == "positive-negative")
                    {
                        label.Foreground = val >= 0
                            ? new SolidColorBrush(Color.FromRgb(80, 200, 120))
                            : new SolidColorBrush(Color.FromRgb(255, 90, 90));
                        label.FontWeight = FontWeights.Bold;
                    }
                }
                else
                {
                    label.Text = "\u2014";
                }
            }
        }
    }

    // ───────────────────── VISUAL MODE ─────────────────────

    private void RenderNoteVisual(CheatSheet sheet)
    {
        NoteVisualPanel.Children.Clear();

        var accent = FindResource("AccentBrush") as Brush ?? Brushes.DodgerBlue;
        var surface = FindResource("SurfaceBrush") as Brush ?? Brushes.Transparent;
        var textBrush = FindResource("TextBrush") as Brush ?? Brushes.White;
        var textSec = FindResource("TextSecondaryBrush") as Brush ?? Brushes.Gray;

        // Title header
        var titleCard = new Border
        {
            Background = accent,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 10, 14, 10),
            Margin = new Thickness(0, 0, 0, 4)
        };
        var titleStack = new StackPanel();
        titleStack.Children.Add(new TextBlock
        {
            Text = sheet.Title,
            FontSize = 14, FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        });
        if (!string.IsNullOrEmpty(sheet.Description))
        {
            titleStack.Children.Add(new TextBlock
            {
                Text = sheet.Description,
                FontSize = 10, Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 0)
            });
        }
        titleCard.Child = titleStack;
        NoteVisualPanel.Children.Add(titleCard);

        var totalSteps = sheet.Steps.Count;
        for (var i = 0; i < totalSteps; i++)
        {
            var step = sheet.Steps[i];
            var isLast = i == totalSteps - 1;

            // Stepper row: left indicator + right content
            var row = new Grid { Margin = new Thickness(0, 0, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left column: circle + connecting line
            var leftPanel = new Grid();

            // Connecting line (except for last)
            if (!isLast)
            {
                var line = new Border
                {
                    Width = 2,
                    Background = new SolidColorBrush(Color.FromArgb(60, 100, 160, 255)),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                    Margin = new Thickness(0, 12, 0, 0)
                };
                leftPanel.Children.Add(line);
            }

            // Circle
            var circle = new Border
            {
                Width = 24, Height = 24,
                CornerRadius = new CornerRadius(12),
                Background = accent,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Child = new TextBlock
                {
                    Text = step.Icon ?? step.Number.ToString(),
                    FontSize = step.Icon != null ? 12 : 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                }
            };
            leftPanel.Children.Add(circle);
            Grid.SetColumn(leftPanel, 0);
            row.Children.Add(leftPanel);

            // Right column: content card
            var content = new Border
            {
                Background = surface,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(4, 0, 0, 8)
            };
            var contentStack = new StackPanel();

            // Step title
            contentStack.Children.Add(new TextBlock
            {
                Text = step.Title,
                FontSize = 11.5, FontWeight = FontWeights.SemiBold,
                Foreground = textBrush
            });

            // Description
            if (!string.IsNullOrEmpty(step.Description))
            {
                contentStack.Children.Add(new TextBlock
                {
                    Text = step.Description,
                    FontSize = 10, Foreground = textSec,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 2, 0, 0)
                });
            }

            // Fields as compact key-value pairs
            if (step.Fields.Count > 0)
            {
                var fieldGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
                fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var fieldRow = 0;
                foreach (var field in step.Fields)
                {
                    fieldGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    // Field label
                    var fLabel = new TextBlock
                    {
                        Text = field.Label,
                        FontSize = 10,
                        Foreground = field.IsOutput ? accent : textSec,
                        FontWeight = field.IsOutput ? FontWeights.SemiBold : FontWeights.Normal,
                        Margin = new Thickness(0, 1, 8, 1)
                    };
                    Grid.SetRow(fLabel, fieldRow);
                    Grid.SetColumn(fLabel, 0);
                    fieldGrid.Children.Add(fLabel);

                    // Field value
                    string displayVal;
                    Brush valColor;
                    if (field.IsOutput)
                    {
                        var computed = _guideValues.TryGetValue(field.Id, out var cv) ? cv : EvaluateFormula(field.Formula ?? "0", _guideValues);
                        _guideValues[field.Id] = computed;
                        displayVal = computed.ToString("F2");
                        if (field.Highlight == "positive-negative")
                            valColor = computed >= 0
                                ? new SolidColorBrush(Color.FromRgb(80, 200, 120))
                                : new SolidColorBrush(Color.FromRgb(255, 90, 90));
                        else
                            valColor = accent;
                    }
                    else
                    {
                        displayVal = field.Default ?? "\u2014";
                        valColor = textBrush;
                    }

                    var unitSuffix = !string.IsNullOrEmpty(field.Unit) ? $" {field.Unit}" : "";
                    var fValue = new TextBlock
                    {
                        Text = displayVal + unitSuffix,
                        FontSize = 10,
                        Foreground = valColor,
                        FontWeight = field.IsOutput ? FontWeights.Bold : FontWeights.Normal,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                        Margin = new Thickness(0, 1, 0, 1)
                    };
                    Grid.SetRow(fValue, fieldRow);
                    Grid.SetColumn(fValue, 1);
                    fieldGrid.Children.Add(fValue);

                    fieldRow++;
                }

                contentStack.Children.Add(fieldGrid);
            }

            // Tip
            if (!string.IsNullOrEmpty(step.Tip))
            {
                contentStack.Children.Add(new TextBlock
                {
                    Text = "\U0001F4A1 " + step.Tip,
                    FontSize = 9.5,
                    Foreground = new SolidColorBrush(Color.FromRgb(220, 190, 80)),
                    FontStyle = FontStyles.Italic,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 4, 0, 0)
                });
            }

            content.Child = contentStack;
            Grid.SetColumn(content, 1);
            row.Children.Add(content);

            NoteVisualPanel.Children.Add(row);
        }

        // Summary card at bottom
        var summaryFields = sheet.Steps
            .SelectMany(s => s.Fields)
            .Where(f => f.IsOutput && f.Highlight == "positive-negative")
            .ToList();

        if (summaryFields.Count > 0)
        {
            var summary = new Border
            {
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 4, 0, 0)
            };

            var summaryStack = new StackPanel();
            summaryStack.Children.Add(new TextBlock
            {
                Text = "RESULT",
                FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                Margin = new Thickness(0, 0, 0, 4)
            });

            var allPositive = true;
            foreach (var sf in summaryFields)
            {
                var val = _guideValues.TryGetValue(sf.Id, out var v) ? v : 0;
                if (val < 0) allPositive = false;
                var unitSuffix = !string.IsNullOrEmpty(sf.Unit) ? $" {sf.Unit}" : "";
                summaryStack.Children.Add(new TextBlock
                {
                    Text = $"{sf.Label}: {val:F2}{unitSuffix}",
                    FontSize = 13, FontWeight = FontWeights.Bold,
                    Foreground = Brushes.White
                });
            }

            summary.Background = allPositive
                ? new SolidColorBrush(Color.FromRgb(40, 140, 80))
                : new SolidColorBrush(Color.FromRgb(180, 50, 50));

            summaryStack.Children.Add(new TextBlock
            {
                Text = allPositive ? "\u2705 Adequate pressure" : "\u274C Booster pump likely required",
                FontSize = 11, Foreground = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)),
                Margin = new Thickness(0, 4, 0, 0)
            });

            summary.Child = summaryStack;
            NoteVisualPanel.Children.Add(summary);
        }
    }

    // ───────────────────── Formula evaluator ─────────────────────

    private static double EvaluateFormula(string formula, Dictionary<string, double> values)
    {
        // Replace {field_id} with numeric values
        var expr = Regex.Replace(formula, @"\{(\w+)\}", m =>
        {
            return values.TryGetValue(m.Groups[1].Value, out var v) ? v.ToString("G") : "0";
        });

        try
        {
            var pos = 0;
            var result = ParseAddSub(expr.Replace(" ", ""), ref pos);
            return double.IsNaN(result) || double.IsInfinity(result) ? 0 : result;
        }
        catch
        {
            return 0;
        }
    }

    private static double ParseAddSub(string s, ref int pos)
    {
        var result = ParseMulDiv(s, ref pos);
        while (pos < s.Length && (s[pos] == '+' || s[pos] == '-'))
        {
            var op = s[pos++];
            var right = ParseMulDiv(s, ref pos);
            result = op == '+' ? result + right : result - right;
        }
        return result;
    }

    private static double ParseMulDiv(string s, ref int pos)
    {
        var result = ParseAtom(s, ref pos);
        while (pos < s.Length && (s[pos] == '*' || s[pos] == '/'))
        {
            var op = s[pos++];
            var right = ParseAtom(s, ref pos);
            result = op == '*' ? result * right : (right != 0 ? result / right : 0);
        }
        return result;
    }

    private static double ParseAtom(string s, ref int pos)
    {
        // Handle unary minus
        if (pos < s.Length && s[pos] == '-')
        {
            pos++;
            return -ParseAtom(s, ref pos);
        }
        // Handle parentheses
        if (pos < s.Length && s[pos] == '(')
        {
            pos++;
            var result = ParseAddSub(s, ref pos);
            if (pos < s.Length && s[pos] == ')') pos++;
            return result;
        }
        // Parse number
        var start = pos;
        while (pos < s.Length && (char.IsDigit(s[pos]) || s[pos] == '.')) pos++;
        return start < pos && double.TryParse(s[start..pos], System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : 0;
    }
}
