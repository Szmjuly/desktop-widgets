using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Firebase;
using DesktopHub.UI.Services;

namespace DesktopHub.UI.Widgets;

public partial class CheatSheetWidget : System.Windows.Controls.UserControl
{
    private readonly CheatSheetService _service;
    private readonly ISettingsService? _settings;
    private readonly ICheatSheetDataService? _dataService;
    private readonly IFirebaseService? _firebaseService;
    private Discipline _currentDiscipline = Discipline.Electrical;
    private CheatSheet? _activeSheet;
    private List<CheatSheet> _currentSheets = new();
    private readonly Dictionary<string, System.Windows.Controls.TextBox> _inputTextBoxes = new();
    private readonly Dictionary<string, System.Windows.Controls.ComboBox> _inputCombos = new();
    private bool _allInputsAreDropdowns;
    private bool _isUpdatingCascade;
    private string? _selectedVoltageHeader;
    private bool _crossDisciplineVisible = true;
    private bool _isLookupMode = true;
    private CheatSheetLayout _activeLayout;
    private bool _isEditor;
    private bool _isAdmin;

    /// <summary>
    /// Fired when the widget wants the hosting overlay to resize.
    /// The double payload is the desired width in device-independent pixels.
    /// </summary>
    public event Action<double>? DesiredWidthChanged;

    /// <summary>Default/list-view width for the overlay.</summary>
    private const double ListViewWidth = 420;

    public CheatSheetWidget(CheatSheetService service, ISettingsService? settings = null,
        ICheatSheetDataService? dataService = null, IFirebaseService? firebaseService = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _settings = settings;
        _dataService = dataService;
        _firebaseService = firebaseService;
        _crossDisciplineVisible = settings?.GetCheatSheetCrossDisciplineSearch() ?? true;
        InitializeComponent();
        Loaded += OnLoaded;

        // Subscribe to data service updates for live refresh
        if (_dataService != null)
            _dataService.DataUpdated += OnDataServiceUpdated;
    }

    private void OnDataServiceUpdated()
    {
        try
        {
            Dispatcher.BeginInvoke(() =>
            {
                RefreshSheetList();
                UpdateCodeBookLabel();
            });
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"CheatSheetWidget.OnDataServiceUpdated: {ex.Message}");
        }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await _service.LoadAsync();
            RefreshSheetList();
            UpdateCodeBookLabel();

            // Check editor permissions in background
            _ = CheckEditorPermissionsAsync();
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"CheatSheetWidget.OnLoaded: Error: {ex.Message}");
        }
    }

    private async Task CheckEditorPermissionsAsync()
    {
        try
        {
            if (_firebaseService == null || !_firebaseService.IsInitialized)
                return;

            _isEditor = await _firebaseService.IsCheatSheetEditorAsync();
            _isAdmin = await _firebaseService.IsUserAdminAsync();

            DebugLogger.Log($"CheatSheetWidget: Editor={_isEditor}, Admin={_isAdmin}");

            // Update UI visibility on dispatcher thread
            await Dispatcher.InvokeAsync(() =>
            {
                UpdateEditorButtonVisibility();
            });
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"CheatSheetWidget: Editor permission check failed: {ex.Message}");
        }
    }

    private void UpdateEditorButtonVisibility()
    {
        // These buttons are added dynamically — check if they exist before setting visibility
        if (AddSheetButton != null)
            AddSheetButton.Visibility = _isEditor ? Visibility.Visible : Visibility.Collapsed;
        if (EditSheetButton != null)
            EditSheetButton.Visibility = (_isEditor && _activeSheet != null) ? Visibility.Visible : Visibility.Collapsed;
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
        CrossDisciplinePanel.Visibility = Visibility.Collapsed;
        CrossDisciplineItems.Children.Clear();

        foreach (var sheet in _currentSheets)
        {
            SheetListItems.Children.Add(CreateSheetCard(sheet));
        }

        var isEmpty = _currentSheets.Count == 0;
        EmptyState.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
        SheetCountLabel.Text = $"{_currentSheets.Count} sheet{(_currentSheets.Count != 1 ? "s" : "")}";

        // Cross-discipline search: when current discipline has no results and user is searching
        if (isEmpty && !string.IsNullOrEmpty(query) && (_settings?.GetCheatSheetCrossDisciplineSearch() ?? true))
        {
            var crossResults = _service.SearchAllDisciplines(query);
            crossResults.Remove(_currentDiscipline);

            if (crossResults.Count > 0)
            {
                CrossDisciplinePanel.Visibility = Visibility.Visible;
                CrossDisciplineItems.Visibility = _crossDisciplineVisible ? Visibility.Visible : Visibility.Collapsed;
                CrossDisciplineToggleLabel.Text = _crossDisciplineVisible ? "Hide" : "Show";

                var totalCross = 0;
                foreach (var kvp in crossResults)
                {
                    var disciplineName = kvp.Key.ToString();
                    // Discipline header
                    CrossDisciplineItems.Children.Add(new TextBlock
                    {
                        Text = disciplineName.ToUpper(),
                        FontSize = 9,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = Helpers.ThemeHelper.GoldDark,
                        Margin = new Thickness(0, totalCross > 0 ? 8 : 0, 0, 4)
                    });

                    foreach (var sheet in kvp.Value.Take(5))
                    {
                        var card = CreateCrossDisciplineCard(sheet, kvp.Key);
                        CrossDisciplineItems.Children.Add(card);
                        totalCross++;
                    }

                    if (kvp.Value.Count > 5)
                    {
                        CrossDisciplineItems.Children.Add(new TextBlock
                        {
                            Text = $"  +{kvp.Value.Count - 5} more in {disciplineName}",
                            FontSize = 10,
                            Foreground = Helpers.ThemeHelper.TextTertiary,
                            Margin = new Thickness(0, 2, 0, 0)
                        });
                    }
                }
            }
        }

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

    private Border CreateCrossDisciplineCard(CheatSheet sheet, Discipline discipline)
    {
        var card = new Border
        {
            Background = Helpers.ThemeHelper.GoldBackground,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 5, 8, 5),
            Margin = new Thickness(0, 0, 0, 3),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var hoverBg = Helpers.ThemeHelper.GoldBackground;
        var normalBg = card.Background;
        card.MouseEnter += (_, _) => card.Background = hoverBg;
        card.MouseLeave += (_, _) => card.Background = normalBg;

        var stack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        stack.Children.Add(new TextBlock
        {
            Text = sheet.Title,
            FontSize = 11,
            Foreground = Helpers.ThemeHelper.TextPrimary,
            VerticalAlignment = VerticalAlignment.Center
        });

        card.Child = stack;
        card.MouseLeftButtonDown += (_, _) =>
        {
            // Switch to the sheet's discipline and open it
            var disciplineIndex = discipline switch
            {
                Discipline.Electrical => 0,
                Discipline.Mechanical => 1,
                Discipline.Plumbing => 2,
                Discipline.FireProtection => 3,
                _ => 0
            };
            DisciplineCombo.SelectedIndex = disciplineIndex;
            OpenSheet(sheet);
        };

        return card;
    }

    private void CrossDisciplineToggle_Click(object sender, MouseButtonEventArgs e)
    {
        _crossDisciplineVisible = !_crossDisciplineVisible;
        CrossDisciplineItems.Visibility = _crossDisciplineVisible ? Visibility.Visible : Visibility.Collapsed;
        CrossDisciplineToggleLabel.Text = _crossDisciplineVisible ? "Hide" : "Show";
    }

    private Border CreateSheetCard(CheatSheet sheet)
    {
        var card = new Border
        {
            Background = Helpers.ThemeHelper.Hover,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 8, 10, 8),
            Margin = new Thickness(0, 0, 0, 4),
            Cursor = System.Windows.Input.Cursors.Hand
        };

        var hoverBg = Helpers.ThemeHelper.HoverMedium;
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
            Foreground = Helpers.ThemeHelper.TextPrimary,
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
                Foreground = Helpers.ThemeHelper.TextSecondary,
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
                    Background = Helpers.ThemeHelper.FaintOverlay,
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(0, 0, 3, 0)
                };
                tagBorder.Child = new TextBlock
                {
                    Text = tag,
                    FontSize = 9,
                    Foreground = Helpers.ThemeHelper.TextTertiary
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
        DesiredWidthChanged?.Invoke(ListViewWidth);
    }

    private void ShowDetailView()
    {
        if (_activeSheet == null) return;

        SheetListPanel.Visibility = Visibility.Collapsed;
        SheetDetailPanel.Visibility = Visibility.Visible;

        DetailTitle.Text = _activeSheet.Title;
        DetailSubtitle.Text = _activeSheet.Subtitle ?? _activeSheet.Description ?? "";

        // Reset all mode panels
        LookupModePanel.Visibility = Visibility.Collapsed;
        TableModePanel.Visibility = Visibility.Collapsed;
        TableView.Visibility = Visibility.Collapsed;
        NoteView.Visibility = Visibility.Collapsed;
        NoteInteractivePanel.Visibility = Visibility.Collapsed;
        NoteVisualPanel.Visibility = Visibility.Collapsed;
        OutputPanel.Visibility = Visibility.Collapsed;
        ViewModeToggle.Visibility = Visibility.Collapsed;
        NoteModeTabs.Visibility = Visibility.Collapsed;
        EditSheetButton.Visibility = (_isEditor && _dataService != null) ? Visibility.Visible : Visibility.Collapsed;

        _inputTextBoxes.Clear();
        _inputCombos.Clear();
        InputFields.Children.Clear();
        OutputFields.Children.Clear();
        _selectedVoltageHeader = null;
        _lastOutputText = null;
        _noteMode = NoteMode.Text;

        // Show Steps mode tabs for any sheet type that has structured steps
        var hasSteps = _activeSheet.Steps != null && _activeSheet.Steps.Count > 0;
        if (hasSteps)
        {
            NoteModeTabs.Visibility = Visibility.Visible;
            NoteTabTextLabel.Text = _activeSheet.SheetType == CheatSheetType.Note ? "Text" : "Table";
            var accent = FindResource("AccentBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.DodgerBlue;
            var surface = FindResource("SurfaceBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Transparent;
            var textSec = FindResource("TextSecondaryBrush") as System.Windows.Media.Brush ?? System.Windows.Media.Brushes.Gray;
            NoteTabText.Background = accent;
            NoteTabTextLabel.Foreground = System.Windows.Media.Brushes.White;
            NoteTabInteractive.Background = surface;
            NoteTabInteractiveLabel.Foreground = textSec;
            NoteTabVisual.Background = surface;
            NoteTabVisualLabel.Foreground = textSec;
            NoteInteractivePanel.Children.Clear();
            NoteVisualPanel.Children.Clear();
        }

        switch (_activeSheet.SheetType)
        {
            case CheatSheetType.Table:
            case CheatSheetType.Calculator:
                RenderTable(_activeSheet);
                break;
            case CheatSheetType.Note:
                NoteView.Visibility = Visibility.Visible;
                RenderNote(_activeSheet);
                DesiredWidthChanged?.Invoke(ListViewWidth);
                break;
        }
    }

    private void SetViewMode(bool lookupMode)
    {
        _isLookupMode = lookupMode;

        if (lookupMode)
        {
            LookupModePanel.Visibility = Visibility.Visible;
            TableModePanel.Visibility = Visibility.Collapsed;
            ViewModeLabel.Text = "Table";

            // GEC sheet uses the calculator only — no input sizer panel
            InputPanel.Visibility = _activeSheet?.Id == "nec-250-66"
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
        else
        {
            LookupModePanel.Visibility = Visibility.Collapsed;
            TableModePanel.Visibility = Visibility.Visible;
            TableView.Visibility = Visibility.Visible;
            ViewModeLabel.Text = "Lookup";
            if (_activeSheet != null)
                RenderDataGrid(_activeSheet, null);
        }

        if (_activeSheet != null)
            DetailFooterLabel.Text = $"{_activeSheet.Rows.Count} row{(_activeSheet.Rows.Count != 1 ? "s" : "")} \u00d7 {_activeSheet.Columns.Count} col{(_activeSheet.Columns.Count != 1 ? "s" : "")}";
    }

    private void ViewModeToggle_Click(object sender, MouseButtonEventArgs e)
    {
        SetViewMode(!_isLookupMode);
    }

    /// <summary>
    /// Computes the ideal overlay width for a given sheet based on its column structure.
    /// </summary>
    private double ComputeIdealWidth(CheatSheet sheet)
    {
        var colCount = sheet.Columns.Count;
        if (colCount <= 2) return 400;
        if (colCount <= 3) return 440;
        if (colCount <= 4) return 480;
        if (colCount <= 5) return 540;
        // 6+ columns (e.g. WSFU, 3-phase motor FLA with 7 cols)
        return Math.Min(680, 400 + colCount * 45);
    }

    /// <summary>
    /// Resolve effective layout: Auto infers from column structure.
    /// Any table with at least 1 input and 1 output column gets CompactLookup (lookup + table toggle).
    /// </summary>
    private static CheatSheetLayout ResolveLayout(CheatSheet sheet)
    {
        if (sheet.Layout != CheatSheetLayout.Auto)
            return sheet.Layout;

        var inputCount = sheet.Columns.Count(c => c.IsInputColumn);
        var outputCount = sheet.Columns.Count(c => c.IsOutputColumn);

        // Any table with input→output columns benefits from lookup mode
        if (inputCount >= 1 && outputCount >= 1)
            return CheatSheetLayout.CompactLookup;

        // No clear input/output distinction — plain table or simple list
        if (sheet.Columns.Count <= 2)
            return CheatSheetLayout.SimpleList;

        return CheatSheetLayout.FullTable;
    }

    private void RenderTable(CheatSheet sheet)
    {
        _activeLayout = ResolveLayout(sheet);
        var hasLookupMode = _activeLayout == CheatSheetLayout.CompactLookup;

        // Determine which input columns to show based on sheet structure
        var inputCols = sheet.Columns.Where(c => c.IsInputColumn).ToList();

        // GEC sheet: the calculator handles both single conduit and parallel sizing,
        // so skip the redundant table sizer and show only the calculator.
        // InputPanel/OutputPanel stay collapsed (reset by ShowSheetDetail) since no lookup fields are built.
        if (sheet.Id == "nec-250-66")
        {
            BuildGecCalculator();
            ViewModeToggle.Visibility = Visibility.Visible;
            DesiredWidthChanged?.Invoke(ComputeIdealWidth(sheet));
            SetViewMode(true);
            return;
        }

        GecCalculatorPanel.Visibility = Visibility.Collapsed;

        // Build input fields for Lookup mode
        if (hasLookupMode && inputCols.Count > 0)
        {
            var dropdownCount = 0;
            foreach (var col in inputCols)
                dropdownCount += BuildInputField(sheet, col);

            if (IsMotorFlaSheet(sheet))
                BuildMotorVoltageSelector(sheet);

            _allInputsAreDropdowns = dropdownCount == inputCols.Count;
            LookupButton.Visibility = _allInputsAreDropdowns ? Visibility.Collapsed : Visibility.Visible;
        }

        // Show view mode toggle for CompactLookup sheets; FullTable/SimpleList go straight to table
        if (hasLookupMode)
        {
            ViewModeToggle.Visibility = Visibility.Visible;
            DesiredWidthChanged?.Invoke(ComputeIdealWidth(sheet));
            SetViewMode(true); // Start in Lookup mode
        }
        else
        {
            ViewModeToggle.Visibility = Visibility.Collapsed;
            // Go straight to Table mode
            TableModePanel.Visibility = Visibility.Visible;
            TableView.Visibility = Visibility.Visible;
            DesiredWidthChanged?.Invoke(ComputeIdealWidth(sheet));
            RenderDataGrid(sheet, null);
        }

        DetailFooterLabel.Text = $"{sheet.Rows.Count} row{(sheet.Rows.Count != 1 ? "s" : "")} \u00d7 {sheet.Columns.Count} col{(sheet.Columns.Count != 1 ? "s" : "")}";
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
                Style = (System.Windows.Style)FindResource("DarkComboBox"),
                Tag = col.Header
            };
            PopulateComboOptions(combo, col, distinctValues);
            _inputCombos[col.Header] = combo;

            combo.SelectionChanged += (_, _) =>
            {
                if (_isUpdatingCascade) return;
                var selected = (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
                proxyBox.Text = selected;

                // Cascade: update other dropdowns based on current selections
                RefreshCascadingDropdowns(sheet);

                if (string.IsNullOrEmpty(selected))
                {
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
                Background = Helpers.ThemeHelper.HoverMedium,
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

    private void PopulateComboOptions(System.Windows.Controls.ComboBox combo, CheatSheetColumn col, List<string> values)
    {
        combo.Items.Clear();
        combo.Items.Add(new ComboBoxItem
        {
            Content = "-- All --",
            Tag = "",
            Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush")
        });
        foreach (var val in values)
        {
            combo.Items.Add(new ComboBoxItem
            {
                Content = col.Unit != null ? $"{val} {col.Unit}" : val,
                Tag = val
            });
        }
        combo.SelectedIndex = 0;
    }

    /// <summary>
    /// Cascading filter: when a dropdown selection changes, update other dropdowns
    /// to only show values that exist in rows matching the current selections.
    /// </summary>
    private void RefreshCascadingDropdowns(CheatSheet sheet)
    {
        if (_isUpdatingCascade || _activeSheet == null) return;
        _isUpdatingCascade = true;

        try
        {
            // Gather current selections
            var selections = new Dictionary<string, string>();
            foreach (var kvp in _inputCombos)
            {
                var selected = (kvp.Value.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
                if (!string.IsNullOrEmpty(selected))
                    selections[kvp.Key] = selected;
            }

            // For each dropdown, compute valid values given OTHER dropdowns' selections
            foreach (var kvp in _inputCombos)
            {
                var header = kvp.Key;
                var combo = kvp.Value;
                var col = sheet.Columns.FirstOrDefault(c => c.Header == header);
                if (col == null) continue;

                var colIdx = sheet.Columns.IndexOf(col);
                var currentSelection = (combo.SelectedItem as ComboBoxItem)?.Tag as string ?? "";

                // Filter rows by all OTHER selections
                var filteredRows = sheet.Rows.AsEnumerable();
                foreach (var otherKvp in selections)
                {
                    if (otherKvp.Key == header) continue;
                    var otherCol = sheet.Columns.FirstOrDefault(c => c.Header == otherKvp.Key);
                    if (otherCol == null) continue;
                    var otherIdx = sheet.Columns.IndexOf(otherCol);
                    filteredRows = filteredRows.Where(r => otherIdx < r.Count && r[otherIdx] == otherKvp.Value);
                }

                var validValues = filteredRows
                    .Where(r => colIdx < r.Count && !string.IsNullOrWhiteSpace(r[colIdx]))
                    .Select(r => r[colIdx])
                    .Distinct()
                    .ToList();

                PopulateComboOptions(combo, col, validValues);

                // Restore selection if still valid
                if (!string.IsNullOrEmpty(currentSelection))
                {
                    for (var i = 1; i < combo.Items.Count; i++)
                    {
                        if ((combo.Items[i] as ComboBoxItem)?.Tag as string == currentSelection)
                        {
                            combo.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }
        }
        finally
        {
            _isUpdatingCascade = false;
        }
    }

    // --- GEC Parallel Conductor Calculator ---

    /// <summary>
    /// AWG/kcmil wire sizes mapped to circular mil area, in order from smallest to largest.
    /// Used for NEC 250.66(B) parallel conductor GEC sizing.
    /// </summary>
    private static readonly (string Label, double Cmil)[] WireSizeCmil = new (string, double)[]
    {
        ("14",    4_110.0),
        ("12",    6_530.0),
        ("10",   10_380.0),
        ("8",    16_510.0),
        ("6",    26_240.0),
        ("4",    41_740.0),
        ("3",    52_620.0),
        ("2",    66_360.0),
        ("1",    83_690.0),
        ("1/0",  105_600.0),
        ("2/0",  133_100.0),
        ("3/0",  167_800.0),
        ("4/0",  211_600.0),
        ("250",  250_000.0),
        ("300",  300_000.0),
        ("350",  350_000.0),
        ("400",  400_000.0),
        ("500",  500_000.0),
        ("600",  600_000.0),
        ("700",  700_000.0),
        ("750",  750_000.0),
        ("800",  800_000.0),
        ("900",  900_000.0),
        ("1000", 1_000_000.0),
        ("1100", 1_100_000.0),
        ("1250", 1_250_000.0),
        ("1500", 1_500_000.0),
        ("1750", 1_750_000.0),
        ("2000", 2_000_000.0),
    };

    /// <summary>
    /// NEC Table 250.66 thresholds — upper cmil boundary (inclusive) for each row.
    /// Cu and Al have separate threshold columns. Index matches the GEC table row order.
    /// </summary>
    private static readonly (double CuMax, double AlMax, string GecCu, string GecAl)[] GecTableThresholds = new[]
    {
        // Row 0: Cu "2 or smaller" (≤66,360), Al "1/0 or smaller" (≤105,600) → GEC Cu 8, Al 6
        (  66_360.0,  105_600.0, "8",   "6"   ),
        // Row 1: Cu "1 or 1/0" (≤105,600), Al "2/0 or 3/0" (≤167,800) → GEC Cu 6, Al 4
        ( 105_600.0,  167_800.0, "6",   "4"   ),
        // Row 2: Cu "2/0 or 3/0" (≤167,800), Al "4/0 or 250" (≤250,000) → GEC Cu 4, Al 2
        ( 167_800.0,  250_000.0, "4",   "2"   ),
        // Row 3: Cu "Over 3/0 thru 350" (≤350,000), Al "Over 250 thru 500" (≤500,000) → GEC Cu 2, Al 1/0
        ( 350_000.0,  500_000.0, "2",   "1/0" ),
        // Row 4: Cu "Over 350 thru 600" (≤600,000), Al "Over 500 thru 900" (≤900,000) → GEC Cu 1/0, Al 3/0
        ( 600_000.0,  900_000.0, "1/0", "3/0" ),
        // Row 5: Cu "Over 600 thru 1100" (≤1,100,000), Al "Over 900 thru 1750" (≤1,750,000) → GEC Cu 2/0, Al 4/0
        (1_100_000.0,1_750_000.0, "2/0", "4/0" ),
        // Row 6: Cu "Over 1100" (>1,100,000), Al "Over 1750" (>1,750,000) → GEC Cu 3/0, Al 250
        (double.MaxValue, double.MaxValue, "3/0", "250" ),
    };

    private System.Windows.Controls.ComboBox? _gecCalcSizeCombo;
    private System.Windows.Controls.ComboBox? _gecCalcSetsCombo;
    private System.Windows.Controls.ComboBox? _gecCalcMaterialCombo;

    /// <summary>
    /// Builds the GEC calculator input fields (conductor size, # of sets, material).
    /// Called from RenderTable when the GEC sheet is detected.
    /// </summary>
    private void BuildGecCalculator()
    {
        GecCalcInputFields.Children.Clear();
        GecCalcOutputPanel.Visibility = Visibility.Collapsed;
        GecCalcOutputFields.Children.Clear();

        // Material selector
        var matGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        matGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        matGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var matLabel = new TextBlock
        {
            Text = "Conductor Material:",
            FontSize = 11,
            Foreground = (System.Windows.Media.Brush)FindResource("DimTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 60
        };
        Grid.SetColumn(matLabel, 0);
        matGrid.Children.Add(matLabel);

        _gecCalcMaterialCombo = new System.Windows.Controls.ComboBox
        {
            FontSize = 12,
            Padding = new Thickness(6, 3, 6, 3),
            Style = (System.Windows.Style)FindResource("DarkComboBox")
        };
        _gecCalcMaterialCombo.Items.Add(new ComboBoxItem { Content = "Copper (Cu)", Tag = "cu" });
        _gecCalcMaterialCombo.Items.Add(new ComboBoxItem { Content = "Aluminum (Al)", Tag = "al" });
        _gecCalcMaterialCombo.SelectedIndex = 0;
        _gecCalcMaterialCombo.SelectionChanged += (_, _) => PerformGecCalc();
        Grid.SetColumn(_gecCalcMaterialCombo, 1);
        matGrid.Children.Add(_gecCalcMaterialCombo);
        GecCalcInputFields.Children.Add(matGrid);

        // Conductor size selector
        var sizeGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        sizeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        sizeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var sizeLabel = new TextBlock
        {
            Text = "Conductor Size:",
            FontSize = 11,
            Foreground = (System.Windows.Media.Brush)FindResource("DimTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 60
        };
        Grid.SetColumn(sizeLabel, 0);
        sizeGrid.Children.Add(sizeLabel);

        _gecCalcSizeCombo = new System.Windows.Controls.ComboBox
        {
            FontSize = 12,
            Padding = new Thickness(6, 3, 6, 3),
            Style = (System.Windows.Style)FindResource("DarkComboBox")
        };
        _gecCalcSizeCombo.Items.Add(new ComboBoxItem
        {
            Content = "-- Select Size --",
            Tag = "",
            Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush")
        });
        foreach (var (label, cmil) in WireSizeCmil)
        {
            var displayText = cmil >= 250_000 ? $"{label} kcmil" : $"#{label} AWG";
            _gecCalcSizeCombo.Items.Add(new ComboBoxItem { Content = displayText, Tag = label });
        }
        _gecCalcSizeCombo.SelectedIndex = 0;
        _gecCalcSizeCombo.SelectionChanged += (_, _) => PerformGecCalc();
        Grid.SetColumn(_gecCalcSizeCombo, 1);
        sizeGrid.Children.Add(_gecCalcSizeCombo);
        GecCalcInputFields.Children.Add(sizeGrid);

        // Number of parallel sets
        var setsGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        setsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
        setsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var setsLabel = new TextBlock
        {
            Text = "Number of Sets:",
            FontSize = 11,
            Foreground = (System.Windows.Media.Brush)FindResource("DimTextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            MinWidth = 60
        };
        Grid.SetColumn(setsLabel, 0);
        setsGrid.Children.Add(setsLabel);

        _gecCalcSetsCombo = new System.Windows.Controls.ComboBox
        {
            FontSize = 12,
            Padding = new Thickness(6, 3, 6, 3),
            Style = (System.Windows.Style)FindResource("DarkComboBox")
        };
        for (var i = 1; i <= 6; i++)
        {
            _gecCalcSetsCombo.Items.Add(new ComboBoxItem
            {
                Content = i == 1 ? "1 (single run)" : $"{i} sets",
                Tag = i.ToString()
            });
        }
        _gecCalcSetsCombo.SelectedIndex = 0; // default to 1 set (single run); increase for parallel
        _gecCalcSetsCombo.SelectionChanged += (_, _) => PerformGecCalc();
        Grid.SetColumn(_gecCalcSetsCombo, 1);
        setsGrid.Children.Add(_gecCalcSetsCombo);
        GecCalcInputFields.Children.Add(setsGrid);

        BuildGecRefTable();
        GecRefTablePanel.Visibility = Visibility.Collapsed;
        GecRefToggleChevron.Text = "\u25BC";

        GecCalculatorPanel.Visibility = Visibility.Visible;
    }

    private void BuildGecRefTable()
    {
        GecRefTableRows.Children.Clear();

        // Header row
        var header = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        void AddHeaderCell(string text, int col)
        {
            var tb = new TextBlock
            {
                Text = text,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = (System.Windows.Media.Brush)FindResource("DimTextBrush"),
                Margin = new Thickness(col == 0 ? 0 : 4, 0, 0, 0)
            };
            Grid.SetColumn(tb, col);
            header.Children.Add(tb);
        }
        AddHeaderCell("Size", 0);
        AddHeaderCell("cmil", 1);
        AddHeaderCell("mm²", 2);
        GecRefTableRows.Children.Add(header);

        // Separator
        GecRefTableRows.Children.Add(new Border
        {
            Height = 1,
            Background = (System.Windows.Media.Brush)FindResource("SurfaceBrush"),
            Margin = new Thickness(0, 2, 0, 4)
        });

        // Data rows
        var isAlternate = false;
        foreach (var (label, cmil) in WireSizeCmil)
        {
            var sizeText = cmil >= 250_000 ? $"{label} kcmil" : $"#{label} AWG";
            var mm2 = cmil * 5.06707e-4; // 1 cmil = 5.06707×10⁻⁴ mm²
            var mm2Text = mm2 >= 100 ? $"{mm2:F0}" : $"{mm2:F1}";

            var row = new Grid
            {
                Background = isAlternate
                    ? Helpers.ThemeHelper.FaintOverlay
                    : System.Windows.Media.Brushes.Transparent,
                Margin = new Thickness(0, 0, 0, 1)
            };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            void AddCell(string text, int col, bool bold = false)
            {
                var tb = new TextBlock
                {
                    Text = text,
                    FontSize = 10,
                    FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal,
                    Foreground = bold
                        ? (System.Windows.Media.Brush)FindResource("TextBrush")
                        : (System.Windows.Media.Brush)FindResource("DimTextBrush"),
                    Margin = new Thickness(col == 0 ? 2 : 4, 1, 0, 1)
                };
                Grid.SetColumn(tb, col);
                row.Children.Add(tb);
            }

            AddCell(sizeText, 0, bold: true);
            AddCell($"{cmil:N0}", 1);
            AddCell(mm2Text, 2);
            GecRefTableRows.Children.Add(row);
            isAlternate = !isAlternate;
        }
    }

    private void GecRefToggle_Click(object sender, MouseButtonEventArgs e)
    {
        var isExpanded = GecRefTablePanel.Visibility == Visibility.Visible;
        GecRefTablePanel.Visibility = isExpanded ? Visibility.Collapsed : Visibility.Visible;
        GecRefToggleChevron.Text = isExpanded ? "\u25BC" : "\u25B2";
    }

    private void GecCalcButton_Click(object sender, MouseButtonEventArgs e)
    {
        PerformGecCalc();
    }

    /// <summary>
    /// Performs the GEC parallel conductor calculation:
    /// conductor cmil × number of sets → lookup in NEC 250.66 thresholds → GEC size.
    /// </summary>
    private void PerformGecCalc()
    {
        GecCalcOutputFields.Children.Clear();

        var sizeTag = (_gecCalcSizeCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "";
        var setsTag = (_gecCalcSetsCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "1";
        var matTag = (_gecCalcMaterialCombo?.SelectedItem as ComboBoxItem)?.Tag as string ?? "cu";

        if (string.IsNullOrEmpty(sizeTag))
        {
            GecCalcOutputPanel.Visibility = Visibility.Collapsed;
            return;
        }

        if (!int.TryParse(setsTag, out var numSets) || numSets < 1) numSets = 1;

        // Find cmil for the selected size
        var cmilEntry = Array.Find(WireSizeCmil, w =>
            w.Label.Equals(sizeTag, StringComparison.OrdinalIgnoreCase));
        if (cmilEntry.Cmil == 0)
        {
            GecCalcOutputPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var totalCmil = cmilEntry.Cmil * numSets;
        var isCu = matTag == "cu";

        // Walk the threshold table to find the matching GEC row
        string gecCu = "3/0", gecAl = "250";
        string matchedRange = "Over 1100";
        foreach (var row in GecTableThresholds)
        {
            var threshold = isCu ? row.CuMax : row.AlMax;
            if (totalCmil <= threshold)
            {
                gecCu = row.GecCu;
                gecAl = row.GecAl;
                // Build range description
                matchedRange = GetGecRangeDescription(totalCmil, isCu);
                break;
            }
        }

        GecCalcOutputPanel.Visibility = Visibility.Visible;

        // Show equivalent cmil
        AddGecCalcOutputCard("Total Circular Mil",
            $"{totalCmil:N0}",
            "cmil",
            $"{numSets} x {cmilEntry.Label} = {totalCmil:N0} cmil",
            Helpers.ThemeHelper.OrangeColor);

        // Show matched table range
        AddGecCalcOutputCard("Table 250.66 Range",
            matchedRange,
            isCu ? "(Cu column)" : "(Al column)",
            null,
            Helpers.ThemeHelper.TextSecondaryColor);

        // Show GEC Cu result
        AddGecCalcOutputCard("GEC Copper",
            gecCu,
            "AWG/kcmil",
            null,
            Helpers.ThemeHelper.GreenColor);

        // Show GEC Al result
        AddGecCalcOutputCard("GEC Aluminum",
            gecAl,
            "AWG/kcmil",
            null,
            Helpers.ThemeHelper.GreenColor);
    }

    private void AddGecCalcOutputCard(string label, string value, string unit, string? subtitle, System.Windows.Media.Color color)
    {
        var card = new Border
        {
            Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x18, color.R, color.G, color.B)),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 6, 10, 6),
            Margin = new Thickness(0, 0, 6, 6),
            MinWidth = 70
        };

        var stack = new StackPanel();

        var valuePanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        valuePanel.Children.Add(new TextBlock
        {
            Text = value,
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(color)
        });
        if (!string.IsNullOrEmpty(unit))
        {
            valuePanel.Children.Add(new TextBlock
            {
                Text = $" {unit}",
                FontSize = 11,
                Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x80, color.R, color.G, color.B)),
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 0, 2)
            });
        }
        stack.Children.Add(valuePanel);

        stack.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 9,
            Foreground = (System.Windows.Media.Brush)FindResource("DimTextBrush"),
            Margin = new Thickness(0, 2, 0, 0)
        });

        if (!string.IsNullOrEmpty(subtitle))
        {
            stack.Children.Add(new TextBlock
            {
                Text = subtitle,
                FontSize = 9,
                FontStyle = FontStyles.Italic,
                Foreground = (System.Windows.Media.Brush)FindResource("MutedTextBrush"),
                Margin = new Thickness(0, 1, 0, 0)
            });
        }

        card.Child = stack;
        GecCalcOutputFields.Children.Add(card);
    }

    private static string GetGecRangeDescription(double totalCmil, bool isCu)
    {
        if (isCu)
        {
            if (totalCmil <= 66_360) return "2 or smaller";
            if (totalCmil <= 105_600) return "1 or 1/0";
            if (totalCmil <= 167_800) return "2/0 or 3/0";
            if (totalCmil <= 350_000) return "Over 3/0 thru 350";
            if (totalCmil <= 600_000) return "Over 350 thru 600";
            if (totalCmil <= 1_100_000) return "Over 600 thru 1100";
            return "Over 1100";
        }
        else
        {
            if (totalCmil <= 105_600) return "1/0 or smaller";
            if (totalCmil <= 167_800) return "2/0 or 3/0";
            if (totalCmil <= 250_000) return "4/0 or 250";
            if (totalCmil <= 500_000) return "Over 250 thru 500";
            if (totalCmil <= 900_000) return "Over 500 thru 900";
            if (totalCmil <= 1_750_000) return "Over 900 thru 1750";
            return "Over 1750";
        }
    }

    private void RenderDataGrid(CheatSheet sheet, List<int>? visibleRowIndices)
    {
        TableContainer.Children.Clear();

        var grid = new Grid { HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch };
        var colCount = sheet.Columns.Count;
        var isSmallTable = colCount <= 2;

        // --- Column sizing: compute per-column star weight from content ---
        for (var c = 0; c < colCount; c++)
        {
            var col = sheet.Columns[c];
            var maxLen = sheet.Rows.Where(r => c < r.Count).Select(r => r[c].Length).DefaultIfEmpty(0).Max();
            var isNumeric = sheet.Rows.Where(r => c < r.Count)
                .All(r => r[c] == "—" || double.TryParse(r[c].Replace(",", ""), out _));

            double starWeight;
            if (isSmallTable)
            {
                starWeight = c == 0 ? 3.0 : 2.0;
            }
            else if (col.IsInputColumn && maxLen > 15)
            {
                starWeight = 3.0;
            }
            else if (col.IsInputColumn && maxLen > 8)
            {
                starWeight = 2.0;
            }
            else if (col.IsInputColumn)
            {
                starWeight = 1.4;
            }
            else if (isNumeric)
            {
                starWeight = 1.0;
            }
            else if (col.IsOutputColumn && maxLen > 15)
            {
                starWeight = 2.0;
            }
            else
            {
                starWeight = 1.0;
            }

            grid.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(starWeight, GridUnitType.Star)
            });
        }

        // --- Header row ---
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        for (var c = 0; c < colCount; c++)
        {
            var col = sheet.Columns[c];
            var isInputCol = col.IsInputColumn;

            var headerBorder = new Border
            {
                Background = (System.Windows.Media.Brush)FindResource("TableHeaderBrush"),
                Padding = new Thickness(6, 5, 6, 5),
                BorderBrush = (System.Windows.Media.Brush)FindResource("HoverBrush"),
                BorderThickness = new Thickness(0, 0, c < colCount - 1 ? 1 : 0, 1)
            };

            // Input columns left-align; output/other columns center
            var textAlign = isInputCol ? TextAlignment.Left : TextAlignment.Center;

            var headerForeground = isInputCol
                ? (System.Windows.Media.Brush)FindResource("AccentBrush")
                : col.IsOutputColumn
                    ? Helpers.ThemeHelper.Green
                    : Helpers.ThemeHelper.TextPrimary;

            // Header text: name + unit inline when short, stacked when needed
            var headerText = col.Header;
            if (col.Unit != null) headerText += $"\n({col.Unit})";

            headerBorder.Child = new TextBlock
            {
                Text = headerText,
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = headerForeground,
                TextAlignment = textAlign,
                TextWrapping = TextWrapping.Wrap,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch
            };

            Grid.SetRow(headerBorder, 0);
            Grid.SetColumn(headerBorder, c);
            grid.Children.Add(headerBorder);
        }

        // --- Data rows ---
        var rowsToShow = visibleRowIndices ?? Enumerable.Range(0, sheet.Rows.Count).ToList();
        for (var ri = 0; ri < rowsToShow.Count; ri++)
        {
            var rowIdx = rowsToShow[ri];
            if (rowIdx < 0 || rowIdx >= sheet.Rows.Count) continue;

            var row = sheet.Rows[rowIdx];
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var gridRow = ri + 1;

            for (var c = 0; c < colCount && c < row.Count; c++)
            {
                var col = sheet.Columns[c];
                var isAlt = ri % 2 == 1;
                var cellBorder = new Border
                {
                    Background = isAlt
                        ? (System.Windows.Media.Brush)FindResource("TableRowAltBrush")
                        : System.Windows.Media.Brushes.Transparent,
                    Padding = new Thickness(6, 4, 6, 4),
                    BorderBrush = (System.Windows.Media.Brush)FindResource("HoverBrush"),
                    BorderThickness = new Thickness(0, 0, c < colCount - 1 ? 1 : 0, 0)
                };

                // Input columns left-align; output/numeric columns center
                var cellAlign = col.IsInputColumn ? TextAlignment.Left : TextAlignment.Center;

                cellBorder.Child = new TextBlock
                {
                    Text = row[c],
                    FontSize = 11,
                    Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                    TextAlignment = cellAlign,
                    TextWrapping = TextWrapping.Wrap,
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
            TelemetryAccessor.TrackClipboardCopy("note_content", "CheatSheet");
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

    private void AddSheetButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isEditor || _dataService == null) return;

        var newSheet = new CheatSheet
        {
            Id = $"custom-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..6]}",
            Title = "New Sheet",
            Discipline = _currentDiscipline,
            SheetType = CheatSheetType.Table,
            Layout = CheatSheetLayout.CompactLookup,
            Columns = new List<CheatSheetColumn>
            {
                new() { Header = "Input", IsInputColumn = true },
                new() { Header = "Output", IsOutputColumn = true }
            },
            Rows = new List<List<string>> { new() { "", "" } }
        };

        OpenSheetEditor(newSheet, isNew: true);
    }

    private void EditSheetButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_isEditor || _dataService == null || _activeSheet == null) return;
        OpenSheetEditor(_activeSheet, isNew: false);
    }

    private void OpenSheetEditor(CheatSheet sheet, bool isNew)
    {
        // Build editor UI inline in the detail panel area
        SheetListPanel.Visibility = Visibility.Collapsed;
        SheetDetailPanel.Visibility = Visibility.Collapsed;

        // Create editor panel dynamically
        var editorPanel = BuildSheetEditorPanel(sheet, isNew);

        // Place it in the main content grid (row 1)
        var mainGrid = SheetListPanel.Parent as Grid;
        if (mainGrid == null) return;

        // Remove any existing editor panel
        var existingEditor = mainGrid.Children.OfType<FrameworkElement>().FirstOrDefault(c => c.Name == "SheetEditorPanel");
        if (existingEditor != null) mainGrid.Children.Remove(existingEditor);

        editorPanel.Name = "SheetEditorPanel";
        Grid.SetRow(editorPanel, 0); // Same row as list/detail (they're overlapping states)
        Grid.SetRowSpan(editorPanel, 2);
        mainGrid.Children.Add(editorPanel);
    }

    private ScrollViewer BuildSheetEditorPanel(CheatSheet sheet, bool isNew)
    {
        var username = Environment.UserName.ToLowerInvariant();

        // Clone the sheet to avoid modifying the original until save
        var editSheet = System.Text.Json.JsonSerializer.Deserialize<CheatSheet>(
            System.Text.Json.JsonSerializer.Serialize(sheet)) ?? sheet;

        var outerScroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        var stack = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };

        // --- Header: Back + Title ---
        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var backBtn = new Border
        {
            Background = System.Windows.Media.Brushes.Transparent,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 3, 6, 3),
            Cursor = System.Windows.Input.Cursors.Hand,
            Margin = new Thickness(0, 0, 8, 0)
        };
        backBtn.Child = new TextBlock { Text = "\u2190", FontSize = 14, Foreground = Helpers.ThemeHelper.Accent };
        backBtn.MouseLeftButtonDown += (_, _) => CloseEditor();
        Grid.SetColumn(backBtn, 0);
        headerGrid.Children.Add(backBtn);

        var headerLabel = new TextBlock
        {
            Text = isNew ? "New Cheat Sheet" : $"Edit: {editSheet.Title}",
            FontSize = 13, FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(headerLabel, 1);
        headerGrid.Children.Add(headerLabel);
        stack.Children.Add(headerGrid);

        // --- Metadata Fields ---
        var titleBox = AddEditorField(stack, "Title", editSheet.Title);
        var subtitleBox = AddEditorField(stack, "Subtitle", editSheet.Subtitle ?? "");
        var descBox = AddEditorField(stack, "Description", editSheet.Description ?? "");
        var tagsBox = AddEditorField(stack, "Tags (comma-separated)", string.Join(", ", editSheet.Tags));

        // Discipline dropdown
        var disciplineCombo = new System.Windows.Controls.ComboBox
        {
            FontSize = 12, Margin = new Thickness(0, 0, 0, 6),
            Style = (System.Windows.Style)FindResource("DarkComboBox")
        };
        foreach (var d in new[] { "Electrical", "Mechanical", "Plumbing", "Fire Protection" })
            disciplineCombo.Items.Add(new ComboBoxItem { Content = d });
        disciplineCombo.SelectedIndex = editSheet.Discipline switch
        {
            Discipline.Electrical => 0,
            Discipline.Mechanical => 1,
            Discipline.Plumbing => 2,
            Discipline.FireProtection => 3,
            _ => 0
        };
        stack.Children.Add(new TextBlock { Text = "Discipline", FontSize = 10, Foreground = Helpers.ThemeHelper.TextSecondary, Margin = new Thickness(0, 4, 0, 2) });
        stack.Children.Add(disciplineCombo);

        // SheetType dropdown
        var typeCombo = new System.Windows.Controls.ComboBox
        {
            FontSize = 12, Margin = new Thickness(0, 0, 0, 6),
            Style = (System.Windows.Style)FindResource("DarkComboBox")
        };
        foreach (var t in new[] { "Table", "Calculator", "Note" })
            typeCombo.Items.Add(new ComboBoxItem { Content = t });
        typeCombo.SelectedIndex = (int)editSheet.SheetType;
        stack.Children.Add(new TextBlock { Text = "Sheet Type", FontSize = 10, Foreground = Helpers.ThemeHelper.TextSecondary, Margin = new Thickness(0, 4, 0, 2) });
        stack.Children.Add(typeCombo);

        // --- Columns Section ---
        stack.Children.Add(new TextBlock
        {
            Text = "COLUMNS", FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = Helpers.ThemeHelper.Accent, Margin = new Thickness(0, 12, 0, 4)
        });

        var colsPanel = new StackPanel();
        var colEditors = new List<(System.Windows.Controls.TextBox header, System.Windows.Controls.TextBox unit, System.Windows.Controls.CheckBox isInput, System.Windows.Controls.CheckBox isOutput)>();

        void AddColumnEditor(CheatSheetColumn col)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 3) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var hdr = new System.Windows.Controls.TextBox
            {
                Text = col.Header, FontSize = 11, Padding = new Thickness(4, 2, 4, 2),
                Background = Helpers.ThemeHelper.HoverMedium, Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(hdr, 0);
            row.Children.Add(hdr);

            var unitTb = new System.Windows.Controls.TextBox
            {
                Text = col.Unit ?? "", FontSize = 11, Padding = new Thickness(4, 2, 4, 2),
                Background = Helpers.ThemeHelper.HoverMedium, Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 4, 0)
            };
            Grid.SetColumn(unitTb, 1);
            row.Children.Add(unitTb);

            var isInp = new System.Windows.Controls.CheckBox { Content = "In", IsChecked = col.IsInputColumn, Foreground = Helpers.ThemeHelper.TextSecondary, FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            Grid.SetColumn(isInp, 2);
            row.Children.Add(isInp);

            var isOut = new System.Windows.Controls.CheckBox { Content = "Out", IsChecked = col.IsOutputColumn, Foreground = Helpers.ThemeHelper.TextSecondary, FontSize = 10, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(isOut, 3);
            row.Children.Add(isOut);

            colsPanel.Children.Add(row);
            colEditors.Add((hdr, unitTb, isInp, isOut));
        }

        foreach (var col in editSheet.Columns) AddColumnEditor(col);

        stack.Children.Add(colsPanel);

        // Add column button
        var addColBtn = new Border
        {
            Background = Helpers.ThemeHelper.Hover, CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3), Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0)
        };
        addColBtn.Child = new TextBlock { Text = "+ Column", FontSize = 10, Foreground = Helpers.ThemeHelper.Accent };
        addColBtn.MouseLeftButtonDown += (_, _) => AddColumnEditor(new CheatSheetColumn { Header = "New Column" });
        stack.Children.Add(addColBtn);

        // --- Rows Section ---
        stack.Children.Add(new TextBlock
        {
            Text = $"ROWS ({editSheet.Rows.Count})", FontSize = 10, FontWeight = FontWeights.SemiBold,
            Foreground = Helpers.ThemeHelper.Accent, Margin = new Thickness(0, 12, 0, 4)
        });

        var rowsPanel = new StackPanel();
        var rowEditors = new List<List<System.Windows.Controls.TextBox>>();

        void AddRowEditor(List<string> rowData)
        {
            var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            var rowCells = new List<System.Windows.Controls.TextBox>();
            for (var c = 0; c < Math.Max(editSheet.Columns.Count, rowData.Count); c++)
            {
                rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var cellTb = new System.Windows.Controls.TextBox
                {
                    Text = c < rowData.Count ? rowData[c] : "",
                    FontSize = 10, Padding = new Thickness(3, 1, 3, 1),
                    Background = Helpers.ThemeHelper.HoverMedium, Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                    BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 2, 0)
                };
                Grid.SetColumn(cellTb, c);
                rowGrid.Children.Add(cellTb);
                rowCells.Add(cellTb);
            }
            rowsPanel.Children.Add(rowGrid);
            rowEditors.Add(rowCells);
        }

        foreach (var row in editSheet.Rows) AddRowEditor(row);

        var rowScrollViewer = new ScrollViewer
        {
            MaxHeight = 200, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = rowsPanel
        };
        stack.Children.Add(rowScrollViewer);

        // Add row button
        var addRowBtn = new Border
        {
            Background = Helpers.ThemeHelper.Hover, CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3), Cursor = System.Windows.Input.Cursors.Hand,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0)
        };
        addRowBtn.Child = new TextBlock { Text = "+ Row", FontSize = 10, Foreground = Helpers.ThemeHelper.Accent };
        addRowBtn.MouseLeftButtonDown += (_, _) =>
        {
            var emptyCells = Enumerable.Range(0, editSheet.Columns.Count).Select(_ => "").ToList();
            AddRowEditor(emptyCells);
        };
        stack.Children.Add(addRowBtn);

        // --- Save / Cancel Buttons ---
        var buttonRow = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };

        var saveBtn = new Border
        {
            Background = Helpers.ThemeHelper.Accent, CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 6, 16, 6), Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 0, 8, 0)
        };
        saveBtn.Child = new TextBlock { Text = "Save", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = System.Windows.Media.Brushes.White };
        saveBtn.MouseLeftButtonDown += async (_, _) =>
        {
            // Collect edited data
            editSheet.Title = titleBox.Text?.Trim() ?? "Untitled";
            editSheet.Subtitle = string.IsNullOrWhiteSpace(subtitleBox.Text) ? null : subtitleBox.Text.Trim();
            editSheet.Description = string.IsNullOrWhiteSpace(descBox.Text) ? null : descBox.Text.Trim();
            editSheet.Tags = (tagsBox.Text ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            editSheet.Discipline = disciplineCombo.SelectedIndex switch
            {
                0 => Discipline.Electrical,
                1 => Discipline.Mechanical,
                2 => Discipline.Plumbing,
                3 => Discipline.FireProtection,
                _ => Discipline.Electrical
            };
            editSheet.SheetType = (CheatSheetType)typeCombo.SelectedIndex;

            // Collect columns
            editSheet.Columns.Clear();
            foreach (var (hdr, unitTb, isInp, isOut) in colEditors)
            {
                if (string.IsNullOrWhiteSpace(hdr.Text)) continue;
                editSheet.Columns.Add(new CheatSheetColumn
                {
                    Header = hdr.Text.Trim(),
                    Unit = string.IsNullOrWhiteSpace(unitTb.Text) ? null : unitTb.Text.Trim(),
                    IsInputColumn = isInp.IsChecked ?? false,
                    IsOutputColumn = isOut.IsChecked ?? false
                });
            }

            // Collect rows
            editSheet.Rows.Clear();
            foreach (var rowCells in rowEditors)
            {
                var rowData = rowCells.Select(tb => tb.Text ?? "").ToList();
                // Skip completely empty rows
                if (rowData.All(string.IsNullOrWhiteSpace)) continue;
                editSheet.Rows.Add(rowData);
            }

            // Save via data service
            try
            {
                await _dataService!.SaveSheetAsync(editSheet, username);
                DebugLogger.Log($"CheatSheetWidget: Saved sheet '{editSheet.Id}' by {username}");

                // Refresh and show the saved sheet
                CloseEditor();
                await _service.LoadAsync();
                RefreshSheetList();
                OpenSheet(editSheet);
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"CheatSheetWidget: Save failed: {ex.Message}");
                System.Windows.MessageBox.Show($"Save failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };
        buttonRow.Children.Add(saveBtn);

        var cancelBtn = new Border
        {
            Background = Helpers.ThemeHelper.Hover, CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 6, 16, 6), Cursor = System.Windows.Input.Cursors.Hand
        };
        cancelBtn.Child = new TextBlock { Text = "Cancel", FontSize = 12, Foreground = (System.Windows.Media.Brush)FindResource("TextBrush") };
        cancelBtn.MouseLeftButtonDown += (_, _) => CloseEditor();
        buttonRow.Children.Add(cancelBtn);

        // Disable/Enable + Delete buttons (if editing existing sheet)
        if (!isNew)
        {
            var disableBtn = new Border
            {
                Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0xFF, 0xD5, 0x4F)),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 6, 12, 6),
                Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(8, 0, 0, 0)
            };
            disableBtn.Child = new TextBlock { Text = "Disable", FontSize = 11, Foreground = Helpers.ThemeHelper.GoldDark };
            disableBtn.MouseLeftButtonDown += async (_, _) =>
            {
                await _dataService!.DisableSheetAsync(editSheet.Id, username);
                CloseEditor();
                await _service.LoadAsync();
                RefreshSheetList();
            };
            buttonRow.Children.Add(disableBtn);

            if (_isAdmin)
            {
                var deleteBtn = new Border
                {
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0xFF, 0x44, 0x44)),
                    CornerRadius = new CornerRadius(6), Padding = new Thickness(12, 6, 12, 6),
                    Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(8, 0, 0, 0)
                };
                deleteBtn.Child = new TextBlock { Text = "Delete", FontSize = 11, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x55, 0x55)) };
                deleteBtn.MouseLeftButtonDown += async (_, _) =>
                {
                    var result = System.Windows.MessageBox.Show(
                        $"Permanently delete '{editSheet.Title}'?\nThis cannot be undone.",
                        "Delete Sheet", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (result != MessageBoxResult.Yes) return;

                    await _dataService!.DeleteSheetAsync(editSheet.Id, username);
                    CloseEditor();
                    await _service.LoadAsync();
                    RefreshSheetList();
                };
                buttonRow.Children.Add(deleteBtn);
            }
        }

        stack.Children.Add(buttonRow);

        outerScroll.Content = stack;
        return outerScroll;
    }

    private System.Windows.Controls.TextBox AddEditorField(StackPanel parent, string label, string value)
    {
        parent.Children.Add(new TextBlock
        {
            Text = label, FontSize = 10,
            Foreground = Helpers.ThemeHelper.TextSecondary,
            Margin = new Thickness(0, 4, 0, 2)
        });

        var tb = new System.Windows.Controls.TextBox
        {
            Text = value, FontSize = 12,
            Padding = new Thickness(6, 4, 6, 4),
            Background = Helpers.ThemeHelper.HoverMedium,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
            CaretBrush = (System.Windows.Media.Brush)FindResource("TextBrush"),
            BorderThickness = new Thickness(0),
            Margin = new Thickness(0, 0, 0, 4)
        };
        parent.Children.Add(tb);
        return tb;
    }

    private void CloseEditor()
    {
        var mainGrid = SheetListPanel.Parent as Grid;
        if (mainGrid != null)
        {
            var editor = mainGrid.Children.OfType<FrameworkElement>().FirstOrDefault(c => c.Name == "SheetEditorPanel");
            if (editor != null) mainGrid.Children.Remove(editor);
        }
        ShowListView();
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
            RenderDataGrid(_activeSheet, null);
            OutputPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var results = _service.Lookup(_activeSheet, inputs);
        OutputFields.Children.Clear();
        _lastOutputText = null;

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
            RenderDataGrid(_activeSheet, null);
            return;
        }

        OutputPanel.Visibility = Visibility.Visible;

        var outputCols = _activeSheet.Columns.Where(c => c.IsOutputColumn).ToList();
        if (IsMotorFlaSheet(_activeSheet) && !string.IsNullOrWhiteSpace(_selectedVoltageHeader))
        {
            outputCols = outputCols
                .Where(c => c.Header.Equals(_selectedVoltageHeader, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var copyLines = new List<string>();

        for (var ri = 0; ri < results.Count; ri++)
        {
            var result = results[ri];

            // Separator between multiple matched rows
            if (ri > 0)
            {
                OutputFields.Children.Add(new Border
                {
                    BorderBrush = Helpers.ThemeHelper.GreenBackground,
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Margin = new Thickness(0, 6, 0, 6),
                    Height = 1
                });
                copyLines.Add("---");
            }

            foreach (var col in outputCols)
            {
                if (!result.TryGetValue(col.Header, out var val)) continue;

                // Clean label: strip unit from header if unit is separate
                var label = col.Header;
                if (col.Unit != null && label.Contains($"({col.Unit})"))
                    label = label.Replace($"({col.Unit})", "").Trim();

                var valueText = val;
                var unitText = col.Unit ?? "";

                // Card-style output: value card with label
                var card = new Border
                {
                    Background = Helpers.ThemeHelper.GreenBackground,
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 0, 6, 6),
                    MinWidth = 70
                };

                var cardStack = new StackPanel();

                // Value (large, bold, green)
                var valuePanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                valuePanel.Children.Add(new TextBlock
                {
                    Text = valueText,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x66, 0xBB, 0x6A))
                });
                if (!string.IsNullOrEmpty(unitText))
                {
                    valuePanel.Children.Add(new TextBlock
                    {
                        Text = $" {unitText}",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x80, 0x66, 0xBB, 0x6A)),
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(0, 0, 0, 2)
                    });
                }
                cardStack.Children.Add(valuePanel);

                // Label (small, below value)
                cardStack.Children.Add(new TextBlock
                {
                    Text = label,
                    FontSize = 9,
                    Foreground = (System.Windows.Media.Brush)FindResource("DimTextBrush"),
                    Margin = new Thickness(0, 2, 0, 0)
                });

                card.Child = cardStack;
                OutputFields.Children.Add(card);

                copyLines.Add($"{label}: {valueText}{(string.IsNullOrEmpty(unitText) ? "" : $" {unitText}")}");
            }
        }

        _lastOutputText = string.Join("\n", copyLines);
        CopyOutputLabel.Text = "\U0001F4CB Copy";

        // Highlight matching rows in the table
        var matchedRowIndices = GetMatchedRowIndicesStrict(_activeSheet, inputs);
        RenderDataGrid(_activeSheet, matchedRowIndices.Count > 0 ? matchedRowIndices : null);
    }

    private string? _lastOutputText;

    private void CopyOutputButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (string.IsNullOrEmpty(_lastOutputText)) return;
        try
        {
            System.Windows.Clipboard.SetText(_lastOutputText);
            TelemetryAccessor.TrackClipboardCopy("output_values", "CheatSheet");
            CopyOutputLabel.Text = "\u2714 Copied!";
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) =>
            {
                CopyOutputLabel.Text = "\U0001F4CB Copy";
                timer.Stop();
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"CheatSheetWidget.CopyOutput: {ex.Message}");
        }
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

    // FindToggle_Click removed — Find bar is now always visible in Table mode

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
