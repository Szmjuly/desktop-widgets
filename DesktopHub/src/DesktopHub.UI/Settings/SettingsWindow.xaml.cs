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
    private readonly Dictionary<string, System.Windows.Controls.Primitives.ToggleButton> _widgetToggles = new();
    private readonly Dictionary<string, System.Windows.Controls.RadioButton> _widgetNavButtons = new();
    private readonly Dictionary<string, Border> _widgetPanels = new();
    private readonly Dictionary<string, Action?> _widgetEnabledCallbacks = new();
    // Group hotkey recording state
    private Border? _activeGroupKeyBox;
    private TextBlock? _activeGroupKeyText;
    private TextBlock? _activeGroupRecordingText;
    private int _activeGroupIndex = -1;

    public SettingsWindow(ISettingsService settings, Action? onHotkeyChanged = null, Action? onCloseShortcutChanged = null, Action? onLivingWidgetsModeChanged = null, Action? onDriveSettingsChanged = null, Action? onTransparencyChanged = null, TaskService? taskService = null, DocOpenService? docService = null, Action? onSearchWidgetEnabledChanged = null, Action? onTimerWidgetEnabledChanged = null, Action? onQuickTasksWidgetEnabledChanged = null, Action? onDocWidgetEnabledChanged = null, Action? onUpdateSettingsChanged = null, Action? onFrequentProjectsWidgetEnabledChanged = null, Action? onFrequentProjectsLayoutChanged = null, Action? onQuickLaunchWidgetEnabledChanged = null, IProjectLaunchDataStore? launchDataStore = null, Action? onQuickLaunchLayoutChanged = null, Action? onWidgetSnapGapChanged = null, Action? onSmartProjectSearchWidgetEnabledChanged = null, Action? onWidgetLauncherLayoutChanged = null, Action? onCheatSheetWidgetEnabledChanged = null, Action? onMetricsViewerWidgetEnabledChanged = null)
    {
        _settings = settings;
        _taskService = taskService;
        _docService = docService;
        _onHotkeyChanged = onHotkeyChanged;
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
    }

    // ===== Dynamic UI generation from WidgetRegistry =====

    private void BuildDynamicUI()
    {
        BuildTransparencySliders();
        BuildWidgetToggles();
        BuildWidgetNavMenu();
        RegisterWidgetPanels();
    }

    private void BuildTransparencySliders()
    {
        if (TransparencySlidersContainer == null) return;
        TransparencySlidersContainer.Children.Clear();

        foreach (var entry in WidgetRegistry.WithTransparencySlider)
        {
            // Label row with link button
            var labelGrid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 12, 0, 8) };
            var label = new TextBlock
            {
                Text = entry.DisplayName,
                FontSize = 13,
                FontWeight = FontWeights.Medium,
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            labelGrid.Children.Add(label);

            var linkBtn = new System.Windows.Controls.Button
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Width = 24, Height = 24,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Link to other sliders",
                Tag = entry.Id
            };
            var linkIcon = new TextBlock { Text = "\U0001F513", FontSize = 12, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var linkIconGrid = new System.Windows.Controls.Grid();
            linkIconGrid.Children.Add(linkIcon);
            linkBtn.Content = linkIconGrid;
            linkBtn.Resources.Add(typeof(Border), new Style(typeof(Border)) { Setters = { new Setter(Border.CornerRadiusProperty, new CornerRadius(4)) } });
            var capturedId = entry.Id;
            linkBtn.Click += (s, e) => OnDynamicLinkButtonClick(capturedId);
            labelGrid.Children.Add(linkBtn);
            _linkButtons[entry.Id] = linkBtn;
            TransparencySlidersContainer.Children.Add(labelGrid);

            // Slider row
            var sliderGrid = new System.Windows.Controls.Grid();
            sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            sliderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var leftLabel = new TextBlock { Text = "Transparent", FontSize = 10, Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            System.Windows.Controls.Grid.SetColumn(leftLabel, 0);
            sliderGrid.Children.Add(leftLabel);

            var slider = new System.Windows.Controls.Slider
            {
                Minimum = 0.4, Maximum = 0.95, Value = entry.DefaultTransparency,
                IsSnapToTickEnabled = false,
                SmallChange = 0.01, LargeChange = 0.05,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = entry.Id,
                Style = (Style)FindResource("StyledSlider")
            };
            slider.ValueChanged += OnDynamicTransparencySliderChanged;
            System.Windows.Controls.Grid.SetColumn(slider, 1);
            sliderGrid.Children.Add(slider);
            _transparencySliders[entry.Id] = slider;

            var rightLabel = new TextBlock { Text = "Opaque", FontSize = 10, Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
            System.Windows.Controls.Grid.SetColumn(rightLabel, 2);
            sliderGrid.Children.Add(rightLabel);

            TransparencySlidersContainer.Children.Add(sliderGrid);
        }
    }

    private void BuildWidgetToggles()
    {
        if (WidgetTogglesContainer == null) return;
        WidgetTogglesContainer.Children.Clear();

        // Special: Search Widget toggle (not in registry as a launcher toggle)
        AddWidgetToggleRow(WidgetTogglesContainer, "search_button", "Search Widget", "Search button in the Widget Launcher", true);

        foreach (var entry in WidgetRegistry.WithLauncherToggle)
        {
            AddWidgetToggleRow(WidgetTogglesContainer, entry.Id, entry.DisplayName, entry.Description, true);
        }
    }

    private void AddWidgetToggleRow(System.Windows.Controls.StackPanel container, string id, string name, string description, bool defaultChecked)
    {
        var grid = new System.Windows.Controls.Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var info = new System.Windows.Controls.StackPanel();
        info.Children.Add(new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeights.Medium, Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"), Margin = new Thickness(0, 0, 0, 2) });
        info.Children.Add(new TextBlock { Text = description, FontSize = 11, Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush") });
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

    private static ControlTemplate CreateToggleSwitchTemplate()
    {
        var template = new ControlTemplate(typeof(System.Windows.Controls.Primitives.ToggleButton));
        var borderFactory = new FrameworkElementFactory(typeof(Border), "Border");
        borderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x50, 0x50, 0x50)));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
        borderFactory.SetValue(Border.WidthProperty, 50.0);
        borderFactory.SetValue(Border.HeightProperty, 28.0);
        var thumbFactory = new FrameworkElementFactory(typeof(System.Windows.Shapes.Ellipse), "Thumb");
        thumbFactory.SetValue(System.Windows.Shapes.Ellipse.WidthProperty, 20.0);
        thumbFactory.SetValue(System.Windows.Shapes.Ellipse.HeightProperty, 20.0);
        thumbFactory.SetValue(System.Windows.Shapes.Ellipse.FillProperty, System.Windows.Media.Brushes.White);
        thumbFactory.SetValue(FrameworkElement.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Left);
        thumbFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 0, 0));
        borderFactory.AppendChild(thumbFactory);
        template.VisualTree = borderFactory;

        var checkedTrigger = new Trigger { Property = System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, Value = true };
        checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xCC)), "Border"));
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
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = entry.Id
            };

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
            checkedTrigger.Setters.Add(new Setter(Border.BackgroundProperty, FindResource("PrimaryBrush"), "Border"));
            template.Triggers.Add(checkedTrigger);

            var hoverTrigger = new Trigger { Property = System.Windows.Controls.RadioButton.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)), "Border"));
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

        _settings.SetWidgetTransparency(widgetId, e.NewValue);
        _ = _settings.SaveAsync();
        StatusText.Text = $"{WidgetRegistry.Get(widgetId)?.DisplayName ?? widgetId} transparency updated";

        if (_settings.GetWidgetTransparencyLinked(widgetId))
        {
            SyncLinkedSliders(e.NewValue);
        }

        _onTransparencyChanged?.Invoke();
    }

    private void OnDynamicLinkButtonClick(string widgetId)
    {
        var isLinked = _settings.GetWidgetTransparencyLinked(widgetId);
        _settings.SetWidgetTransparencyLinked(widgetId, !isLinked);
        _ = _settings.SaveAsync();

        if (_linkButtons.TryGetValue(widgetId, out var btn))
            UpdateLinkButton(btn, !isLinked);

        if (!isLinked && _transparencySliders.TryGetValue(widgetId, out var slider))
        {
            SyncLinkedSliders(slider.Value);
        }

        var name = WidgetRegistry.Get(widgetId)?.DisplayName ?? widgetId;
        StatusText.Text = !isLinked ? $"{name} transparency linked" : $"{name} transparency unlinked";
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
            
            PDriveEnabledToggle.IsChecked = _settings.GetPDriveEnabled();
            var pDrivePath = _settings.GetPDrivePath();
            PDrivePathBox.Text = string.IsNullOrEmpty(pDrivePath) ? "P:\\" : pDrivePath;
            
            // Load transparency settings (static Settings Window slider + dynamic widget sliders)
            _isUpdatingSliders = true;
            SettingsTransparencySlider.Value = _settings.GetSettingsTransparency();
            foreach (var kvp in _transparencySliders)
            {
                kvp.Value.Value = _settings.GetWidgetTransparency(kvp.Key);
            }
            WidgetSnapGapSlider.Value = _settings.GetWidgetSnapGap();
            UpdateWidgetSnapGapValueText(_settings.GetWidgetSnapGap());
            WidgetOverlapPreventionToggle.IsChecked = _settings.GetWidgetOverlapPrevention();
            _isUpdatingSliders = false;

            ApplySettingsWindowTransparency(SettingsTransparencySlider.Value);
            
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

            UpdateAllLinkButtons();
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
                    _activeGroupKeyBox.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF));
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

    private void MenuButton_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.RadioButton radioButton)
        {
            // Hide all static panels
            if (ShortcutsPanel != null) ShortcutsPanel.Visibility = Visibility.Collapsed;
            if (AppearancePanel != null) AppearancePanel.Visibility = Visibility.Collapsed;
            if (GeneralPanel != null) GeneralPanel.Visibility = Visibility.Collapsed;
            if (UpdatesPanel != null) UpdatesPanel.Visibility = Visibility.Collapsed;

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


    private void SettingsScroll_ScrollChanged(object sender, System.Windows.Controls.ScrollChangedEventArgs e)
    {
    }

    private void SettingsTransparencySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RootBorder == null || _isUpdatingSliders || _settings == null) return;
        
        _settings.SetSettingsTransparency(e.NewValue);
        _ = _settings.SaveAsync();
        StatusText.Text = "Settings transparency updated";

        // Update this window's transparency in real-time
        ApplySettingsWindowTransparency(e.NewValue);
        
        // Sync linked sliders
        if (_settings.GetSettingsTransparencyLinked())
        {
            SyncLinkedSliders(e.NewValue);
        }
        
        // Notify windows to update their transparency
        _onTransparencyChanged?.Invoke();
    }

    private void ApplySettingsWindowTransparency(double transparency)
    {
        if (RootBorder == null)
            return;

        var clamped = Math.Clamp(transparency, 0.0, 1.0);
        var alpha = (byte)(clamped * 255);
        var color = System.Windows.Media.Color.FromArgb(alpha, 0x18, 0x18, 0x18);
        RootBorder.Background = new System.Windows.Media.SolidColorBrush(color);
    }

    private void LinkSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var isLinked = _settings.GetSettingsTransparencyLinked();
        _settings.SetSettingsTransparencyLinked(!isLinked);
        _ = _settings.SaveAsync();
        
        UpdateLinkButton(LinkSettingsButton, !isLinked);
        
        if (!isLinked)
        {
            SyncLinkedSliders(SettingsTransparencySlider.Value);
        }
        
        StatusText.Text = !isLinked ? "Settings transparency linked" : "Settings transparency unlinked";
    }

    private void SyncLinkedSliders(double value)
    {
        _isUpdatingSliders = true;
        
        // Sync static Settings Window slider
        if (_settings.GetSettingsTransparencyLinked())
        {
            SettingsTransparencySlider.Value = value;
            _settings.SetSettingsTransparency(value);
        }
        
        // Sync all dynamic widget sliders that are linked
        foreach (var kvp in _transparencySliders)
        {
            if (_settings.GetWidgetTransparencyLinked(kvp.Key))
            {
                kvp.Value.Value = value;
                _settings.SetWidgetTransparency(kvp.Key, value);
            }
        }
        
        _isUpdatingSliders = false;
    }

    private void UpdateLinkButton(System.Windows.Controls.Button button, bool isLinked)
    {
        // Update the button's content to show linked/unlinked state
        var contentGrid = button.Content as System.Windows.Controls.Grid;
        if (contentGrid == null)
        {
            // Create a grid with icon if it doesn't exist
            contentGrid = new System.Windows.Controls.Grid();
            var textBlock = new System.Windows.Controls.TextBlock
            {
                Text = isLinked ? "\U0001F517" : "\U0001F513",
                FontSize = 12,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            contentGrid.Children.Add(textBlock);
            button.Content = contentGrid;
        }
        else if (contentGrid.Children.Count > 0 && contentGrid.Children[0] is System.Windows.Controls.TextBlock tb)
        {
            tb.Text = isLinked ? "\U0001F517" : "\U0001F513";
        }
        
        // Update background color
        button.Background = isLinked 
            ? (System.Windows.Media.Brush)FindResource("PrimaryBrush") 
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF));
    }

    private void UpdateAllLinkButtons()
    {
        // Static Settings Window link button
        UpdateLinkButton(LinkSettingsButton, _settings.GetSettingsTransparencyLinked());
        
        // Dynamic widget link buttons
        foreach (var kvp in _linkButtons)
        {
            UpdateLinkButton(kvp.Value, _settings.GetWidgetTransparencyLinked(kvp.Key));
        }
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
