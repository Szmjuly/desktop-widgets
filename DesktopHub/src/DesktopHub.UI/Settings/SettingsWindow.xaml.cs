using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Settings;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;

namespace DesktopHub.UI;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settings;
    private readonly TaskService? _taskService;
    private readonly DocOpenService? _docService;
    private bool _isRecordingCloseShortcut;
    private int _recordedCloseShortcutModifiers;
    private int _recordedCloseShortcutKey;
    private Action? _onHotkeyChanged;
    private Action? _onHotkeyReleaseForProbe;
    private Func<IReadOnlyCollection<(int mods, int key)>>? _failedHotkeysProvider;
    private Action? _onCloseShortcutChanged;
    private Action? _onLivingWidgetsModeChanged;
    private Action? _onDriveSettingsChanged;
    private Action? _onTransparencyChanged;
    private Action? _onSearchWidgetEnabledChanged;
    private Action? _onTimerWidgetEnabledChanged;
    private Action? _onQuickTasksWidgetEnabledChanged;
    private Action? _onDocWidgetEnabledChanged;
    private Action? _onUpdateSettingsChanged;
    private Action? _onFrequentProjectsWidgetEnabledChanged;
    private Action? _onFrequentProjectsLayoutChanged;
    private Action? _onQuickLaunchWidgetEnabledChanged;
    private Action? _onQuickLaunchLayoutChanged;
    private Action? _onWidgetLauncherLayoutChanged;
    private Action? _onSmartProjectSearchWidgetEnabledChanged;
    private Action? _onWidgetSnapGapChanged;
    private IProjectLaunchDataStore? _launchDataStore;
    private bool _isUpdatingSliders;
    private bool _isLoadingQTSettings;
    private bool _isLoadingDQSettings;
    private bool _isLoadingFPSettings;
    private bool _isLoadingQLSettings;
    private bool _isLoadingSPSettings;
    // Dynamic UI element tracking (populated from WidgetRegistry)
    private readonly Dictionary<string, System.Windows.Controls.Slider> _transparencySliders = new();
    private readonly Dictionary<string, System.Windows.Controls.Button> _linkButtons = new();
    private readonly Dictionary<string, Border> _groupBadges = new();
    private readonly Dictionary<string, TextBlock> _percentLabels = new();
    private readonly Dictionary<string, System.Windows.Controls.Primitives.ToggleButton> _widgetToggles = new();
    private readonly Dictionary<string, System.Windows.Controls.RadioButton> _widgetNavButtons = new();
    private readonly Dictionary<string, Border> _widgetPanels = new();
    private readonly Dictionary<string, Action?> _widgetEnabledCallbacks = new();
    // Group hotkey recording state
    private Border? _activeGroupKeyBox;
    private TextBlock? _activeGroupKeyText;
    private TextBlock? _activeGroupRecordingText;
    private int _activeGroupIndex = -1;

    // When true (via the delegate set at construction), the current user is in
    // dev_users / has tier=dev and is allowed to see the Developer Panel toggle
    // in the Widget Launcher settings tab. Non-dev users never see that row.
    private readonly Func<bool> _isDeveloperUser;

    public SettingsWindow(ISettingsService settings, Action? onHotkeyChanged = null, Action? onCloseShortcutChanged = null, Action? onLivingWidgetsModeChanged = null, Action? onDriveSettingsChanged = null, Action? onTransparencyChanged = null, TaskService? taskService = null, DocOpenService? docService = null, Action? onSearchWidgetEnabledChanged = null, Action? onTimerWidgetEnabledChanged = null, Action? onQuickTasksWidgetEnabledChanged = null, Action? onDocWidgetEnabledChanged = null, Action? onUpdateSettingsChanged = null, Action? onFrequentProjectsWidgetEnabledChanged = null, Action? onFrequentProjectsLayoutChanged = null, Action? onQuickLaunchWidgetEnabledChanged = null, IProjectLaunchDataStore? launchDataStore = null, Action? onQuickLaunchLayoutChanged = null, Action? onWidgetSnapGapChanged = null, Action? onSmartProjectSearchWidgetEnabledChanged = null, Action? onWidgetLauncherLayoutChanged = null, Action? onCheatSheetWidgetEnabledChanged = null, Action? onMetricsViewerWidgetEnabledChanged = null, Action? onDeveloperPanelWidgetEnabledChanged = null, Action? onHotkeyReleaseForProbe = null, Func<IReadOnlyCollection<(int mods, int key)>>? failedHotkeysProvider = null, Func<bool>? isDeveloperUser = null)
    {
        _settings = settings;
        _taskService = taskService;
        _docService = docService;
        _isDeveloperUser = isDeveloperUser ?? (() => false);
        _onHotkeyChanged = onHotkeyChanged;
        _onHotkeyReleaseForProbe = onHotkeyReleaseForProbe;
        _failedHotkeysProvider = failedHotkeysProvider;
        _onCloseShortcutChanged = onCloseShortcutChanged;
        _onLivingWidgetsModeChanged = onLivingWidgetsModeChanged;
        _onDriveSettingsChanged = onDriveSettingsChanged;
        _onTransparencyChanged = onTransparencyChanged;
        _onSearchWidgetEnabledChanged = onSearchWidgetEnabledChanged;
        _onTimerWidgetEnabledChanged = onTimerWidgetEnabledChanged;
        _onQuickTasksWidgetEnabledChanged = onQuickTasksWidgetEnabledChanged;
        _onDocWidgetEnabledChanged = onDocWidgetEnabledChanged;
        _onUpdateSettingsChanged = onUpdateSettingsChanged;
        _onFrequentProjectsWidgetEnabledChanged = onFrequentProjectsWidgetEnabledChanged;
        _onFrequentProjectsLayoutChanged = onFrequentProjectsLayoutChanged;
        _onQuickLaunchWidgetEnabledChanged = onQuickLaunchWidgetEnabledChanged;
        _onQuickLaunchLayoutChanged = onQuickLaunchLayoutChanged;
        _onSmartProjectSearchWidgetEnabledChanged = onSmartProjectSearchWidgetEnabledChanged;
        _onWidgetLauncherLayoutChanged = onWidgetLauncherLayoutChanged;
        _onWidgetSnapGapChanged = onWidgetSnapGapChanged;
        _launchDataStore = launchDataStore;

        // Populate widget-enabled callback dictionary from constructor params
        _widgetEnabledCallbacks[WidgetIds.Timer] = onTimerWidgetEnabledChanged;
        _widgetEnabledCallbacks[WidgetIds.QuickTasks] = onQuickTasksWidgetEnabledChanged;
        _widgetEnabledCallbacks[WidgetIds.DocQuickOpen] = onDocWidgetEnabledChanged;
        _widgetEnabledCallbacks[WidgetIds.FrequentProjects] = onFrequentProjectsWidgetEnabledChanged;
        _widgetEnabledCallbacks[WidgetIds.QuickLaunch] = onQuickLaunchWidgetEnabledChanged;
        _widgetEnabledCallbacks[WidgetIds.SmartProjectSearch] = onSmartProjectSearchWidgetEnabledChanged;
        _widgetEnabledCallbacks[WidgetIds.CheatSheet] = onCheatSheetWidgetEnabledChanged;
        _widgetEnabledCallbacks[WidgetIds.MetricsViewer] = onMetricsViewerWidgetEnabledChanged;
        _widgetEnabledCallbacks[WidgetIds.DeveloperPanel] = onDeveloperPanelWidgetEnabledChanged;
        _widgetEnabledCallbacks[WidgetIds.SearchOverlay] = onSearchWidgetEnabledChanged;

        // Suppress all slider/control events during XAML initialization
        _isUpdatingSliders = true;
        _isLoadingQTSettings = true;
        _isLoadingDQSettings = true;
        _isLoadingFPSettings = true;
        _isLoadingQLSettings = true;
        _isLoadingSPSettings = true;

        InitializeComponent();

        // Build dynamic UI from WidgetRegistry (transparency sliders, toggles, nav menu)
        BuildDynamicUI();

        // Re-enable event handlers now that all XAML elements exist
        _isUpdatingSliders = false;
        _isLoadingQTSettings = false;
        _isLoadingDQSettings = false;
        _isLoadingFPSettings = false;
        _isLoadingQLSettings = false;
        _isLoadingSPSettings = false;

        // Setup transparency when window handle is available
        SourceInitialized += (s, e) =>
        {
            WindowBlur.SetupTransparency(this);
            
            // DO NOT apply rounded corners to window region - it clips the window
            // WindowBlur.ApplyRoundedCorners(this, 12);
            
            WindowHelper.UpdateRootClip(RootBorder, 12, "SettingsWindow");
            
            // Ensure window background is completely transparent
            this.Background = null;
        };

        // Update clip on resize
        SizeChanged += (s, e) =>
        {
            // DO NOT apply window region rounding - causes clipping
            // WindowBlur.ApplyRoundedCorners(this, 12);
            WindowHelper.UpdateRootClip(RootBorder, 12, "SettingsWindow");
        };

        // Load settings after initialization
        this.Loaded += async (s, e) =>
        {
            // DISABLE blur - it renders as solid black
            // WindowBlur.EnableBlur(this, useAcrylic: true);
            
            await LoadSettingsAsync();
        };

        // Subscribe to theme changes — rebuild dynamic UI so baked-in colors refresh
        var app = (App)System.Windows.Application.Current;
        if (app.Theme != null)
        {
            app.Theme.ThemeChanged += OnThemeChanged;
        }
    }

    // ===== Dynamic UI generation from WidgetRegistry =====

    private void BuildDynamicUI()
    {
        BuildTransparencySliders();
        BuildWidgetToggles();
        BuildWidgetNavMenu();
        RegisterWidgetPanels();
    }

    // Group colors: A=blue, B=green, C=purple
    private static readonly Dictionary<string, System.Windows.Media.Color> GroupColors = new()
    {
        { "A", System.Windows.Media.Color.FromRgb(100, 155, 240) },
        { "B", System.Windows.Media.Color.FromRgb(76, 175, 80) },
        { "C", System.Windows.Media.Color.FromRgb(171, 71, 188) },
    };

    private void BuildTransparencySliders()
    {
        if (TransparencySlidersContainer == null) return;
        TransparencySlidersContainer.Children.Clear();
        _transparencySliders.Clear();
        _groupBadges.Clear();
        _percentLabels.Clear();

        // Settings Window (special entry, not in WidgetRegistry)
        AddTransparencyRow("settings", "⚙", "Settings Window", 0.78);

        // All registered widgets
        foreach (var entry in WidgetRegistry.WithTransparencySlider)
            AddTransparencyRow(entry.Id, entry.Icon, entry.DisplayName, entry.DefaultTransparency);
    }

    private void AddTransparencyRow(string id, string icon, string name, double defaultValue)
    {
        // Outer card with colored left border for group indication
        var card = new Border
        {
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 2, 0, 2),
            Padding = new Thickness(10, 7, 10, 7),
            BorderThickness = new Thickness(2, 0, 0, 0),
            BorderBrush = System.Windows.Media.Brushes.Transparent,
            Tag = id
        };
        card.SetResourceReference(Border.BackgroundProperty, "FaintOverlayBrush");

        var rowGrid = new System.Windows.Controls.Grid();
        // Icon | Name | GroupBadge | Slider | Percent
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // icon
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // name
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });  // group badge
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) }); // slider
        rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(36) }); // percent

        // Icon
        var iconBlock = new TextBlock
        {
            Text = icon, FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0)
        };
        System.Windows.Controls.Grid.SetColumn(iconBlock, 0);
        rowGrid.Children.Add(iconBlock);

        // Name
        var nameBlock = new TextBlock
        {
            Text = name, FontSize = 12, FontWeight = FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        nameBlock.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        System.Windows.Controls.Grid.SetColumn(nameBlock, 1);
        rowGrid.Children.Add(nameBlock);

        // Group badge (clickable, cycles — → A → B → C → —)
        var badgeText = new TextBlock
        {
            Text = "—", FontSize = 10, FontWeight = FontWeights.Bold,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        badgeText.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");
        var badge = new Border
        {
            Width = 22, Height = 22,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(6, 0, 8, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            ToolTip = "Click to cycle group: — → A → B → C → —",
            Child = badgeText,
            Tag = id
        };
        badge.SetResourceReference(Border.BackgroundProperty, "CardBrush");
        badge.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
        badge.MouseLeftButtonDown += (s, e) => { OnGroupBadgeClick(id); e.Handled = true; };
        System.Windows.Controls.Grid.SetColumn(badge, 2);
        rowGrid.Children.Add(badge);
        _groupBadges[id] = badge;

        // Slider
        var slider = new System.Windows.Controls.Slider
        {
            Minimum = 0.4, Maximum = 0.95, Value = defaultValue,
            IsSnapToTickEnabled = false,
            SmallChange = 0.01, LargeChange = 0.05,
            VerticalAlignment = VerticalAlignment.Center,
            Tag = id,
            Style = (Style)FindResource("StyledSlider")
        };
        slider.ValueChanged += OnDynamicTransparencySliderChanged;
        System.Windows.Controls.Grid.SetColumn(slider, 3);
        rowGrid.Children.Add(slider);
        _transparencySliders[id] = slider;

        // Percentage label
        var pctLabel = new TextBlock
        {
            Text = $"{(int)(defaultValue * 100)}%",
            FontSize = 10, FontWeight = FontWeights.Medium,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(4, 0, 0, 0)
        };
        pctLabel.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        System.Windows.Controls.Grid.SetColumn(pctLabel, 4);
        rowGrid.Children.Add(pctLabel);
        _percentLabels[id] = pctLabel;

        card.Child = rowGrid;
        TransparencySlidersContainer.Children.Add(card);
    }

    private void BuildWidgetToggles()
    {
        if (WidgetTogglesContainer == null) return;
        WidgetTogglesContainer.Children.Clear();

        // Special: Search Widget toggle (not in registry as a launcher toggle)
        AddWidgetToggleRow(WidgetTogglesContainer, "search_button", "Search Widget", "Search button in the Widget Launcher", true);

        var isDev = _isDeveloperUser();
        foreach (var entry in WidgetRegistry.WithLauncherToggle)
        {
            // Hide the Developer Panel row from non-dev users so the toggle isn't
            // even visible. Dev users see it and can toggle it like any other widget.
            if (entry.Id == WidgetIds.DeveloperPanel && !isDev)
                continue;

            AddWidgetToggleRow(WidgetTogglesContainer, entry.Id, entry.DisplayName, entry.Description, true);
        }
    }

    private void AddWidgetToggleRow(System.Windows.Controls.StackPanel container, string id, string name, string description, bool defaultChecked)
    {
        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new System.Windows.Controls.StackPanel();
        var nameText = new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeights.Medium, Margin = new Thickness(0, 0, 0, 2) };
        nameText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        info.Children.Add(nameText);
        var descText = new TextBlock { Text = description, FontSize = 11 };
        descText.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        info.Children.Add(descText);
        System.Windows.Controls.Grid.SetColumn(info, 0);
        grid.Children.Add(info);

        var toggle = new System.Windows.Controls.Primitives.ToggleButton
        {
            Width = 50, Height = 28,
            VerticalAlignment = VerticalAlignment.Center,
            IsChecked = defaultChecked,
            Cursor = System.Windows.Input.Cursors.Hand,
            Tag = id
        };
        // Apply toggle switch template
        var template = CreateToggleSwitchTemplate();
        toggle.Style = new Style(typeof(System.Windows.Controls.Primitives.ToggleButton)) { Setters = { new Setter(System.Windows.Controls.Primitives.ToggleButton.TemplateProperty, template) } };
        var capturedId = id;
        toggle.Checked += (s, e) => OnDynamicWidgetToggleChanged(capturedId, true);
        toggle.Unchecked += (s, e) => OnDynamicWidgetToggleChanged(capturedId, false);
        System.Windows.Controls.Grid.SetColumn(toggle, 1);
        grid.Children.Add(toggle);
        _widgetToggles[id] = toggle;

        container.Children.Add(grid);
    }

    private ControlTemplate CreateToggleSwitchTemplate()
    {
        var template = new ControlTemplate(typeof(System.Windows.Controls.Primitives.ToggleButton));
        var borderFactory = new FrameworkElementFactory(typeof(Border), "Border");
        borderFactory.SetResourceReference(Border.BackgroundProperty, "ToggleOffBrush");
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
        borderFactory.SetValue(Border.WidthProperty, 50.0);
        borderFactory.SetValue(Border.HeightProperty, 28.0);
        var thumbFactory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse), "Thumb");
        thumbFactory.SetValue(System.Windows.Shapes.Ellipse.WidthProperty, 20.0);
        thumbFactory.SetValue(System.Windows.Shapes.Ellipse.HeightProperty, 20.0);
        thumbFactory.SetResourceReference(System.Windows.Shapes.Ellipse.FillProperty, "ToggleThumbBrush");
        thumbFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Left);
        thumbFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 0, 0));
        borderFactory.AppendChild(thumbFactory);
        template.VisualTree = borderFactory;

        var toggleOnBrush = FindResource("ToggleOnBrush");
        var checkedTrigger = new Trigger { Property = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Value = true };
        checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, toggleOnBrush, "Border"));
        checkedTrigger.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Right, "Thumb"));
        checkedTrigger.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0), "Thumb"));
        template.Triggers.Add(checkedTrigger);

        return template;
    }

    private void BuildWidgetNavMenu()
    {
        if (WidgetNavMenuContainer == null) return;
        WidgetNavMenuContainer.Children.Clear();

        foreach (var entry in WidgetRegistry.WithSettingsTab)
        {
            var radioBtn = new System.Windows.Controls.RadioButton
            {
                Content = $"{entry.Icon} {entry.ResolvedSettingsTabLabel}",
                GroupName = "SettingsMenu",
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(12, 10, 12, 10),
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = entry.Id
            };
            radioBtn.SetResourceReference(System.Windows.Controls.RadioButton.ForegroundProperty, "TextPrimaryBrush");

            // Apply the same RadioButton template as the static nav items
            var template = new ControlTemplate(typeof(System.Windows.Controls.RadioButton));
            var borderFactory = new FrameworkElementFactory(typeof(Border), "Border");
            borderFactory.SetValue(Border.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            borderFactory.SetBinding(Border.PaddingProperty, new System.Windows.Data.Binding("Padding") { RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent) });
            var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            borderFactory.AppendChild(contentFactory);
            template.VisualTree = borderFactory;

            var checkedTrigger = new Trigger { Property = System.Windows.Controls.RadioButton.IsCheckedProperty, Value = true };
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, FindResource("AccentBrush"), "Border"));
            checkedTrigger.Setters.Add(new Setter(System.Windows.Documents.TextElement.ForegroundProperty, FindResource("TextOnAccentBrush"), "Border"));
            template.Triggers.Add(checkedTrigger);

            var hoverTrigger = new Trigger { Property = System.Windows.Controls.RadioButton.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, FindResource("HoverStrongBrush"), "Border"));
            template.Triggers.Add(hoverTrigger);

            radioBtn.Template = template;
            radioBtn.Checked += MenuButton_Checked;
            _widgetNavButtons[entry.Id] = radioBtn;
            WidgetNavMenuContainer.Children.Add(radioBtn);
        }
    }

    private void RegisterWidgetPanels()
    {
        // Map widget IDs to their existing panel borders (panels keep their custom XAML content)
        if (SmartProjectSearchPanel != null) _widgetPanels[WidgetIds.SmartProjectSearch] = SmartProjectSearchPanel;
        if (WidgetLauncherPanel != null) _widgetPanels[WidgetIds.WidgetLauncher] = WidgetLauncherPanel;
        if (QuickTasksPanel != null) _widgetPanels[WidgetIds.QuickTasks] = QuickTasksPanel;
        if (DocQuickOpenPanel != null) _widgetPanels[WidgetIds.DocQuickOpen] = DocQuickOpenPanel;
        if (FrequentProjectsPanel != null) _widgetPanels[WidgetIds.FrequentProjects] = FrequentProjectsPanel;
        if (QuickLaunchPanel != null) _widgetPanels[WidgetIds.QuickLaunch] = QuickLaunchPanel;
        if (MetricsViewerPanel != null) _widgetPanels[WidgetIds.MetricsViewer] = MetricsViewerPanel;
        if (CheatSheetPanel != null) _widgetPanels[WidgetIds.CheatSheet] = CheatSheetPanel;
    }

    // ===== Dynamic event handlers =====

    private void OnDynamicTransparencySliderChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isUpdatingSliders || _settings == null) return;
        if (sender is not System.Windows.Controls.Slider slider || slider.Tag is not string widgetId) return;

        // Save the value
        if (widgetId == "settings")
        {
            _settings.SetSettingsTransparency(e.NewValue);
            ApplySettingsWindowTransparency(e.NewValue);
        }
        else
        {
            _settings.SetWidgetTransparency(widgetId, e.NewValue);
        }

        // Update percentage label
        if (_percentLabels.TryGetValue(widgetId, out var pctLabel))
            pctLabel.Text = $"{(int)(e.NewValue * 100)}%";

        // Sync group members
        var group = _settings.GetWidgetTransparencyGroup(widgetId);
        if (!string.IsNullOrEmpty(group))
            SyncGroupSliders(group, e.NewValue, widgetId);

        _ = _settings.SaveAsync();
        _onTransparencyChanged?.Invoke();
    }

    private void OnGroupBadgeClick(string widgetId)
    {
        var currentGroup = _settings.GetWidgetTransparencyGroup(widgetId);
        // Cycle: "" → "A" → "B" → "C" → ""
        var nextGroup = currentGroup switch
        {
            "" => "A",
            "A" => "B",
            "B" => "C",
            "C" => "",
            _ => ""
        };

        _settings.SetWidgetTransparencyGroup(widgetId, nextGroup);
        _ = _settings.SaveAsync();

        UpdateGroupBadge(widgetId, nextGroup);

        // If joining a group, sync this widget's value to the group leader
        if (!string.IsNullOrEmpty(nextGroup))
        {
            var groupLeaderValue = GetGroupLeaderValue(nextGroup, widgetId);
            if (groupLeaderValue.HasValue && _transparencySliders.TryGetValue(widgetId, out var slider))
            {
                _isUpdatingSliders = true;
                slider.Value = groupLeaderValue.Value;
                if (widgetId == "settings")
                {
                    _settings.SetSettingsTransparency(groupLeaderValue.Value);
                    ApplySettingsWindowTransparency(groupLeaderValue.Value);
                }
                else
                    _settings.SetWidgetTransparency(widgetId, groupLeaderValue.Value);
                if (_percentLabels.TryGetValue(widgetId, out var pctLabel))
                    pctLabel.Text = $"{(int)(groupLeaderValue.Value * 100)}%";
                _isUpdatingSliders = false;
                _ = _settings.SaveAsync();
                _onTransparencyChanged?.Invoke();
            }
        }

        var name = widgetId == "settings" ? "Settings Window" : (WidgetRegistry.Get(widgetId)?.DisplayName ?? widgetId);
        StatusText.Text = string.IsNullOrEmpty(nextGroup)
            ? $"{name} ungrouped"
            : $"{name} added to group {nextGroup}";
    }

    private void OnDynamicWidgetToggleChanged(string id, bool enabled)
    {
        if (_isUpdatingSliders) return;

        if (id == "search_button")
        {
            _settings.SetSearchWidgetEnabled(enabled);
            _ = _settings.SaveAsync();
            _onSearchWidgetEnabledChanged?.Invoke();
            StatusText.Text = enabled ? "Search widget enabled" : "Search widget disabled";
            return;
        }

        _settings.SetWidgetEnabled(id, enabled);
        _ = _settings.SaveAsync();

        if (_widgetEnabledCallbacks.TryGetValue(id, out var callback))
            callback?.Invoke();

        var name = WidgetRegistry.Get(id)?.DisplayName ?? id;
        StatusText.Text = enabled ? $"{name} enabled" : $"{name} disabled";
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            // Ensure settings are loaded
            await _settings.LoadAsync();
            
            var (closeModifiers, closeKey) = _settings.GetCloseShortcut();
            CloseShortcutText.Text = FormatHotkey(closeModifiers, closeKey);

            AutoStartToggle.IsChecked = _settings.GetAutoStart();
            LivingWidgetsModeToggle.IsChecked = _settings.GetLivingWidgetsMode();
            PathSearchEnabledToggle.IsChecked = _settings.GetPathSearchEnabled();
            PathSearchSubDirsToggle.IsChecked = _settings.GetPathSearchShowSubDirs();
            PathSearchSubFilesToggle.IsChecked = _settings.GetPathSearchShowSubFiles();
            PathSearchShowHiddenToggle.IsChecked = _settings.GetPathSearchShowHidden();
            
            // Load drive settings
            QDriveEnabledToggle.IsChecked = _settings.GetQDriveEnabled();
            var qDrivePath = _settings.GetQDrivePath();
            QDrivePathBox.Text = string.IsNullOrEmpty(qDrivePath) ? "Q:\\" : qDrivePath;
            QDriveLabelText.Text = $"Q: Drive ({_settings.GetDriveLabel("Q")})";

            PDriveEnabledToggle.IsChecked = _settings.GetPDriveEnabled();
            var pDrivePath = _settings.GetPDrivePath();
            PDrivePathBox.Text = string.IsNullOrEmpty(pDrivePath) ? "P:\\" : pDrivePath;
            PDriveLabelText.Text = $"P: Drive ({_settings.GetDriveLabel("P")})";

            LDriveEnabledToggle.IsChecked = _settings.GetLDriveEnabled();
            var lDrivePath = _settings.GetLDrivePath();
            LDrivePathBox.Text = string.IsNullOrEmpty(lDrivePath) ? "L:\\" : lDrivePath;
            LDriveLabelText.Text = $"L: Drive ({_settings.GetDriveLabel("L")})";

            ArchiveDriveEnabledToggle.IsChecked = _settings.GetArchiveDriveEnabled();
            var archiveDrivePath = _settings.GetArchiveDrivePath();
            ArchiveDrivePathBox.Text = string.IsNullOrEmpty(archiveDrivePath) ? "" : archiveDrivePath;
            ArchiveDriveLabelText.Text = $"Archive Drive ({_settings.GetDriveLabel("Archive")})";

            
            // Load transparency settings (all sliders are now dynamic)
            _isUpdatingSliders = true;
            foreach (var kvp in _transparencySliders)
            {
                if (kvp.Key == "settings")
                    kvp.Value.Value = _settings.GetSettingsTransparency();
                else
                    kvp.Value.Value = _settings.GetWidgetTransparency(kvp.Key);

                if (_percentLabels.TryGetValue(kvp.Key, out var pctLabel))
                    pctLabel.Text = $"{(int)(kvp.Value.Value * 100)}%";
            }
            WidgetSnapGapSlider.Value = _settings.GetWidgetSnapGap();
            UpdateWidgetSnapGapValueText(_settings.GetWidgetSnapGap());
            WidgetOverlapPreventionToggle.IsChecked = _settings.GetWidgetOverlapPrevention();
            _isUpdatingSliders = false;

            ApplySettingsWindowTransparency(_settings.GetSettingsTransparency());
            UpdateAllGroupBadges();
            
            // Load widget enabled toggles from registry
            if (_widgetToggles.TryGetValue("search_button", out var searchToggle))
                searchToggle.IsChecked = _settings.GetSearchWidgetEnabled();
            foreach (var entry in WidgetRegistry.WithLauncherToggle)
            {
                if (_widgetToggles.TryGetValue(entry.Id, out var toggle))
                    toggle.IsChecked = _settings.GetWidgetEnabled(entry.Id);
            }
            
            // Load update settings
            AutoUpdateCheckToggle.IsChecked = _settings.GetAutoUpdateCheckEnabled();
            AutoUpdateInstallToggle.IsChecked = _settings.GetAutoUpdateInstallEnabled();
            LoadUpdateFrequencyCombo();

            // Load telemetry consent state for the Privacy toggle on the Updates tab.
            TelemetryConsentToggle.IsChecked = _settings.GetTelemetryConsentGiven();
            
            // Load notification duration setting
            LoadNotificationDurationSetting();
            
            // Load Quick Tasks config
            LoadQuickTasksSettings();
            
            var maxVisibleWidgets = _settings.GetWidgetLauncherMaxVisibleWidgets();
            WidgetLauncherMaxVisibleWidgetsSlider.Value = maxVisibleWidgets;
            UpdateWidgetLauncherMaxVisibleWidgetsText(maxVisibleWidgets);

            _isLoadingSPSettings = true;
            var attachModeEnabled = _settings.GetSmartProjectSearchAttachToSearchOverlayMode();
            SmartProjectSearchAttachModeToggle.IsChecked = attachModeEnabled;
            // Smart Project Search enabled toggle is now dynamic â€” sync attach mode disable state
            if (_widgetToggles.TryGetValue(WidgetIds.SmartProjectSearch, out var spToggle))
            {
                spToggle.IsChecked = _settings.GetSmartProjectSearchWidgetEnabled();
                spToggle.IsEnabled = !attachModeEnabled;
            }
            SmartSearchFileTypesInput.Text = string.Join(", ", _settings.GetSmartProjectSearchFileTypes());
            var latestMode = _settings.GetSmartProjectSearchLatestMode();
            SmartSearchLatestListRadio.IsChecked = !string.Equals(latestMode, "single", StringComparison.OrdinalIgnoreCase);
            SmartSearchLatestSingleRadio.IsChecked = string.Equals(latestMode, "single", StringComparison.OrdinalIgnoreCase);
            _isLoadingSPSettings = false;
            
            // Load Frequent Projects sliders
            _isLoadingFPSettings = true;
            FP_MaxShownSlider.Value = _settings.GetMaxFrequentProjectsShown();
            FP_MaxSavedSlider.Value = _settings.GetMaxFrequentProjectsSaved();
            _isLoadingFPSettings = false;
            
            // Initialize Quick Launch section state
            _isLoadingQLSettings = true;
            _isLoadingQLSettings = false;
            
            LoadHotkeyGroupsUI();
            LoadMetricsSettings();
            LoadCheatSheetSnapGridSetting();
            LoadThemeButtonState();

            UpdateAllGroupBadges();
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SettingsWindow: LoadSettingsAsync error: {ex.Message}");
            if (StatusText != null)
            {
                StatusText.Text = $"Failed to load settings: {ex.Message}";
            }
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_activeGroupIndex >= 0)
            {
                // Cancel group key recording
                if (_activeGroupKeyText != null)
                    _activeGroupKeyText.Visibility = Visibility.Visible;
                if (_activeGroupRecordingText != null)
                    _activeGroupRecordingText.Visibility = Visibility.Collapsed;
                if (_activeGroupKeyBox != null)
                    _activeGroupKeyBox.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
                _activeGroupKeyBox = null;
                _activeGroupKeyText = null;
                _activeGroupRecordingText = null;
                _activeGroupIndex = -1;
                e.Handled = true;
                return;
            }
            if (_isRecordingCloseShortcut)
            {
                StopRecordingCloseShortcut();
                e.Handled = true;
                return;
            }
            this.Close();
            e.Handled = true;
            return;
        }

        if (_activeGroupIndex >= 0)
        {
            // Recording key for a hotkey group
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            int mods = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0) mods |= (int)GlobalHotkey.MOD_CONTROL;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0) mods |= (int)GlobalHotkey.MOD_ALT;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) mods |= (int)GlobalHotkey.MOD_SHIFT;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0) mods |= (int)GlobalHotkey.MOD_WIN;
            int vk = KeyInterop.VirtualKeyFromKey(e.Key);

            var groups = _settings.GetHotkeyGroups();

            // 1) Reject if another group in this app already uses this combo.
            for (int i = 0; i < groups.Count; i++)
            {
                if (i == _activeGroupIndex) continue;
                if (groups[i].Key != 0 && groups[i].Modifiers == mods && groups[i].Key == vk)
                {
                    CancelHotkeyRecordingUI();
                    HotkeyConflictDialog.ShowForInternalConflict(FormatHotkey(mods, vk), i + 1, this);
                    e.Handled = true;
                    return;
                }
            }

            // 2) Probe against Windows. Release our own hotkeys first so we don't false-positive
            //    against our current registration for this or any other group.
            _onHotkeyReleaseForProbe?.Invoke();

            bool probeOk = GlobalHotkey.TryProbeRegister((uint)mods, (uint)vk, out _);

            if (!probeOk)
            {
                // Restore original hotkey registrations (settings weren't changed yet).
                _onHotkeyChanged?.Invoke();
                CancelHotkeyRecordingUI();
                HotkeyConflictDialog.ShowForSettingsConflict(FormatHotkey(mods, vk), mods, vk, this);
                e.Handled = true;
                return;
            }

            // 3) Commit the change.
            if (_activeGroupIndex < groups.Count)
            {
                groups[_activeGroupIndex].Modifiers = mods;
                groups[_activeGroupIndex].Key = vk;
                _settings.SetHotkeyGroups(groups);
                _ = _settings.SaveAsync();
                _onHotkeyChanged?.Invoke();
            }

            _activeGroupKeyBox = null;
            _activeGroupKeyText = null;
            _activeGroupRecordingText = null;
            _activeGroupIndex = -1;
            StatusText.Text = "Hotkey group updated!";
            // Rebuild panel so widget pills appear for newly-keyed groups
            RebuildHotkeyGroupsPanel(groups);
            e.Handled = true;
            return;
        }

        if (_isRecordingCloseShortcut)
        {
            // Ignore modifier-only keys
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin)
            {
                e.Handled = true;
                return;
            }

            // Record the close shortcut
            _recordedCloseShortcutModifiers = 0;
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                _recordedCloseShortcutModifiers |= (int)GlobalHotkey.MOD_CONTROL;
            if ((Keyboard.Modifiers & ModifierKeys.Alt) != 0)
                _recordedCloseShortcutModifiers |= (int)GlobalHotkey.MOD_ALT;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0)
                _recordedCloseShortcutModifiers |= (int)GlobalHotkey.MOD_SHIFT;
            if ((Keyboard.Modifiers & ModifierKeys.Windows) != 0)
                _recordedCloseShortcutModifiers |= (int)GlobalHotkey.MOD_WIN;

            _recordedCloseShortcutKey = KeyInterop.VirtualKeyFromKey(e.Key);

            // Update display
            var closeShortcutLabel = FormatHotkey(_recordedCloseShortcutModifiers, _recordedCloseShortcutKey);
            CloseShortcutText.Text = closeShortcutLabel;

            // Save the new close shortcut
            _settings.SetCloseShortcut(_recordedCloseShortcutModifiers, _recordedCloseShortcutKey);
            _ = _settings.SaveAsync();

            StopRecordingCloseShortcut();
            StatusText.Text = "Close shortcut updated!";

            // Notify parent to update close shortcut
            _onCloseShortcutChanged?.Invoke();

            e.Handled = true;
        }
    }

    // ===== Theme Selection =====

    private bool _isLoadingTheme;

    private void LoadThemeButtonState()
    {
        var app = (App)System.Windows.Application.Current;
        var themeSetting = app.Theme?.ThemeSetting ?? "Dark";
        SelectThemeComboItem(themeSetting);
    }

    private void SelectThemeComboItem(string themeSetting)
    {
        _isLoadingTheme = true;
        for (int i = 0; i < ThemeCombo.Items.Count; i++)
        {
            if (ThemeCombo.Items[i] is System.Windows.Controls.ComboBoxItem item &&
                string.Equals(item.Tag?.ToString(), themeSetting, StringComparison.OrdinalIgnoreCase))
            {
                ThemeCombo.SelectedIndex = i;
                break;
            }
        }
        _isLoadingTheme = false;
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingTheme) return;
        if (ThemeCombo.SelectedItem is System.Windows.Controls.ComboBoxItem selected &&
            selected.Tag is string theme)
        {
            ApplyThemeSetting(theme);
        }
    }

    private void ApplyThemeSetting(string theme)
    {
        var app = (App)System.Windows.Application.Current;
        app.Theme?.SetTheme(theme);
        // Keep the main settings instance in sync so the theme isn't overwritten
        // when SearchOverlay saves other settings (ThemeService uses a separate instance)
        _settings.SetTheme(theme);
        SelectThemeComboItem(theme);
        StatusText.Text = $"Theme set to {theme}";
        TelemetryAccessor.TrackSettingChanged("theme", theme);
    }

    /// <summary>
    /// Called when ThemeService switches themes. Rebuilds dynamic UI that uses baked-in
    /// colors (transparency sliders, widget nav menu) and re-applies window transparency.
    /// </summary>
    private void OnThemeChanged(string resolvedTheme)
    {
        Dispatcher.BeginInvoke(() =>
        {
            DebugLogger.Log($"SettingsWindow.OnThemeChanged: Theme switched to {resolvedTheme}, rebuilding dynamic UI");

            // Rebuild dynamic UI elements that baked in theme colors at construction time
            _isUpdatingSliders = true;
            BuildTransparencySliders();
            BuildWidgetToggles();
            BuildWidgetNavMenu();
            _isUpdatingSliders = false;

            // Re-load slider values from settings
            foreach (var kvp in _transparencySliders)
            {
                if (kvp.Key == "settings")
                    kvp.Value.Value = _settings.GetSettingsTransparency();
                else
                    kvp.Value.Value = _settings.GetWidgetTransparency(kvp.Key);

                if (_percentLabels.TryGetValue(kvp.Key, out var pctLabel))
                    pctLabel.Text = $"{(int)(kvp.Value.Value * 100)}%";
            }
            UpdateAllGroupBadges();

            // Rebuild hotkey groups UI
            LoadHotkeyGroupsUI();

            // Re-load widget toggle states
            foreach (var kvp in _widgetToggles)
            {
                if (kvp.Key == "search_button")
                    kvp.Value.IsChecked = _settings.GetSearchWidgetEnabled();
                else
                    kvp.Value.IsChecked = _settings.GetWidgetEnabled(kvp.Key);
            }

            // Re-apply settings window transparency with the new theme color
            ApplySettingsWindowTransparency(_settings.GetSettingsTransparency());

            // Update theme button visuals
            LoadThemeButtonState();
        });
    }

    private void CloseShortcutBox_MouseDown(object sender, MouseButtonEventArgs e)
    {
        StartRecordingCloseShortcut();
    }

    private void StartRecordingCloseShortcut()
    {
        _isRecordingCloseShortcut = true;
        CloseShortcutText.Visibility = Visibility.Collapsed;
        CloseShortcutRecordingText.Visibility = Visibility.Visible;
        CloseShortcutBox.BorderBrush = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
        this.Focus();
    }

    private void StopRecordingCloseShortcut()
    {
        _isRecordingCloseShortcut = false;
        CloseShortcutRecordingText.Visibility = Visibility.Collapsed;
        CloseShortcutText.Visibility = Visibility.Visible;
        CloseShortcutBox.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
    }

    private void ResetCloseShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        const int defaultModifiers = 0x0000; // No modifiers
        const int defaultKey = 0x1B; // ESC

        _settings.SetCloseShortcut(defaultModifiers, defaultKey);
        _ = _settings.SaveAsync();

        CloseShortcutText.Text = FormatHotkey(defaultModifiers, defaultKey);
        StatusText.Text = "Close shortcut reset to default.";

        _onCloseShortcutChanged?.Invoke();
    }

    private async void AutoStartToggle_Checked(object sender, RoutedEventArgs e)
    {
        _settings.SetAutoStart(true);
        await _settings.SaveAsync();
        SetAutoStart(true);
        StatusText.Text = "Auto-start enabled";
    }

    private async void AutoStartToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _settings.SetAutoStart(false);
        await _settings.SaveAsync();
        SetAutoStart(false);
        StatusText.Text = "Auto-start disabled";
    }

    private void SetAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null)
                return;

            const string appName = "DesktopHub";

            if (enable)
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    key.SetValue(appName, $"\"{exePath}\"");
                }
            }
            else
            {
                key.DeleteValue(appName, false);
            }
            TelemetryAccessor.TrackSettingChanged("auto_start", enable.ToString());
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Failed to update auto-start: {ex.Message}";
        }
    }

    private async void QDrivePathBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        try
        {
            if (_settings == null)
            {
                DebugLogger.Log("SettingsWindow: _settings is null in QDrivePathBox_TextChanged");
                return;
            }
            
            var path = QDrivePathBox.Text;
            if (!string.IsNullOrWhiteSpace(path))
            {
                _settings.SetQDrivePath(path);
                await _settings.SaveAsync();
                if (StatusText != null)
                {
                    StatusText.Text = "Q: drive path updated";
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SettingsWindow: QDrivePathBox_TextChanged error: {ex.Message}");
            if (StatusText != null)
            {
                StatusText.Text = $"Error updating path: {ex.Message}";
            }
        }
    }

    private async void PDrivePathBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        try
        {
            if (_settings == null)
            {
                DebugLogger.Log("SettingsWindow: _settings is null in PDrivePathBox_TextChanged");
                return;
            }
            
            var path = PDrivePathBox.Text;
            if (!string.IsNullOrWhiteSpace(path))
            {
                _settings.SetPDrivePath(path);
                await _settings.SaveAsync();
                if (StatusText != null)
                {
                    StatusText.Text = "P: drive path updated";
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SettingsWindow: PDrivePathBox_TextChanged error: {ex.Message}");
            if (StatusText != null)
            {
                StatusText.Text = $"Error updating path: {ex.Message}";
            }
        }
    }

    private async void QDriveEnabledToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SetQDriveEnabled(true);
        await _settings.SaveAsync();
        TelemetryAccessor.TrackSettingChanged("q_drive_enabled", "true");
        if (StatusText != null)
        {
            StatusText.Text = "Q: drive enabled - updating projects...";
        }
        _onDriveSettingsChanged?.Invoke();
        await Task.Delay(2000);
        if (StatusText != null && StatusText.Text.Contains("updating"))
            StatusText.Text = "Q: drive enabled";
    }

    private async void QDriveEnabledToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SetQDriveEnabled(false);
        await _settings.SaveAsync();
        TelemetryAccessor.TrackSettingChanged("q_drive_enabled", "false");
        if (StatusText != null)
        {
            StatusText.Text = "Q: drive disabled - updating projects...";
        }
        _onDriveSettingsChanged?.Invoke();
        await Task.Delay(2000);
        if (StatusText != null && StatusText.Text.Contains("updating"))
            StatusText.Text = "Q: drive disabled";
    }

    private async void PDriveEnabledToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SetPDriveEnabled(true);
        await _settings.SaveAsync();
        TelemetryAccessor.TrackSettingChanged("p_drive_enabled", "true");
        if (StatusText != null)
        {
            StatusText.Text = "P: drive enabled - scanning...";
        }
        _onDriveSettingsChanged?.Invoke();
        
        // Wait for scan to complete and update status
        await Task.Delay(3000);
        if (StatusText != null && StatusText.Text.Contains("scanning"))
        {
            StatusText.Text = "P: drive enabled - scan complete";
        }
    }

    private async void PDriveEnabledToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SetPDriveEnabled(false);
        await _settings.SaveAsync();
        TelemetryAccessor.TrackSettingChanged("p_drive_enabled", "false");
        if (StatusText != null)
        {
            StatusText.Text = "P: drive disabled - updating projects...";
        }
        _onDriveSettingsChanged?.Invoke();
        await Task.Delay(2000);
        if (StatusText != null && StatusText.Text.Contains("updating"))
            StatusText.Text = "P: drive disabled";
    }

    private async void LDrivePathBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        try
        {
            if (_settings == null) return;
            var path = LDrivePathBox.Text;
            if (!string.IsNullOrWhiteSpace(path))
            {
                _settings.SetLDrivePath(path);
                await _settings.SaveAsync();
                if (StatusText != null)
                    StatusText.Text = "L: drive path updated";
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SettingsWindow: LDrivePathBox_TextChanged error: {ex.Message}");
            if (StatusText != null)
                StatusText.Text = $"Error updating path: {ex.Message}";
        }
    }

    private async void LDriveEnabledToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SetLDriveEnabled(true);
        await _settings.SaveAsync();
        TelemetryAccessor.TrackSettingChanged("l_drive_enabled", "true");
        if (StatusText != null)
            StatusText.Text = "L: drive enabled - scanning...";
        _onDriveSettingsChanged?.Invoke();
        await Task.Delay(3000);
        if (StatusText != null && StatusText.Text.Contains("scanning"))
            StatusText.Text = "L: drive enabled - scan complete";
    }

    private async void LDriveEnabledToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SetLDriveEnabled(false);
        await _settings.SaveAsync();
        TelemetryAccessor.TrackSettingChanged("l_drive_enabled", "false");
        if (StatusText != null)
            StatusText.Text = "L: drive disabled - updating projects...";
        _onDriveSettingsChanged?.Invoke();
        await Task.Delay(2000);
        if (StatusText != null && StatusText.Text.Contains("updating"))
            StatusText.Text = "L: drive disabled";
    }

    private async void ArchiveDrivePathBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        try
        {
            if (_settings == null) return;
            var path = ArchiveDrivePathBox.Text;
            _settings.SetArchiveDrivePath(path ?? "");
            await _settings.SaveAsync();
            if (StatusText != null)
                StatusText.Text = "Archive drive path updated";
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SettingsWindow: ArchiveDrivePathBox_TextChanged error: {ex.Message}");
            if (StatusText != null)
                StatusText.Text = $"Error updating path: {ex.Message}";
        }
    }

    private async void ArchiveDriveEnabledToggle_Checked(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SetArchiveDriveEnabled(true);
        await _settings.SaveAsync();
        TelemetryAccessor.TrackSettingChanged("archive_drive_enabled", "true");
        if (StatusText != null)
            StatusText.Text = "Archive drive enabled - scanning...";
        _onDriveSettingsChanged?.Invoke();
        await Task.Delay(3000);
        if (StatusText != null && StatusText.Text.Contains("scanning"))
            StatusText.Text = "Archive drive enabled - scan complete";
    }

    private async void ArchiveDriveEnabledToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_settings == null) return;
        _settings.SetArchiveDriveEnabled(false);
        await _settings.SaveAsync();
        TelemetryAccessor.TrackSettingChanged("archive_drive_enabled", "false");
        if (StatusText != null)
            StatusText.Text = "Archive drive disabled - updating projects...";
        _onDriveSettingsChanged?.Invoke();
        await Task.Delay(2000);
        if (StatusText != null && StatusText.Text.Contains("updating"))
            StatusText.Text = "Archive drive disabled";
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            // Ensure window is active before dragging to avoid "stuck" behavior
            this.Activate();
            this.DragMove();
        }
    }

    /// <summary>
    /// Programmatically selects the Shortcuts tab. Safe to call before or after the window is shown.
    /// </summary>
    public void NavigateToShortcuts()
    {
        if (ShortcutsMenuButton != null)
        {
            ShortcutsMenuButton.IsChecked = true;
        }
    }

    private void MenuButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton radioButton)
        {
            // Hide all static panels
            if (ShortcutsPanel != null) ShortcutsPanel.Visibility = Visibility.Collapsed;
            if (AppearancePanel != null) AppearancePanel.Visibility = Visibility.Collapsed;
            if (GeneralPanel != null) GeneralPanel.Visibility = Visibility.Collapsed;
            if (UpdatesPanel != null) UpdatesPanel.Visibility = Visibility.Collapsed;
            if (PrivacyPanel != null) PrivacyPanel.Visibility = Visibility.Collapsed;
            if (TagsPanel != null) TagsPanel.Visibility = Visibility.Collapsed;

            // Hide all dynamic widget panels
            foreach (var panel in _widgetPanels.Values)
                panel.Visibility = Visibility.Collapsed;

            // Show selected static panel
            if (radioButton.Name == "ShortcutsMenuButton" && ShortcutsPanel != null)
            {
                ShortcutsPanel.Visibility = Visibility.Visible;
            }
            else if (radioButton.Name == "AppearanceMenuButton" && AppearancePanel != null)
            {
                AppearancePanel.Visibility = Visibility.Visible;
            }
            else if (radioButton.Name == "GeneralMenuButton" && GeneralPanel != null)
            {
                GeneralPanel.Visibility = Visibility.Visible;
            }
            else if (radioButton.Name == "UpdatesMenuButton" && UpdatesPanel != null)
            {
                UpdatesPanel.Visibility = Visibility.Visible;
            }
            else if (radioButton.Name == "PrivacyMenuButton" && PrivacyPanel != null)
            {
                PrivacyPanel.Visibility = Visibility.Visible;
                // Refresh toggle state whenever the user opens the tab so it reflects
                // any external changes (e.g. if the first-run dialog has since fired).
                if (_settings != null)
                    TelemetryConsentToggle.IsChecked = _settings.GetTelemetryConsentGiven();
            }
            else if (radioButton.Name == "TagsMenuButton" && TagsPanel != null)
            {
                TagsPanel.Visibility = Visibility.Visible;
                LoadTagSettings();
                LoadSearchHistorySettings();
            }
            else if (radioButton.Tag is string widgetId && _widgetPanels.TryGetValue(widgetId, out var panel))
            {
                // Dynamic widget panel from registry
                panel.Visibility = Visibility.Visible;

                // Run any per-panel load logic
                if (widgetId == WidgetIds.DocQuickOpen) LoadDocQuickOpenSettings();
                else if (widgetId == WidgetIds.FrequentProjects) LoadFrequentProjectsSettings();
                else if (widgetId == WidgetIds.QuickLaunch) LoadQuickLaunchSettings();
            }
        }
    }

    private static string FormatHotkey(int modifiers, int key) =>
        HotkeyFormatter.FormatHotkey(modifiers, key);

    private void CancelHotkeyRecordingUI()
    {
        if (_activeGroupKeyText != null)
            _activeGroupKeyText.Visibility = Visibility.Visible;
        if (_activeGroupRecordingText != null)
            _activeGroupRecordingText.Visibility = Visibility.Collapsed;
        if (_activeGroupKeyBox != null)
            _activeGroupKeyBox.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
        _activeGroupKeyBox = null;
        _activeGroupKeyText = null;
        _activeGroupRecordingText = null;
        _activeGroupIndex = -1;
    }


    private void SettingsScroll_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
    }

    private void ApplySettingsWindowTransparency(double transparency)
    {
        if (RootBorder == null)
            return;

        var clamped = Math.Clamp(transparency, 0.0, 1.0);
        var alpha = (byte)(clamped * 255);
        var baseColor = Helpers.ThemeHelper.GetColor("WindowBackgroundDeepColor");
        var color = System.Windows.Media.Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B);
        RootBorder.Background = new System.Windows.Media.SolidColorBrush(color);
    }

    /// <summary>
    /// Sync all sliders in the same transparency group to a new value (except the source).
    /// </summary>
    private void SyncGroupSliders(string group, double value, string sourceWidgetId)
    {
        if (string.IsNullOrEmpty(group)) return;

        _isUpdatingSliders = true;
        foreach (var kvp in _transparencySliders)
        {
            if (kvp.Key == sourceWidgetId) continue;
            if (_settings.GetWidgetTransparencyGroup(kvp.Key) != group) continue;

            kvp.Value.Value = value;
            if (kvp.Key == "settings")
            {
                _settings.SetSettingsTransparency(value);
                ApplySettingsWindowTransparency(value);
            }
            else
            {
                _settings.SetWidgetTransparency(kvp.Key, value);
            }

            if (_percentLabels.TryGetValue(kvp.Key, out var pctLabel))
                pctLabel.Text = $"{(int)(value * 100)}%";
        }
        _isUpdatingSliders = false;
    }

    /// <summary>
    /// Get the transparency value of the first other widget already in the given group (the "leader").
    /// </summary>
    private double? GetGroupLeaderValue(string group, string excludeWidgetId)
    {
        foreach (var kvp in _transparencySliders)
        {
            if (kvp.Key == excludeWidgetId) continue;
            if (_settings.GetWidgetTransparencyGroup(kvp.Key) == group)
                return kvp.Value.Value;
        }
        return null;
    }

    /// <summary>
    /// Update a single group badge's visual state (letter, color, card accent).
    /// </summary>
    private void UpdateGroupBadge(string widgetId, string group)
    {
        if (!_groupBadges.TryGetValue(widgetId, out var badge)) return;
        var badgeText = badge.Child as TextBlock;
        if (badgeText == null) return;

        if (string.IsNullOrEmpty(group))
        {
            badgeText.Text = "—";
            badgeText.SetResourceReference(TextBlock.ForegroundProperty, "TextTertiaryBrush");
            badge.SetResourceReference(Border.BackgroundProperty, "CardBrush");
            badge.SetResourceReference(Border.BorderBrushProperty, "CardBorderBrush");
        }
        else if (GroupColors.TryGetValue(group, out var color))
        {
            badgeText.Text = group;
            badgeText.Foreground = new System.Windows.Media.SolidColorBrush(color);
            badge.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x28, color.R, color.G, color.B));
            badge.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x50, color.R, color.G, color.B));
        }

        // Update the card's left border accent
        var card = badge.Parent is System.Windows.Controls.Grid g ? g.Parent as Border : null;
        if (card != null)
        {
            if (!string.IsNullOrEmpty(group) && GroupColors.TryGetValue(group, out var accentColor))
                card.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x80, accentColor.R, accentColor.G, accentColor.B));
            else
                card.BorderBrush = System.Windows.Media.Brushes.Transparent;
        }
    }

    /// <summary>
    /// Refresh all group badges from saved settings.
    /// </summary>
    private void UpdateAllGroupBadges()
    {
        foreach (var kvp in _groupBadges)
        {
            var group = _settings.GetWidgetTransparencyGroup(kvp.Key);
            UpdateGroupBadge(kvp.Key, group);
        }
    }

    private void LinkAllButton_Click(object sender, RoutedEventArgs e)
    {
        // Set all widgets to group A so they all sync together
        foreach (var kvp in _transparencySliders)
        {
            _settings.SetWidgetTransparencyGroup(kvp.Key, "A");
        }
        _ = _settings.SaveAsync();
        UpdateAllGroupBadges();

        // Sync all to the first slider's value
        var leaderValue = _transparencySliders.Values.FirstOrDefault()?.Value ?? 0.78;
        SyncGroupSliders("A", leaderValue, "");
        _ = _settings.SaveAsync();
        _onTransparencyChanged?.Invoke();

        StatusText.Text = "All widgets linked to Group A";
    }

    private void UnlinkAllButton_Click(object sender, RoutedEventArgs e)
    {
        // Remove all widgets from groups
        foreach (var kvp in _transparencySliders)
        {
            _settings.SetWidgetTransparencyGroup(kvp.Key, "");
        }
        _ = _settings.SaveAsync();
        UpdateAllGroupBadges();

        StatusText.Text = "All widgets ungrouped";
    }

    private void LoadNotificationDurationSetting()
    {
        var durationMs = _settings.GetNotificationDurationMs();
        
        // Select the appropriate radio button based on duration
        if (durationMs <= 0)
        {
            NotificationDisabledRadio.IsChecked = true;
        }
        else if (durationMs <= 2500)
        {
            NotificationShortRadio.IsChecked = true;
        }
        else if (durationMs <= 3500)
        {
            NotificationMediumRadio.IsChecked = true;
        }
        else
        {
            NotificationLongRadio.IsChecked = true;
        }
    }

    private void NotificationDurationRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton radioButton && radioButton.IsChecked == true)
        {
            int newDurationMs;
            
            if (radioButton == NotificationShortRadio)
            {
                newDurationMs = 2000; // 2 seconds
            }
            else if (radioButton == NotificationMediumRadio)
            {
                newDurationMs = 3000; // 3 seconds
            }
            else if (radioButton == NotificationLongRadio)
            {
                newDurationMs = 5000; // 5 seconds
            }
            else if (radioButton == NotificationDisabledRadio)
            {
                newDurationMs = 0; // Disabled
            }
            else
            {
                return; // Unknown radio button
            }

            _settings.SetNotificationDurationMs(newDurationMs);
            _ = _settings.SaveAsync();
            
            // Update status text
            var statusText = newDurationMs switch
            {
                0 => "Notifications disabled",
                2000 => "Notification duration: Short (2 seconds)",
                3000 => "Notification duration: Medium (3 seconds)",
                5000 => "Notification duration: Long (5 seconds)",
                _ => "Notification duration updated"
            };
            
            StatusText.Text = statusText;
        }
    }

    private void UpdateWidgetSnapGapValueText(int gapPixels)
    {
        if (WidgetSnapGapValueText != null)
        {
            WidgetSnapGapValueText.Text = $"{gapPixels} px";
        }
    }

    private void WidgetSnapGapSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var gap = (int)e.NewValue;
        UpdateWidgetSnapGapValueText(gap);

        if (_isUpdatingSliders || _settings == null || !IsLoaded)
            return;

        _settings.SetWidgetSnapGap(gap);
        _ = _settings.SaveAsync();

        var appliedGap = _settings.GetWidgetSnapGap();
        UpdateWidgetSnapGapValueText(appliedGap);
        StatusText.Text = $"Widget snap gap: {appliedGap} px";
        _onWidgetSnapGapChanged?.Invoke();
    }

    private void WidgetOverlapPreventionToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded || _isUpdatingSliders) return;

        var enabled = WidgetOverlapPreventionToggle.IsChecked == true;
        _settings.SetWidgetOverlapPrevention(enabled);
        _ = _settings.SaveAsync();
        StatusText.Text = enabled
            ? "Widget overlap prevention enabled â€” widgets cannot overlap"
            : "Widget overlap prevention disabled â€” free placement allowed";
    }

    private void UpdateWidgetLauncherMaxVisibleWidgetsText(int value)
    {
        if (WidgetLauncherMaxVisibleWidgetsValueText != null)
            WidgetLauncherMaxVisibleWidgetsValueText.Text = value.ToString();
    }

    private void WidgetLauncherMaxVisibleWidgetsSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var value = (int)Math.Round(e.NewValue);
        UpdateWidgetLauncherMaxVisibleWidgetsText(value);

        if (_isUpdatingSliders || _settings == null || !IsLoaded)
            return;

        _settings.SetWidgetLauncherMaxVisibleWidgets(value);
        _ = _settings.SaveAsync();
        StatusText.Text = $"Widget launcher max visible widgets: {_settings.GetWidgetLauncherMaxVisibleWidgets()}";
        _onWidgetLauncherLayoutChanged?.Invoke();
    }

    private async void LivingWidgetsModeToggle_Checked(object sender, RoutedEventArgs e)
    {
        _settings.SetLivingWidgetsMode(true);
        await _settings.SaveAsync();
        StatusText.Text = "Living Widgets Mode enabled - widgets are now draggable and pinnable";
        _onLivingWidgetsModeChanged?.Invoke();
    }

    private async void LivingWidgetsModeToggle_Unchecked(object sender, RoutedEventArgs e)
    {
        _settings.SetLivingWidgetsMode(false);
        await _settings.SaveAsync();
        StatusText.Text = "Living Widgets Mode disabled - widgets will auto-hide when clicking away";
        _onLivingWidgetsModeChanged?.Invoke();
    }

    private void PathSearchToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        _settings.SetPathSearchEnabled(PathSearchEnabledToggle.IsChecked == true);
        _settings.SetPathSearchShowSubDirs(PathSearchSubDirsToggle.IsChecked == true);
        _settings.SetPathSearchShowSubFiles(PathSearchSubFilesToggle.IsChecked == true);
        _settings.SetPathSearchShowHidden(PathSearchShowHiddenToggle.IsChecked == true);
        _ = _settings.SaveAsync();
        var enabled = PathSearchEnabledToggle.IsChecked == true;
        StatusText.Text = enabled ? "Path search enabled" : "Path search disabled";
    }

}
