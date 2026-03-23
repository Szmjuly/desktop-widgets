using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using DesktopHub.Infrastructure.Data;
using DesktopHub.Infrastructure.Firebase;
using DesktopHub.Infrastructure.Scanning;
using DesktopHub.Infrastructure.Search;
using DesktopHub.Infrastructure.Settings;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class SearchOverlay
{
    public SearchOverlay()
    {
        DebugLogger.Clear();
        DebugLogger.Log("SearchOverlay: Constructor starting");

        InitializeComponent();

        // Initialize services
        _scanner = new ProjectScanner();
        _dataStore = new SqliteDataStore();
        _settings = new SettingsService();

        // Create tag + vocabulary + registry services — needs FirebaseService from App
        _vocabService = new TagVocabularyService(); // Local-only, no Firebase
        var app = (App)System.Windows.Application.Current;
        var firebaseService = app.FirebaseManager?.FirebaseService;
        if (firebaseService != null)
        {
            _tagService = new ProjectTagService(firebaseService);
            _tagRegistryService = new TagRegistryService(firebaseService);
            _searchService = new SearchService(_tagService);
        }
        else
        {
            _searchService = new SearchService();
        }
        _timerService = new TimerService();
        _taskService = new TaskService(new Infrastructure.Data.TaskDataStore());
        _docService = new DocOpenService(new Infrastructure.Scanning.DocumentScanner());
        _smartProjectSearchService = new SmartProjectSearchService(new Infrastructure.Scanning.DocumentScanner(), _settings);
        if (firebaseService != null)
        {
            _cheatSheetDataService = new CheatSheetDataService(firebaseService);
            _cheatSheetService = new CheatSheetService(_cheatSheetDataService);
        }
        else
        {
            _cheatSheetService = new CheatSheetService();
        }

        // Setup transparency when window handle is available
        SourceInitialized += (s, e) =>
        {
            try
            {
                DebugLogger.Log("SearchOverlay: SourceInitialized - Setting up transparency");
                DebugLogger.Log($"  Window.Background before: {this.Background}");
                DebugLogger.Log($"  Window.AllowsTransparency: {this.AllowsTransparency}");
                DebugLogger.Log($"  Window.WindowStyle: {this.WindowStyle}");

                WindowBlur.SetupTransparency(this);

                // DO NOT apply rounded corners to window region - it clips the window and creates white edges
                // WindowBlur.ApplyRoundedCorners(this, 12);
                DebugLogger.Log("  Skipping window region rounding to avoid clipping");

                UpdateRootClip(12);

                DebugLogger.Log($"  Window.Background after: {this.Background}");
                DebugLogger.Log($"  RootBorder.Background: {RootBorder.Background}");
                DebugLogger.Log("SearchOverlay: Transparency setup complete");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"SearchOverlay: Transparency setup FAILED: {ex}");
                System.Windows.MessageBox.Show($"Transparency setup failed: {ex.Message}\n\nCheck Desktop for DesktopHub_Debug.log", "Debug", MessageBoxButton.OK);
            }
        };

        // Reapply rounded corners on resize and reposition attached smart search window
        SizeChanged += (s, e) =>
        {
            // DO NOT apply window region rounding - causes clipping
            // WindowBlur.ApplyRoundedCorners(this, 12);
            if (!_suspendRootClipUpdates)
            {
                UpdateRootClip(12);
            }
            PositionSmartSearchAttachedWindow();
        };

        // Keep attached smart search window positioned below overlay when dragged
        LocationChanged += (s, e) =>
        {
            PositionSmartSearchAttachedWindow();
        };
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // Enable blur effect (window handle is now available)
            try
            {
                DebugLogger.Log("SearchOverlay: Window_Loaded - Enabling blur");
                DebugLogger.Log($"  Window dimensions: {this.ActualWidth}x{this.ActualHeight}");
                DebugLogger.Log($"  Window Width/Height props: {this.Width}x{this.Height}");
                DebugLogger.Log($"  RootBorder dimensions: {RootBorder.ActualWidth}x{RootBorder.ActualHeight}");
                DebugLogger.Log($"  RootBorder Width/Height props: {RootBorder.Width}x{RootBorder.Height}");
                DebugLogger.Log($"  RootBorder margin: {RootBorder.Margin}");
                DebugLogger.Log($"  RootBorder padding: {RootBorder.Padding}");
                DebugLogger.Log($"  RootBorder HAlign: {RootBorder.HorizontalAlignment}");
                DebugLogger.Log($"  RootBorder VAlign: {RootBorder.VerticalAlignment}");
                DebugLogger.Log($"  RootBorder DesiredSize: {RootBorder.DesiredSize}");
                DebugLogger.Log($"  RootBorder RenderSize: {RootBorder.RenderSize}");
                DebugLogger.Log($"  Window is visible: {this.IsVisible}");
                DebugLogger.Log($"  Window opacity: {this.Opacity}");
                DebugLogger.Log($"  Window Content: {this.Content}");
                DebugLogger.Log($"  Content is RootBorder: {ReferenceEquals(this.Content, RootBorder)}");

                // Check if RootBorder fills the window
                var widthDiff = this.ActualWidth - RootBorder.ActualWidth;
                var heightDiff = this.ActualHeight - RootBorder.ActualHeight;
                DebugLogger.Log($"  Size difference (Window - Border): {widthDiff}x{heightDiff}");
                if (widthDiff > 1 || heightDiff > 1)
                {
                    DebugLogger.Log("  WARNING: RootBorder does NOT fill window - white edges will show!");
                }

                // DISABLE blur - it's rendering as solid black on this Windows version
                DebugLogger.Log("  SKIPPING blur - testing raw transparency without blur");
                // WindowBlur.EnableBlur(this, useAcrylic: false);

                DebugLogger.Log("SearchOverlay: Blur enabled");
                DebugLogger.Log($"  Final Window.Background: {this.Background}");
                DebugLogger.Log($"  Final RootBorder.Background: {RootBorder.Background}");

                // Log opacity of all layers
                var rootBg = RootBorder.Background as System.Windows.Media.SolidColorBrush;
                if (rootBg != null)
                {
                    DebugLogger.Log($"  RootBorder background color: {rootBg.Color}");
                    DebugLogger.Log($"  RootBorder alpha: {rootBg.Color.A} ({rootBg.Color.A / 255.0 * 100:F1}%)");
                }

                // Check if we can actually see through
                DebugLogger.Log($"  Window.Opacity: {this.Opacity}");
                DebugLogger.Log($"  RootBorder.Opacity: {RootBorder.Opacity}");
                DebugLogger.Log($"  Window.IsVisible: {this.IsVisible}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"SearchOverlay: Blur FAILED: {ex}");
            }

            // Load settings
            await _settings.LoadAsync();

            // Initialize database
            try
            {
                DebugLogger.Log("SearchOverlay: Starting database initialization");
                await _dataStore.InitializeAsync();
                DebugLogger.Log("SearchOverlay: Database initialized successfully");

                // Initialize launch tracking data store (same DB)
                _launchDataStore = new ProjectLaunchDataStore();
                await _launchDataStore.InitializeAsync();
                DebugLogger.Log("SearchOverlay: Launch tracking data store initialized");

                // Initialize tag service (loads local cache, starts background Firebase sync)
                if (_tagService != null)
                {
                    try
                    {
                        await _tagService.InitializeAsync();

                        // Refresh local vocabulary suggestions from cached project tags
                        _vocabService?.RefreshFromCache(_tagService.GetAllCachedTags());

                        // Initialize shared custom tag key registry
                        if (_tagRegistryService != null)
                            await _tagRegistryService.InitializeAsync();

                        RefreshTagCarousel();
                        DebugLogger.Log("SearchOverlay: Tag + vocabulary + registry services initialized");
                    }
                    catch (Exception tagEx)
                    {
                        DebugLogger.Log($"SearchOverlay: Tag service init failed (non-fatal): {tagEx.Message}");
                    }
                }
            }
            catch (Exception dbEx)
            {
                DebugLogger.Log($"SearchOverlay: DATABASE INITIALIZATION FAILED: {dbEx.Message}");
                DebugLogger.Log($"SearchOverlay: Exception type: {dbEx.GetType().Name}");
                DebugLogger.Log($"SearchOverlay: Stack trace: {dbEx.StackTrace}");
                if (dbEx.InnerException != null)
                {
                    DebugLogger.Log($"SearchOverlay: Inner exception: {dbEx.InnerException.Message}");
                    DebugLogger.Log($"SearchOverlay: Inner exception type: {dbEx.InnerException.GetType().Name}");
                }
                throw;
            }

            // Load persisted search history
            try
            {
                _searchHistoryStore = new Infrastructure.Data.SearchHistoryStore();
                await _searchHistoryStore.LoadAsync();

                var retentionDays = _settings.GetSearchHistoryRetentionDays();
                await _searchHistoryStore.PruneOlderThanAsync(retentionDays);

                _searchHistory = _searchHistoryStore.GetEntries(25);
                DebugLogger.Log($"SearchOverlay: Loaded {_searchHistory.Count} search history entries");
            }
            catch (Exception histEx)
            {
                DebugLogger.Log($"SearchOverlay: Search history load failed (non-fatal): {histEx.Message}");
            }

            var (modifiers, key) = _settings.GetHotkey();
            var hotkeyLabel = FormatHotkey(modifiers, key);

            // Initialize system tray icon
            _trayIcon = new TrayIcon(this, hotkeyLabel, _settings);

            // Initialize widget launcher
            _widgetLauncher = new WidgetLauncher(_settings);
            _widgetLauncher.SearchWidgetRequested += OnSearchWidgetRequested;
            _widgetLauncher.TimerWidgetRequested += OnTimerWidgetRequested;
            _widgetLauncher.QuickTasksWidgetRequested += OnQuickTasksWidgetRequested;
            _widgetLauncher.DocQuickOpenRequested += OnDocQuickOpenRequested;
            _widgetLauncher.FrequentProjectsRequested += OnFrequentProjectsRequested;
            _widgetLauncher.QuickLaunchRequested += OnQuickLaunchRequested;
            _widgetLauncher.SmartProjectSearchRequested += OnSmartProjectSearchRequested;
            _widgetLauncher.CheatSheetRequested += OnCheatSheetRequested;
            _widgetLauncher.MetricsViewerRequested += OnMetricsViewerRequested;
            _widgetLauncher.ProjectInfoRequested += OnProjectInfoRequested;

            RegisterWidgetWindow(this);
            RegisterWidgetWindow(_widgetLauncher);

            // Register global hotkeys from all hotkey groups
            RegisterHotkeysFromGroups();

            // Initialize dragging mode based on Living Widgets Mode setting
            // Note: Virtual desktop pinning is deferred until after window is fully loaded
            UpdateDraggingMode();

            // Restore saved widget positions and visibility if in Living Widgets Mode
            var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
            if (isLivingWidgetsMode)
            {
                var (overlayLeft, overlayTop) = _settings.GetSearchOverlayPosition();
                if (overlayLeft.HasValue && overlayTop.HasValue)
                {
                    // Validate restored position against current screen bounds
                    // (display config may have changed, e.g. office 2-monitor → RDP single-monitor)
                    var restoredRect = new Rect(overlayLeft.Value, overlayTop.Value,
                        this.ActualWidth > 0 ? this.ActualWidth : this.Width,
                        this.ActualHeight > 0 ? this.ActualHeight : this.Height);
                    var clamped = ClampRectToScreen(restoredRect, GetConfiguredWidgetGap());
                    this.Left = clamped.Left;
                    this.Top = clamped.Top;
                    DebugLogger.Log($"Restored search overlay position: saved({overlayLeft.Value:F0}, {overlayTop.Value:F0}) → clamped({clamped.Left:F0}, {clamped.Top:F0})");
                }

                var (launcherLeft, launcherTop) = _settings.GetWidgetLauncherPosition();
                if (launcherLeft.HasValue && launcherTop.HasValue && _widgetLauncher != null)
                {
                    var launcherWidth = _widgetLauncher.ActualWidth > 0 ? _widgetLauncher.ActualWidth : _widgetLauncher.Width;
                    var launcherHeight = _widgetLauncher.ActualHeight > 0 ? _widgetLauncher.ActualHeight : _widgetLauncher.Height;
                    var launcherRect = new Rect(launcherLeft.Value, launcherTop.Value,
                        double.IsNaN(launcherWidth) ? 180 : launcherWidth,
                        double.IsNaN(launcherHeight) ? 300 : launcherHeight);
                    var clampedLauncher = ClampRectToScreen(launcherRect, GetConfiguredWidgetGap());
                    _widgetLauncher.Left = clampedLauncher.Left;
                    _widgetLauncher.Top = clampedLauncher.Top;
                    DebugLogger.Log($"Restored widget launcher position: saved({launcherLeft.Value:F0}, {launcherTop.Value:F0}) → clamped({clampedLauncher.Left:F0}, {clampedLauncher.Top:F0})");
                }

                // Restore search overlay visibility state
                var searchOverlayVisible = _settings.GetSearchOverlayVisible();
                if (searchOverlayVisible)
                {
                    ShowOverlay();
                    DebugLogger.Log($"Restored search overlay visibility: {searchOverlayVisible}");
                }
                else
                {
                    HideOverlay();
                    DebugLogger.Log($"Restored search overlay visibility: {searchOverlayVisible}");
                }

                // Restore widget launcher visibility state
                var widgetLauncherVisible = _settings.GetWidgetLauncherVisible();
                if (_widgetLauncher != null)
                {
                    _widgetLauncher.Visibility = widgetLauncherVisible ? Visibility.Visible : Visibility.Hidden;
                    DebugLogger.Log($"Restored widget launcher visibility: {widgetLauncherVisible}");
                }
            }
            else
            {
                // Not in Living Widgets Mode - hide overlay by default
                HideOverlay();
            }

            // Restore individual widgets if they were visible (regardless of Living Widgets Mode)
            var timerVisible = _settings.GetTimerWidgetVisible();
            if (timerVisible)
            {
                CreateTimerOverlay();
                DebugLogger.Log("Restored timer widget from previous session");
            }

            var quickTasksVisible = _settings.GetQuickTasksWidgetVisible();
            if (quickTasksVisible)
            {
                CreateQuickTasksOverlay();
                DebugLogger.Log("Restored quick tasks widget from previous session");
            }

            var docVisible = _settings.GetDocWidgetVisible();
            if (docVisible)
            {
                CreateDocOverlay();
                DebugLogger.Log("Restored doc quick open widget from previous session");
            }

            var frequentProjectsVisible = _settings.GetFrequentProjectsWidgetVisible();
            if (frequentProjectsVisible)
            {
                CreateFrequentProjectsOverlay();
                DebugLogger.Log("Restored frequent projects widget from previous session");
            }

            var quickLaunchVisible = _settings.GetQuickLaunchWidgetVisible();
            if (quickLaunchVisible)
            {
                CreateQuickLaunchOverlay();
                DebugLogger.Log("Restored quick launch widget from previous session");
            }

            var smartProjectSearchAttachModeEnabled = _settings.GetSmartProjectSearchAttachToSearchOverlayMode();
            var smartProjectSearchVisible = _settings.GetSmartProjectSearchWidgetVisible();
            if (!smartProjectSearchAttachModeEnabled && smartProjectSearchVisible)
            {
                CreateSmartProjectSearchOverlay();
                DebugLogger.Log("Restored smart project search widget from previous session");
            }

            var cheatSheetVisible = _settings.GetCheatSheetWidgetVisible();
            if (cheatSheetVisible)
            {
                CreateCheatSheetOverlay();
                DebugLogger.Log("Restored cheat sheet widget from previous session");
            }

            ApplySmartProjectSearchAttachModeState();

            if (isLivingWidgetsMode)
            {
                NormalizeDocStartupGapIfNeeded();
                RefreshAttachmentMappings();
                TrackVisibleWindowBounds();
            }
            else
            {
                // Non-live mode: detect display config changes and re-layout if needed
                var currentFingerprint = Helpers.ScreenHelper.GetDisplayConfigFingerprint();
                var savedFingerprint = _settings.GetLastDisplayConfigFingerprint();
                if (!string.IsNullOrEmpty(savedFingerprint) && savedFingerprint != currentFingerprint)
                {
                    DebugLogger.Log($"Display config changed: {savedFingerprint} → {currentFingerprint}. Re-arranging widgets.");
                    RearrangeNonLiveWidgets();
                }
                _settings.SetLastDisplayConfigFingerprint(currentFingerprint);
            }

            // Load projects in the background
            _ = LoadProjectsAsync();

            // Purge any DB records whose folder no longer exists on disk (handles renames)
            _ = Task.Run(async () => await PurgeNonExistentProjectsAsync());

            // Start background scan if needed
            _ = Task.Run(async () => await BackgroundScanAsync());

            // Start IPC listener for commands from second instances
            _ = Task.Run(() => StartIpcListener());

            // Start desktop follower to move widgets across virtual desktops
            if (isLivingWidgetsMode)
            {
                StartDesktopFollower();
            }

            // Initialize update indicator manager and register widgets
            _updateIndicatorManager = new UpdateIndicatorManager();
            _updateIndicatorManager.RegisterWidget("SearchOverlay", 1, this,
                visible => Dispatcher.Invoke(() => SetUpdateIndicatorVisible(visible)));
            if (_widgetLauncher != null)
                _updateIndicatorManager.RegisterWidget("WidgetLauncher", 2, _widgetLauncher,
                    visible => Dispatcher.Invoke(() => _widgetLauncher.SetUpdateIndicatorVisible(visible)));
            if (_timerOverlay != null)
                _updateIndicatorManager.RegisterWidget("TimerOverlay", 3, _timerOverlay,
                    visible => Dispatcher.Invoke(() => _timerOverlay.SetUpdateIndicatorVisible(visible)));
            if (_quickTasksOverlay != null)
                _updateIndicatorManager.RegisterWidget("QuickTasksOverlay", 4, _quickTasksOverlay,
                    visible => Dispatcher.Invoke(() => _quickTasksOverlay.SetUpdateIndicatorVisible(visible)));
            if (_docOverlay != null)
                _updateIndicatorManager.RegisterWidget("DocQuickOpenOverlay", 5, _docOverlay,
                    visible => Dispatcher.Invoke(() => _docOverlay.SetUpdateIndicatorVisible(visible)));
            if (_smartProjectSearchOverlay != null)
                _updateIndicatorManager.RegisterWidget("SmartProjectSearchOverlay", 8, _smartProjectSearchOverlay,
                    visible => Dispatcher.Invoke(() => _smartProjectSearchOverlay.SetUpdateIndicatorVisible(visible)));
            if (_cheatSheetOverlay != null)
                _updateIndicatorManager.RegisterWidget("CheatSheetOverlay", 9, _cheatSheetOverlay,
                    visible => Dispatcher.Invoke(() => _cheatSheetOverlay.SetUpdateIndicatorVisible(visible)));

            // Re-apply in case settings changed while startup widgets were restoring.
            UpdateSmartProjectSearchWidgetButton();
            UpdateCheatSheetWidgetButton();

            // Initialize periodic update checking
            InitializeUpdateCheckService();

            // Subscribe to theme changes so transparency + colors refresh
            var app2 = (App)System.Windows.Application.Current;
            if (app2.Theme != null)
            {
                app2.Theme.ThemeChanged += OnThemeChanged;
            }

            // Load transparency setting
            // ...
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to initialize: {ex.Message}",
                "DesktopHub",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );
        }
    }
}
