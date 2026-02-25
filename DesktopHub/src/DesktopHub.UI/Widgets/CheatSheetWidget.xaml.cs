using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopHub.Core.Models;
using DesktopHub.UI.Services;

namespace DesktopHub.UI.Widgets;

public partial class CheatSheetWidget : System.Windows.Controls.UserControl
{
    private readonly CheatSheetService _service;
    private Discipline _currentDiscipline = Discipline.Electrical;
    private CheatSheet? _activeSheet;
    private List<CheatSheet> _currentSheets = new();
    private readonly Dictionary<string, System.Windows.Controls.TextBox> _inputTextBoxes = new();
    private bool _allInputsAreDropdowns;
    private string? _selectedVoltageHeader;

    public CheatSheetWidget(CheatSheetService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _service.LoadAsync();
            RefreshSheetList();
            UpdateCodeBookLabel();
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"CheatSheetWidget.OnLoaded: Error: {ex.Message}");
        }
    }

    private void DisciplineCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || DisciplineCombo.SelectedIndex < 0) return;

        _currentDiscipline = DisciplineCombo.SelectedIndex switch
        {
            0 => Discipline.Electrical,
            1 => Discipline.Mechanical,
            2 => Discipline.Plumbing,
            3 => Discipline.FireProtection,
            _ => Discipline.Electrical
        };

        // Go back to list if viewing detail
        ShowListView();
        RefreshSheetList();
        UpdateCodeBookLabel();
    }

    private void UpdateCodeBookLabel()
    {
        var codeBooks = _service.GetCodeBooks(_currentDiscipline);
        if (codeBooks.Count > 0)
        {
            CodeBookLabel.Text = string.Join(", ", codeBooks.Select(cb => $"{cb.Name} {cb.Edition}"));
        }
        else
        {
            CodeBookLabel.Text = "No code books configured";
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ClearSearchButton.Visibility = string.IsNullOrEmpty(SearchBox.Text)
            ? Visibility.Collapsed
            : Visibility.Visible;
        RefreshSheetList();
    }

    private void ClearSearchButton_Click(object sender, MouseButtonEventArgs e)
    {
        SearchBox.Text = "";
        SearchBox.Focus();
    }

    private void RefreshSheetList()
    {
        var query = SearchBox?.Text?.Trim() ?? "";
        _currentSheets = string.IsNullOrEmpty(query)
            ? _service.GetSheets(_currentDiscipline)
            : _service.Search(_currentDiscipline, query);

        SheetListItems.Children.Clear();

        foreach (var sheet in _currentSheets)
        {
            SheetListItems.Children.Add(CreateSheetCard(sheet));
        }

        EmptyState.Visibility = _currentSheets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SheetCountLabel.Text = $"{_currentSheets.Count} sheet{(_currentSheets.Count != 1 ? "s" : "")}";

        // Show jurisdiction info if applicable
        var jurisdictions = _service.GetJurisdictions();
        var relevantJurisdictions = jurisdictions
            .Where(j => _service.GetCodeBooks(_currentDiscipline).Any(cb => cb.Id == j.AdoptedCodeBookId))
            .ToList();

        if (relevantJurisdictions.Count > 0)
        {
            JurisdictionBar.Visibility = Visibility.Visible;
            var jTexts = relevantJurisdictions.Select(j => $"{j.Name}: {j.AdoptionName}");
            JurisdictionLabel.Text = string.Join(" | ", jTexts);
        }
        else
        {
            JurisdictionBar.Visibility = Visibility.Collapsed;
        }
    }

    private Border CreateSheetCard(CheatSheet sheet)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x10, 0xF5, 0xF7, 0xFA)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 4),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var hoverBg = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0xF5, 0xF7, 0xFA));
        var normalBg = card.Background;
        card.MouseEnter += (_, _) => card.Background = hoverBg;
        card.MouseLeave += (_, _) => card.Background = normalBg;

        var stack = new StackPanel();

        // Title row
        var titleRow = new Grid();
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var title = new TextBlock
        {
            Text = sheet.Title,
            FontSize = 12,
            FontWeight = FontWeights.Medium,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF5, 0xF7, 0xFA)),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(title, 0);
        titleRow.Children.Add(title);

        // Type badge
        var typeText = sheet.SheetType switch
        {
            CheatSheetType.Table => "TABLE",
            CheatSheetType.Calculator => "CALC",
            CheatSheetType.Note => "NOTE",
            _ => ""
        };
        var typeBadge = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30,
                sheet.SheetType == CheatSheetType.Calculator ? (byte)0x4F : sheet.SheetType == CheatSheetType.Note ? (byte)0xFF : (byte)0x66,
                sheet.SheetType == CheatSheetType.Calculator ? (byte)0xC3 : sheet.SheetType == CheatSheetType.Note ? (byte)0xD5 : (byte)0xBB,
                sheet.SheetType == CheatSheetType.Calculator ? (byte)0xF7 : sheet.SheetType == CheatSheetType.Note ? (byte)0x4F : (byte)0x6A)),
            CornerRadius = new CornerRadius(3),
            Padding = new Thickness(5, 1, 5, 1),
            VerticalAlignment = VerticalAlignment.Center
        };
        typeBadge.Child = new TextBlock
        {
            Text = typeText,
            FontSize = 8,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC,
                sheet.SheetType == CheatSheetType.Calculator ? (byte)0x4F : sheet.SheetType == CheatSheetType.Note ? (byte)0xFF : (byte)0x66,
                sheet.SheetType == CheatSheetType.Calculator ? (byte)0xC3 : sheet.SheetType == CheatSheetType.Note ? (byte)0xD5 : (byte)0xBB,
                sheet.SheetType == CheatSheetType.Calculator ? (byte)0xF7 : sheet.SheetType == CheatSheetType.Note ? (byte)0x4F : (byte)0x6A))
        };
        Grid.SetColumn(typeBadge, 1);
        titleRow.Children.Add(typeBadge);

        stack.Children.Add(titleRow);

        // Subtitle
        if (!string.IsNullOrEmpty(sheet.Subtitle))
        {
            stack.Children.Add(new TextBlock
            {
                Text = sheet.Subtitle,
                FontSize = 10,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB6, 0xC3, 0xCA)),
                Margin = new Thickness(0, 2, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
        }

        // Tags
        if (sheet.Tags.Count > 0)
        {
            var tagPanel = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            foreach (var tag in sheet.Tags.Take(5))
            {
                var tagBorder = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x15, 0xF5, 0xF7, 0xFA)),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(0, 0, 3, 0)
                };
                tagBorder.Child = new TextBlock
                {
                    Text = tag,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x80, 0xF5, 0xF7, 0xFA))
                };
                tagPanel.Children.Add(tagBorder);
            }
            stack.Children.Add(tagPanel);
        }

        card.Child = stack;
        card.MouseLeftButtonDown += (_, _) => OpenSheet(sheet);
        return card;
    }

    private void OpenSheet(CheatSheet sheet)
    {
        _activeSheet = sheet;
        ShowDetailView();

        // Track cheat sheet view
        TelemetryAccessor.TrackCheatSheet(sheet.Id);
    }

    private void ShowListView()
    {
        _activeSheet = null;
        if (SheetListPanel == null || SheetDetailPanel == null) return;
        SheetListPanel.Visibility = Visibility.Visible;
        SheetDetailPanel.Visibility = Visibility.Collapsed;
    }

    private void ShowDetailView()
    {
        if (_activeSheet == null) return;

        SheetListPanel.Visibility = Visibility.Collapsed;
        SheetDetailPanel.Visibility = Visibility.Visible;

        DetailTitle.Text = _activeSheet.Title;
        DetailSubtitle.Text = _activeSheet.Subtitle ?? _activeSheet.Description ?? "";

        // Reset views
        TableView.Visibility = Visibility.Collapsed;
        NoteView.Visibility = Visibility.Collapsed;
        OutputPanel.Visibility = Visibility.Collapsed;
        InputPanel.Visibility = Visibility.Collapsed;
        FindBar.Visibility = Visibility.Collapsed;
        FindToggle.Visibility = Visibility.Collapsed;

        _inputTextBoxes.Clear();
        InputFields.Children.Clear();
        OutputFields.Children.Clear();
        _selectedVoltageHeader = null;

        switch (_activeSheet.SheetType)
        {
            case CheatSheetType.Table:
            case CheatSheetType.Calculator:
                RenderTable(_activeSheet);
                break;
            case CheatSheetType.Note:
                RenderNote(_activeSheet);
                break;
        }
    }

    private void RenderTable(CheatSheet sheet)
    {
        TableView.Visibility = Visibility.Visible;

        // Determine which input columns to show based on sheet structure
        var inputCols = sheet.Columns.Where(c => c.IsInputColumn).ToList();

        // Special handling for GEC Sizing: show CU or AL toggle, not both at once
        var isGecSheet = sheet.Id == "nec-250-66";
        if (isGecSheet && inputCols.Count == 2)
        {
            // Only show one input column at a time with a toggle
            inputCols = new List<CheatSheetColumn> { inputCols[0] }; // Start with Cu
        }

        var dropdownCount = 0;
        if (inputCols.Count > 0)
        {
            InputPanel.Visibility = Visibility.Visible;

            // For GEC sheet, add Cu/Al toggle before input fields
            if (isGecSheet)
            {
                var toggleGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                toggleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                toggleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var toggleLabel = new TextBlock
                {
                    Text = "Material:",
                    FontSize = 11,
                    Foreground = (System.Windows.Media.Brush)FindResource("DimTextBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0),
                    MinWidth = 60
                };
                Grid.SetColumn(toggleLabel, 0);
                toggleGrid.Children.Add(toggleLabel);

                var materialCombo = new System.Windows.Controls.ComboBox
                {
                    FontSize = 12,
                    Padding = new Thickness(6, 3, 6, 3),
                    Style = (System.Windows.Style)FindResource("DarkComboBox")
                };
                materialCombo.Items.Add(new ComboBoxItem { Content = "Copper (Cu)", Tag = "cu" });
                materialCombo.Items.Add(new ComboBoxItem { Content = "Aluminum (Al)", Tag = "al" });
                materialCombo.SelectedIndex = 0;
                materialCombo.SelectionChanged += (_, _) =>
                {
                    var tag = (materialCombo.SelectedItem as ComboBoxItem)?.Tag as string ?? "cu";
                    // Rebuild input fields for the other column
                    RebuildGecInput(sheet, tag == "al" ? 1 : 0);
                };
                Grid.SetColumn(materialCombo, 1);
                toggleGrid.Children.Add(materialCombo);
                InputFields.Children.Add(toggleGrid);
            }

            foreach (var col in inputCols)
            {
                dropdownCount += BuildInputField(sheet, col);
            }

            if (IsMotorFlaSheet(sheet))
            {
                BuildMotorVoltageSelector(sheet);
            }
        }

        _allInputsAreDropdowns = inputCols.Count > 0 && dropdownCount == inputCols.Count;

        // Hide lookup button and find toggle when all inputs are dropdowns (auto-lookup fires)
        if (_allInputsAreDropdowns)
        {
            LookupButton.Visibility = Visibility.Collapsed;
            FindToggle.Visibility = Visibility.Collapsed;
        }
        else
        {
            LookupButton.Visibility = Visibility.Visible;
            FindToggle.Visibility = Visibility.Visible;
        }

        // Render the data grid
        RenderDataGrid(sheet, null);
        DetailFooterLabel.Text = $"{sheet.Rows.Count} row{(sheet.Rows.Count != 1 ? "s" : "")} × {sheet.Columns.Count} col{(sheet.Columns.Count != 1 ? "s" : "")}";
    }

    /// <summary>
    /// Builds a single input field (dropdown or textbox) for a column. Returns 1 if dropdown, 0 if textbox.
    /// </summary>
    private int BuildInputField(CheatSheet sheet, CheatSheetColumn col)
    {
        var colIdx = sheet.Columns.IndexOf(col);
        var distinctValues = sheet.Rows
            .Where(r => colIdx < r.Count && !string.IsNullOrWhiteSpace(r[colIdx]))
            .Select(r => r[colIdx])
            .Distinct()
            .ToList();

        var useDropdown = distinctValues.Count > 1 && distinctValues.Count <= 30;

        var fieldGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock
        {
            Text = $"{col.Header}{(col.Unit != null ? $" ({col.Unit})" : "")}:",
            FontSize = 11,
            Foreground = (System.Windows.Media.Brush)FindResource("DimTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 60
        };
        Grid.SetColumn(label, 0);
        fieldGrid.Children.Add(label);

        var proxyBox = new System.Windows.Controls.TextBox { Visibility = Visibility.Collapsed };
        _inputTextBoxes[col.Header] = proxyBox;

        if (useDropdown)
        {
            var combo = new System.Windows.Controls.ComboBox
            {
                FontSize = 12,
                Padding = new Thickness(6, 3, 6, 3),
                Style = (System.Windows.Style)FindResource("DarkComboBox")
            };
            combo.Items.Add(new ComboBoxItem
            {
                Content = $"-- All --",
                Tag = "",
                Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush")
            });
            foreach (var val in distinctValues)
            {
                combo.Items.Add(new ComboBoxItem
                {
                    Content = col.Unit != null ? $"{val} {col.Unit}" : val,
                    Tag = val
                });
            }
            combo.SelectedIndex = 0;
            combo.SelectionChanged += (_, _) =>
            {
                var selected = (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
                proxyBox.Text = selected;
                if (string.IsNullOrEmpty(selected))
                {
                    // Reset: show all rows, hide output
                    OutputPanel.Visibility = Visibility.Collapsed;
                    if (_activeSheet != null) RenderDataGrid(_activeSheet, null);
                }
                else
                {
                    PerformLookup();
                }
            };
            Grid.SetColumn(combo, 1);
            fieldGrid.Children.Add(combo);
            fieldGrid.Children.Add(proxyBox);
            InputFields.Children.Add(fieldGrid);
            return 1;
        }
        else
        {
            var input = new System.Windows.Controls.TextBox
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                BorderThickness = new Thickness(0),
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                CaretBrush = (System.Windows.Media.Brush)FindResource("TextBrush"),
                FontSize = 12,
                Padding = new Thickness(6, 3, 6, 3)
            };
            input.KeyDown += (_, args) =>
            {
                if (args.Key == Key.Enter)
                    PerformLookup();
            };
            Grid.SetColumn(input, 1);
            fieldGrid.Children.Add(input);
            _inputTextBoxes[col.Header] = input;
            InputFields.Children.Add(fieldGrid);
            return 0;
        }
    }

    /// <summary>
    /// Rebuilds the GEC input field when the user toggles between Cu and Al.
    /// </summary>
    private void RebuildGecInput(CheatSheet sheet, int colIndex)
    {
        if (sheet.Columns.Count <= colIndex) return;
        var col = sheet.Columns[colIndex];

        // Remove all input field grids except the first (material toggle)
        while (InputFields.Children.Count > 1)
            InputFields.Children.RemoveAt(InputFields.Children.Count - 1);

        // Clear old input references for input columns
        foreach (var ic in sheet.Columns.Where(c => c.IsInputColumn))
            _inputTextBoxes.Remove(ic.Header);

        // Hide output when switching
        OutputPanel.Visibility = Visibility.Collapsed;

        BuildInputField(sheet, col);

        // Reset table to all rows
        RenderDataGrid(sheet, null);
    }

    private void RenderDataGrid(CheatSheet sheet, List<int>? visibleRowIndices)
    {
        TableContainer.Children.Clear();

        var grid = new Grid { HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };

        // Define columns with equal Star widths to fill container
        for (var c = 0; c < sheet.Columns.Count; c++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        }

        // Header row
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var c = 0; c < sheet.Columns.Count; c++)
        {
            var col = sheet.Columns[c];
            var headerBorder = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("TableHeaderBrush"),
                Padding = new Thickness(8, 5, 8, 5),
                BorderBrush = (System.Windows.Media.Brush)FindResource("HoverBrush"),
                BorderThickness = new Thickness(0, 0, c < sheet.Columns.Count - 1 ? 1 : 0, 1)
            };

            var headerText = col.Header;
            if (col.Unit != null) headerText += $"\n({col.Unit})";

            headerBorder.Child = new TextBlock
            {
                Text = headerText,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = col.IsInputColumn
                    ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                    : col.IsOutputColumn
                        ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x66, 0xBB, 0x6A))
                        : (System.Windows.Media.Brush)FindResource("TextBrush"),
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };

            Grid.SetRow(headerBorder, 0);
            Grid.SetColumn(headerBorder, c);
            grid.Children.Add(headerBorder);
        }

        // Data rows
        var rowsToShow = visibleRowIndices ?? Enumerable.Range(0, sheet.Rows.Count).ToList();
        for (var ri = 0; ri < rowsToShow.Count; ri++)
        {
            var rowIdx = rowsToShow[ri];
            if (rowIdx < 0 || rowIdx >= sheet.Rows.Count) continue;

            var row = sheet.Rows[rowIdx];
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var gridRow = ri + 1;

            for (var c = 0; c < sheet.Columns.Count && c < row.Count; c++)
            {
                var isAlt = ri % 2 == 1;
                var cellBorder = new Border
                {
                    Background = isAlt
                        ? (System.Windows.Media.Brush)FindResource("TableRowAltBrush")
                        : System.Windows.Media.Brushes.Transparent,
                    Padding = new Thickness(8, 4, 8, 4),
                    BorderBrush = (System.Windows.Media.Brush)FindResource("HoverBrush"),
                    BorderThickness = new Thickness(0, 0, c < sheet.Columns.Count - 1 ? 1 : 0, 0)
                };

                cellBorder.Child = new TextBlock
                {
                    Text = row[c],
                    FontSize = 11,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                    TextAlignment = TextAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
                };

                Grid.SetRow(cellBorder, gridRow);
                Grid.SetColumn(cellBorder, c);
                grid.Children.Add(cellBorder);
            }
        }

        var outerBorder = new Border
        {
            Background = (System.Windows.Media.Brush)FindResource("SurfaceBrush"),
            CornerRadius = new CornerRadius(6),
            BorderBrush = (System.Windows.Media.Brush)FindResource("HoverBrush"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(2),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
        };
        outerBorder.Child = grid;
        TableContainer.Children.Add(outerBorder);
    }

    private void RenderNote(CheatSheet sheet)
    {
        NoteView.Visibility = Visibility.Visible;
        NoteContent.Text = sheet.NoteContent ?? "(No content)";

        // Show jurisdiction info if applicable
        if (!string.IsNullOrEmpty(sheet.JurisdictionId))
        {
            var jurisdiction = _service.GetJurisdictions()
                .FirstOrDefault(j => j.JurisdictionId == sheet.JurisdictionId);
            if (jurisdiction != null)
            {
                DetailSubtitle.Text = $"{sheet.Subtitle} — {jurisdiction.Name}";
            }
        }

        DetailFooterLabel.Text = sheet.SheetType == CheatSheetType.Note ? "Note" : "";
    }

    private void CopyNoteButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (_activeSheet?.NoteContent == null) return;
        try
        {
            System.Windows.Clipboard.SetText(_activeSheet.NoteContent);
            CopyNoteLabel.Text = "\u2714 Copied!";
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) =>
            {
                CopyNoteLabel.Text = "\U0001F4CB Copy";
                timer.Stop();
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"CheatSheetWidget.CopyNote: {ex.Message}");
        }
    }

    private void BackButton_Click(object sender, MouseButtonEventArgs e)
    {
        ShowListView();
        RefreshSheetList();
    }

    private void LookupButton_Click(object sender, MouseButtonEventArgs e)
    {
        PerformLookup();
    }

    private void PerformLookup()
    {
        if (_activeSheet == null) return;

        var inputs = new Dictionary<string, string>();
        foreach (var kvp in _inputTextBoxes)
        {
            var val = kvp.Value.Text?.Trim() ?? "";
            if (!string.IsNullOrEmpty(val))
                inputs[kvp.Key] = val;
        }

        if (IsMotorFlaSheet(_activeSheet) && string.IsNullOrWhiteSpace(_selectedVoltageHeader))
        {
            var hasOtherInputs = inputs.Count > 0;
            if (hasOtherInputs)
            {
                var resultsWithoutOutput = _service.Lookup(_activeSheet, inputs);
                OutputPanel.Visibility = Visibility.Collapsed;
                if (resultsWithoutOutput.Count > 0)
                    RenderDataGrid(_activeSheet, GetMatchedRowIndicesStrict(_activeSheet, inputs));
                else
                    RenderDataGrid(_activeSheet, null);
                return;
            }
        }

        if (inputs.Count == 0)
        {
            // Show all rows
            RenderDataGrid(_activeSheet, null);
            OutputPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var results = _service.Lookup(_activeSheet, inputs);

        OutputFields.Children.Clear();

        if (results.Count == 0)
        {
            OutputPanel.Visibility = Visibility.Visible;
            OutputFields.Children.Add(new TextBlock
            {
                Text = "No matching rows found",
                FontSize = 11,
                Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush"),
                FontStyle = FontStyles.Italic
            });

            // Still show full table
            RenderDataGrid(_activeSheet, null);
            return;
        }

        OutputPanel.Visibility = Visibility.Visible;

        // Show output columns from matched rows
        var outputCols = _activeSheet.Columns.Where(c => c.IsOutputColumn).ToList();
        if (IsMotorFlaSheet(_activeSheet) && !string.IsNullOrWhiteSpace(_selectedVoltageHeader))
        {
            outputCols = outputCols
                .Where(c => c.Header.Equals(_selectedVoltageHeader, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
        foreach (var result in results)
        {
            var resultPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 6) };
            foreach (var col in outputCols)
            {
                if (result.TryGetValue(col.Header, out var val))
                {
                    var row = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 1, 0, 1) };
                    row.Children.Add(new TextBlock
                    {
                        Text = $"{col.Header}: ",
                        FontSize = 11,
                        Foreground = (System.Windows.Media.Brush)FindResource("DimTextBrush")
                    });
                    row.Children.Add(new TextBlock
                    {
                        Text = val + (col.Unit != null ? $" {col.Unit}" : ""),
                        FontSize = 11,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x66, 0xBB, 0x6A))
                    });
                    resultPanel.Children.Add(row);
                }
            }
            OutputFields.Children.Add(resultPanel);
        }

        // Highlight matching rows in the table
        var matchedRowIndices = GetMatchedRowIndicesStrict(_activeSheet, inputs);
        RenderDataGrid(_activeSheet, matchedRowIndices.Count > 0 ? matchedRowIndices : null);
    }

    private static bool IsMotorFlaSheet(CheatSheet sheet)
        => sheet.Id == "motor-fla-1ph" || sheet.Id == "motor-fla-3ph";

    private void BuildMotorVoltageSelector(CheatSheet sheet)
    {
        var voltageCols = sheet.Columns.Where(c => c.IsOutputColumn).ToList();
        if (voltageCols.Count == 0)
            return;

        var fieldGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        fieldGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var label = new TextBlock
        {
            Text = "Voltage:",
            FontSize = 11,
            Foreground = (System.Windows.Media.Brush)FindResource("DimTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 60
        };
        Grid.SetColumn(label, 0);
        fieldGrid.Children.Add(label);

        var combo = new System.Windows.Controls.ComboBox
        {
            FontSize = 12,
            Padding = new Thickness(6, 3, 6, 3),
            Style = (System.Windows.Style)FindResource("DarkComboBox")
        };
        combo.Items.Add(new ComboBoxItem
        {
            Content = "-- Select Voltage --",
            Tag = "",
            Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush")
        });
        foreach (var vc in voltageCols)
        {
            combo.Items.Add(new ComboBoxItem { Content = vc.Header, Tag = vc.Header });
        }
        combo.SelectedIndex = 0;
        combo.SelectionChanged += (_, _) =>
        {
            _selectedVoltageHeader = (combo.SelectedItem as ComboBoxItem)?.Tag as string;
            if (string.IsNullOrWhiteSpace(_selectedVoltageHeader))
            {
                OutputPanel.Visibility = Visibility.Collapsed;
                if (_activeSheet != null)
                    RenderDataGrid(_activeSheet, null);
                return;
            }
            PerformLookup();
        };
        Grid.SetColumn(combo, 1);
        fieldGrid.Children.Add(combo);

        InputFields.Children.Add(fieldGrid);
    }

    private List<int> GetMatchedRowIndicesStrict(CheatSheet sheet, Dictionary<string, string> inputs)
    {
        var matchedRowIndices = new List<int>();
        for (var i = 0; i < sheet.Rows.Count; i++)
        {
            var row = sheet.Rows[i];
            var match = true;
            foreach (var input in inputs)
            {
                var colIdx = sheet.Columns.FindIndex(c =>
                    c.Header.Equals(input.Key, StringComparison.OrdinalIgnoreCase));
                if (colIdx < 0 || colIdx >= row.Count)
                {
                    match = false;
                    break;
                }

                if (!CellMatchesStrict(row[colIdx], input.Value))
                {
                    match = false;
                    break;
                }
            }

            if (match)
                matchedRowIndices.Add(i);
        }

        return matchedRowIndices;
    }

    private static bool CellMatchesStrict(string cellValue, string inputValue)
    {
        if (string.IsNullOrWhiteSpace(inputValue))
            return true;

        var normalizedInput = NormalizeQueryForSearchLocal(inputValue.Trim());

        if (cellValue.Equals(inputValue, StringComparison.OrdinalIgnoreCase))
            return true;
        if (!normalizedInput.Equals(inputValue, StringComparison.OrdinalIgnoreCase) &&
            cellValue.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase))
            return true;

        if (TryParseFlexibleNumberLocal(cellValue, out var cellNum) && TryParseFlexibleNumberLocal(normalizedInput, out var inputNum))
            return Math.Abs(cellNum - inputNum) < 0.001;

        return false;
    }

    private static string NormalizeQueryForSearchLocal(string query)
    {
        var q = query.Trim();
        var units = new[] { "kcmil", "kva", "kw", "kwh", "hp", " a", "amp", "amps", "volt", "volts",
                            "v", "w", "hz", "pf", "va", "mva", "mw", "awg" };
        foreach (var unit in units)
        {
            if (q.EndsWith(unit, StringComparison.OrdinalIgnoreCase))
            {
                var stripped = q[..^unit.Length].TrimEnd();
                if (stripped.Length > 0)
                    return stripped;
            }
        }
        return q;
    }

    private static bool TryParseFlexibleNumberLocal(string raw, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var s = raw.Trim();
        if (double.TryParse(s, out value))
            return true;

        var dashParts = s.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (dashParts.Length == 2 &&
            double.TryParse(dashParts[0], out var whole) &&
            TryParseFractionLocal(dashParts[1], out var frac))
        {
            value = whole + frac;
            return true;
        }

        return TryParseFractionLocal(s, out value);
    }

    private static bool TryParseFractionLocal(string raw, out double value)
    {
        value = 0;
        var parts = raw.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;

        if (!double.TryParse(parts[0], out var num) || !double.TryParse(parts[1], out var den))
            return false;
        if (Math.Abs(den) < 0.0000001)
            return false;

        value = num / den;
        return true;
    }

    private void FindToggle_Click(object sender, MouseButtonEventArgs e)
    {
        FindBar.Visibility = FindBar.Visibility == Visibility.Visible
            ? Visibility.Collapsed
            : Visibility.Visible;

        if (FindBar.Visibility == Visibility.Visible)
        {
            FindBox.Focus();
        }
        else
        {
            FindBox.Text = "";
            if (_activeSheet != null)
                RenderDataGrid(_activeSheet, null);
        }
    }

    private void FindBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_activeSheet == null) return;

        var text = FindBox.Text?.Trim() ?? "";
        var matchingRows = _service.FindInTable(_activeSheet, text);

        FindCount.Text = string.IsNullOrEmpty(text)
            ? ""
            : $"{matchingRows.Count}/{_activeSheet.Rows.Count}";

        RenderDataGrid(_activeSheet, matchingRows);
    }
}
