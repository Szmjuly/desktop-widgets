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

    public SettingsWindow(ISettingsService settings, Action? onHotkeyChanged = null, Action? onCloseShortcutChanged = null, Action? onLivingWidgetsModeChanged = null, Action? onDriveSettingsChanged = null, Action? onTransparencyChanged = null, TaskService? taskService = null, DocOpenService? docService = null, Action? onSearchWidgetEnabledChanged = null, Action? onTimerWidgetEnabledChanged = null, Action? onQuickTasksWidgetEnabledChanged = null, Action? onDocWidgetEnabledChanged = null, Action? onUpdateSettingsChanged = null, Action? onFrequentProjectsWidgetEnabledChanged = null, Action? onFrequentProjectsLayoutChanged = null, Action? onQuickLaunchWidgetEnabledChanged = null, IProjectLaunchDataStore? launchDataStore = null, Action? onQuickLaunchLayoutChanged = null, Action? onWidgetSnapGapChanged = null, Action? onSmartProjectSearchWidgetEnabledChanged = null, Action? onWidgetLauncherLayoutChanged = null, Action? onCheatSheetWidgetEnabledChanged = null)
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
            
            UpdateRootClip(12);
            
            // Ensure window background is completely transparent
            this.Background = null;
        };

        // Update clip on resize
        SizeChanged += (s, e) =>
        {
            // DO NOT apply window region rounding - causes clipping
            // WindowBlur.ApplyRoundedCorners(this, 12);
            UpdateRootClip(12);
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
            var linkIcon = new TextBlock { Text = "🔓", FontSize = 12, HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
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
                TickFrequency = 0.05, IsSnapToTickEnabled = true,
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
        borderFactory.SetValue(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33)));
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
            // Smart Project Search enabled toggle is now dynamic — sync attach mode disable state
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

    private static string FormatHotkey(int modifiers, int key)
    {
        var parts = new List<string>();

        if ((modifiers & (int)GlobalHotkey.MOD_CONTROL) != 0)
            parts.Add("Ctrl");

        if ((modifiers & (int)GlobalHotkey.MOD_ALT) != 0)
            parts.Add("Alt");

        if ((modifiers & (int)GlobalHotkey.MOD_SHIFT) != 0)
            parts.Add("Shift");

        if ((modifiers & (int)GlobalHotkey.MOD_WIN) != 0)
            parts.Add("Win");

        var keyLabel = KeyInterop.KeyFromVirtualKey(key);
        var keyText = keyLabel != Key.None ? keyLabel.ToString() : $"0x{key:X}";
        parts.Add(keyText);

        return string.Join("+", parts);
    }

    private void UpdateRootClip(double radiusDip)
    {
        try
        {
            if (RootBorder.ActualWidth <= 0 || RootBorder.ActualHeight <= 0)
            {
                return;
            }
            
            var rect = new System.Windows.Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight);
            RootBorder.Clip = new System.Windows.Media.RectangleGeometry(rect, radiusDip, radiusDip);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SettingsWindow: UpdateRootClip error: {ex.Message}");
        }
    }

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
                Text = isLinked ? "🔗" : "🔓",
                FontSize = 12,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };
            contentGrid.Children.Add(textBlock);
            button.Content = contentGrid;
        }
        else if (contentGrid.Children.Count > 0 && contentGrid.Children[0] is System.Windows.Controls.TextBlock tb)
        {
            tb.Text = isLinked ? "🔗" : "🔓";
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
            ? "Widget overlap prevention enabled — widgets cannot overlap"
            : "Widget overlap prevention disabled — free placement allowed";
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

    // ===== Quick Tasks Settings =====

    private void LoadQuickTasksSettings()
    {
        if (_taskService == null) return;

        _isLoadingQTSettings = true;
        try
        {
            var config = _taskService.Config;
            QT_ShowCompletedToggle.IsChecked = config.ShowCompletedTasks;
            QT_CompactModeToggle.IsChecked = config.CompactMode;
            QT_AutoCarryOverToggle.IsChecked = config.AutoCarryOver;
            QT_CompletedOpacitySlider.Value = config.CompletedOpacity;

            // Default priority
            QT_PriorityLow.IsChecked = config.DefaultPriority == "low";
            QT_PriorityNormal.IsChecked = config.DefaultPriority == "normal";
            QT_PriorityHigh.IsChecked = config.DefaultPriority == "high";

            // Sort mode
            QT_SortManual.IsChecked = config.SortBy == "manual";
            QT_SortPriority.IsChecked = config.SortBy == "priority";
            QT_SortCreated.IsChecked = config.SortBy == "created";

            // Categories
            RenderCategoriesList();
        }
        finally
        {
            _isLoadingQTSettings = false;
        }
    }

    private void RenderCategoriesList()
    {
        if (_taskService == null || QT_CategoriesList == null) return;

        QT_CategoriesList.Children.Clear();
        foreach (var category in _taskService.Config.Categories)
        {
            var cat = category;
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new System.Windows.Controls.TextBlock
            {
                Text = cat,
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);

            var removeBtn = new System.Windows.Controls.Button
            {
                Content = "✕",
                Width = 26,
                Height = 26,
                FontSize = 11,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)),
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x52, 0x52)),
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            // Apply rounded template
            var btnTemplate = new System.Windows.Controls.ControlTemplate(typeof(System.Windows.Controls.Button));
            var btnBorder = new FrameworkElementFactory(typeof(Border));
            btnBorder.SetValue(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)));
            btnBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            var btnContent = new FrameworkElementFactory(typeof(System.Windows.Controls.ContentPresenter));
            btnContent.SetValue(System.Windows.Controls.ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
            btnContent.SetValue(System.Windows.Controls.ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            btnBorder.AppendChild(btnContent);
            btnTemplate.VisualTree = btnBorder;
            removeBtn.Template = btnTemplate;
            removeBtn.Click += async (s, ev) =>
            {
                _taskService.Config.Categories.Remove(cat);
                await _taskService.ApplyConfigAsync();
                RenderCategoriesList();
                StatusText.Text = $"Category '{cat}' removed";
            };
            Grid.SetColumn(removeBtn, 1);
            row.Children.Add(removeBtn);

            QT_CategoriesList.Children.Add(row);
        }
    }

    private async void QT_ShowCompletedToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        _taskService.Config.ShowCompletedTasks = QT_ShowCompletedToggle.IsChecked == true;
        await _taskService.ApplyConfigAsync();
        StatusText.Text = _taskService.Config.ShowCompletedTasks ? "Completed tasks visible" : "Completed tasks hidden";
    }

    private async void QT_CompactModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        _taskService.Config.CompactMode = QT_CompactModeToggle.IsChecked == true;
        await _taskService.ApplyConfigAsync();
        StatusText.Text = _taskService.Config.CompactMode ? "Compact mode enabled" : "Compact mode disabled";
    }

    private async void QT_AutoCarryOverToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        var enabled = QT_AutoCarryOverToggle.IsChecked == true;
        await _taskService.SetAutoCarryOverAsync(enabled);
        StatusText.Text = enabled ? "Auto carry-over enabled" : "Auto carry-over disabled — incomplete carry-overs removed";
    }

    private async void QT_CompletedOpacitySlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        _taskService.Config.CompletedOpacity = QT_CompletedOpacitySlider.Value;
        await _taskService.ApplyConfigAsync();
    }

    private async void QT_DefaultPriority_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        if (QT_PriorityLow.IsChecked == true) _taskService.Config.DefaultPriority = "low";
        else if (QT_PriorityHigh.IsChecked == true) _taskService.Config.DefaultPriority = "high";
        else _taskService.Config.DefaultPriority = "normal";
        await _taskService.ApplyConfigAsync();
        StatusText.Text = $"Default priority: {_taskService.Config.DefaultPriority}";
    }

    private async void QT_SortMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQTSettings || _taskService == null) return;
        if (QT_SortPriority.IsChecked == true) _taskService.Config.SortBy = "priority";
        else if (QT_SortCreated.IsChecked == true) _taskService.Config.SortBy = "created";
        else _taskService.Config.SortBy = "manual";
        await _taskService.ApplyConfigAsync();
        StatusText.Text = $"Sort order: {_taskService.Config.SortBy}";
    }

    private async void QT_AddCategory_Click(object sender, RoutedEventArgs e)
    {
        await AddNewCategory();
    }

    private async void QT_NewCategoryInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await AddNewCategory();
            e.Handled = true;
        }
    }

    private async Task AddNewCategory()
    {
        if (_taskService == null) return;
        var name = QT_NewCategoryInput.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        if (_taskService.Config.Categories.Contains(name)) return;

        _taskService.Config.Categories.Add(name);
        await _taskService.ApplyConfigAsync();
        QT_NewCategoryInput.Text = "";
        RenderCategoriesList();
        StatusText.Text = $"Category '{name}' added";
    }

    // ========== Doc Quick Open Settings ==========

    private void LoadDocQuickOpenSettings()
    {
        if (_docService == null) return;
        _isLoadingDQSettings = true;
        try
        {
            var cfg = _docService.Config;
            DQ_ShowFileSizeToggle.IsChecked = cfg.ShowFileSize;
            DQ_ShowDateModifiedToggle.IsChecked = cfg.ShowDateModified;
            DQ_ShowFileExtToggle.IsChecked = cfg.ShowFileExtension;
            DQ_CompactModeToggle.IsChecked = cfg.CompactMode;
            DQ_MaxDepthSlider.Value = cfg.MaxDepth;
            DQ_MaxDepthValue.Text = cfg.MaxDepth.ToString();
            DQ_MaxFilesSlider.Value = cfg.MaxFiles;
            DQ_MaxFilesValue.Text = cfg.MaxFiles.ToString();
            DQ_ExtensionsInput.Text = string.Join(", ", cfg.Extensions);
            DQ_ExcludedFoldersInput.Text = string.Join(", ", cfg.ExcludedFolders);
            DQ_AutoOpenToggle.IsChecked = cfg.AutoOpenLastProject;
            DQ_RecentCountSlider.Value = cfg.RecentFilesCount;
            DQ_RecentCountValue.Text = cfg.RecentFilesCount.ToString();

            // Sort radio
            switch (cfg.SortBy)
            {
                case "date": DQ_SortDate.IsChecked = true; break;
                case "type": DQ_SortType.IsChecked = true; break;
                case "size": DQ_SortSize.IsChecked = true; break;
                default: DQ_SortName.IsChecked = true; break;
            }

            // Group radio
            switch (cfg.GroupBy)
            {
                case "category": DQ_GroupCategory.IsChecked = true; break;
                case "extension": DQ_GroupExt.IsChecked = true; break;
                case "subfolder": DQ_GroupSubfolder.IsChecked = true; break;
                default: DQ_GroupNone.IsChecked = true; break;
            }

            // Transparency is now loaded in the Appearance tab
        }
        finally
        {
            _isLoadingDQSettings = false;
        }
    }

    private async void DQ_ShowFileSizeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        _docService.Config.ShowFileSize = DQ_ShowFileSizeToggle.IsChecked == true;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Show file size: {_docService.Config.ShowFileSize}";
    }

    private async void DQ_ShowDateModifiedToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        _docService.Config.ShowDateModified = DQ_ShowDateModifiedToggle.IsChecked == true;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Show date modified: {_docService.Config.ShowDateModified}";
    }

    private async void DQ_ShowFileExtToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        _docService.Config.ShowFileExtension = DQ_ShowFileExtToggle.IsChecked == true;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Show file extension: {_docService.Config.ShowFileExtension}";
    }

    private async void DQ_CompactModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        _docService.Config.CompactMode = DQ_CompactModeToggle.IsChecked == true;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Compact mode: {_docService.Config.CompactMode}";
    }

    private async void DQ_MaxDepthSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        var val = (int)DQ_MaxDepthSlider.Value;
        DQ_MaxDepthValue.Text = val.ToString();
        _docService.Config.MaxDepth = val;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Max scan depth: {val}";
    }

    private async void DQ_MaxFilesSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        var val = (int)DQ_MaxFilesSlider.Value;
        DQ_MaxFilesValue.Text = val.ToString();
        _docService.Config.MaxFiles = val;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Max files: {val}";
    }

    private async void DQ_ExcludedFoldersInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        var text = DQ_ExcludedFoldersInput.Text;
        var folders = text.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                         .Select(x => x.Trim())
                         .Where(x => !string.IsNullOrEmpty(x))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .ToList();
        _docService.Config.ExcludedFolders = folders;
        DQ_ExcludedFoldersInput.Text = string.Join(", ", folders);
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Excluded folders updated ({folders.Count} folders)";
    }

    private async void DQ_ExtensionsInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        var text = DQ_ExtensionsInput.Text;
        var exts = text.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries)
                       .Select(x => x.Trim().TrimStart('.').ToLowerInvariant())
                       .Where(x => !string.IsNullOrEmpty(x))
                       .Distinct()
                       .ToList();
        _docService.Config.Extensions = exts;
        DQ_ExtensionsInput.Text = string.Join(", ", exts);
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"File extensions updated ({exts.Count} types)";
    }

    private async void DQ_SortChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        if (DQ_SortDate.IsChecked == true) _docService.Config.SortBy = "date";
        else if (DQ_SortType.IsChecked == true) _docService.Config.SortBy = "type";
        else if (DQ_SortSize.IsChecked == true) _docService.Config.SortBy = "size";
        else _docService.Config.SortBy = "name";
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Sort order: {_docService.Config.SortBy}";
    }

    private async void DQ_GroupChanged(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        if (DQ_GroupCategory.IsChecked == true) _docService.Config.GroupBy = "category";
        else if (DQ_GroupExt.IsChecked == true) _docService.Config.GroupBy = "extension";
        else if (DQ_GroupSubfolder.IsChecked == true) _docService.Config.GroupBy = "subfolder";
        else _docService.Config.GroupBy = "none";
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Group by: {_docService.Config.GroupBy}";
    }

    private async void DQ_AutoOpenToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        _docService.Config.AutoOpenLastProject = DQ_AutoOpenToggle.IsChecked == true;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Remember last project: {_docService.Config.AutoOpenLastProject}";
    }

    private async void DQ_RecentCountSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingDQSettings || _docService == null) return;
        var val = (int)DQ_RecentCountSlider.Value;
        DQ_RecentCountValue.Text = val.ToString();
        _docService.Config.RecentFilesCount = val;
        await _docService.ApplyConfigAsync();
        StatusText.Text = $"Recent files count: {val}";
    }


    // Old hardcoded transparency/link handlers removed — now handled by OnDynamicTransparencySliderChanged / OnDynamicLinkButtonClick

    // Old hardcoded widget toggle handlers removed — now handled by OnDynamicWidgetToggleChanged

    private void SmartProjectSearchAttachModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded || _isLoadingSPSettings) return;

        var attachEnabled = SmartProjectSearchAttachModeToggle.IsChecked == true;
        _settings.SetSmartProjectSearchAttachToSearchOverlayMode(attachEnabled);

        _isLoadingSPSettings = true;
        _widgetToggles.TryGetValue(WidgetIds.SmartProjectSearch, out var spEnabledToggle);
        if (attachEnabled)
        {
            var currentLauncherEnabled = _settings.GetSmartProjectSearchWidgetEnabled();
            _settings.SetSmartProjectSearchWidgetEnabledBeforeAttachMode(currentLauncherEnabled);
            _settings.SetSmartProjectSearchWidgetEnabled(false);
            if (spEnabledToggle != null) { spEnabledToggle.IsChecked = false; spEnabledToggle.IsEnabled = false; }
            StatusText.Text = "Smart Project Search attached mode enabled";
        }
        else
        {
            var restoreLauncherEnabled = _settings.GetSmartProjectSearchWidgetEnabledBeforeAttachMode();
            _settings.SetSmartProjectSearchWidgetEnabled(restoreLauncherEnabled);
            if (spEnabledToggle != null) { spEnabledToggle.IsEnabled = true; spEnabledToggle.IsChecked = restoreLauncherEnabled; }
            StatusText.Text = "Smart Project Search attached mode disabled";
        }
        _isLoadingSPSettings = false;

        _ = _settings.SaveAsync();
        _onSmartProjectSearchWidgetEnabledChanged?.Invoke();
    }

    private void SmartSearchLatestMode_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded || _isLoadingSPSettings) return;
        var mode = SmartSearchLatestSingleRadio.IsChecked == true ? "single" : "list";
        _settings.SetSmartProjectSearchLatestMode(mode);
        _ = _settings.SaveAsync();
        StatusText.Text = mode == "single"
            ? "Smart search latest mode: single newest result"
            : "Smart search latest mode: newest-first list";
    }

    private void SmartSearchFileTypesInput_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded || _isLoadingSPSettings) return;

        var values = SmartSearchFileTypesInput.Text
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(v => v.Trim().TrimStart('.').ToLowerInvariant())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _settings.SetSmartProjectSearchFileTypes(values);
        SmartSearchFileTypesInput.Text = string.Join(", ", _settings.GetSmartProjectSearchFileTypes());
        _ = _settings.SaveAsync();
        StatusText.Text = $"Smart search file types updated ({_settings.GetSmartProjectSearchFileTypes().Count} entries)";
    }

    // ===== General Tab - Update Settings =====

    private void AutoUpdateCheckToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = AutoUpdateCheckToggle.IsChecked == true;
        _settings.SetAutoUpdateCheckEnabled(enabled);
        _ = _settings.SaveAsync();
        _onUpdateSettingsChanged?.Invoke();
        StatusText.Text = enabled ? "Auto update check enabled" : "Auto update check disabled";
    }

    private void AutoUpdateInstallToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        var enabled = AutoUpdateInstallToggle.IsChecked == true;
        _settings.SetAutoUpdateInstallEnabled(enabled);
        _ = _settings.SaveAsync();
        StatusText.Text = enabled ? "Auto install enabled" : "Auto install disabled";
    }

    private void UpdateFrequencyCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_settings == null || !IsLoaded) return;
        if (UpdateFrequencyCombo.SelectedItem is System.Windows.Controls.ComboBoxItem item && item.Tag is string tagStr && int.TryParse(tagStr, out int minutes))
        {
            _settings.SetUpdateCheckFrequencyMinutes(minutes);
            _ = _settings.SaveAsync();
            _onUpdateSettingsChanged?.Invoke();
            StatusText.Text = $"Update check frequency set to {minutes / 60} hour(s)";
        }
    }

    private void LoadUpdateFrequencyCombo()
    {
        var currentMinutes = _settings.GetUpdateCheckFrequencyMinutes();
        for (int i = 0; i < UpdateFrequencyCombo.Items.Count; i++)
        {
            if (UpdateFrequencyCombo.Items[i] is System.Windows.Controls.ComboBoxItem item && 
                item.Tag is string tagStr && int.TryParse(tagStr, out int minutes) && minutes == currentMinutes)
            {
                UpdateFrequencyCombo.SelectedIndex = i;
                return;
            }
        }
        // Default to "Every 6 hours" if no match
        UpdateFrequencyCombo.SelectedIndex = 1;
    }

    // ===== Hotkey Groups =====

    private void LoadHotkeyGroupsUI()
    {
        if (_settings == null) return;
        var groups = _settings.GetHotkeyGroups();
        RebuildHotkeyGroupsPanel(groups);
    }

    private void RebuildHotkeyGroupsPanel(List<HotkeyGroup> groups)
    {
        HotkeyGroupsPanel.Children.Clear();
        for (int i = 0; i < groups.Count; i++)
        {
            var groupRow = BuildHotkeyGroupRow(groups, i);
            HotkeyGroupsPanel.Children.Add(groupRow);
        }
        // Add group button — max 5 groups
        if (groups.Count < 5)
        {
            var addBtn = new System.Windows.Controls.Button
            {
                Content = "+ Add Hotkey Group",
                Margin = new Thickness(0, 8, 0, 0),
                Padding = new Thickness(14, 7, 14, 7),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF)),
                Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
                BorderThickness = new Thickness(1),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            };
            addBtn.Click += (s, e) =>
            {
                var newGroups = _settings.GetHotkeyGroups();
                newGroups.Add(new HotkeyGroup { Modifiers = 0, Key = 0, Widgets = new List<string>() });
                _settings.SetHotkeyGroups(newGroups);
                _ = _settings.SaveAsync();
                RebuildHotkeyGroupsPanel(newGroups);
            };
            HotkeyGroupsPanel.Children.Add(addBtn);
        }
    }

    private UIElement BuildHotkeyGroupRow(List<HotkeyGroup> groups, int index)
    {
        var group = groups[index];
        var outerBorder = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(14, 12, 14, 12),
            Margin = new Thickness(0, 0, 0, 8),
        };

        var stack = new StackPanel();
        outerBorder.Child = stack;

        // Row header: group number + key binding recorder + remove button
        var headerRow = new Grid();
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var groupLabel = new TextBlock
        {
            Text = $"Group {index + 1}",
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = (System.Windows.Media.Brush)FindResource("TextBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0),
        };
        Grid.SetColumn(groupLabel, 0);
        headerRow.Children.Add(groupLabel);

        // Key binding box
        var keyText = new TextBlock
        {
            Text = group.Key != 0 ? FormatHotkey(group.Modifiers, group.Key) : "Click to set hotkey",
            FontSize = 12,
            Foreground = group.Key != 0
                ? (System.Windows.Media.Brush)FindResource("TextBrush")
                : (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
        };
        var recordingText = new TextBlock
        {
            Text = "Press any key combo...",
            FontSize = 12,
            Foreground = (System.Windows.Media.Brush)FindResource("PrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };
        var keyBox = new Border
        {
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x25, 0x00, 0x00, 0x00)),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(10, 5, 10, 5),
            Cursor = System.Windows.Input.Cursors.Hand,
            MinWidth = 140,
            Tag = false, // recording state
        };
        var keyBoxInner = new Grid();
        keyBoxInner.Children.Add(keyText);
        keyBoxInner.Children.Add(recordingText);
        keyBox.Child = keyBoxInner;
        Grid.SetColumn(keyBox, 1);

        int capturedIndex = index;
        keyBox.MouseDown += (s, e) =>
        {
            bool isRecording = (bool)keyBox.Tag;
            if (!isRecording)
            {
                keyBox.Tag = true;
                keyText.Visibility = Visibility.Collapsed;
                recordingText.Visibility = Visibility.Visible;
                keyBox.BorderBrush = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
                _activeGroupKeyBox = keyBox;
                _activeGroupKeyText = keyText;
                _activeGroupRecordingText = recordingText;
                _activeGroupIndex = capturedIndex;
                this.Focus();
            }
        };
        headerRow.Children.Add(keyBox);

        // Remove button (hidden for group 1 if it's the only group)
        var removeBtn = new System.Windows.Controls.Button
        {
            Content = "✕",
            FontSize = 11,
            Width = 28,
            Height = 28,
            Margin = new Thickness(8, 0, 0, 0),
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x18, 0xFF, 0x40, 0x40)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x80, 0x80)),
            BorderThickness = new Thickness(0),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = groups.Count > 1 ? Visibility.Visible : Visibility.Collapsed,
        };
        removeBtn.Click += (s, e) =>
        {
            var newGroups = _settings.GetHotkeyGroups();
            newGroups.RemoveAt(capturedIndex);
            _settings.SetHotkeyGroups(newGroups);
            _ = _settings.SaveAsync();
            _onHotkeyChanged?.Invoke();
            RebuildHotkeyGroupsPanel(newGroups);
        };
        Grid.SetColumn(removeBtn, 3);
        headerRow.Children.Add(removeBtn);

        stack.Children.Add(headerRow);

        // Widget pill row
        var pillLabel = new TextBlock
        {
            Text = group.Key != 0 ? "Widgets in this group:" : "Widgets in this group (set a hotkey to activate):",
            FontSize = 11,
            Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
            Margin = new Thickness(0, 10, 0, 6),
        };
        stack.Children.Add(pillLabel);

        var pillPanel = new WrapPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
        foreach (var widgetId in WidgetIds.All)
        {
            bool inGroup = group.Widgets.Contains(widgetId);
            // Check if widget is in another group
            int ci = capturedIndex;
            bool inOtherGroup = groups.Where((g, gi) => gi != ci).Any(g => g.Widgets.Contains(widgetId));
            if (inGroup == false && inOtherGroup) continue; // hide widgets owned by another group (exclusive)

            var pill = BuildWidgetPill(widgetId, inGroup, capturedIndex, groups);
            pillPanel.Children.Add(pill);
        }
        stack.Children.Add(pillPanel);

        // Show unassigned widgets note
        var allAssigned = WidgetIds.All.All(id => groups.Any(g => g.Widgets.Contains(id)));
        if (!allAssigned && index == groups.Count - 1)
        {
            var unassignedPanel = new WrapPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            var unassignedLabel = new TextBlock
            {
                Text = "Unassigned (no hotkey): ",
                FontSize = 11,
                Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            };
            unassignedPanel.Children.Add(unassignedLabel);
            foreach (var widgetId in WidgetIds.All)
            {
                bool inAnyGroup = groups.Any(g => g.Widgets.Contains(widgetId));
                if (!inAnyGroup)
                {
                    var capturedWidgetId = widgetId;
                    var chipText = new TextBlock
                    {
                        Text = WidgetIds.DisplayName(widgetId),
                        FontSize = 11,
                        Foreground = (System.Windows.Media.Brush)FindResource("TextSecondaryBrush"),
                        IsHitTestVisible = false,
                    };
                    var chip = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF)),
                        BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(12),
                        Padding = new Thickness(8, 3, 8, 3),
                        Margin = new Thickness(0, 2, 6, 2),
                        Cursor = System.Windows.Input.Cursors.Hand,
                        Child = chipText,
                    };
                    chip.MouseLeftButtonUp += (s, ev) =>
                    {
                        var newGroups = _settings.GetHotkeyGroups();
                        // Find first group that has a hotkey assigned
                        var targetIdx = newGroups.FindIndex(g => g.Key != 0);
                        if (targetIdx >= 0)
                        {
                            newGroups[targetIdx].Widgets.Add(capturedWidgetId);
                            _settings.SetHotkeyGroups(newGroups);
                            _ = _settings.SaveAsync();
                            _onHotkeyChanged?.Invoke();
                            RebuildHotkeyGroupsPanel(newGroups);
                        }
                    };
                    unassignedPanel.Children.Add(chip);
                }
            }
            stack.Children.Add(unassignedPanel);
        }

        return outerBorder;
    }

    private UIElement BuildWidgetPill(string widgetId, bool inGroup, int groupIndex, List<HotkeyGroup> groups)
    {
        var pill = new Border
        {
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(0, 2, 6, 2),
            Cursor = System.Windows.Input.Cursors.Hand,
            BorderThickness = new Thickness(1),
        };
        var pillText = new TextBlock { Text = WidgetIds.DisplayName(widgetId), FontSize = 12, IsHitTestVisible = false };
        pill.Child = pillText;
        ApplyPillStyle(pill, pillText, inGroup);

        pill.MouseLeftButtonUp += (s, e) =>
        {
            var newGroups = _settings.GetHotkeyGroups();
            bool nowIn = newGroups[groupIndex].Widgets.Contains(widgetId);
            if (!nowIn)
            {
                // Exclusive: remove from any other group first
                foreach (var g in newGroups)
                    g.Widgets.Remove(widgetId);
                newGroups[groupIndex].Widgets.Add(widgetId);
            }
            else
            {
                newGroups[groupIndex].Widgets.Remove(widgetId);
            }
            _settings.SetHotkeyGroups(newGroups);
            _ = _settings.SaveAsync();
            _onHotkeyChanged?.Invoke();
            RebuildHotkeyGroupsPanel(newGroups);
        };

        return pill;
    }

    private static void ApplyPillStyle(Border pill, TextBlock pillText, bool active)
    {
        if (active)
        {
            pill.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0x58, 0xC4, 0xFF));
            pill.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xA0, 0x58, 0xC4, 0xFF));
            pillText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xA8, 0xD8, 0xFF));
        }
        else
        {
            pill.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x18, 0xFF, 0xFF, 0xFF));
            pill.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
            pillText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xB0, 0xB8, 0xC8));
        }
    }

    // ===== Frequent Projects Tab =====

    private async void LoadFrequentProjectsSettings()
    {
        _isLoadingFPSettings = true;
        try
        {
            FP_MaxShownSlider.Value = _settings.GetMaxFrequentProjectsShown();
            FP_MaxShownValue.Text = _settings.GetMaxFrequentProjectsShown().ToString();
            FP_MaxSavedSlider.Value = _settings.GetMaxFrequentProjectsSaved();
            FP_MaxSavedValue.Text = _settings.GetMaxFrequentProjectsSaved().ToString();
            FP_GridModeToggle.IsChecked = _settings.GetFrequentProjectsGridMode();

            // Load stats
            if (_launchDataStore != null)
            {
                var topProjects = await _launchDataStore.GetTopProjectsAsync(100);
                var totalLaunches = topProjects.Sum(p => p.LaunchCount);
                FP_StatsText.Text = $"Tracking {topProjects.Count} project{(topProjects.Count == 1 ? "" : "s")} with {totalLaunches} total launch{(totalLaunches == 1 ? "" : "es")}";
            }
            else
            {
                FP_StatsText.Text = "Launch tracking not available";
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"LoadFrequentProjectsSettings error: {ex.Message}");
        }
        finally
        {
            _isLoadingFPSettings = false;
        }
    }

    private void FP_MaxShownSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingFPSettings || _settings == null || !IsLoaded) return;
        var value = (int)e.NewValue;
        _settings.SetMaxFrequentProjectsShown(value);
        _ = _settings.SaveAsync();
        if (FP_MaxShownValue != null) FP_MaxShownValue.Text = value.ToString();
        StatusText.Text = $"Max projects shown: {value}";
    }

    private void FP_MaxSavedSlider_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isLoadingFPSettings || _settings == null || !IsLoaded) return;
        var value = (int)e.NewValue;
        _settings.SetMaxFrequentProjectsSaved(value);
        _ = _settings.SaveAsync();
        if (FP_MaxSavedValue != null) FP_MaxSavedValue.Text = value.ToString();
        StatusText.Text = $"Max projects tracked: {value}";
    }

    private void FP_GridModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingFPSettings || _settings == null || !IsLoaded) return;
        var gridMode = FP_GridModeToggle.IsChecked == true;
        _settings.SetFrequentProjectsGridMode(gridMode);
        _ = _settings.SaveAsync();
        _onFrequentProjectsLayoutChanged?.Invoke();
        StatusText.Text = gridMode ? "Frequent Projects: grid mode" : "Frequent Projects: list mode";
    }

    private async void FP_ResetData_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to reset all project launch data?\n\nThis will clear all launch counts and cannot be undone.",
            "Reset Launch Data",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes && _launchDataStore != null)
        {
            try
            {
                await _launchDataStore.ClearAllAsync();
                FP_StatsText.Text = "Tracking 0 projects with 0 total launches";
                StatusText.Text = "All launch data has been reset";
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"FP_ResetData_Click error: {ex.Message}");
                StatusText.Text = $"Failed to reset data: {ex.Message}";
            }
        }
    }

    // ===== Quick Launch Tab =====

    private async void LoadQuickLaunchSettings()
    {
        _isLoadingQLSettings = true;
        try
        {
            QL_HorizontalModeToggle.IsChecked = _settings.GetQuickLaunchHorizontalMode();

            // Load current items
            var config = await Infrastructure.Settings.QuickLaunchConfig.LoadAsync();
            var items = config.Items.OrderBy(i => i.SortOrder).ToList();
            QL_ItemCountText.Text = $"{items.Count} item{(items.Count == 1 ? "" : "s")} configured";

            QL_ItemsList.Children.Clear();
            foreach (var item in items)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var icon = new TextBlock
                {
                    Text = item.Icon,
                    FontSize = 14,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var info = new StackPanel();
                info.Children.Add(new TextBlock
                {
                    Text = item.Name,
                    FontSize = 12,
                    FontWeight = FontWeights.Medium,
                    Foreground = FindResource("TextBrush") as System.Windows.Media.Brush
                });
                info.Children.Add(new TextBlock
                {
                    Text = item.Path,
                    FontSize = 10,
                    Foreground = FindResource("TextSecondaryBrush") as System.Windows.Media.Brush,
                    TextTrimming = TextTrimming.CharacterEllipsis
                });

                Grid.SetColumn(icon, 0);
                Grid.SetColumn(info, 1);
                row.Children.Add(icon);
                row.Children.Add(info);

                var border = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10F5F7FA")),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 6, 8, 6),
                    Child = row
                };

                QL_ItemsList.Children.Add(border);
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"LoadQuickLaunchSettings error: {ex.Message}");
        }
        finally
        {
            _isLoadingQLSettings = false;
        }
    }

    private void QL_HorizontalModeToggle_Changed(object sender, RoutedEventArgs e)
    {
        if (_isLoadingQLSettings || _settings == null || !IsLoaded) return;
        var horizontal = QL_HorizontalModeToggle.IsChecked == true;
        _settings.SetQuickLaunchHorizontalMode(horizontal);
        _ = _settings.SaveAsync();
        _onQuickLaunchLayoutChanged?.Invoke();
        StatusText.Text = horizontal ? "Quick Launch: horizontal mode" : "Quick Launch: vertical mode";
    }

    private async void QL_ClearAll_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to remove all Quick Launch items?\n\nThis cannot be undone.",
            "Clear All Items",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                var config = await Infrastructure.Settings.QuickLaunchConfig.LoadAsync();
                config.Items.Clear();
                await config.SaveAsync();
                QL_ItemsList.Children.Clear();
                QL_ItemCountText.Text = "0 items configured";
                StatusText.Text = "All Quick Launch items cleared";
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"QL_ClearAll_Click error: {ex.Message}");
                StatusText.Text = $"Failed to clear items: {ex.Message}";
            }
        }
    }
}
