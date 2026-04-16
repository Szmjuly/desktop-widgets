using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DesktopHub.Core.Abstractions;
using DesktopHub.Infrastructure.Firebase;

// Resolve WPF+WinForms ambiguity
using WpfControl = System.Windows.Controls.Control;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;
using DesktopHub.Core.Models;
using DesktopHub.UI.Dialogs;
using DesktopHub.UI.Services;

namespace DesktopHub.UI.Widgets;

public partial class ProjectInfoWidget : System.Windows.Controls.UserControl
{
    private readonly IProjectTagService _tagService;
    private readonly ITagVocabularyService? _vocabService;
    private readonly IMasterStructureService? _masterStructureService;
    private readonly IFirebaseService? _firebaseService;

    public event EventHandler? CloseRequested;

    private string? _currentProjectNumber;
    private string? _currentProjectPath;
    private readonly Dictionary<string, System.Windows.Controls.Control> _fieldControls = new();
    private bool _isLocked = true;
    private bool _isEditor;

    // Multi-select fields (Engineers, Code References, Project Labels, etc.)
    private readonly Dictionary<string, WrapPanel> _multiSelectChipPanels = new();
    private readonly Dictionary<string, List<string>> _multiSelectValues = new();
    private readonly Dictionary<string, System.Windows.Controls.ComboBox> _multiSelectCombos = new();
    private readonly Dictionary<string, StackPanel> _multiSelectContainers = new();

    // Missing field indicators — references to the input control borders for orange outline
    private readonly Dictionary<string, WpfControl> _fieldIndicatorControls = new();

    // Editor action buttons (add/remove field/category) — hidden when locked
    private readonly List<UIElement> _editorActionElements = new();

    public ProjectInfoWidget(
        IProjectTagService tagService,
        ITagVocabularyService? vocabService,
        IMasterStructureService? masterStructureService = null,
        IFirebaseService? firebaseService = null)
    {
        InitializeComponent();
        _tagService = tagService;
        _vocabService = vocabService;
        _masterStructureService = masterStructureService;
        _firebaseService = firebaseService;

        // Listen for master structure changes to rebuild the UI
        if (_masterStructureService != null)
            _masterStructureService.StructureUpdated += OnMasterStructureUpdated;

        BuildFieldUI();
        UpdateLockState();

        // Check editor permissions in background
        Loaded += async (s, e) => await CheckEditorPermissionsAsync();
    }

    private async Task CheckEditorPermissionsAsync()
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized) return;
        try
        {
            _isEditor = await _firebaseService.IsCheatSheetEditorAsync();
            await Dispatcher.InvokeAsync(UpdateEditorButtonVisibility);
        }
        catch (Exception ex)
        {
            DesktopHub.Infrastructure.Logging.InfraLogger.Log($"ProjectInfoWidget: Editor check failed: {ex.Message}");
        }
    }

    private void OnMasterStructureUpdated()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            BuildFieldUI();
            UpdateFieldsEnabled();
            if (_currentProjectNumber != null)
                _ = LoadTagsForProject(_currentProjectNumber);
        }));
    }

    private void UpdateEditorButtonVisibility()
    {
        // The "+" buttons are dynamically added in BuildFieldUI, re-run it to show/hide them
        BuildFieldUI();
        UpdateFieldsEnabled();
        if (_currentProjectNumber != null)
            _ = LoadTagsForProject(_currentProjectNumber);
    }

    /// <summary>
    /// Set the active project (called when user selects a project in search results).
    /// </summary>
    public async Task SetProjectAsync(string projectNumber, string? displayName)
    {
        if (string.IsNullOrWhiteSpace(projectNumber))
        {
            _currentProjectNumber = null;
            _currentProjectPath = null;
            ProjectHeaderText.Text = "Select a project";
            ClearAllFields();
            HideMissingIndicators();
            StatusText.Text = "Select a project to view its info.";
            _isLocked = true;
            UpdateLockState();
            return;
        }

        _currentProjectNumber = projectNumber;
        _currentProjectPath = displayName;
        ProjectHeaderText.Text = string.IsNullOrEmpty(displayName) ? projectNumber : displayName;

        await LoadTagsForProject(projectNumber);
        _isLocked = true;
        UpdateLockState();
    }

    private async Task LoadTagsForProject(string projectNumber)
    {
        StatusText.Text = "Loading...";

        // Load project-specific structure override if available
        if (_masterStructureService != null)
            await _masterStructureService.GetProjectOverrideAsync(projectNumber);

        var tags = await _tagService.GetTagsAsync(projectNumber);

        ClearAllFields();

        if (tags != null)
        {
            // Load custom tags into the Project Tags panel
            _projectTagValues.Clear();
            foreach (var kvp in tags.Custom)
                _projectTagValues.Add(kvp);
            RebuildProjectTagChips();

            var mergedFields = _masterStructureService?.GetMergedFields(projectNumber) ?? GetBuiltInFields();
            foreach (var field in mergedFields)
            {
                string? value;
                if (field.IsBuiltIn)
                    value = GetTagValue(tags, field.Key);
                else
                    tags.Custom.TryGetValue(field.Key, out value);

                if (string.IsNullOrEmpty(value)) continue;

                if (field.InputMode == TagInputMode.MultiSelect && _multiSelectValues.ContainsKey(field.Key))
                {
                    _multiSelectValues[field.Key] = value.Split(',', StringSplitOptions.TrimEntries)
                        .Where(s => s.Length > 0).ToList();
                    RebuildMultiSelectChips(field.Key);
                }
                else if (_fieldControls.TryGetValue(field.Key, out var control))
                {
                    SetControlText(control, value);
                }
            }
            StatusText.Text = $"Loaded tags for {projectNumber}";
        }
        else
        {
            StatusText.Text = $"No tags yet — unlock to edit.";
        }

        UpdateMissingIndicators();
    }

    private static List<MasterFieldDefinition> GetBuiltInFields()
    {
        int order = 0;
        return TagFieldRegistry.Fields.Select(f => MasterFieldDefinition.FromBuiltIn(f, order++)).ToList();
    }

    private const string ProjectTagsCategoryName = "Project Tags";

    private static readonly Dictionary<string, string> CategoryIcons = new()
    {
        ["Project Tags"] = "\U0001F3F7",
        ["Electrical"]   = "\u26A1",
        ["Mechanical"]   = "\u2744",
        ["Building"]     = "\U0001F3E0",
        ["Location"]     = "\U0001F4CD",
        ["People"]       = "\U0001F465",
        ["Code"]         = "\U0001F4D6",
        ["Other"]        = "\U0001F4CB"
    };

    // Project Tags tab state — edits ProjectTags.Custom dictionary (key:value pairs)
    private WrapPanel? _projectTagsChipPanel;
    private System.Windows.Controls.TextBox? _projectTagsKeyInput;
    private System.Windows.Controls.TextBox? _projectTagsValueInput;
    private readonly List<KeyValuePair<string, string>> _projectTagValues = new();

    private readonly Dictionary<string, StackPanel> _categoryPanels = new();
    private readonly Dictionary<string, Border> _categoryHeaders = new();
    private readonly Dictionary<string, Border> _sidebarButtons = new();

    private void BuildFieldUI()
    {
        FieldsPanel.Children.Clear();
        SidebarTabs.Children.Clear();
        _fieldControls.Clear();
        _categoryPanels.Clear();
        _categoryHeaders.Clear();
        _sidebarButtons.Clear();
        _multiSelectChipPanels.Clear();
        _multiSelectValues.Clear();
        _multiSelectCombos.Clear();
        _multiSelectContainers.Clear();
        _fieldIndicatorControls.Clear();
        _editorActionElements.Clear();

        // Use merged fields from master structure service, or fall back to static baseline
        var allFields = _masterStructureService?.GetMergedFields(_currentProjectNumber) ?? GetBuiltInFields();
        var allCategories = _masterStructureService?.GetMergedCategories(_currentProjectNumber)
            ?? CategoryIcons.Select((kv, i) => new MasterCategoryDefinition { Name = kv.Key, Icon = kv.Value, SortOrder = i, IsBuiltIn = true }).ToList();

        var fieldsByCategory = allFields.GroupBy(f => f.Category ?? "Other").ToList();

        foreach (var catDef in allCategories)
        {
            var catName = catDef.Name;
            var icon = catDef.Icon;
            if (string.IsNullOrEmpty(icon))
                icon = CategoryIcons.GetValueOrDefault(catName, "\U0001F4CB");

            var fieldsInCategory = fieldsByCategory.FirstOrDefault(g => g.Key.Equals(catName, StringComparison.OrdinalIgnoreCase));
            // Skip empty categories unless the editor can add to them
            if (fieldsInCategory == null && !_isEditor) continue;

            // --- Sidebar icon tab ---
            var sideBtn = new Border
            {
                Style = (Style)FindResource("SideTabButton"),
                ToolTip = catName,
                Tag = catName
            };
            var sideIcon = new TextBlock
            {
                Text = icon,
                FontSize = 14,
                Foreground = Helpers.ThemeHelper.TextSecondary,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            sideBtn.Child = sideIcon;
            sideBtn.MouseLeftButtonDown += SidebarTab_Click;
            SidebarTabs.Children.Add(sideBtn);
            _sidebarButtons[catName] = sideBtn;

            // --- Collapsible category header ---
            var fieldsContainer = new StackPanel { Margin = new Thickness(0, 0, 0, 2) };

            var headerBorder = new Border
            {
                Style = (Style)FindResource("CategoryToggle"),
                Tag = fieldsContainer
            };
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var headerIcon = new TextBlock
            {
                Text = icon, FontSize = 12,
                Foreground = Helpers.ThemeHelper.TextSecondary,
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(headerIcon, 0);
            headerGrid.Children.Add(headerIcon);

            var headerText = new TextBlock
            {
                Text = catName, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = Helpers.ThemeHelper.Accent,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(headerText, 1);
            headerGrid.Children.Add(headerText);

            // Editor "+" button to add a field to this category
            if (_isEditor)
            {
                var capturedCatName = catName;
                var addFieldBtn = new Border
                {
                    Background = Helpers.ThemeHelper.AccentLight,
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(4, 0, 4, 0),
                    Margin = new Thickness(6, 0, 4, 0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = $"Add field to {catName}"
                };
                addFieldBtn.Child = new TextBlock
                {
                    Text = "+ Field",
                    FontSize = 9,
                    Foreground = Helpers.ThemeHelper.Accent,
                    VerticalAlignment = VerticalAlignment.Center
                };
                addFieldBtn.MouseLeftButtonDown += (s, ev) =>
                {
                    ev.Handled = true;
                    OnAddFieldClicked(capturedCatName);
                };
                Grid.SetColumn(addFieldBtn, 3);
                headerGrid.Children.Add(addFieldBtn);
                _editorActionElements.Add(addFieldBtn);

                // Remove button for non-built-in categories
                if (!catDef.IsBuiltIn && _masterStructureService != null)
                {
                    var removeCatBtn = new TextBlock
                    {
                        Text = "\u2715",
                        FontSize = 9,
                        Foreground = Helpers.ThemeHelper.TextTertiary,
                        VerticalAlignment = VerticalAlignment.Center,
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Margin = new Thickness(2, 0, 2, 0),
                        ToolTip = $"Remove category '{capturedCatName}'"
                    };
                    removeCatBtn.MouseEnter += (s, ev) => { if (s is TextBlock tb) tb.Foreground = Helpers.ThemeHelper.Red; };
                    removeCatBtn.MouseLeave += (s, ev) => { if (s is TextBlock tb) tb.Foreground = Helpers.ThemeHelper.TextTertiary; };
                    removeCatBtn.MouseLeftButtonDown += (s, ev) =>
                    {
                        ev.Handled = true;
                        OnRemoveCategoryClicked(capturedCatName);
                    };
                    // Insert between + Field and collapse arrow — add a new column
                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    Grid.SetColumn(removeCatBtn, 5);
                    headerGrid.Children.Add(removeCatBtn);
                    _editorActionElements.Add(removeCatBtn);
                }
            }

            var collapseArrow = new TextBlock
            {
                Text = "\u25BC", FontSize = 8,
                Foreground = Helpers.ThemeHelper.TextTertiary,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                RenderTransform = new System.Windows.Media.RotateTransform(0)
            };
            Grid.SetColumn(collapseArrow, 4);
            headerGrid.Children.Add(collapseArrow);

            headerBorder.Child = headerGrid;
            headerBorder.MouseLeftButtonDown += CategoryHeader_Click;

            FieldsPanel.Children.Add(headerBorder);
            _categoryHeaders[catName] = headerBorder;

            // --- Content inside the collapsible container ---
            if (catName.Equals(ProjectTagsCategoryName, StringComparison.OrdinalIgnoreCase))
            {
                // Special: Project Tags gets a dedicated chip-based tag editor
                BuildProjectTagsPanel(fieldsContainer);
            }
            else if (fieldsInCategory != null)
            {
                foreach (var field in fieldsInCategory)
                {
                    BuildFieldRow(fieldsContainer, field);
                }
            }

            FieldsPanel.Children.Add(fieldsContainer);
            _categoryPanels[catName] = fieldsContainer;
        }

        // Editor: "Add Category" button at the bottom of the sidebar
        if (_isEditor)
        {
            var addCatBtn = new Border
            {
                Style = (Style)FindResource("SideTabButton"),
                ToolTip = "Add new category",
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(0, 4, 0, 0)
            };
            addCatBtn.Child = new TextBlock
            {
                Text = "+",
                FontSize = 16, FontWeight = FontWeights.Bold,
                Foreground = Helpers.ThemeHelper.Accent,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            addCatBtn.MouseLeftButtonDown += (s, ev) =>
            {
                ev.Handled = true;
                OnAddCategoryClicked();
            };
            SidebarTabs.Children.Add(addCatBtn);
            _editorActionElements.Add(addCatBtn);
        }
    }

    private void BuildFieldRow(StackPanel fieldsContainer, MasterFieldDefinition field)
    {
        var row = new Grid { Margin = new Thickness(4, 4, 4, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // Editor: add remove button for non-built-in fields
        if (_isEditor && !field.IsBuiltIn)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

        var label = new TextBlock
        {
            Text = field.DisplayName,
            Style = (Style)FindResource("FieldLabel")
        };
        Grid.SetColumn(label, 0);
        row.Children.Add(label);

        // Get vocabulary values: prefer master structure extended values, fall back to field defaults
        var vocabValues = _masterStructureService != null
            ? _masterStructureService.GetExtendedValues(field.Key, _currentProjectNumber)
            : (_vocabService?.GetValues(field.Key) ?? field.SuggestedValues.ToList());

        // Also merge from vocabulary service (user-entered values from cache)
        if (_vocabService != null)
        {
            var cachedValues = _vocabService.GetValues(field.Key);
            foreach (var v in cachedValues)
            {
                if (!vocabValues.Contains(v, StringComparer.OrdinalIgnoreCase))
                    vocabValues.Add(v);
            }
        }

        if (field.InputMode == TagInputMode.MultiSelect)
        {
            // --- Multi-select: inline combo + add, chips below ---
            var container = new StackPanel();

            var addRow = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var combo = new System.Windows.Controls.ComboBox
            {
                IsEditable = true,
                Style = (Style)FindResource("DarkCombo"),
                ItemContainerStyle = (Style)FindResource("DarkComboItem"),
                Margin = new Thickness(0, 0, 3, 0)
            };
            foreach (var val in vocabValues)
                combo.Items.Add(val);
            Grid.SetColumn(combo, 0);
            addRow.Children.Add(combo);

            var capturedKey = field.Key;
            var addBtn = new System.Windows.Controls.Button
            {
                Content = "+",
                Style = (Style)FindResource("SmallAddButton"),
                Width = 22, Height = 22
            };
            addBtn.Click += (s, ev) =>
            {
                var val = combo.Text?.Trim();
                if (string.IsNullOrEmpty(val)) return;
                if (!_multiSelectValues.ContainsKey(capturedKey))
                    _multiSelectValues[capturedKey] = new List<string>();
                if (!_multiSelectValues[capturedKey].Contains(val, StringComparer.OrdinalIgnoreCase))
                {
                    _multiSelectValues[capturedKey].Add(val);
                    RebuildMultiSelectChips(capturedKey);
                    UpdateMissingIndicators();
                }
                combo.Text = "";
            };
            Grid.SetColumn(addBtn, 1);
            addRow.Children.Add(addBtn);

            container.Children.Add(addRow);

            var chipPanel = new WrapPanel { Margin = new Thickness(0) };
            container.Children.Add(chipPanel);

            Grid.SetColumn(container, 1);
            row.Children.Add(container);

            _multiSelectChipPanels[field.Key] = chipPanel;
            _multiSelectValues[field.Key] = new List<string>();
            _multiSelectCombos[field.Key] = combo;
            _multiSelectContainers[field.Key] = container;
            _fieldIndicatorControls[field.Key] = combo;
        }
        else if (field.InputMode == TagInputMode.Dropdown)
        {
            var combo = new System.Windows.Controls.ComboBox
            {
                IsEditable = true,
                Style = (Style)FindResource("DarkCombo"),
                ItemContainerStyle = (Style)FindResource("DarkComboItem")
            };
            foreach (var val in vocabValues)
                combo.Items.Add(val);

            Grid.SetColumn(combo, 1);
            row.Children.Add(combo);
            _fieldControls[field.Key] = combo;
            _fieldIndicatorControls[field.Key] = combo;
        }
        else
        {
            var textBox = new System.Windows.Controls.TextBox
            {
                Style = (Style)FindResource("DarkTextBox")
            };
            Grid.SetColumn(textBox, 1);
            row.Children.Add(textBox);
            _fieldControls[field.Key] = textBox;
            _fieldIndicatorControls[field.Key] = textBox;
        }

        // Editor remove button for dynamic fields
        if (_isEditor && !field.IsBuiltIn && _masterStructureService != null)
        {
            var capturedFieldKey = field.Key;
            var removeFieldBtn = new TextBlock
            {
                Text = "\u2715",
                FontSize = 9,
                Foreground = Helpers.ThemeHelper.TextTertiary,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0),
                ToolTip = $"Remove field '{field.DisplayName}'"
            };
            removeFieldBtn.MouseEnter += (s, ev) => { if (s is TextBlock tb) tb.Foreground = Helpers.ThemeHelper.Red; };
            removeFieldBtn.MouseLeave += (s, ev) => { if (s is TextBlock tb) tb.Foreground = Helpers.ThemeHelper.TextTertiary; };
            removeFieldBtn.MouseLeftButtonDown += (s, ev) =>
            {
                ev.Handled = true;
                OnRemoveFieldClicked(capturedFieldKey, field.DisplayName);
            };
            Grid.SetColumn(removeFieldBtn, 2);
            row.Children.Add(removeFieldBtn);
            _editorActionElements.Add(removeFieldBtn);
        }

        fieldsContainer.Children.Add(row);
    }

    // --- Project Tags panel ---

    private void BuildProjectTagsPanel(StackPanel container)
    {
        _projectTagsChipPanel = null;
        _projectTagsKeyInput = null;
        _projectTagsValueInput = null;

        var panel = new StackPanel { Margin = new Thickness(4, 4, 4, 4) };

        // Input row: tag name + value (optional) + add button
        var inputRow = new Grid { Margin = new Thickness(0, 0, 0, 6) };
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        inputRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var keyInput = new System.Windows.Controls.TextBox
        {
            Style = (Style)FindResource("DarkTextBox"),
            FontSize = 11,
            Margin = new Thickness(0, 0, 3, 0)
        };
        // Watermark
        var keyWatermark = "Tag name";
        keyInput.Foreground = Helpers.ThemeHelper.TextTertiary;
        keyInput.Text = keyWatermark;
        keyInput.GotFocus += (s, ev) => { if (keyInput.Text == keyWatermark) { keyInput.Text = ""; keyInput.Foreground = Helpers.ThemeHelper.TextPrimary; } };
        keyInput.LostFocus += (s, ev) => { if (string.IsNullOrWhiteSpace(keyInput.Text)) { keyInput.Text = keyWatermark; keyInput.Foreground = Helpers.ThemeHelper.TextTertiary; } };
        keyInput.KeyDown += (s, ev) =>
        {
            if (ev.Key == System.Windows.Input.Key.Enter) { AddProjectTag(); ev.Handled = true; }
        };
        Grid.SetColumn(keyInput, 0);
        inputRow.Children.Add(keyInput);
        _projectTagsKeyInput = keyInput;

        var valueInput = new System.Windows.Controls.TextBox
        {
            Style = (Style)FindResource("DarkTextBox"),
            FontSize = 11,
            Margin = new Thickness(0, 0, 3, 0)
        };
        var valWatermark = "Value (optional)";
        valueInput.Foreground = Helpers.ThemeHelper.TextTertiary;
        valueInput.Text = valWatermark;
        valueInput.GotFocus += (s, ev) => { if (valueInput.Text == valWatermark) { valueInput.Text = ""; valueInput.Foreground = Helpers.ThemeHelper.TextPrimary; } };
        valueInput.LostFocus += (s, ev) => { if (string.IsNullOrWhiteSpace(valueInput.Text)) { valueInput.Text = valWatermark; valueInput.Foreground = Helpers.ThemeHelper.TextTertiary; } };
        valueInput.KeyDown += (s, ev) =>
        {
            if (ev.Key == System.Windows.Input.Key.Enter) { AddProjectTag(); ev.Handled = true; }
        };
        Grid.SetColumn(valueInput, 1);
        inputRow.Children.Add(valueInput);
        _projectTagsValueInput = valueInput;

        var addBtn = new System.Windows.Controls.Button
        {
            Content = "+",
            Style = (Style)FindResource("SmallAddButton"),
            Width = 24, Height = 24,
            Margin = new Thickness(1, 0, 0, 0)
        };
        addBtn.Click += (s, ev) => AddProjectTag();
        Grid.SetColumn(addBtn, 2);
        inputRow.Children.Add(addBtn);

        panel.Children.Add(inputRow);

        // Chip display area
        var chipPanel = new WrapPanel { Margin = new Thickness(0) };
        panel.Children.Add(chipPanel);
        _projectTagsChipPanel = chipPanel;

        // Empty state hint
        var hint = new TextBlock
        {
            Text = "No tags yet. Add a tag name and optional value above.",
            FontSize = 10,
            Foreground = Helpers.ThemeHelper.TextTertiary,
            Margin = new Thickness(0, 2, 0, 0),
            FontStyle = FontStyles.Italic
        };
        hint.Tag = "hint";
        panel.Children.Add(hint);

        container.Children.Add(panel);
        RebuildProjectTagChips();
    }

    private void AddProjectTag()
    {
        if (_projectTagsKeyInput == null || _projectTagsValueInput == null) return;
        var key = _projectTagsKeyInput.Text?.Trim();
        if (string.IsNullOrEmpty(key) || key == "Tag name") return;

        // Don't add duplicates
        if (_projectTagValues.Any(t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase))) return;

        var val = _projectTagsValueInput.Text?.Trim();
        if (val == "Value (optional)") val = "";

        _projectTagValues.Add(new KeyValuePair<string, string>(key, val ?? ""));

        // Reset inputs
        _projectTagsKeyInput.Text = "Tag name";
        _projectTagsKeyInput.Foreground = Helpers.ThemeHelper.TextTertiary;
        _projectTagsValueInput.Text = "Value (optional)";
        _projectTagsValueInput.Foreground = Helpers.ThemeHelper.TextTertiary;

        RebuildProjectTagChips();
    }

    private void RebuildProjectTagChips()
    {
        if (_projectTagsChipPanel == null) return;
        _projectTagsChipPanel.Children.Clear();

        foreach (var kvp in _projectTagValues)
        {
            var capturedKey = kvp.Key;
            var displayText = string.IsNullOrEmpty(kvp.Value) ? kvp.Key : $"{kvp.Key}: {kvp.Value}";

            var chipBorder = new Border
            {
                Background = Helpers.ThemeHelper.AccentLight,
                BorderBrush = Helpers.ThemeHelper.Accent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(8, 3, 6, 3),
                Margin = new Thickness(0, 2, 4, 2)
            };

            var chipStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            chipStack.Children.Add(new TextBlock
            {
                Text = displayText,
                FontSize = 11,
                Foreground = Helpers.ThemeHelper.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                MaxWidth = 200
            });

            var removeBtn = new TextBlock
            {
                Text = "\u2715",
                FontSize = 8,
                Foreground = Helpers.ThemeHelper.TextTertiary,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(6, 0, 0, 0)
            };
            removeBtn.MouseEnter += (s, ev) =>
            {
                if (s is TextBlock tb) tb.Foreground = Helpers.ThemeHelper.Red;
            };
            removeBtn.MouseLeave += (s, ev) =>
            {
                if (s is TextBlock tb) tb.Foreground = Helpers.ThemeHelper.TextTertiary;
            };
            removeBtn.MouseLeftButtonDown += (s, ev) =>
            {
                if (_isLocked) { ev.Handled = true; return; }
                _projectTagValues.RemoveAll(t => t.Key.Equals(capturedKey, StringComparison.OrdinalIgnoreCase));
                RebuildProjectTagChips();
                ev.Handled = true;
            };
            chipStack.Children.Add(removeBtn);

            chipBorder.Child = chipStack;
            _projectTagsChipPanel.Children.Add(chipBorder);
        }

        // Update hint visibility
        if (_projectTagsChipPanel.Parent is StackPanel parent)
        {
            foreach (var child in parent.Children)
            {
                if (child is TextBlock tb && tb.Tag is "hint")
                    tb.Visibility = _projectTagValues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }

    // --- Editor: Add Field / Add Category handlers ---

    private void OnAddFieldClicked(string category)
    {
        if (_masterStructureService == null) return;

        var dialog = new AddFieldDialog(
            _masterStructureService.GetMergedCategories(_currentProjectNumber)
                .Select(c => c.Name).ToList(),
            category,
            hasProject: _currentProjectNumber != null);

        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true && dialog.ResultField != null)
        {
            var username = Environment.UserName.ToLowerInvariant();
            if (dialog.IsMasterScope)
            {
                _ = _masterStructureService.AddMasterFieldAsync(dialog.ResultField, username);
                TelemetryAccessor.TrackEvent(TelemetryCategory.FieldManagement,
                    TelemetryEventType.MasterFieldAdded);
            }
            else if (_currentProjectNumber != null)
            {
                _ = _masterStructureService.AddProjectFieldAsync(_currentProjectNumber, dialog.ResultField, username);
                TelemetryAccessor.TrackEvent(TelemetryCategory.FieldManagement,
                    TelemetryEventType.ProjectFieldAdded);
            }
        }
    }

    private void OnAddCategoryClicked()
    {
        if (_masterStructureService == null) return;

        var dialog = new AddCategoryDialog(hasProject: _currentProjectNumber != null);
        dialog.Owner = Window.GetWindow(this);
        if (dialog.ShowDialog() == true && dialog.ResultCategory != null)
        {
            var username = Environment.UserName.ToLowerInvariant();
            if (dialog.IsMasterScope)
            {
                _ = _masterStructureService.AddMasterCategoryAsync(dialog.ResultCategory, username);
                TelemetryAccessor.TrackEvent(TelemetryCategory.FieldManagement,
                    TelemetryEventType.MasterCategoryAdded);
            }
            else if (_currentProjectNumber != null)
            {
                _ = _masterStructureService.AddProjectCategoryAsync(_currentProjectNumber, dialog.ResultCategory, username);
                TelemetryAccessor.TrackEvent(TelemetryCategory.FieldManagement,
                    TelemetryEventType.ProjectCategoryAdded);
            }
        }
    }

    private void OnRemoveFieldClicked(string fieldKey, string displayName)
    {
        if (_masterStructureService == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Remove the field '{displayName}'?\n\nThis will remove the field definition. Existing data in projects using this field will remain in custom storage.",
            "Remove Field",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var username = Environment.UserName.ToLowerInvariant();

        // Determine if it's a master or project-specific field
        var masterField = _masterStructureService.GetMasterStructure().Fields
            .Any(f => f.Key.Equals(fieldKey, StringComparison.OrdinalIgnoreCase));

        if (masterField)
        {
            _ = _masterStructureService.RemoveMasterFieldAsync(fieldKey, username);
        }
        else if (_currentProjectNumber != null)
        {
            _ = _masterStructureService.RemoveProjectFieldAsync(_currentProjectNumber, fieldKey, username);
        }
    }

    private void OnRemoveCategoryClicked(string categoryName)
    {
        if (_masterStructureService == null) return;

        var result = System.Windows.MessageBox.Show(
            $"Remove the category '{categoryName}' and all its dynamic fields?\n\nBuilt-in fields will not be affected. This cannot be undone.",
            "Remove Category",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes) return;

        var username = Environment.UserName.ToLowerInvariant();

        var masterCat = _masterStructureService.GetMasterStructure().Categories
            .Any(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));

        if (masterCat)
        {
            _ = _masterStructureService.RemoveMasterCategoryAsync(categoryName, username);
        }
        else if (_currentProjectNumber != null)
        {
            _ = _masterStructureService.RemoveProjectCategoryAsync(_currentProjectNumber, categoryName, username);
        }
    }

    private void CategoryHeader_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is StackPanel panel)
        {
            var isCollapsed = panel.Visibility == Visibility.Collapsed;
            panel.Visibility = isCollapsed ? Visibility.Visible : Visibility.Collapsed;

            // Rotate the arrow indicator
            if (border.Child is Grid headerGrid && headerGrid.Children.Count > 0)
            {
                var arrow = headerGrid.Children[headerGrid.Children.Count - 1] as TextBlock;
                if (arrow?.RenderTransform is System.Windows.Media.RotateTransform rt)
                    rt.Angle = isCollapsed ? 0 : -90;
            }

            e.Handled = true;
        }
    }

    private void SidebarTab_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not string catName) return;
        if (!_categoryPanels.TryGetValue(catName, out var panel)) return;
        if (!_categoryHeaders.TryGetValue(catName, out var header)) return;

        bool isOpen = panel.Visibility == Visibility.Visible;
        bool inView = IsElementInView(header);

        if (isOpen && inView)
        {
            // Open and visible → collapse
            SetCategoryExpanded(catName, false);
        }
        else if (isOpen && !inView)
        {
            // Open but scrolled out of view → scroll to it
            header.BringIntoView();
        }
        else if (!isOpen && inView)
        {
            // Closed and visible → expand
            SetCategoryExpanded(catName, true);
        }
        else
        {
            // Closed and not visible → expand + scroll
            SetCategoryExpanded(catName, true);
            // Delay BringIntoView so layout updates after expand
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded,
                new Action(() => header.BringIntoView()));
        }

        e.Handled = true;
    }

    private void SetCategoryExpanded(string catName, bool expand)
    {
        if (!_categoryPanels.TryGetValue(catName, out var panel)) return;
        panel.Visibility = expand ? Visibility.Visible : Visibility.Collapsed;

        if (_categoryHeaders.TryGetValue(catName, out var header)
            && header.Child is Grid headerGrid && headerGrid.Children.Count > 0)
        {
            var arrow = headerGrid.Children[headerGrid.Children.Count - 1] as TextBlock;
            if (arrow?.RenderTransform is System.Windows.Media.RotateTransform rt)
                rt.Angle = expand ? 0 : -90;
        }
    }

    private bool IsElementInView(FrameworkElement element)
    {
        if (FieldsScrollViewer == null || element.Visibility != Visibility.Visible) return false;
        try
        {
            var transform = element.TransformToAncestor(FieldsScrollViewer);
            var position = transform.Transform(new System.Windows.Point(0, 0));
            return position.Y >= -element.ActualHeight && position.Y < FieldsScrollViewer.ViewportHeight;
        }
        catch { return false; }
    }

    private void CloseButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private void LockButton_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        _isLocked = !_isLocked;
        UpdateLockState();
        e.Handled = true;
    }

    private void UpdateLockState()
    {
        LockIcon.Text = _isLocked ? "\U0001F512" : "\U0001F513";
        LockText.Text = _isLocked ? "Edit" : "Lock";
        LockButton.ToolTip = _isLocked ? "Click to unlock editing" : "Click to lock editing";
        // Only show lock button when a project is loaded
        LockButton.Visibility = _currentProjectNumber != null ? Visibility.Visible : Visibility.Collapsed;
        UpdateFieldsEnabled();
    }

    private void UpdateFieldsEnabled()
    {
        bool enabled = !_isLocked && _currentProjectNumber != null;
        foreach (var control in _fieldControls.Values)
            control.IsEnabled = enabled;
        foreach (var container in _multiSelectContainers.Values)
            container.IsEnabled = enabled;
        if (_projectTagsKeyInput != null)
            _projectTagsKeyInput.IsEnabled = enabled;
        if (_projectTagsValueInput != null)
            _projectTagsValueInput.IsEnabled = enabled;
        SaveButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
        var editorVis = enabled ? Visibility.Visible : Visibility.Collapsed;
        foreach (var el in _editorActionElements)
            el.Visibility = editorVis;
    }

    /// <summary>
    /// Re-lock the widget (called when overlay is hidden/closed).
    /// </summary>
    public void ResetLock()
    {
        _isLocked = true;
        UpdateLockState();
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentProjectNumber)) return;

        var newTags = new ProjectTags();

        // Save single-value fields
        foreach (var (key, control) in _fieldControls)
        {
            var val = GetControlText(control).Trim();
            if (string.IsNullOrEmpty(val)) continue;
            SetTagField(newTags, key, val);
        }

        // Save multi-select fields
        foreach (var (key, values) in _multiSelectValues)
        {
            if (values.Count == 0) continue;
            var joined = string.Join(", ", values);
            SetTagField(newTags, key, joined);
        }

        // Save custom project tags from the dedicated Project Tags panel
        foreach (var kvp in _projectTagValues)
        {
            if (!string.IsNullOrEmpty(kvp.Key))
                newTags.Custom[kvp.Key] = kvp.Value;
        }

        var isNew = !_tagService.HasTags(_currentProjectNumber);
        await _tagService.SaveTagsAsync(_currentProjectNumber, newTags);

        var filledCount = _fieldControls.Count(kv => !string.IsNullOrWhiteSpace(GetControlText(kv.Value)))
                        + _multiSelectValues.Count(kv => kv.Value.Count > 0);

        TelemetryAccessor.TrackTag(
            isNew ? TelemetryEventType.TagCreated : TelemetryEventType.TagUpdated,
            projectNumber: _currentProjectNumber,
            tagCount: filledCount,
            source: "project_info_widget");

        UpdateMissingIndicators();
        StatusText.Text = $"Tags saved for {_currentProjectNumber}";
    }

    private void ClearAllFields()
    {
        foreach (var control in _fieldControls.Values)
            SetControlText(control, "");

        foreach (var key in _multiSelectValues.Keys)
        {
            _multiSelectValues[key].Clear();
            RebuildMultiSelectChips(key);
        }

        _projectTagValues.Clear();
        if (_projectTagsChipPanel != null)
            RebuildProjectTagChips();
    }

    private void RebuildMultiSelectChips(string fieldKey)
    {
        if (!_multiSelectChipPanels.TryGetValue(fieldKey, out var chipPanel)) return;
        chipPanel.Children.Clear();

        if (!_multiSelectValues.TryGetValue(fieldKey, out var values) || values.Count == 0)
            return;

        foreach (var val in values)
        {
            var capturedVal = val;
            var chipBorder = new Border
            {
                Background = Helpers.ThemeHelper.AccentLight,
                BorderBrush = Helpers.ThemeHelper.Accent,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1, 3, 1),
                Margin = new Thickness(0, 1, 3, 1)
            };

            var chipStack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
            chipStack.Children.Add(new TextBlock
            {
                Text = val,
                FontSize = 10,
                Foreground = Helpers.ThemeHelper.TextPrimary,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 160
            });

            var removeBtn = new TextBlock
            {
                Text = "\u2715",
                FontSize = 7,
                Foreground = Helpers.ThemeHelper.TextTertiary,
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0)
            };
            removeBtn.MouseEnter += (s, ev) =>
            {
                if (s is TextBlock tb)
                    tb.Foreground = Helpers.ThemeHelper.Red;
            };
            removeBtn.MouseLeave += (s, ev) =>
            {
                if (s is TextBlock tb)
                    tb.Foreground = Helpers.ThemeHelper.TextTertiary;
            };
            removeBtn.MouseLeftButtonDown += (s, ev) =>
            {
                if (_isLocked) { ev.Handled = true; return; }
                if (_multiSelectValues.TryGetValue(fieldKey, out var vals))
                {
                    vals.Remove(capturedVal);
                    RebuildMultiSelectChips(fieldKey);
                    UpdateMissingIndicators();
                }
                ev.Handled = true;
            };
            chipStack.Children.Add(removeBtn);

            chipBorder.Child = chipStack;
            chipPanel.Children.Add(chipBorder);
        }
    }

    private static System.Windows.Media.SolidColorBrush _orangeOutlineBrush =>
        Helpers.ThemeHelper.Orange;
    private static System.Windows.Media.SolidColorBrush _normalBorderBrush =>
        Helpers.ThemeHelper.Border;

    private void UpdateMissingIndicators()
    {
        if (_currentProjectNumber == null)
        {
            HideMissingIndicators();
            return;
        }

        var mergedFields = _masterStructureService?.GetMergedFields(_currentProjectNumber) ?? GetBuiltInFields();
        foreach (var field in mergedFields)
        {
            if (!_fieldIndicatorControls.TryGetValue(field.Key, out var control)) continue;

            bool hasValue;
            if (field.InputMode == TagInputMode.MultiSelect)
                hasValue = _multiSelectValues.TryGetValue(field.Key, out var vals) && vals.Count > 0;
            else
                hasValue = _fieldControls.TryGetValue(field.Key, out var ctrl) && !string.IsNullOrWhiteSpace(GetControlText(ctrl));

            ApplyMissingOutline(control, !hasValue);
        }
    }

    private void HideMissingIndicators()
    {
        foreach (var control in _fieldIndicatorControls.Values)
            ApplyMissingOutline(control, false);
    }

    private static void ApplyMissingOutline(WpfControl control, bool showOutline)
    {
        // For ComboBox: walk visual tree to find the ToggleButton's Border named "Bd"
        // For TextBox: set BorderBrush directly (style triggers will override when focused/hovered)
        if (control is System.Windows.Controls.TextBox tb)
        {
            tb.BorderBrush = showOutline ? _orangeOutlineBrush : _normalBorderBrush;
        }
        else if (control is System.Windows.Controls.ComboBox combo)
        {
            // Apply via Tag so the DarkCombo template can read it
            combo.Tag = showOutline ? "missing" : null;
            // Walk visual tree to find the toggle button border
            var bd = FindVisualChildByName<Border>(combo, "Bd");
            if (bd != null)
                bd.BorderBrush = showOutline ? _orangeOutlineBrush : _normalBorderBrush;
        }
    }

    private static T? FindVisualChildByName<T>(System.Windows.DependencyObject parent, string name) where T : FrameworkElement
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T fe && fe.Name == name) return fe;
            var result = FindVisualChildByName<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }

    private static string GetControlText(System.Windows.Controls.Control control) => control switch
    {
        System.Windows.Controls.ComboBox combo => combo.Text ?? "",
        System.Windows.Controls.TextBox tb => tb.Text ?? "",
        _ => ""
    };

    private static void SetControlText(System.Windows.Controls.Control control, string text)
    {
        if (control is System.Windows.Controls.ComboBox combo) combo.Text = text;
        else if (control is System.Windows.Controls.TextBox tb) tb.Text = text;
    }

    private static string? GetTagValue(ProjectTags tags, string key) => key.ToLowerInvariant() switch
    {
        "voltage" => tags.Voltage,
        "phase" => tags.Phase,
        "amperage_service" => tags.AmperageService,
        "amperage_generator" => tags.AmperageGenerator,
        "generator_brand" => tags.GeneratorBrand,
        "generator_load_kw" => tags.GeneratorLoadKw,
        "hvac_type" => tags.HvacType,
        "hvac_brand" => tags.HvacBrand,
        "hvac_tonnage" => tags.HvacTonnage,
        "hvac_load_kw" => tags.HvacLoadKw,
        "square_footage" => tags.SquareFootage,
        "build_type" => tags.BuildType,
        "location_city" => tags.LocationCity,
        "location_state" => tags.LocationState,
        "location_municipality" => tags.LocationMunicipality,
        "location_address" => tags.LocationAddress,
        "stamping_engineer" => tags.StampingEngineer,
        "lighting_designer" => tags.LightingDesigner,
        "av_it_designer" => tags.AvItDesigner,
        "engineers" => tags.Engineers.Count > 0 ? string.Join(", ", tags.Engineers) : null,
        "code_refs" => tags.CodeReferences.Count > 0 ? string.Join(", ", tags.CodeReferences) : null,
        "project_labels" => tags.ProjectLabels.Count > 0 ? string.Join(", ", tags.ProjectLabels) : null,
        _ => tags.Custom.TryGetValue(key, out var customVal) ? customVal : null
    };

    private static void SetTagField(ProjectTags tags, string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "voltage": tags.Voltage = value; break;
            case "phase": tags.Phase = value; break;
            case "amperage_service": tags.AmperageService = value; break;
            case "amperage_generator": tags.AmperageGenerator = value; break;
            case "generator_brand": tags.GeneratorBrand = value; break;
            case "generator_load_kw": tags.GeneratorLoadKw = value; break;
            case "hvac_type": tags.HvacType = value; break;
            case "hvac_brand": tags.HvacBrand = value; break;
            case "hvac_tonnage": tags.HvacTonnage = value; break;
            case "hvac_load_kw": tags.HvacLoadKw = value; break;
            case "square_footage": tags.SquareFootage = value; break;
            case "build_type": tags.BuildType = value; break;
            case "location_city": tags.LocationCity = value; break;
            case "location_state": tags.LocationState = value; break;
            case "location_municipality": tags.LocationMunicipality = value; break;
            case "location_address": tags.LocationAddress = value; break;
            case "stamping_engineer": tags.StampingEngineer = value; break;
            case "lighting_designer": tags.LightingDesigner = value; break;
            case "av_it_designer": tags.AvItDesigner = value; break;
            case "engineers":
                tags.Engineers = value.Split(',', StringSplitOptions.TrimEntries).Where(s => s.Length > 0).ToList();
                break;
            case "code_refs":
                tags.CodeReferences = value.Split(',', StringSplitOptions.TrimEntries).Where(s => s.Length > 0).ToList();
                break;
            case "project_labels":
                tags.ProjectLabels = value.Split(',', StringSplitOptions.TrimEntries).Where(s => s.Length > 0).ToList();
                break;
            default:
                tags.Custom[key] = value;
                break;
        }
    }
}
