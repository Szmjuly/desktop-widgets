using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using DesktopHub.Core.Abstractions;

// Resolve WPF+WinForms ambiguity
using WpfControl = System.Windows.Controls.Control;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfTextBox = System.Windows.Controls.TextBox;
using DesktopHub.Core.Models;
using DesktopHub.UI.Services;

namespace DesktopHub.UI.Widgets;

public partial class ProjectInfoWidget : System.Windows.Controls.UserControl
{
    private readonly IProjectTagService _tagService;
    private readonly ITagVocabularyService? _vocabService;

    public event EventHandler? CloseRequested;

    private string? _currentProjectNumber;
    private string? _currentProjectPath;
    private readonly Dictionary<string, System.Windows.Controls.Control> _fieldControls = new();
    private bool _isLocked = true;

    // Multi-select fields (Engineers, Code References)
    private readonly Dictionary<string, WrapPanel> _multiSelectChipPanels = new();
    private readonly Dictionary<string, List<string>> _multiSelectValues = new();
    private readonly Dictionary<string, System.Windows.Controls.ComboBox> _multiSelectCombos = new();
    private readonly Dictionary<string, StackPanel> _multiSelectContainers = new();

    // Missing field indicators — references to the input control borders for orange outline
    private readonly Dictionary<string, WpfControl> _fieldIndicatorControls = new();

    public ProjectInfoWidget(IProjectTagService tagService, ITagVocabularyService? vocabService)
    {
        InitializeComponent();
        _tagService = tagService;
        _vocabService = vocabService;

        BuildFieldUI();
        UpdateLockState();
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

        var tags = await _tagService.GetTagsAsync(projectNumber);

        ClearAllFields();

        if (tags != null)
        {
            foreach (var field in TagFieldRegistry.Fields)
            {
                var value = GetTagValue(tags, field.Key);
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

    private static readonly Dictionary<string, string> CategoryIcons = new()
    {
        ["Electrical"] = "\u26A1",
        ["Mechanical"] = "\u2744",
        ["Building"]   = "\U0001F3E0",
        ["Location"]   = "\U0001F4CD",
        ["People"]     = "\U0001F465",
        ["Code"]       = "\U0001F4D6",
        ["Other"]      = "\U0001F4CB"
    };

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

        var categories = TagFieldRegistry.Fields.GroupBy(f => f.Category ?? "Other").ToList();

        foreach (var category in categories)
        {
            var catName = category.Key;
            var icon = CategoryIcons.GetValueOrDefault(catName, "\U0001F4CB");

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
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(160, 170, 185)),
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

            var headerIcon = new TextBlock
            {
                Text = icon, FontSize = 12,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(140, 155, 175)),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(headerIcon, 0);
            headerGrid.Children.Add(headerIcon);

            var headerText = new TextBlock
            {
                Text = catName, FontSize = 11, FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(100, 155, 240)),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(headerText, 1);
            headerGrid.Children.Add(headerText);

            var collapseArrow = new TextBlock
            {
                Text = "\u25BC", FontSize = 8,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(120, 130, 140)),
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                RenderTransform = new System.Windows.Media.RotateTransform(0)
            };
            Grid.SetColumn(collapseArrow, 3);
            headerGrid.Children.Add(collapseArrow);

            headerBorder.Child = headerGrid;
            headerBorder.MouseLeftButtonDown += CategoryHeader_Click;

            FieldsPanel.Children.Add(headerBorder);
            _categoryHeaders[catName] = headerBorder;

            // --- Field rows inside the collapsible container ---
            foreach (var field in category)
            {
                var row = new Grid { Margin = new Thickness(4, 4, 4, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(115) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var label = new TextBlock
                {
                    Text = field.DisplayName,
                    Style = (Style)FindResource("FieldLabel")
                };
                Grid.SetColumn(label, 0);
                row.Children.Add(label);

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
                    var vocabValues = _vocabService?.GetValues(field.Key) ?? field.SuggestedValues.ToList();
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
                    var vocabValues = _vocabService?.GetValues(field.Key) ?? field.SuggestedValues.ToList();
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

                fieldsContainer.Children.Add(row);
            }

            FieldsPanel.Children.Add(fieldsContainer);
            _categoryPanels[catName] = fieldsContainer;
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
        SaveButton.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
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

            if (_vocabService != null)
                await _vocabService.AddValueAsync(key, val);
        }

        // Save multi-select fields
        foreach (var (key, values) in _multiSelectValues)
        {
            if (values.Count == 0) continue;
            var joined = string.Join(", ", values);
            SetTagField(newTags, key, joined);

            if (_vocabService != null)
                await _vocabService.AddValuesAsync(key, values);
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
                Background = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(0x18, 100, 155, 240)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromArgb(0x40, 100, 155, 240)),
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
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(180, 200, 220)),
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 160
            });

            var removeBtn = new TextBlock
            {
                Text = "\u2715",
                FontSize = 7,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(120, 120, 130)),
                VerticalAlignment = VerticalAlignment.Center,
                Cursor = System.Windows.Input.Cursors.Hand,
                Margin = new Thickness(4, 0, 0, 0)
            };
            removeBtn.MouseEnter += (s, ev) =>
            {
                if (s is TextBlock tb)
                    tb.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(220, 80, 80));
            };
            removeBtn.MouseLeave += (s, ev) =>
            {
                if (s is TextBlock tb)
                    tb.Foreground = new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromRgb(120, 120, 130));
            };
            removeBtn.MouseLeftButtonDown += (s, ev) =>
            {
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

    private static readonly System.Windows.Media.SolidColorBrush _orangeOutlineBrush =
        new(System.Windows.Media.Color.FromArgb(0x90, 255, 152, 0));
    private static readonly System.Windows.Media.SolidColorBrush _normalBorderBrush =
        new(System.Windows.Media.Color.FromRgb(0x48, 0x48, 0x48));

    private void UpdateMissingIndicators()
    {
        if (_currentProjectNumber == null)
        {
            HideMissingIndicators();
            return;
        }

        foreach (var field in TagFieldRegistry.Fields)
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
        "engineers" => tags.Engineers.Count > 0 ? string.Join(", ", tags.Engineers) : null,
        "code_refs" => tags.CodeReferences.Count > 0 ? string.Join(", ", tags.CodeReferences) : null,
        _ => null
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
            case "engineers":
                tags.Engineers = value.Split(',', StringSplitOptions.TrimEntries).Where(s => s.Length > 0).ToList();
                break;
            case "code_refs":
                tags.CodeReferences = value.Split(',', StringSplitOptions.TrimEntries).Where(s => s.Length > 0).ToList();
                break;
            default:
                tags.Custom[key] = value;
                break;
        }
    }
}
