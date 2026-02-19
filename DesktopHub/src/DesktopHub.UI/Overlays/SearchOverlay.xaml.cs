using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Forms;
using System.Windows.Automation;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Data;
using DesktopHub.Infrastructure.Scanning;
using DesktopHub.Infrastructure.Search;
using DesktopHub.Infrastructure.Settings;
using DesktopHub.UI.Helpers;
using DesktopHub.UI.Services;
using DesktopHub.UI.Widgets;

namespace DesktopHub.UI;

public partial class SearchOverlay : Window
{
    private readonly IProjectScanner _scanner;
    private readonly ISearchService _searchService;
    private readonly IDataStore _dataStore;
    private readonly ISettingsService _settings;
    private readonly TimerService _timerService;
    private GlobalHotkey? _hotkey;
    private TrayIcon? _trayIcon;
    private TimerOverlay? _timerOverlay;
    private QuickTasksOverlay? _quickTasksOverlay;
    private DocQuickOpenOverlay? _docOverlay;
    private FrequentProjectsOverlay? _frequentProjectsOverlay;
    private QuickLaunchOverlay? _quickLaunchOverlay;
    private SmartProjectSearchOverlay? _smartProjectSearchOverlay;
    private WidgetLauncher? _widgetLauncher;
    private TaskService? _taskService;
    private DocOpenService? _docService;
    private SmartProjectSearchService? _smartProjectSearchService;
    private IProjectLaunchDataStore? _launchDataStore;
    private List<Project> _allProjects = new();
    private List<Project> _filteredProjects = new();
    private CancellationTokenSource? _searchCts;
    private List<string> _searchHistory = new();
    private bool _isPathSearchResults = false;
    private string? _activePathSearchRootDisplay;
    private bool _isResultsCollapsed = false;
    private bool _userManuallySizedResults = false;
    private bool _isTogglingViaHotkey = false;
    private DateTime _lastHotkeyPress = DateTime.MinValue;
    private System.Windows.Threading.DispatcherTimer? _deactivateTimer;
    private CancellationTokenSource? _ipcCts;
    private bool _isClosing = false;
    private bool _isDragging = false;
    private System.Windows.Point _dragStartPoint;
    private bool _isAutoArrangingWidgets = false;
    private const double WidgetSnapThreshold = 16;
    private readonly Dictionary<Window, Rect> _lastWidgetBounds = new();
    private readonly Dictionary<Window, Window> _verticalAttachments = new();
    private Helpers.DesktopFollower? _desktopFollower;
    private UpdateCheckService? _updateCheckService;
    private UpdateIndicatorManager? _updateIndicatorManager;
    private static readonly HashSet<string> PathSearchStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "and", "or", "the", "from", "for", "to", "of", "in", "on", "at", "by", "with"
    };
    private static readonly Dictionary<string, string[]> PathSearchAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["fault"] = new[] { "fault", "short", "short-circuit", "short circuit", "sc" },
        ["current"] = new[] { "current", "amp", "amps", "amperage", "kaic", "aic" },
        ["letter"] = new[] { "letter", "ltr", "memo", "correspondence" },
        ["fpl"] = new[] { "fpl", "fp&l", "florida power", "florida power and light", "utility" },
        ["utility"] = new[] { "utility", "fpl", "power" },
    };

    public bool IsClosing => _isClosing;
    public TaskService? TaskService => _taskService;
    public DocOpenService? DocService => _docService;
    public IProjectLaunchDataStore? LaunchDataStore => _launchDataStore;

    public SearchOverlay()
    {
        DebugLogger.Clear();
        DebugLogger.Log("SearchOverlay: Constructor starting");
        
        InitializeComponent();

        // Initialize services
        _scanner = new ProjectScanner();
        _searchService = new SearchService();
        _dataStore = new SqliteDataStore();
        _settings = new SettingsService();
        _timerService = new TimerService();
        _taskService = new TaskService(new Infrastructure.Data.TaskDataStore());
        _docService = new DocOpenService(new Infrastructure.Scanning.DocumentScanner());
        _smartProjectSearchService = new SmartProjectSearchService(new Infrastructure.Scanning.DocumentScanner(), _settings);

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

        // Reapply rounded corners on resize
        SizeChanged += (s, e) =>
        {
            // DO NOT apply window region rounding - causes clipping
            // WindowBlur.ApplyRoundedCorners(this, 12);
            UpdateRootClip(12);
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

            RegisterWidgetWindow(this);
            RegisterWidgetWindow(_widgetLauncher);

            // Register global hotkey (Ctrl+Alt+Space by default)
            try
            {
                _hotkey = new GlobalHotkey(this, (uint)modifiers, (uint)key);
                _hotkey.HotkeyPressed += OnHotkeyPressed;
                _hotkey.ShouldSuppressHotkey = () => ShouldSuppressHotkey(modifiers, key);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to register hotkey ({hotkeyLabel}). You can still open the search from the tray icon.\n\n{ex.Message}",
                    "DesktopHub",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning
                );
            }

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
                    this.Left = overlayLeft.Value;
                    this.Top = overlayTop.Value;
                    DebugLogger.Log($"Restored search overlay position: ({overlayLeft.Value}, {overlayTop.Value})");
                }
                
                var (launcherLeft, launcherTop) = _settings.GetWidgetLauncherPosition();
                if (launcherLeft.HasValue && launcherTop.HasValue && _widgetLauncher != null)
                {
                    _widgetLauncher.Left = launcherLeft.Value;
                    _widgetLauncher.Top = launcherTop.Value;
                    DebugLogger.Log($"Restored widget launcher position: ({launcherLeft.Value}, {launcherTop.Value})");
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

            var smartProjectSearchVisible = _settings.GetSmartProjectSearchWidgetVisible();
            if (smartProjectSearchVisible)
            {
                CreateSmartProjectSearchOverlay();
                DebugLogger.Log("Restored smart project search widget from previous session");
            }

            if (isLivingWidgetsMode)
            {
                NormalizeDocStartupGapIfNeeded();
                RefreshAttachmentMappings();
                TrackVisibleWindowBounds();
            }

            // Load projects in the background
            _ = LoadProjectsAsync();

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

            // Initialize periodic update checking
            InitializeUpdateCheckService();
            
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

    private async void CreateSmartProjectSearchOverlay(double? left = null, double? top = null)
    {
        if (_smartProjectSearchService == null)
            return;

        _smartProjectSearchOverlay = new SmartProjectSearchOverlay(_smartProjectSearchService, _settings);
        ApplyResponsiveWidgetWidth(_smartProjectSearchOverlay);
        RegisterWidgetWindow(_smartProjectSearchOverlay);
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        _smartProjectSearchOverlay.Topmost = !isLivingWidgetsMode;

        var (savedLeft, savedTop) = _settings.GetSmartProjectSearchWidgetPosition();
        _smartProjectSearchOverlay.Left = left ?? savedLeft ?? (this.Left + this.Width + GetConfiguredWidgetGap());
        _smartProjectSearchOverlay.Top = top ?? savedTop ?? (this.Top + 380);

        if (isLivingWidgetsMode)
            _smartProjectSearchOverlay.EnableDragging();

        _smartProjectSearchOverlay.Show();
        _smartProjectSearchOverlay.UpdateTransparency();
        _smartProjectSearchOverlay.Tag = "WasVisible";

        if (isLivingWidgetsMode)
        {
            ApplyLiveLayoutForWindow(_smartProjectSearchOverlay);
            RefreshAttachmentMappings();
            TrackVisibleWindowBounds();
        }

        if (isLivingWidgetsMode && _desktopFollower != null)
        {
            _desktopFollower.TrackWindow(_smartProjectSearchOverlay);
        }

        var smartRef = _smartProjectSearchOverlay;
        _updateIndicatorManager?.RegisterWidget("SmartProjectSearchOverlay", 8, _smartProjectSearchOverlay,
            visible => Dispatcher.Invoke(() => smartRef.SetUpdateIndicatorVisible(visible)));

        if (ResultsList.SelectedItem is ProjectViewModel vm)
        {
            try
            {
                await _smartProjectSearchOverlay.Widget.SetProjectAsync(vm.Path, $"{vm.FullNumber} {vm.Name}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"CreateSmartProjectSearchOverlay: Failed to prime selected project: {ex.Message}");
            }
        }

        DebugLogger.Log($"CreateSmartProjectSearchOverlay: Created at ({_smartProjectSearchOverlay.Left}, {_smartProjectSearchOverlay.Top})");
    }

    private void OnSmartProjectSearchRequested(object? sender, EventArgs e)
    {
        try
        {
            if (_smartProjectSearchOverlay == null)
            {
                CreateSmartProjectSearchOverlay();
            }
            else
            {
                if (_smartProjectSearchOverlay.Visibility == Visibility.Visible)
                {
                    _smartProjectSearchOverlay.Visibility = Visibility.Hidden;
                    _smartProjectSearchOverlay.Tag = null;
                    DebugLogger.Log("OnSmartProjectSearchRequested: Smart search overlay hidden");
                }
                else
                {
                    _smartProjectSearchOverlay.Visibility = Visibility.Visible;
                    _smartProjectSearchOverlay.Tag = "WasVisible";
                    DebugLogger.Log("OnSmartProjectSearchRequested: Smart search overlay shown");
                }
            }
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnSmartProjectSearchRequested: Error: {ex}");
            System.Windows.MessageBox.Show($"Error with smart project search overlay: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadAllProjects()
    {
        try
        {
            DebugLogger.Log($"LoadAllProjects: Starting, total projects: {_allProjects.Count}");
            ShowLoading(true);
            
            // Get selected filters
            var selectedYear = YearFilter.SelectedItem?.ToString();
            var selectedLocation = DriveLocationFilter.SelectedItem?.ToString();
            
            // Start with all projects, but filter by enabled drives first
            _filteredProjects = _allProjects.Where(p => 
            {
                if (p.DriveLocation == "Q") return _settings.GetQDriveEnabled();
                if (p.DriveLocation == "P") return _settings.GetPDriveEnabled();
                return false; // Unknown drive, exclude
            }).ToList();
            
            DebugLogger.Log($"LoadAllProjects: After drive filter: {_filteredProjects.Count} projects (Q enabled: {_settings.GetQDriveEnabled()}, P enabled: {_settings.GetPDriveEnabled()})");
            
            // Apply year filter
            if (selectedYear != "All Years" && !string.IsNullOrEmpty(selectedYear))
            {
                _filteredProjects = _filteredProjects.Where(p => p.Year == selectedYear).ToList();
            }
            
            // Apply drive location filter
            if (!string.IsNullOrEmpty(selectedLocation) && selectedLocation != "All Locations")
            {
                var driveFilter = selectedLocation.Contains("Florida") ? "Q" : "P";
                _filteredProjects = _filteredProjects.Where(p => p.DriveLocation == driveFilter).ToList();
            }
            
            DebugLogger.Log($"LoadAllProjects: Final filtered count: {_filteredProjects.Count} projects for year {selectedYear}, location {selectedLocation}");
            ResultsList.ItemsSource = _filteredProjects;
            _isPathSearchResults = false;
            _activePathSearchRootDisplay = null;
            UpdateResultsHeader();
            UpdateHistoryVisibility();
            ShowLoading(false);
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SearchOverlay: LoadAllProjects error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void UpdateResultsHeader()
    {
        var count = ResultsList.Items.Count;

        if (_isPathSearchResults)
        {
            if (!string.IsNullOrWhiteSpace(_activePathSearchRootDisplay))
            {
                ResultsHeaderText.Text = $"Path: {_activePathSearchRootDisplay} ({count})";
            }
            else
            {
                ResultsHeaderText.Text = $"Path Results ({count})";
            }
            return;
        }

        var selectedYear = YearFilter.SelectedItem?.ToString();
        
        if (selectedYear == "All Years" || string.IsNullOrEmpty(selectedYear))
        {
            ResultsHeaderText.Text = $"Projects ({count})";
        }
        else
        {
            ResultsHeaderText.Text = $"{selectedYear} Projects ({count})";
        }
    }

    private async Task LoadProjectsAsync()
    {
        try
        {
            ShowLoading(true);
            StatusText.Text = "Loading projects...";

            _allProjects = await _dataStore.GetAllProjectsAsync();

            // Populate year and drive location filters
            PopulateYearFilter();
            PopulateDriveLocationFilter();

            // Count only projects from enabled drives
            var enabledProjectsCount = _allProjects.Count(p => 
            {
                if (p.DriveLocation == "Q") return _settings.GetQDriveEnabled();
                if (p.DriveLocation == "P") return _settings.GetPDriveEnabled();
                return false;
            });

            StatusText.Text = $"{enabledProjectsCount} projects loaded";
            DebugLogger.Log($"LoadProjectsAsync: Total in DB: {_allProjects.Count}, From enabled drives: {enabledProjectsCount} (Q: {_settings.GetQDriveEnabled()}, P: {_settings.GetPDriveEnabled()})");
            ShowLoading(false);
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Error loading projects: {ex.Message}";
            ShowLoading(false);
        }
    }

    private static bool LooksLikePathInput(string query)
    {
        var trimmed = query.TrimStart();
        return trimmed.StartsWith("\\\\") ||
               Regex.IsMatch(trimmed, @"^[a-zA-Z]:[\\/]");
    }

    private static bool TryExtractPathAndFilter(string query, out string path, out string filter)
    {
        path = string.Empty;
        filter = string.Empty;

        var trimmed = query.Trim();
        if (trimmed.Length == 0 || !LooksLikePathInput(trimmed))
            return false;

        var delimiterIndex = trimmed.IndexOf("::", StringComparison.Ordinal);
        if (delimiterIndex >= 0)
        {
            path = trimmed[..delimiterIndex].Trim().Trim('"');
            filter = trimmed[(delimiterIndex + 2)..].Trim();
            return path.Length > 0;
        }

        if (trimmed.StartsWith('"'))
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote > 1)
            {
                path = trimmed[1..closingQuote].Trim();
                filter = trimmed[(closingQuote + 1)..].Trim();
                return path.Length > 0;
            }
        }

        var tokens = Regex.Split(trimmed, @"\s+")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();
        if (tokens.Length == 0)
            return false;

        var candidate = tokens[0];
        string? bestPath = System.IO.Directory.Exists(candidate) ? candidate : null;
        var bestTokenCount = bestPath != null ? 1 : 0;

        for (var i = 1; i < tokens.Length; i++)
        {
            candidate += " " + tokens[i];
            if (System.IO.Directory.Exists(candidate))
            {
                bestPath = candidate;
                bestTokenCount = i + 1;
            }
        }

        if (bestPath != null)
        {
            path = bestPath;
            filter = string.Join(" ", tokens.Skip(bestTokenCount));
            return true;
        }

        path = trimmed.Trim('"');
        filter = string.Empty;
        return true;
    }

    private static bool IsHiddenOrSystem(string path)
    {
        try
        {
            var attr = System.IO.File.GetAttributes(path);
            return (attr & System.IO.FileAttributes.Hidden) != 0 ||
                   (attr & System.IO.FileAttributes.System) != 0;
        }
        catch
        {
            return true;
        }
    }

    private static IEnumerable<(string Path, bool IsDirectory)> EnumeratePathEntries(
        string rootPath,
        bool includeDirectories,
        bool includeFiles,
        CancellationToken token)
    {
        var stack = new Stack<string>();
        stack.Push(rootPath);

        while (stack.Count > 0)
        {
            if (token.IsCancellationRequested)
                yield break;

            var current = stack.Pop();
            IEnumerable<string> subDirectories;
            try
            {
                subDirectories = System.IO.Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var dir in subDirectories)
            {
                if (token.IsCancellationRequested)
                    yield break;

                if (includeDirectories)
                    yield return (dir, true);

                stack.Push(dir);
            }

            if (!includeFiles)
                continue;

            IEnumerable<string> files;
            try
            {
                files = System.IO.Directory.EnumerateFiles(current);
            }
            catch
            {
                continue;
            }

            foreach (var file in files)
            {
                if (token.IsCancellationRequested)
                    yield break;
                yield return (file, false);
            }
        }
    }

    private static bool IsRegexQuery(string query, out string pattern)
    {
        pattern = string.Empty;

        if (query.StartsWith("re:", StringComparison.OrdinalIgnoreCase))
        {
            pattern = query[3..].Trim();
            return pattern.Length > 0;
        }

        if (query.Length >= 3 && query[0] == '/' && query.LastIndexOf('/') > 0)
        {
            var lastSlash = query.LastIndexOf('/');
            pattern = query[1..lastSlash].Trim();
            return pattern.Length > 0;
        }

        return false;
    }

    private static List<string> ExtractPathSearchTerms(string query)
    {
        var terms = new List<string>();
        foreach (Match match in Regex.Matches(query, "\"([^\"]+)\"|(\\S+)"))
        {
            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            var normalized = value.Trim().ToLowerInvariant();
            if (normalized.Length == 0 || PathSearchStopWords.Contains(normalized))
                continue;

            terms.Add(normalized);
        }

        return terms;
    }

    private static IEnumerable<string> ExpandPathSearchAliases(string term)
    {
        if (PathSearchAliases.TryGetValue(term, out var aliases) && aliases.Length > 0)
        {
            return aliases
                .Append(term)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(a => a.ToLowerInvariant());
        }

        if (term.EndsWith('s') && term.Length > 3)
        {
            return new[] { term, term[..^1] };
        }

        return new[] { term };
    }

    private static double ScorePathEntry(string fullPath, string normalizedQuery, IReadOnlyList<string> terms, Regex? regex)
    {
        if (regex != null)
            return regex.IsMatch(fullPath) ? 10.0 : 0.0;

        var normalizedPath = fullPath.ToLowerInvariant();
        var fileName = System.IO.Path.GetFileName(fullPath).ToLowerInvariant();

        if (terms.Count == 0)
        {
            return normalizedPath.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
        }

        var minimumHits = terms.Count <= 2 ? terms.Count : terms.Count - 1;
        var hits = 0;
        var score = 0.0;

        foreach (var term in terms)
        {
            var best = 0.0;
            foreach (var alias in ExpandPathSearchAliases(term))
            {
                if (fileName.StartsWith(alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 4.0);
                if (fileName.Contains(alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 3.0);
                if (normalizedPath.Contains(alias, StringComparison.OrdinalIgnoreCase)) best = Math.Max(best, 1.5);
            }

            if (best > 0)
            {
                hits++;
                score += best;
            }
        }

        var hasPhrase = normalizedPath.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase);
        if (hasPhrase)
            score += 4.0;

        if (hits < minimumHits && !hasPhrase)
            return 0.0;

        return score;
    }

    private static string? BuildPathMatchStatus(string rootPath, string fullPath)
    {
        try
        {
            var relative = System.IO.Path.GetRelativePath(rootPath, fullPath);
            var parent = System.IO.Path.GetDirectoryName(relative);
            if (string.IsNullOrWhiteSpace(parent) || parent == ".")
                return null;
            return parent;
        }
        catch
        {
            return null;
        }
    }

    private void PopulateYearFilter()
    {
        var years = _allProjects
            .Select(p => p.Year)
            .Distinct()
            .OrderByDescending(y => y)
            .ToList();

        years.Insert(0, "All Years");

        YearFilter.ItemsSource = years;
        YearFilter.SelectedIndex = 0;
    }

    private void PopulateDriveLocationFilter()
    {
        bool qEnabled = _settings.GetQDriveEnabled();
        bool pEnabled = _settings.GetPDriveEnabled();
        int enabledCount = (qEnabled ? 1 : 0) + (pEnabled ? 1 : 0);

        var locations = new List<string>();

        if (enabledCount <= 1)
        {
            // Single drive — show only that drive name, no dropdown interaction
            if (qEnabled) locations.Add("Florida (Q:)");
            else if (pEnabled) locations.Add("Connecticut (P:)");
            else locations.Add("No Locations");

            DriveLocationFilter.ItemsSource = locations;
            DriveLocationFilter.SelectedIndex = 0;
            DriveLocationFilter.IsHitTestVisible = false;
            DriveLocationFilter.Cursor = System.Windows.Input.Cursors.Arrow;
        }
        else
        {
            // Multiple drives — show "All Locations" plus each drive
            locations.Add("All Locations");
            if (qEnabled) locations.Add("Florida (Q:)");
            if (pEnabled) locations.Add("Connecticut (P:)");

            DriveLocationFilter.ItemsSource = locations;
            DriveLocationFilter.SelectedIndex = 0;
            DriveLocationFilter.IsHitTestVisible = true;
            DriveLocationFilter.Cursor = System.Windows.Input.Cursors.Hand;
        }
    }

    private async Task BackgroundScanAsync()
    {
        try
        {
            // Check if we need to scan (based on last scan time)
            var lastScan = await _dataStore.GetLastScanTimeAsync();
            var scanInterval = TimeSpan.FromMinutes(_settings.GetScanIntervalMinutes());

            if (lastScan == null || DateTime.UtcNow - lastScan.Value > scanInterval)
            {
                var allScannedProjects = new List<Project>();

                // Scan Q: drive (Florida) - only if enabled
                if (_settings.GetQDriveEnabled())
                {
                    await Dispatcher.InvokeAsync(() => StatusText.Text = "Scanning Q: drive (Florida)...");
                    var qDrivePath = _settings.GetQDrivePath();
                    if (Directory.Exists(qDrivePath))
                    {
                        try
                        {
                            var qProjects = await _scanner.ScanProjectsAsync(qDrivePath, "Q", CancellationToken.None);
                            allScannedProjects.AddRange(qProjects);
                            DebugLogger.Log($"Q: drive scan completed: {qProjects.Count} projects found");
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"Q: drive scan error: {ex.Message}");
                        }
                    }
                }
                else
                {
                    DebugLogger.Log("Q: drive scanning disabled - skipping");
                }

                // Scan P: drive (Connecticut) - only if enabled
                if (_settings.GetPDriveEnabled())
                {
                    await Dispatcher.InvokeAsync(() => StatusText.Text = "Scanning P: drive (Connecticut)...");
                    var pDrivePath = _settings.GetPDrivePath();
                    if (Directory.Exists(pDrivePath))
                    {
                        try
                        {
                            var pProjects = await _scanner.ScanProjectsAsync(pDrivePath, "P", CancellationToken.None);
                            allScannedProjects.AddRange(pProjects);
                            DebugLogger.Log($"P: drive scan completed: {pProjects.Count} projects found");
                        }
                        catch (Exception ex)
                        {
                            DebugLogger.Log($"P: drive scan error: {ex.Message}");
                        }
                    }
                }
                else
                {
                    DebugLogger.Log("P: drive scanning disabled - skipping");
                }

                // Update database with all scanned projects (Q and P drives are separate)
                await _dataStore.BatchUpsertProjectsAsync(allScannedProjects);
                await _dataStore.UpdateLastScanTimeAsync(DateTime.UtcNow);

                // Reload projects
                await Dispatcher.InvokeAsync(async () => await LoadProjectsAsync());
            }
        }
        catch (Exception ex)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                StatusText.Text = $"Scan error: {ex.Message}";
            });
        }
    }

    private async void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // Cancel previous search
        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        var query = SearchBox.Text;

        if (string.IsNullOrWhiteSpace(query))
        {
            // Show all projects when search is blank
            _isPathSearchResults = false;
            _activePathSearchRootDisplay = null;
            LoadAllProjects();
            UpdateHistoryVisibility();

            // Clear Doc Quick Open widget when search is cleared
            if (_docOverlay?.Widget != null)
            {
                try { await _docOverlay.Widget.SetProjectAsync("", null); }
                catch { }
            }
            
            // Auto-collapse when search cleared (only if user hasn't manually toggled)
            if (!_userManuallySizedResults && !_isResultsCollapsed)
            {
                _isResultsCollapsed = true;
                ResultsContainer.Visibility = Visibility.Collapsed;
                CollapseIconRotation.Angle = -90;
                this.Height = 140;
            }
            return;
        }

        // Update history visibility (hide horizontal pills when typing)
        UpdateHistoryVisibility();

        // Detect path-like input and perform directory listing/path-scoped search if enabled
        if (LooksLikePathInput(query))
        {
            if (_settings.GetPathSearchEnabled())
            {
                await PerformPathSearch(query, token);
            }
            else
            {
                _isPathSearchResults = false;
                _activePathSearchRootDisplay = null;
                ResultsList.ItemsSource = null;
                StatusText.Text = "Path detected — enable Path Search in General settings (supports C:\\Path :: terms)";
                UpdateResultsHeader();
                ShowLoading(false);
            }
            return;
        }

        try
        {
            // Debounce search (wait 250ms for slower PCs)
            await Task.Delay(250, token);

            if (token.IsCancellationRequested)
                return;

            ShowLoading(true);

            _isPathSearchResults = false;
            _activePathSearchRootDisplay = null;

            // Apply filters before searching
            var selectedYear = YearFilter.SelectedItem?.ToString();
            var selectedLocation = DriveLocationFilter.SelectedItem?.ToString();
            
            // Start with all projects, but filter by enabled drives first
            var projectsToSearch = _allProjects.Where(p => 
            {
                if (p.DriveLocation == "Q") return _settings.GetQDriveEnabled();
                if (p.DriveLocation == "P") return _settings.GetPDriveEnabled();
                return false; // Unknown drive, exclude
            }).ToList();
            
            // Apply year filter
            if (selectedYear != "All Years" && !string.IsNullOrEmpty(selectedYear))
            {
                projectsToSearch = projectsToSearch.Where(p => p.Year == selectedYear).ToList();
            }
            
            // Apply drive location filter
            if (!string.IsNullOrEmpty(selectedLocation) && selectedLocation != "All Locations")
            {
                var driveFilter = selectedLocation.Contains("Florida") ? "Q" : "P";
                projectsToSearch = projectsToSearch.Where(p => p.DriveLocation == driveFilter).ToList();
            }

            // Search filtered projects
            var results = await _searchService.SearchAsync(query, projectsToSearch);

            if (token.IsCancellationRequested)
                return;

            // Update UI - batch operations to reduce overhead
            var projectViewModels = results.Select(r => new ProjectViewModel(r.Project)).ToList();
            ResultsList.ItemsSource = projectViewModels;

            if (results.Any())
            {
                ResultsList.SelectedIndex = 0;
                StatusText.Text = $"{results.Count} result{(results.Count == 1 ? "" : "s")} found";
                
                // Auto-expand when search has results (only if user hasn't manually toggled)
                if (!_userManuallySizedResults && _isResultsCollapsed)
                {
                    _isResultsCollapsed = false;
                    ResultsContainer.Visibility = Visibility.Visible;
                    CollapseIconRotation.Angle = 0;
                    this.Height = 500;
                }
                
                // History tracking removed - only track on actual project launch
            }
            else
            {
                StatusText.Text = "No results found";
            }

            UpdateResultsHeader();
            UpdateHistoryVisibility();
            ShowLoading(false);
        }
        catch (OperationCanceledException)
        {
            // Search was cancelled, ignore
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"SearchBox_TextChanged: Search error: {ex.Message}\n{ex.StackTrace}");
            StatusText.Text = $"Search error: {ex.Message}";
            ShowLoading(false);
        }
    }

    private List<Project> GetFilteredProjectsByYear()
    {
        var selectedYear = YearFilter.SelectedItem as string;
        if (string.IsNullOrEmpty(selectedYear) || selectedYear == "All Years")
        {
            return _allProjects;
        }

        return _allProjects.Where(p => p.Year == selectedYear).ToList();
    }

    private void AddToSearchHistory(string query)
    {
        // Remove if already exists
        _searchHistory.Remove(query);
        
        // Add to front
        _searchHistory.Insert(0, query);
        
        // Keep only last 25 to prevent excessive memory usage
        if (_searchHistory.Count > 25)
        {
            _searchHistory = _searchHistory.Take(25).ToList();
        }
    }

    private void YearFilter_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Reload projects with new year filter
        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            // Re-trigger search with new filter
            SearchBox_TextChanged(SearchBox, new TextChangedEventArgs(e.RoutedEvent, System.Windows.Controls.UndoAction.None));
        }
        else
        {
            // Reload all projects with new year filter
            LoadAllProjects();
        }
    }

    private void DriveLocationFilter_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        // Reload projects with new drive location filter
        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            // Re-trigger search with new filter
            SearchBox_TextChanged(SearchBox, new TextChangedEventArgs(e.RoutedEvent, System.Windows.Controls.UndoAction.None));
        }
        else
        {
            // Reload all projects with new drive location filter
            LoadAllProjects();
        }
    }

    private void WidgetLauncherToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_widgetLauncher != null)
        {
            if (_widgetLauncher.Visibility == Visibility.Visible)
            {
                _widgetLauncher.Visibility = Visibility.Hidden;
                DebugLogger.Log("Widget launcher hidden via toggle button");
            }
            else
            {
                _widgetLauncher.Visibility = Visibility.Visible;
                DebugLogger.Log("Widget launcher shown via toggle button");
            }
            
            // Save visibility state if in Living Widgets Mode
            var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
            if (isLivingWidgetsMode)
            {
                _settings.SetWidgetLauncherVisible(_widgetLauncher.Visibility == Visibility.Visible);
                _ = _settings.SaveAsync();
                DebugLogger.Log($"Saved widget launcher visibility state: {_widgetLauncher.Visibility == Visibility.Visible}");
            }
        }
    }
    
    private void HistoryItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Border border && border.DataContext is string query)
        {
            SearchBox.Text = query;
        }
    }

    private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        DebugLogger.LogHeader("SearchBox GotFocus");
        DebugLogger.LogVariable("Window.IsActive", this.IsActive);
        DebugLogger.LogVariable("Window.IsFocused", this.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocusWithin", SearchBox.IsKeyboardFocusWithin);
    }

    private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
    {
        DebugLogger.LogHeader("SearchBox LostFocus");
        DebugLogger.LogVariable("Window.IsActive", this.IsActive);
        DebugLogger.LogVariable("Window.IsFocused", this.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
    }

    private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        DebugLogger.LogHeader("SearchBox PreviewKeyDown");
        DebugLogger.LogVariable("Key", e.Key);
        DebugLogger.LogVariable("SystemKey", e.SystemKey);
        DebugLogger.LogVariable("KeyStates", e.KeyStates);
        DebugLogger.LogVariable("IsDown", e.IsDown);
        DebugLogger.LogVariable("IsUp", e.IsUp);
        DebugLogger.LogVariable("IsRepeat", e.IsRepeat);
        DebugLogger.LogVariable("Keyboard.Modifiers", System.Windows.Input.Keyboard.Modifiers);
        DebugLogger.LogVariable("SearchBox.Text (before)", SearchBox.Text);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        DebugLogger.LogHeader("Window KeyDown");
        DebugLogger.LogVariable("Key", e.Key);
        DebugLogger.LogVariable("Handled", e.Handled);
        DebugLogger.LogVariable("Source", e.Source?.GetType().Name ?? "<null>");
        
        // Check if close shortcut was pressed
        var (closeModifiers, closeKey) = _settings.GetCloseShortcut();
        var currentModifiers = 0;
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) != 0)
            currentModifiers |= (int)GlobalHotkey.MOD_CONTROL;
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0)
            currentModifiers |= (int)GlobalHotkey.MOD_ALT;
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0)
            currentModifiers |= (int)GlobalHotkey.MOD_SHIFT;
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Windows) != 0)
            currentModifiers |= (int)GlobalHotkey.MOD_WIN;
        
        var currentKey = System.Windows.Input.KeyInterop.VirtualKeyFromKey(e.Key);
        
        if (currentModifiers == closeModifiers && currentKey == closeKey)
        {
            DebugLogger.Log($"Window_KeyDown: Close shortcut pressed -> Hiding overlay");
            HideOverlay();
            e.Handled = true;
            return;
        }
        
        switch (e.Key)
        {
            case System.Windows.Input.Key.Enter:
                DebugLogger.Log("Window_KeyDown: Enter pressed -> Opening selected project");
                OpenSelectedProject();
                e.Handled = true;
                break;

            case System.Windows.Input.Key.Down:
                if (ResultsList.Items.Count > 0)
                {
                    var newIndex = Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1);
                    DebugLogger.Log($"Window_KeyDown: Down pressed -> Selecting item {newIndex}");
                    ResultsList.SelectedIndex = newIndex;
                    ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                }
                e.Handled = true;
                break;

            case System.Windows.Input.Key.Up:
                if (ResultsList.Items.Count > 0)
                {
                    var newIndex = Math.Max(ResultsList.SelectedIndex - 1, 0);
                    DebugLogger.Log($"Window_KeyDown: Up pressed -> Selecting item {newIndex}");
                    ResultsList.SelectedIndex = newIndex;
                    ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                }
                e.Handled = true;
                break;

            case System.Windows.Input.Key.C when System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control:
                DebugLogger.Log("Window_KeyDown: Ctrl+C pressed -> Copying path");
                CopySelectedProjectPath();
                e.Handled = true;
                break;
        }
    }

    private async void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Feed selected project to Doc Quick Open widget
        if (ResultsList.SelectedItem is ProjectViewModel vm && _docOverlay?.Widget != null)
        {
            try
            {
                await _docOverlay.Widget.SetProjectAsync(vm.Path, $"{vm.FullNumber} {vm.Name}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"ResultsList_SelectionChanged: Error feeding project to doc widget: {ex.Message}");
            }
        }

        if (ResultsList.SelectedItem is ProjectViewModel smartVm && _smartProjectSearchService != null)
        {
            try
            {
                await _smartProjectSearchService.SetProjectAsync(smartVm.Path, $"{smartVm.FullNumber} {smartVm.Name}");
            }
            catch (Exception ex)
            {
                DebugLogger.Log($"ResultsList_SelectionChanged: Error feeding project to smart search service: {ex.Message}");
            }
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        OpenSelectedProject();
    }

    private async void OpenSelectedProject()
    {
        string? itemPath = null;
        string? itemNumber = null;
        string? itemName = null;
        bool isDirectoryResult = true;

        if (ResultsList.SelectedItem is ProjectViewModel vm)
        {
            itemPath = vm.Path;
            itemNumber = vm.FullNumber;
            itemName = vm.Name;
        }
        else if (ResultsList.SelectedItem is PathSearchResultViewModel psvm)
        {
            itemPath = psvm.Path;
            isDirectoryResult = psvm.IsDirectory;
        }

        if (itemPath == null) return;

        try
        {
            // Track search query when project is actually opened
            var query = SearchBox.Text;
            if (!string.IsNullOrWhiteSpace(query))
            {
                AddToSearchHistory(query);
            }
            
            if (isDirectoryResult || Directory.Exists(itemPath))
            {
                Process.Start("explorer.exe", itemPath);
            }
            else
            {
                Process.Start(new ProcessStartInfo(itemPath) { UseShellExecute = true });
            }

            // Record the launch for frequency tracking (only for project results)
            if (_launchDataStore != null && itemNumber != null && itemName != null)
            {
                try
                {
                    await _launchDataStore.RecordLaunchAsync(itemPath, itemNumber, itemName);
                    DebugLogger.Log($"OpenSelectedProject: Recorded launch for {itemNumber}");

                    // Refresh the frequent projects widget if it's open
                    if (_frequentProjectsOverlay?.Widget != null)
                    {
                        await _frequentProjectsOverlay.Widget.RefreshAsync();
                    }
                }
                catch (Exception trackEx)
                {
                    DebugLogger.Log($"OpenSelectedProject: Failed to record launch: {trackEx.Message}");
                }
            }
            
            // Only hide overlay if NOT in Living Widgets Mode (live widget mode keeps it open)
            var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
            if (!isLivingWidgetsMode)
            {
                HideOverlay();
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Failed to open: {ex.Message}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error
            );
        }
    }

    private void CopySelectedProjectPath()
    {
        string? path = null;
        if (ResultsList.SelectedItem is ProjectViewModel vm)
            path = vm.Path;
        else if (ResultsList.SelectedItem is PathSearchResultViewModel psvm)
            path = psvm.Path;

        if (path != null)
        {
            try
            {
                System.Windows.Clipboard.SetText(path);
                StatusText.Text = "Path copied to clipboard";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to copy: {ex.Message}";
            }
        }
    }

    private void CopyPathBorder_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        string? path = null;
        if (sender is Border border && border.DataContext is ProjectViewModel vm)
            path = vm.Path;
        else if (sender is Border border2 && border2.DataContext is PathSearchResultViewModel psvm)
            path = psvm.Path;

        if (path != null)
        {
            e.Handled = true;
            try
            {
                System.Windows.Clipboard.SetText(path);
                StatusText.Text = "Path copied to clipboard";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to copy: {ex.Message}";
            }
        }
    }

    private void CopyPathBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0xFF, 0xFF, 0xFF));
            border.Opacity = 1.0;
        }
    }

    private void CopyPathBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is Border border)
        {
            border.Background = System.Windows.Media.Brushes.Transparent;
            border.Opacity = 0.6;
        }
    }

    private void ResultsList_PreviewMouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Find the ListBoxItem that was right-clicked
        // Note: OriginalSource can be a Run (ContentElement), not a Visual,
        // so we must use LogicalTreeHelper for non-Visual elements first.
        var element = e.OriginalSource as DependencyObject;
        while (element != null && element is not System.Windows.Controls.ListBoxItem)
        {
            element = element is System.Windows.Media.Visual
                ? System.Windows.Media.VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        if (element is System.Windows.Controls.ListBoxItem item)
        {
            string? itemPath = null;
            string? itemProjectNumber = null;
            string? itemProjectName = null;
            bool isProjectResult = false;
            if (item.DataContext is ProjectViewModel vm)
            {
                ResultsList.SelectedItem = vm;
                itemPath = vm.Path;
                itemProjectNumber = vm.FullNumber;
                itemProjectName = vm.Name;
                isProjectResult = true;
            }
            else if (item.DataContext is PathSearchResultViewModel psvm)
            {
                ResultsList.SelectedItem = psvm;
                itemPath = psvm.Path;
                itemProjectNumber = psvm.FullNumber;
                itemProjectName = psvm.Name;
            }

            if (itemPath != null)
            {
                var capturedPath = itemPath;
                var capturedProjectNumber = itemProjectNumber?.Trim() ?? string.Empty;
                var capturedProjectName = itemProjectName?.Trim() ?? string.Empty;
                var capturedNumberAndName = string.Join(" ", new[] { capturedProjectNumber, capturedProjectName }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));
                var isDirectoryPath = Directory.Exists(capturedPath);
                var isFilePath = !isDirectoryPath && File.Exists(capturedPath);
                var fileExtension = Path.GetExtension(capturedPath)?.TrimStart('.').ToUpperInvariant();
                var openFileHeader = string.IsNullOrWhiteSpace(fileExtension) ? "Open File" : $"Open {fileExtension}";

                void CopyText(string text, string statusMessage)
                {
                    try
                    {
                        System.Windows.Clipboard.SetText(text);
                        StatusText.Text = statusMessage;
                    }
                    catch { }
                }

                var menu = CreateDarkContextMenu();

                if (isProjectResult)
                {
                    var copyNumberItem = new MenuItem { Header = "Copy Project Number", IsEnabled = !string.IsNullOrWhiteSpace(capturedProjectNumber) };
                    copyNumberItem.Click += (s, args) => CopyText(capturedProjectNumber, "Project number copied to clipboard");
                    menu.Items.Add(copyNumberItem);

                    var copyNameItem = new MenuItem { Header = "Copy Project Name", IsEnabled = !string.IsNullOrWhiteSpace(capturedProjectName) };
                    copyNameItem.Click += (s, args) => CopyText(capturedProjectName, "Project name copied to clipboard");
                    menu.Items.Add(copyNameItem);

                    var copyNumberAndNameItem = new MenuItem { Header = "Copy Number + Name", IsEnabled = !string.IsNullOrWhiteSpace(capturedNumberAndName) };
                    copyNumberAndNameItem.Click += (s, args) => CopyText(capturedNumberAndName, "Project number + name copied to clipboard");
                    menu.Items.Add(copyNumberAndNameItem);

                    menu.Items.Add(new Separator());

                    var openItem = new MenuItem { Header = "Open Folder" };
                    openItem.Click += (s, args) =>
                    {
                        try { Process.Start("explorer.exe", capturedPath); }
                        catch { }
                    };
                    menu.Items.Add(openItem);

                    var copyItem = new MenuItem { Header = "Copy Path" };
                    copyItem.Click += (s, args) => CopyText(capturedPath, "Path copied to clipboard");
                    menu.Items.Add(copyItem);
                }
                else if (isFilePath)
                {
                    var openFileItem = new MenuItem { Header = openFileHeader };
                    openFileItem.Click += (s, args) =>
                    {
                        try { Process.Start(new ProcessStartInfo(capturedPath) { UseShellExecute = true }); }
                        catch { }
                    };
                    menu.Items.Add(openFileItem);

                    var copyItem = new MenuItem { Header = "Copy Path" };
                    copyItem.Click += (s, args) => CopyText(capturedPath, "Path copied to clipboard");
                    menu.Items.Add(copyItem);

                    var openParentItem = new MenuItem { Header = "Open Parent Directory" };
                    openParentItem.Click += (s, args) =>
                    {
                        try { Process.Start("explorer.exe", $"/select,\"{capturedPath}\""); }
                        catch { }
                    };
                    menu.Items.Add(openParentItem);
                }
                else
                {
                    var openItem = new MenuItem { Header = "Open Folder" };
                    openItem.Click += (s, args) =>
                    {
                        try { Process.Start("explorer.exe", capturedPath); }
                        catch { }
                    };
                    menu.Items.Add(openItem);

                    var copyItem = new MenuItem { Header = "Copy Path" };
                    copyItem.Click += (s, args) => CopyText(capturedPath, "Path copied to clipboard");
                    menu.Items.Add(copyItem);
                }

                ResultsList.ContextMenu = menu;
                menu.IsOpen = true;
                e.Handled = true;
            }
        }
    }

    private static ContextMenu CreateDarkContextMenu()
    {
        var menuBg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1E, 0x1E, 0x1E));
        var menuBorder = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        var itemFg = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE0, 0xE0, 0xE0));
        var hoverBg = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x30, 0x4F, 0xC3, 0xF7));
        var transparentBrush = System.Windows.Media.Brushes.Transparent;

        // Build a MenuItem ControlTemplate that fully replaces WPF default chrome
        var itemTemplate = new ControlTemplate(typeof(MenuItem));
        var borderFactory = new FrameworkElementFactory(typeof(Border));
        borderFactory.Name = "Bd";
        borderFactory.SetValue(Border.BackgroundProperty, transparentBrush);
        borderFactory.SetValue(Border.PaddingProperty, new Thickness(10, 6, 10, 6));
        borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        borderFactory.SetValue(Border.MarginProperty, new Thickness(2, 1, 2, 1));

        var contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
        contentFactory.SetValue(ContentPresenter.ContentSourceProperty, "Header");
        contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
        borderFactory.AppendChild(contentFactory);
        itemTemplate.VisualTree = borderFactory;

        // Hover trigger
        var hoverTrigger = new Trigger { Property = MenuItem.IsHighlightedProperty, Value = true };
        hoverTrigger.Setters.Add(new Setter(Border.BackgroundProperty, hoverBg, "Bd"));
        itemTemplate.Triggers.Add(hoverTrigger);

        // MenuItem style using the custom template
        var itemStyle = new Style(typeof(MenuItem));
        itemStyle.Setters.Add(new Setter(MenuItem.ForegroundProperty, itemFg));
        itemStyle.Setters.Add(new Setter(MenuItem.TemplateProperty, itemTemplate));
        itemStyle.Setters.Add(new Setter(MenuItem.CursorProperty, System.Windows.Input.Cursors.Hand));

        // ContextMenu with custom template to remove system chrome
        var contextMenuTemplate = new ControlTemplate(typeof(ContextMenu));
        var menuBorderFactory = new FrameworkElementFactory(typeof(Border));
        menuBorderFactory.SetValue(Border.BackgroundProperty, menuBg);
        menuBorderFactory.SetValue(Border.BorderBrushProperty, menuBorder);
        menuBorderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        menuBorderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        menuBorderFactory.SetValue(Border.PaddingProperty, new Thickness(2, 4, 2, 4));

        var shadowEffect = new System.Windows.Media.Effects.DropShadowEffect
        {
            BlurRadius = 12,
            ShadowDepth = 2,
            Opacity = 0.5,
            Color = System.Windows.Media.Color.FromRgb(0, 0, 0)
        };
        menuBorderFactory.SetValue(Border.EffectProperty, shadowEffect);

        var itemsPresenter = new FrameworkElementFactory(typeof(ItemsPresenter));
        menuBorderFactory.AppendChild(itemsPresenter);
        contextMenuTemplate.VisualTree = menuBorderFactory;

        var menu = new ContextMenu
        {
            Template = contextMenuTemplate,
            HasDropShadow = false
        };
        menu.Resources[typeof(MenuItem)] = itemStyle;

        return menu;
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        DebugLogger.LogSeparator("WINDOW DEACTIVATED");
        DebugLogger.LogHeader("Window Lost Focus");
        DebugLogger.LogVariable("Window.IsActive", this.IsActive);
        DebugLogger.LogVariable("Window.IsFocused", this.IsFocused);
        DebugLogger.LogVariable("Window.Visibility", this.Visibility);
        DebugLogger.LogVariable("_isTogglingViaHotkey", _isTogglingViaHotkey);
        DebugLogger.LogVariable("SearchBox.IsFocused", SearchBox.IsFocused);
        DebugLogger.LogVariable("SearchBox.IsKeyboardFocused", SearchBox.IsKeyboardFocused);
        
        // Don't auto-hide if Living Widgets Mode is enabled
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        DebugLogger.LogVariable("LivingWidgetsMode", isLivingWidgetsMode);
        if (isLivingWidgetsMode)
        {
            DebugLogger.Log("Window_Deactivated: IGNORING - Living Widgets Mode is enabled");
            return;
        }
        
        // Don't auto-hide if we're in the middle of a hotkey toggle
        if (_isTogglingViaHotkey)
        {
            DebugLogger.Log("Window_Deactivated: IGNORING - hotkey toggle in progress");
            return;
        }
        
        // Don't process if window is closing or not loaded
        if (!IsLoaded || IsClosing)
        {
            DebugLogger.Log("Window_Deactivated: IGNORING - window is closing or not loaded");
            return;
        }
        
        // Use a delayed auto-hide to avoid race conditions with hotkey toggles
        // Cancel any existing timer
        if (_deactivateTimer != null)
        {
            DebugLogger.Log("Window_Deactivated: Stopping existing deactivate timer");
            _deactivateTimer.Stop();
        }
        
        // Create new timer with 150ms delay
        DebugLogger.Log("Window_Deactivated: Starting 150ms delay timer before auto-hide");
        _deactivateTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(150)
        };
        _deactivateTimer.Tick += (s, args) =>
        {
            DebugLogger.LogHeader("Deactivate Timer Tick (after 150ms)");
            _deactivateTimer?.Stop();
            
            // Additional safety check - don't process if window is closing or not loaded
            if (!IsLoaded || _isClosing)
            {
                DebugLogger.Log("Deactivate Timer: IGNORING - window is closing or not loaded");
                return;
            }
            
            DebugLogger.LogVariable("Window.IsActive (now)", this.IsActive);
            DebugLogger.LogVariable("_isTogglingViaHotkey (now)", _isTogglingViaHotkey);
            DebugLogger.LogVariable("Window.Visibility (now)", this.Visibility);
            DebugLogger.LogVariable("_isClosing", _isClosing);
            
            // Check if widget launcher or timer overlay is active
            var isWidgetLauncherActive = _widgetLauncher != null && _widgetLauncher.IsActive;
            var isTimerOverlayActive = _timerOverlay != null && _timerOverlay.IsActive;
            
            DebugLogger.LogVariable("WidgetLauncher.IsActive", isWidgetLauncherActive);
            DebugLogger.LogVariable("TimerOverlay.IsActive", isTimerOverlayActive);
            
            // Double-check we're still deactivated and not toggling
            // Don't auto-hide if widget launcher or timer overlay is active
            if (!this.IsActive && !_isTogglingViaHotkey && this.Visibility == Visibility.Visible 
                && !isWidgetLauncherActive && !isTimerOverlayActive && !_isClosing)
            {
                DebugLogger.Log("Window_Deactivated: Conditions met -> AUTO-HIDING overlay");
                HideOverlay();
            }
            else
            {
                DebugLogger.Log($"Window_Deactivated: SKIPPING auto-hide:");
                DebugLogger.LogVariable("  Reason: IsActive", this.IsActive);
                DebugLogger.LogVariable("  Reason: _isTogglingViaHotkey", _isTogglingViaHotkey);
                DebugLogger.LogVariable("  Reason: Visibility", this.Visibility);
                DebugLogger.LogVariable("  Reason: _isClosing", _isClosing);
            }
        };
        _deactivateTimer.Start();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        DebugLogger.Log("Window_Closing: Setting closing state");
        _isClosing = true;
        
        // Stop desktop follower
        StopDesktopFollower();
        
        // Save Living Widgets Mode-specific positions (search overlay, widget launcher)
        var isLivingWidgetsMode = _settings.GetLivingWidgetsMode();
        if (isLivingWidgetsMode)
        {
            _settings.SetSearchOverlayPosition(this.Left, this.Top);
            _settings.SetSearchOverlayVisible(this.Visibility == Visibility.Visible);
            
            if (_widgetLauncher != null)
            {
                _settings.SetWidgetLauncherPosition(_widgetLauncher.Left, _widgetLauncher.Top);
                // Save widget launcher visibility state
                _settings.SetWidgetLauncherVisible(_widgetLauncher.Visibility == Visibility.Visible);
            }
        }
        
        // Always save individual widget positions and visibility (regardless of Living Widgets Mode)
        if (_timerOverlay != null)
        {
            _settings.SetTimerWidgetPosition(_timerOverlay.Left, _timerOverlay.Top);
            _settings.SetTimerWidgetVisible(_timerOverlay.Visibility == Visibility.Visible);
            DebugLogger.Log($"Window_Closing: Saved timer position: ({_timerOverlay.Left}, {_timerOverlay.Top}), visible: {_timerOverlay.Visibility == Visibility.Visible}");
        }
        else
        {
            _settings.SetTimerWidgetVisible(false);
        }
        
        if (_quickTasksOverlay != null)
        {
            _settings.SetQuickTasksWidgetPosition(_quickTasksOverlay.Left, _quickTasksOverlay.Top);
            _settings.SetQuickTasksWidgetVisible(_quickTasksOverlay.Visibility == Visibility.Visible);
            DebugLogger.Log($"Window_Closing: Saved quick tasks position: ({_quickTasksOverlay.Left}, {_quickTasksOverlay.Top}), visible: {_quickTasksOverlay.Visibility == Visibility.Visible}");
        }
        else
        {
            _settings.SetQuickTasksWidgetVisible(false);
        }
        
        if (_docOverlay != null)
        {
            _settings.SetDocWidgetPosition(_docOverlay.Left, _docOverlay.Top);
            _settings.SetDocWidgetVisible(_docOverlay.Visibility == Visibility.Visible);
            DebugLogger.Log($"Window_Closing: Saved doc overlay position: ({_docOverlay.Left}, {_docOverlay.Top}), visible: {_docOverlay.Visibility == Visibility.Visible}");
        }
        else
        {
            _settings.SetDocWidgetVisible(false);
        }
        
        if (_frequentProjectsOverlay != null)
        {
            _settings.SetFrequentProjectsWidgetPosition(_frequentProjectsOverlay.Left, _frequentProjectsOverlay.Top);
            _settings.SetFrequentProjectsWidgetVisible(_frequentProjectsOverlay.Visibility == Visibility.Visible);
            DebugLogger.Log($"Window_Closing: Saved frequent projects overlay position: ({_frequentProjectsOverlay.Left}, {_frequentProjectsOverlay.Top})");
        }
        else
        {
            _settings.SetFrequentProjectsWidgetVisible(false);
        }
        
        if (_quickLaunchOverlay != null)
        {
            _settings.SetQuickLaunchWidgetPosition(_quickLaunchOverlay.Left, _quickLaunchOverlay.Top);
            _settings.SetQuickLaunchWidgetVisible(_quickLaunchOverlay.Visibility == Visibility.Visible);
            DebugLogger.Log($"Window_Closing: Saved quick launch overlay position: ({_quickLaunchOverlay.Left}, {_quickLaunchOverlay.Top})");
        }
        else
        {
            _settings.SetQuickLaunchWidgetVisible(false);
        }

        if (_smartProjectSearchOverlay != null)
        {
            _settings.SetSmartProjectSearchWidgetPosition(_smartProjectSearchOverlay.Left, _smartProjectSearchOverlay.Top);
            _settings.SetSmartProjectSearchWidgetVisible(_smartProjectSearchOverlay.Visibility == Visibility.Visible);
            DebugLogger.Log($"Window_Closing: Saved smart search overlay position: ({_smartProjectSearchOverlay.Left}, {_smartProjectSearchOverlay.Top})");
        }
        else
        {
            _settings.SetSmartProjectSearchWidgetVisible(false);
        }
        
        // Save async but don't await (app is closing)
        _ = _settings.SaveAsync();
        DebugLogger.Log("Window_Closing: Saved widget positions and visibility state");
    }

    private void ShowLoading(bool show)
    {
        LoadingIndicator.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private static System.Windows.Media.Color Blend(System.Windows.Media.Color baseColor, System.Windows.Media.Color tint, double amount)
    {
        amount = Math.Max(0, Math.Min(1, amount));
        byte r = (byte)Math.Round(baseColor.R + (tint.R - baseColor.R) * amount);
        byte g = (byte)Math.Round(baseColor.G + (tint.G - baseColor.G) * amount);
        byte b = (byte)Math.Round(baseColor.B + (tint.B - baseColor.B) * amount);
        return System.Windows.Media.Color.FromRgb(r, g, b);
    }

    private static double GetLuminance(System.Windows.Media.Color color)
    {
        return (0.2126 * color.R + 0.7152 * color.G + 0.0722 * color.B) / 255.0;
    }

    public void ReloadHotkey()
    {
        Dispatcher.Invoke(async () =>
        {
            try
            {
                _hotkey?.Dispose();
                
                await _settings.LoadAsync();
                var (modifiers, key) = _settings.GetHotkey();
                
                _hotkey = new GlobalHotkey(this, (uint)modifiers, (uint)key);
                _hotkey.HotkeyPressed += OnHotkeyPressed;
                
                StatusText.Text = $"Hotkey updated to {FormatHotkey(modifiers, key)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Failed to update hotkey: {ex.Message}";
            }
        });
    }

    private void StartIpcListener()
    {
        _ipcCts = new CancellationTokenSource();
        var token = _ipcCts.Token;

        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream("DesktopHub_IPC", PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                    DebugLogger.Log("IPC: Waiting for connection...");
                    
                    await server.WaitForConnectionAsync(token);
                    DebugLogger.Log("IPC: Client connected");

                    using var reader = new StreamReader(server);
                    var command = await reader.ReadLineAsync();
                    
                    if (!string.IsNullOrEmpty(command))
                    {
                        DebugLogger.Log($"IPC: Received command: {command}");
                        await Dispatcher.InvokeAsync(() => HandleIpcCommand(command));
                    }
                }
                catch (OperationCanceledException)
                {
                    DebugLogger.Log("IPC: Listener cancelled");
                    break;
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"IPC: Error: {ex.Message}");
                    await Task.Delay(1000, token);
                }
            }
        }, token);
    }

    private void HandleIpcCommand(string command)
    {
        DebugLogger.Log($"IPC: Handling command: {command}");
        
        switch (command)
        {
            case "SHOW_OVERLAY":
                if (this.Visibility != Visibility.Visible)
                {
                    ShowOverlay();
                }
                else
                {
                    this.Activate();
                    SearchBox.Focus();
                }
                break;

            case "SHOW_SETTINGS":
                _trayIcon?.ShowSettings();
                break;

            case "CLOSE_APP":
                var confirmed = ConfirmationDialog.Show("Are you sure you want to exit DesktopHub?", this);
                if (confirmed)
                {
                    System.Windows.Application.Current.Shutdown();
                }
                break;

            default:
                DebugLogger.Log($"IPC: Unknown command: {command}");
                break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _ipcCts?.Cancel();
        _ipcCts?.Dispose();
        _hotkey?.Dispose();
        _trayIcon?.Dispose();
        base.OnClosed(e);
    }

    // ViewModel for binding
    private void UpdateRootClip(double radiusDip)
    {
        try
        {
            DebugLogger.Log($"UpdateRootClip: Starting with radiusDip={radiusDip}");
            DebugLogger.Log($"  RootBorder.ActualWidth: {RootBorder.ActualWidth}");
            DebugLogger.Log($"  RootBorder.ActualHeight: {RootBorder.ActualHeight}");
            DebugLogger.Log($"  RootBorder.Width: {RootBorder.Width}");
            DebugLogger.Log($"  RootBorder.Height: {RootBorder.Height}");
            DebugLogger.Log($"  RootBorder.Margin: {RootBorder.Margin}");
            DebugLogger.Log($"  RootBorder.Padding: {RootBorder.Padding}");
            DebugLogger.Log($"  RootBorder.BorderThickness: {RootBorder.BorderThickness}");
            DebugLogger.Log($"  RootBorder.CornerRadius: {RootBorder.CornerRadius}");
            
            if (RootBorder.ActualWidth <= 0 || RootBorder.ActualHeight <= 0)
            {
                DebugLogger.Log("  Skipping clip - RootBorder not rendered yet");
                return;
            }
            
            var rect = new System.Windows.Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight);
            RootBorder.Clip = new System.Windows.Media.RectangleGeometry(rect, radiusDip, radiusDip);
            DebugLogger.Log($"  Applied clip geometry: {rect.Width}x{rect.Height} with radius {radiusDip}");
            DebugLogger.Log($"  RootBorder.Clip: {RootBorder.Clip}");
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"UpdateRootClip: EXCEPTION - {ex}");
        }
    }

    // Path search result that reuses the same DataTemplate bindings as ProjectViewModel
    private class PathSearchResultViewModel
    {
        public string FullNumber { get; }
        public string Name { get; }
        public string Path { get; }
        public string? Location { get; }
        public string? Status { get; }
        public bool IsDirectory { get; }
        public bool IsFavorite { get; } = false;

        public PathSearchResultViewModel(string fullPath, bool isDirectory, string? status = null)
        {
            Path = fullPath;
            IsDirectory = isDirectory;
            Name = System.IO.Path.GetFileName(fullPath);
            if (string.IsNullOrEmpty(Name))
                Name = fullPath; // root paths like C:\
            FullNumber = isDirectory ? "📁" : GetFileIcon(fullPath);
            Location = isDirectory ? "Directory" : GetFileSize(fullPath);
            Status = status;
        }

        private static string GetFileIcon(string path)
        {
            var ext = System.IO.Path.GetExtension(path)?.ToLowerInvariant();
            return ext switch
            {
                ".exe" or ".msi" => "⚙️",
                ".pdf" => "📄",
                ".doc" or ".docx" => "📝",
                ".xls" or ".xlsx" => "📊",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".svg" => "🖼️",
                ".zip" or ".rar" or ".7z" => "📦",
                ".txt" or ".log" or ".csv" => "📃",
                ".dwg" or ".dxf" => "📐",
                _ => "📄"
            };
        }

        private static string? GetFileSize(string path)
        {
            try
            {
                var info = new System.IO.FileInfo(path);
                if (!info.Exists) return null;
                var bytes = info.Length;
                if (bytes < 1024) return $"{bytes} B";
                if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
                if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
                return $"{bytes / (1024.0 * 1024 * 1024):F1} GB";
            }
            catch { return null; }
        }
    }

    private async Task PerformPathSearch(string query, CancellationToken token)
    {
        try
        {
            await Task.Delay(200, token);
            if (token.IsCancellationRequested) return;

            ShowLoading(true);

            if (!TryExtractPathAndFilter(query, out var path, out var scopedQuery))
            {
                _isPathSearchResults = false;
                _activePathSearchRootDisplay = null;
                ResultsList.ItemsSource = null;
                StatusText.Text = "Path format not recognized. Use C:\\Folder or C:\\Folder :: search terms";
                UpdateResultsHeader();
                ShowLoading(false);
                return;
            }

            _isPathSearchResults = true;
            _activePathSearchRootDisplay = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
            if (string.IsNullOrWhiteSpace(_activePathSearchRootDisplay))
                _activePathSearchRootDisplay = path;

            if (!System.IO.Directory.Exists(path))
            {
                ResultsList.ItemsSource = null;
                StatusText.Text = "Directory not found";
                UpdateResultsHeader();
                ShowLoading(false);
                return;
            }

            var showDirs = _settings.GetPathSearchShowSubDirs();
            var showFiles = _settings.GetPathSearchShowSubFiles();
            var showHidden = _settings.GetPathSearchShowHidden();
            var results = new List<PathSearchResultViewModel>();

            if (!showDirs && !showFiles)
            {
                ResultsList.ItemsSource = null;
                StatusText.Text = "Enable folder and/or file path results in General settings";
                UpdateResultsHeader();
                ShowLoading(false);
                return;
            }

            var scopedQueryNormalized = scopedQuery.Trim();
            var isRecursiveScopedSearch = scopedQueryNormalized.Length > 0;

            await Task.Run(() =>
            {
                try
                {
                    if (!isRecursiveScopedSearch)
                    {
                        if (showDirs)
                        {
                            foreach (var dir in System.IO.Directory.GetDirectories(path))
                            {
                                if (token.IsCancellationRequested) return;
                                if (!showHidden && IsHiddenOrSystem(dir))
                                    continue;
                                results.Add(new PathSearchResultViewModel(dir, true));
                            }
                        }

                        if (showFiles)
                        {
                            foreach (var file in System.IO.Directory.GetFiles(path))
                            {
                                if (token.IsCancellationRequested) return;
                                if (!showHidden && IsHiddenOrSystem(file))
                                    continue;
                                results.Add(new PathSearchResultViewModel(file, false));
                            }
                        }
                        return;
                    }

                    var regexPattern = string.Empty;
                    var isRegexQuery = IsRegexQuery(scopedQueryNormalized, out regexPattern);
                    Regex? regex = null;

                    if (isRegexQuery)
                    {
                        try
                        {
                            regex = new Regex(regexPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                        }
                        catch
                        {
                            return;
                        }
                    }

                    var terms = ExtractPathSearchTerms(scopedQueryNormalized);
                    var ranked = new List<(PathSearchResultViewModel ViewModel, double Score)>();
                    const int maxMatches = 350;

                    foreach (var (entryPath, isDir) in EnumeratePathEntries(path, showDirs, showFiles, token))
                    {
                        if (token.IsCancellationRequested) return;
                        if (!showHidden && IsHiddenOrSystem(entryPath))
                            continue;

                        var score = ScorePathEntry(entryPath, scopedQueryNormalized, terms, regex);
                        if (score <= 0)
                            continue;

                        var status = BuildPathMatchStatus(path, entryPath);
                        ranked.Add((new PathSearchResultViewModel(entryPath, isDir, status), score));

                        if (ranked.Count >= maxMatches)
                            break;
                    }

                    foreach (var match in ranked
                        .OrderByDescending(r => r.Score)
                        .ThenBy(r => r.ViewModel.Name)
                        .Select(r => r.ViewModel))
                    {
                        results.Add(match);
                    }
                }
                catch (UnauthorizedAccessException)
                {
                    // Silently skip inaccessible directories
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"PerformPathSearch: Error enumerating {path}: {ex.Message}");
                }
            }, token);

            if (token.IsCancellationRequested) return;

            ResultsList.ItemsSource = results;

            if (results.Count > 0)
            {
                ResultsList.SelectedIndex = 0;

                if (isRecursiveScopedSearch)
                {
                    var modeLabel = IsRegexQuery(scopedQueryNormalized, out _) ? "regex" : "smart";
                    StatusText.Text = $"{results.Count} match{(results.Count == 1 ? "" : "es")} in {modeLabel} search";
                }
                else
                {
                    var dirCount = results.Count(r => r.IsDirectory);
                    var fileCount = results.Count - dirCount;
                    var parts = new List<string>();
                    if (dirCount > 0) parts.Add($"{dirCount} folder{(dirCount == 1 ? "" : "s")}");
                    if (fileCount > 0) parts.Add($"{fileCount} file{(fileCount == 1 ? "" : "s")}");
                    StatusText.Text = $"Path: {string.Join(", ", parts)}";
                }

                if (!_userManuallySizedResults && _isResultsCollapsed)
                {
                    _isResultsCollapsed = false;
                    ResultsContainer.Visibility = Visibility.Visible;
                    CollapseIconRotation.Angle = 0;
                    this.Height = 500;
                }
            }
            else
            {
                if (isRecursiveScopedSearch)
                {
                    StatusText.Text = "No path matches found";
                }
                else
                {
                    StatusText.Text = "Directory is empty";
                }
            }

            UpdateResultsHeader();
            ShowLoading(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            DebugLogger.Log($"PerformPathSearch: Error: {ex.Message}");
            StatusText.Text = $"Path search error: {ex.Message}";
            ShowLoading(false);
        }
    }

    // ViewModel for binding
    private class ProjectViewModel
    {
        public string FullNumber { get; }
        public string Name { get; }
        public string Path { get; }
        public string? Location { get; }
        public string? Status { get; }
        public bool IsFavorite { get; }

        public ProjectViewModel(Project project)
        {
            FullNumber = project.FullNumber;
            Name = project.Name;
            Path = project.Path;
            Location = project.Metadata?.Location;
            Status = project.Metadata?.Status;
            IsFavorite = project.Metadata?.IsFavorite ?? false;
        }
    }

    private void RegisterWidgetWindow(Window? window)
    {
        if (window == null)
            return;

        window.LocationChanged -= WidgetWindow_LocationChanged;
        window.SizeChanged -= WidgetWindow_SizeChanged;
        window.IsVisibleChanged -= WidgetWindow_IsVisibleChanged;
        window.Closed -= WidgetWindow_Closed;
        window.MouseLeftButtonUp -= WidgetWindow_MouseLeftButtonUp;
        window.LostMouseCapture -= WidgetWindow_LostMouseCapture;

        window.LocationChanged += WidgetWindow_LocationChanged;
        window.SizeChanged += WidgetWindow_SizeChanged;
        window.IsVisibleChanged += WidgetWindow_IsVisibleChanged;
        window.Closed += WidgetWindow_Closed;
        window.MouseLeftButtonUp += WidgetWindow_MouseLeftButtonUp;
        window.LostMouseCapture += WidgetWindow_LostMouseCapture;
    }

    private void UnregisterWidgetWindow(Window? window)
    {
        if (window == null)
            return;

        window.LocationChanged -= WidgetWindow_LocationChanged;
        window.SizeChanged -= WidgetWindow_SizeChanged;
        window.IsVisibleChanged -= WidgetWindow_IsVisibleChanged;
        window.Closed -= WidgetWindow_Closed;
        window.MouseLeftButtonUp -= WidgetWindow_MouseLeftButtonUp;
        window.LostMouseCapture -= WidgetWindow_LostMouseCapture;
    }

    private void WidgetWindow_LocationChanged(object? sender, EventArgs e)
    {
        if (sender is not Window window)
            return;

        if (_isAutoArrangingWidgets || !_settings.GetLivingWidgetsMode())
        {
            TrackVisibleWindowBounds();
            return;
        }

        HandleWindowMoved(window);
    }

    private void WidgetWindow_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is not Window window)
            return;

        if (_isAutoArrangingWidgets || !_settings.GetLivingWidgetsMode())
        {
            TrackVisibleWindowBounds();
            return;
        }

        HandleWindowResized(window);
    }

    private void WidgetWindow_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not Window window)
            return;

        if (!_settings.GetLivingWidgetsMode())
            return;

        if (window.Visibility == Visibility.Visible)
        {
            ApplyLiveLayoutForWindow(window);
        }
        else
        {
            DetachWindowFromAttachments(window);
        }

        RefreshAttachmentMappings();
        TrackVisibleWindowBounds();
    }

    private void WidgetWindow_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Window window)
            return;

        FinalizeWindowDragLayout(window);
    }

    private void WidgetWindow_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is not Window window)
            return;

        FinalizeWindowDragLayout(window);
    }

    private void FinalizeWindowDragLayout(Window window)
    {
        if (_isAutoArrangingWidgets || !_settings.GetLivingWidgetsMode())
            return;

        if (window == this)
            return;

        if (window.Visibility != Visibility.Visible || !window.IsLoaded)
            return;

        var gap = GetConfiguredWidgetGap();
        var currentRect = GetWindowRect(window);

        void Finalize()
        {
            UpdateDynamicOverlayMaxHeight(window);
            RecalculateDocOverlayConstraints();
            MoveAttachedFollowers(window);
            RefreshAttachmentMappings();
            TrackVisibleWindowBounds();
        }

        // Check if drop position causes overlap
        if (CountRectOverlaps(currentRect, window) > 0)
        {
            // Invalid drop - animate back to pre-drag position, but only if that position is itself clean
            if (_lastWidgetBounds.TryGetValue(window, out var originalRect) && CountRectOverlaps(originalRect, window) == 0)
            {
                AnimateWindowToPosition(window, originalRect.Left, originalRect.Top, Finalize);
            }
            else
            {
                // Pre-drag position was also overlapping (e.g. search overlay grew after placement),
                // so resolve to a genuinely clear position
                var resolvedRect = ResolveWindowOverlaps(window, currentRect, gap);
                AnimateWindowToPosition(window, resolvedRect.Left, resolvedRect.Top, Finalize);
            }
        }
        else
        {
            // Valid drop - apply live layout (snap to edges, etc.)
            ApplyLiveLayoutForWindow(window);
            Finalize();
        }
    }

    private void AnimateWindowToPosition(Window window, double targetLeft, double targetTop, Action? onComplete = null)
    {
        const int animationDurationMs = 200;
        const int animationSteps = 12;
        var startLeft = window.Left;
        var startTop = window.Top;
        var currentStep = 0;

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds((double)animationDurationMs / animationSteps)
        };

        timer.Tick += (s, e) =>
        {
            currentStep++;
            var isDone = currentStep >= animationSteps;

            // Ease-out: t goes 0→1, eased position avoids abrupt stop
            var t = isDone ? 1.0 : 1.0 - Math.Pow(1.0 - (double)currentStep / animationSteps, 2);
            var newLeft = startLeft + (targetLeft - startLeft) * t;
            var newTop = startTop + (targetTop - startTop) * t;

            var previousAutoArrange = _isAutoArrangingWidgets;
            _isAutoArrangingWidgets = true;
            try
            {
                window.Left = newLeft;
                window.Top = newTop;
            }
            finally
            {
                _isAutoArrangingWidgets = previousAutoArrange;
            }

            if (isDone)
            {
                timer.Stop();
                onComplete?.Invoke();
            }
        };

        timer.Start();
    }

    private void WidgetWindow_Closed(object? sender, EventArgs e)
    {
        if (sender is not Window window)
            return;

        UnregisterWidgetWindow(window);
        DetachWindowFromAttachments(window);
        _lastWidgetBounds.Remove(window);
        RefreshAttachmentMappings();
        TrackVisibleWindowBounds();
    }

    private IEnumerable<Window> GetManagedWidgetWindows(bool includeHidden = false)
    {
        var windows = new Window?[]
        {
            this,
            _widgetLauncher,
            _timerOverlay,
            _quickTasksOverlay,
            _docOverlay,
            _frequentProjectsOverlay,
            _quickLaunchOverlay,
            _smartProjectSearchOverlay
        };

        var seen = new HashSet<Window>();
        foreach (var window in windows)
        {
            if (window == null)
                continue;

            if (!seen.Add(window))
                continue;

            if (!window.IsLoaded)
                continue;

            if (!includeHidden && window.Visibility != Visibility.Visible)
                continue;

            yield return window;
        }
    }

    private static bool IsWindowBeingDragged(Window window)
    {
        return window.IsMouseCaptured && System.Windows.Input.Mouse.LeftButton == MouseButtonState.Pressed;
    }

    private static Rect GetWindowRect(Window window)
    {
        var width = window.ActualWidth;
        if (width <= 1 || double.IsNaN(width))
            width = window.Width;
        if (width <= 1 || double.IsNaN(width))
            width = window.RenderSize.Width;

        var height = window.ActualHeight;
        if (height <= 1 || double.IsNaN(height))
            height = window.Height;
        if (height <= 1 || double.IsNaN(height))
            height = window.RenderSize.Height;

        return new Rect(window.Left, window.Top, Math.Max(1, width), Math.Max(1, height));
    }

    private Rect GetScreenWorkArea(Rect rect)
    {
        var center = new System.Drawing.Point(
            (int)Math.Round(rect.Left + rect.Width / 2.0),
            (int)Math.Round(rect.Top + rect.Height / 2.0)
        );
        var screen = Screen.FromPoint(center);
        return new Rect(screen.WorkingArea.Left, screen.WorkingArea.Top, screen.WorkingArea.Width, screen.WorkingArea.Height);
    }

    private double GetConfiguredWidgetGap()
    {
        return Math.Max(4, _settings.GetWidgetSnapGap());
    }

    private double GetResponsiveColumnWidgetWidth()
    {
        var referenceRect = GetWindowRect(this);
        var workArea = GetScreenWorkArea(referenceRect);
        var targetWidth = workArea.Width * 0.22;
        return Math.Round(Math.Clamp(targetWidth, 400.0, 460.0));
    }

    private void ApplyResponsiveWidgetWidth(Window window)
    {
        if (window is QuickTasksOverlay or DocQuickOpenOverlay or FrequentProjectsOverlay or QuickLaunchOverlay or SmartProjectSearchOverlay)
        {
            window.Width = GetResponsiveColumnWidgetWidth();
        }
    }

    private static double HorizontalOverlap(Rect a, Rect b)
    {
        return Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));
    }

    private static double VerticalOverlap(Rect a, Rect b)
    {
        return Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));
    }

    private static bool RectsOverlap(Rect a, Rect b)
    {
        return HorizontalOverlap(a, b) > 0.5 && VerticalOverlap(a, b) > 0.5;
    }

    private void MoveWindowTo(Window window, double left, double top)
    {
        if (Math.Abs(window.Left - left) < 0.5 && Math.Abs(window.Top - top) < 0.5)
            return;

        var previousAutoArrange = _isAutoArrangingWidgets;
        _isAutoArrangingWidgets = true;
        try
        {
            window.Left = left;
            window.Top = top;
        }
        finally
        {
            _isAutoArrangingWidgets = previousAutoArrange;
        }
    }

    private Rect ClampRectToScreen(Rect rect, double gap)
    {
        var workArea = GetScreenWorkArea(rect);

        var minLeft = workArea.Left + gap;
        var maxLeft = workArea.Right - gap - rect.Width;
        if (maxLeft < minLeft)
        {
            minLeft = workArea.Left;
            maxLeft = workArea.Right - rect.Width;
        }

        var minTop = workArea.Top + gap;
        var maxTop = workArea.Bottom - gap - rect.Height;
        if (maxTop < minTop)
        {
            minTop = workArea.Top;
            maxTop = workArea.Bottom - rect.Height;
        }

        var clampedLeft = Math.Max(minLeft, Math.Min(rect.Left, maxLeft));
        var clampedTop = Math.Max(minTop, Math.Min(rect.Top, maxTop));
        return new Rect(clampedLeft, clampedTop, rect.Width, rect.Height);
    }

    private Rect SnapRectToScreenEdges(Rect rect, double gap)
    {
        var workArea = GetScreenWorkArea(rect);

        var leftSnap = workArea.Left + gap;
        var rightSnap = workArea.Right - gap - rect.Width;
        var topSnap = workArea.Top + gap;
        var bottomSnap = workArea.Bottom - gap - rect.Height;

        if (Math.Abs(rect.Left - leftSnap) <= WidgetSnapThreshold)
            rect.X = leftSnap;
        else if (Math.Abs(rect.Left - rightSnap) <= WidgetSnapThreshold)
            rect.X = rightSnap;

        if (Math.Abs(rect.Top - topSnap) <= WidgetSnapThreshold)
            rect.Y = topSnap;
        else if (Math.Abs(rect.Top - bottomSnap) <= WidgetSnapThreshold)
            rect.Y = bottomSnap;

        return ClampRectToScreen(rect, gap);
    }

    private Rect SnapRectToOtherWindows(Window movingWindow, Rect rect, double gap)
    {
        var bestXDistance = WidgetSnapThreshold + 1;
        var bestYDistance = WidgetSnapThreshold + 1;
        double? snappedX = null;
        double? snappedY = null;

        void ConsiderX(double candidate)
        {
            var distance = Math.Abs(rect.Left - candidate);
            if (distance <= WidgetSnapThreshold && distance < bestXDistance)
            {
                bestXDistance = distance;
                snappedX = candidate;
            }
        }

        void ConsiderY(double candidate)
        {
            var distance = Math.Abs(rect.Top - candidate);
            if (distance <= WidgetSnapThreshold && distance < bestYDistance)
            {
                bestYDistance = distance;
                snappedY = candidate;
            }
        }

        foreach (var otherWindow in GetManagedWidgetWindows())
        {
            if (otherWindow == movingWindow)
                continue;

            var otherRect = GetWindowRect(otherWindow);
            var hasVerticalOverlap = VerticalOverlap(rect, otherRect) > 24;
            var hasHorizontalOverlap = HorizontalOverlap(rect, otherRect) > 24;

            if (hasVerticalOverlap)
            {
                ConsiderX(otherRect.Left);
                ConsiderX(otherRect.Right - rect.Width);
                ConsiderX(otherRect.Right + gap);
                ConsiderX(otherRect.Left - rect.Width - gap);
            }

            if (hasHorizontalOverlap)
            {
                ConsiderY(otherRect.Top);
                ConsiderY(otherRect.Bottom - rect.Height);
                ConsiderY(otherRect.Bottom + gap);
                ConsiderY(otherRect.Top - rect.Height - gap);
            }
        }

        if (snappedX.HasValue)
            rect.X = snappedX.Value;
        if (snappedY.HasValue)
            rect.Y = snappedY.Value;

        return rect;
    }

    private int CountRectOverlaps(Rect rect, Window movingWindow)
    {
        var overlapCount = 0;
        foreach (var otherWindow in GetManagedWidgetWindows())
        {
            if (otherWindow == movingWindow)
                continue;

            if (RectsOverlap(rect, GetWindowRect(otherWindow)))
                overlapCount++;
        }

        return overlapCount;
    }

    private Rect ResolveWindowOverlaps(Window movingWindow, Rect rect, double gap)
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            Rect? overlap = null;
            foreach (var otherWindow in GetManagedWidgetWindows())
            {
                if (otherWindow == movingWindow)
                    continue;

                var otherRect = GetWindowRect(otherWindow);
                if (RectsOverlap(rect, otherRect))
                {
                    overlap = otherRect;
                    break;
                }
            }

            if (!overlap.HasValue)
                break;

            var overlapRect = overlap.Value;
            var candidates = new List<Rect>
            {
                new Rect(overlapRect.Left - rect.Width - gap, rect.Top, rect.Width, rect.Height),
                new Rect(overlapRect.Right + gap, rect.Top, rect.Width, rect.Height),
                new Rect(rect.Left, overlapRect.Top - rect.Height - gap, rect.Width, rect.Height),
                new Rect(rect.Left, overlapRect.Bottom + gap, rect.Width, rect.Height)
            };

            var bestCandidate = rect;
            var bestOverlapCount = int.MaxValue;
            var bestDistance = double.MaxValue;

            foreach (var candidate in candidates)
            {
                var clamped = ClampRectToScreen(candidate, gap);
                var overlapCount = CountRectOverlaps(clamped, movingWindow);
                var distance = Math.Abs(clamped.Left - rect.Left) + Math.Abs(clamped.Top - rect.Top);

                if (overlapCount < bestOverlapCount || (overlapCount == bestOverlapCount && distance < bestDistance))
                {
                    bestCandidate = clamped;
                    bestOverlapCount = overlapCount;
                    bestDistance = distance;
                }
            }

            if (Math.Abs(bestCandidate.Left - rect.Left) < 0.5 && Math.Abs(bestCandidate.Top - rect.Top) < 0.5)
                break;

            rect = bestCandidate;
        }

        return ClampRectToScreen(rect, gap);
    }

    private void ApplyLiveLayoutForWindow(Window window)
    {
        if (!_settings.GetLivingWidgetsMode())
            return;

        if (window.Visibility != Visibility.Visible || !window.IsLoaded)
            return;

        var gap = GetConfiguredWidgetGap();
        var rect = GetWindowRect(window);
        rect = SnapRectToScreenEdges(rect, gap);
        rect = SnapRectToOtherWindows(window, rect, gap);
        rect = ResolveWindowOverlaps(window, rect, gap);
        MoveWindowTo(window, rect.Left, rect.Top);
        UpdateDynamicOverlayMaxHeight(window);
    }

    private void RecalculateDocOverlayConstraints()
    {
        if (_docOverlay != null && _docOverlay.IsLoaded && _docOverlay.Visibility == Visibility.Visible)
            UpdateDynamicOverlayMaxHeight(_docOverlay);

        if (this.IsLoaded && this.Visibility == Visibility.Visible)
            UpdateDynamicOverlayMaxHeight(this);
    }

    private void UpdateDynamicOverlayMaxHeight(Window window)
    {
        if (window is not DocQuickOpenOverlay && window != this)
            return;

        var rect = GetWindowRect(window);
        var workArea = GetScreenWorkArea(rect);
        var gap = GetConfiguredWidgetGap();

        // Default ceiling: screen work area bottom
        var limitY = workArea.Bottom - gap;

        // For any widget directly below us (significant horizontal overlap), calculate how far
        // Doc can grow while keeping that widget on-screen when pushed down.
        // Formula: if Doc.Bottom = limitY, the widget below lands at limitY+gap,
        // its bottom = limitY + gap + widget.Height <= screenBottom - gap
        // => limitY <= screenBottom - widget.Height - 2*gap
        foreach (var other in GetManagedWidgetWindows())
        {
            if (other == window)
                continue;

            var otherRect = GetWindowRect(other);

            // Only widgets whose top is below our top position
            if (otherRect.Top <= window.Top)
                continue;

            // Must have at least 30% horizontal overlap to be considered "below" us
            var overlapAmount = HorizontalOverlap(rect, otherRect);
            var requiredOverlap = Math.Min(rect.Width, otherRect.Width) * 0.3;
            if (overlapAmount < requiredOverlap)
                continue;

            // Maximum bottom Doc can reach while the widget below still fits on screen after being pushed
            var pushLimit = workArea.Bottom - otherRect.Height - 2 * gap;
            limitY = Math.Min(limitY, pushLimit);
        }

        var minHeight = window is DocQuickOpenOverlay ? 200.0 : 140.0;
        window.MaxHeight = Math.Max(minHeight, limitY - window.Top);
    }

    private void DetachWindowFromAttachments(Window window)
    {
        _verticalAttachments.Remove(window);

        var dependents = _verticalAttachments
            .Where(kvp => kvp.Value == window)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var dependent in dependents)
        {
            _verticalAttachments.Remove(dependent);
        }
    }

    private void AttachNearestWindowBelow(Window anchor, Rect previousAnchorBounds)
    {
        var anchorRect = GetWindowRect(anchor);
        var maxAttachDistance = GetConfiguredWidgetGap() + 48;
        Window? bestFollower = null;
        var bestDistance = double.MaxValue;

        foreach (var candidate in GetManagedWidgetWindows())
        {
            if (candidate == anchor)
                continue;

            var candidateRect = GetWindowRect(candidate);
            if (candidateRect.Top < previousAnchorBounds.Bottom - WidgetSnapThreshold)
                continue;

            var overlapAmount = HorizontalOverlap(anchorRect, candidateRect);
            var requiredOverlap = Math.Min(anchorRect.Width, candidateRect.Width) * 0.25;
            if (overlapAmount < requiredOverlap)
                continue;

            var distance = candidateRect.Top - previousAnchorBounds.Bottom;
            if (distance > maxAttachDistance)
                continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestFollower = candidate;
            }
        }

        if (bestFollower != null)
        {
            _verticalAttachments[bestFollower] = anchor;
        }
    }

    private void AttachImpactedWindowsBelow(Window anchor, Rect previousAnchorBounds)
    {
        var anchorRect = GetWindowRect(anchor);
        var bottomDelta = anchorRect.Bottom - previousAnchorBounds.Bottom;
        if (Math.Abs(bottomDelta) <= 0.5)
            return;

        double minCaptureTop;
        double maxCaptureTop;

        if (bottomDelta > 0)
        {
            // Growing: capture widgets that were at/under the old bottom and now intersect the growth zone.
            minCaptureTop = previousAnchorBounds.Bottom - WidgetSnapThreshold;
            maxCaptureTop = anchorRect.Bottom + WidgetSnapThreshold;
        }
        else
        {
            // Shrinking: capture widgets that were likely pushed by us (around old bottom + gap)
            // so they can be pulled back up when the anchor contracts.
            var gap = GetConfiguredWidgetGap();
            minCaptureTop = anchorRect.Bottom - WidgetSnapThreshold;
            maxCaptureTop = previousAnchorBounds.Bottom + gap + WidgetSnapThreshold;
        }

        var attachedAny = false;

        foreach (var candidate in GetManagedWidgetWindows())
        {
            if (candidate == anchor)
                continue;

            var candidateRect = GetWindowRect(candidate);
            var isAlreadyFollower = _verticalAttachments.TryGetValue(candidate, out var existingAnchor) && existingAnchor == anchor;

            if (!isAlreadyFollower)
            {
                if (candidateRect.Top < minCaptureTop || candidateRect.Top > maxCaptureTop)
                    continue;

                var overlapAmount = HorizontalOverlap(anchorRect, candidateRect);
                var requiredOverlap = Math.Min(anchorRect.Width, candidateRect.Width) * 0.25;
                if (overlapAmount < requiredOverlap)
                    continue;
            }

            _verticalAttachments[candidate] = anchor;
            attachedAny = true;
        }

        if (!attachedAny)
        {
            AttachNearestWindowBelow(anchor, previousAnchorBounds);
        }
    }

    private void MoveAttachedFollowers(Window anchor)
    {
        MoveAttachedFollowers(anchor, new HashSet<Window>());
    }

    private void MoveAttachedFollowers(Window anchor, HashSet<Window> visited)
    {
        if (!visited.Add(anchor))
            return;

        var anchorRect = GetWindowRect(anchor);
        var gap = GetConfiguredWidgetGap();
        var isSearchAnchor = anchor == this;

        var followers = _verticalAttachments
            .Where(kvp => kvp.Value == anchor)
            .Select(kvp => kvp.Key)
            .Where(w => w.Visibility == Visibility.Visible && w.IsLoaded)
            .OrderBy(w => GetWindowRect(w).Top)
            .ToList();

        foreach (var follower in followers)
        {
            var followerRect = GetWindowRect(follower);
            var desiredTop = anchorRect.Bottom + gap;
            MoveWindowTo(follower, followerRect.Left, desiredTop);

            // Keep followers directly anchored to Search bottom during Search resize,
            // matching Doc-style push/pull behavior instead of lateral/overlap nudges.
            if (!isSearchAnchor)
            {
                ApplyLiveLayoutForWindow(follower);
            }

            MoveAttachedFollowers(follower, visited);
        }
    }

    private void NormalizeDocStartupGapIfNeeded()
    {
        if (!_settings.GetLivingWidgetsMode())
            return;

        if (_docOverlay == null || !_docOverlay.IsLoaded || _docOverlay.Visibility != Visibility.Visible)
            return;

        // If no project is loaded, Doc Quick Open is in its compact state.
        // Pull the nearest widget below back up so we don't preserve a stale "pushed-down" gap from a previous session.
        if (_docService?.ProjectInfo != null)
            return;

        var docRect = GetWindowRect(_docOverlay);
        var gap = GetConfiguredWidgetGap();
        Window? bestFollower = null;
        var bestDistance = double.MaxValue;

        foreach (var candidate in GetManagedWidgetWindows())
        {
            if (candidate == _docOverlay)
                continue;

            var candidateRect = GetWindowRect(candidate);
            if (candidateRect.Top <= docRect.Bottom + WidgetSnapThreshold)
                continue;

            var overlapAmount = HorizontalOverlap(docRect, candidateRect);
            var requiredOverlap = Math.Min(docRect.Width, candidateRect.Width) * 0.25;
            if (overlapAmount < requiredOverlap)
                continue;

            var distance = candidateRect.Top - docRect.Bottom;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestFollower = candidate;
            }
        }

        if (bestFollower == null)
            return;

        var followerRect = GetWindowRect(bestFollower);
        var desiredTop = docRect.Bottom + gap;

        if (Math.Abs(followerRect.Top - desiredTop) <= 0.5)
            return;

        MoveWindowTo(bestFollower, followerRect.Left, desiredTop);
        RefreshAttachmentMappings();
        MoveAttachedFollowers(_docOverlay);

        DebugLogger.Log($"NormalizeDocStartupGapIfNeeded: Pulled {bestFollower.GetType().Name} from y={followerRect.Top:F1} to y={desiredTop:F1} (Doc had no project loaded)");
    }

    private void RefreshAttachmentMappings()
    {
        if (!_settings.GetLivingWidgetsMode())
        {
            _verticalAttachments.Clear();
            return;
        }

        var windows = GetManagedWidgetWindows().ToList();
        _verticalAttachments.Clear();

        var targetGap = GetConfiguredWidgetGap();
        foreach (var follower in windows)
        {
            var followerRect = GetWindowRect(follower);
            Window? bestAnchor = null;
            var bestScore = double.MaxValue;

            foreach (var anchor in windows)
            {
                if (anchor == follower)
                    continue;

                var anchorRect = GetWindowRect(anchor);
                var verticalGap = followerRect.Top - anchorRect.Bottom;
                if (verticalGap < -WidgetSnapThreshold || verticalGap > targetGap + (WidgetSnapThreshold * 2))
                    continue;

                var overlapAmount = HorizontalOverlap(anchorRect, followerRect);
                var requiredOverlap = Math.Min(anchorRect.Width, followerRect.Width) * 0.25;
                if (overlapAmount < requiredOverlap)
                    continue;

                var score = Math.Abs(verticalGap - targetGap);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestAnchor = anchor;
                }
            }

            if (bestAnchor != null)
            {
                _verticalAttachments[follower] = bestAnchor;
            }
        }
    }

    private void TrackVisibleWindowBounds()
    {
        var visibleWindows = GetManagedWidgetWindows().ToHashSet();

        foreach (var window in visibleWindows)
        {
            _lastWidgetBounds[window] = GetWindowRect(window);
        }

        var staleWindows = _lastWidgetBounds.Keys
            .Where(w => !visibleWindows.Contains(w))
            .ToList();

        foreach (var staleWindow in staleWindows)
        {
            _lastWidgetBounds.Remove(staleWindow);
        }
    }

    private void HandleWindowMoved(Window window)
    {
        if (window.Visibility != Visibility.Visible || !window.IsLoaded)
        {
            TrackVisibleWindowBounds();
            return;
        }

        if (_verticalAttachments.ContainsKey(window))
        {
            _verticalAttachments.Remove(window);
        }

        var gap = GetConfiguredWidgetGap();
        var currentRect = GetWindowRect(window);
        
        if (IsWindowBeingDragged(window))
        {
            // During active drag, only clamp to screen edges - don't prevent overlap
            // This eliminates jitter by allowing free movement during drag
            var clampedRect = ClampRectToScreen(currentRect, gap);
            if (Math.Abs(clampedRect.Left - currentRect.Left) > 0.5 || Math.Abs(clampedRect.Top - currentRect.Top) > 0.5)
            {
                MoveWindowTo(window, clampedRect.Left, clampedRect.Top);
                currentRect = clampedRect;
            }

            // Don't update _lastWidgetBounds during drag - preserve pre-drag position for snap-back
            return;
        }

        ApplyLiveLayoutForWindow(window);
        RecalculateDocOverlayConstraints();
        MoveAttachedFollowers(window);
        RefreshAttachmentMappings();
        TrackVisibleWindowBounds();
    }

    private void HandleWindowResized(Window window)
    {
        if (window.Visibility != Visibility.Visible || !window.IsLoaded)
        {
            TrackVisibleWindowBounds();
            return;
        }

        if (_lastWidgetBounds.TryGetValue(window, out var previousBounds))
        {
            var currentBounds = GetWindowRect(window);
            var grewDownward = currentBounds.Height > previousBounds.Height + 0.5;
            var shrankDownward = currentBounds.Height < previousBounds.Height - 0.5;

            if (grewDownward || shrankDownward)
            {
                AttachImpactedWindowsBelow(window, previousBounds);
                MoveAttachedFollowers(window);

                if (window == this)
                {
                    var followerCount = _verticalAttachments.Count(kvp => kvp.Value == window);
                    DebugLogger.Log($"SearchResize: bottomDelta={(currentBounds.Bottom - previousBounds.Bottom):F1}, followers={followerCount}");
                }
            }
        }

        if (window == this)
        {
            // Keep Search Overlay anchored: constrain its max height like Doc Quick Open,
            // but don't run overlap-resolution nudges that can shift it left/right.
            UpdateDynamicOverlayMaxHeight(window);
            var gap = GetConfiguredWidgetGap();
            var rect = GetWindowRect(window);
            rect = SnapRectToScreenEdges(rect, gap);
            MoveWindowTo(window, rect.Left, rect.Top);
            TrackVisibleWindowBounds();
            return;
        }

        ApplyLiveLayoutForWindow(window);
        RefreshAttachmentMappings();
        TrackVisibleWindowBounds();
    }
    public async void OnDriveSettingsChanged()
    {
        try
        {
            DebugLogger.Log("OnDriveSettingsChanged: Drive settings changed, reloading projects...");
            
            // Reload projects from database to apply new filtering
            await Dispatcher.InvokeAsync(async () => 
            {
                await LoadProjectsAsync();
                
                // Trigger a background scan to pick up newly enabled drives
                _ = BackgroundScanAsync();
            });
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"OnDriveSettingsChanged: Error reloading projects: {ex.Message}");
        }
    }

    public void UpdateSearchWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetSearchWidgetEnabled();
            _widgetLauncher.UpdateSearchButtonVisibility(enabled);
            DebugLogger.Log($"UpdateSearchWidgetButton: Search button visibility set to {enabled}");
        }
    }

    public void UpdateWidgetLauncherLayout()
    {
        _widgetLauncher?.RefreshLayoutFromSettings();
        DebugLogger.Log("UpdateWidgetLauncherLayout: Launcher layout refreshed from settings");
    }

    public void UpdateTimerWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetTimerWidgetEnabled();
            _widgetLauncher.UpdateTimerButtonVisibility(enabled);
            DebugLogger.Log($"UpdateTimerWidgetButton: Timer button visibility set to {enabled}");
        }
    }

    public void UpdateQuickTasksWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetQuickTasksWidgetEnabled();
            _widgetLauncher.UpdateQuickTasksButtonVisibility(enabled);
            DebugLogger.Log($"UpdateQuickTasksWidgetButton: QuickTasks button visibility set to {enabled}");
        }
    }

    public void UpdateDocWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetDocWidgetEnabled();
            _widgetLauncher.UpdateDocButtonVisibility(enabled);
            DebugLogger.Log($"UpdateDocWidgetButton: Doc button visibility set to {enabled}");
        }
    }

    public void UpdateFrequentProjectsWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetFrequentProjectsWidgetEnabled();
            _widgetLauncher.UpdateFrequentProjectsButtonVisibility(enabled);
            DebugLogger.Log($"UpdateFrequentProjectsWidgetButton: visibility set to {enabled}");
        }
    }

    public void UpdateFrequentProjectsLayout()
    {
        if (_frequentProjectsOverlay != null && _frequentProjectsOverlay.IsVisible)
        {
            var left = _frequentProjectsOverlay.Left;
            var top = _frequentProjectsOverlay.Top;
            _frequentProjectsOverlay.Close();
            _frequentProjectsOverlay = null;

            CreateFrequentProjectsOverlay(left, top);
            _frequentProjectsOverlay?.Show();
            DebugLogger.Log("UpdateFrequentProjectsLayout: Recreated Frequent Projects overlay with new layout");
        }
    }

    public void UpdateQuickLaunchWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetQuickLaunchWidgetEnabled();
            _widgetLauncher.UpdateQuickLaunchButtonVisibility(enabled);
            DebugLogger.Log($"UpdateQuickLaunchWidgetButton: visibility set to {enabled}");
        }
    }

    public void UpdateSmartProjectSearchWidgetButton()
    {
        if (_widgetLauncher != null)
        {
            var enabled = _settings.GetSmartProjectSearchWidgetEnabled();
            _widgetLauncher.UpdateSmartProjectSearchButtonVisibility(enabled);
            DebugLogger.Log($"UpdateSmartProjectSearchWidgetButton: visibility set to {enabled}");
        }
    }

    public void UpdateQuickLaunchLayout()
    {
        if (_quickLaunchOverlay != null && _quickLaunchOverlay.IsVisible)
        {
            // Get current position before closing
            var left = _quickLaunchOverlay.Left;
            var top = _quickLaunchOverlay.Top;
            _quickLaunchOverlay.Close();
            _quickLaunchOverlay = null;

            // Recreate with new layout
            CreateQuickLaunchOverlay(left, top);
            _quickLaunchOverlay?.Show();
            DebugLogger.Log("UpdateQuickLaunchLayout: Recreated Quick Launch overlay with new layout");
        }
    }

    public void RefreshLiveWidgetLayout()
    {
        if (!_settings.GetLivingWidgetsMode())
            return;

        foreach (var window in GetManagedWidgetWindows().ToList())
        {
            ApplyLiveLayoutForWindow(window);
        }

        RefreshAttachmentMappings();
        TrackVisibleWindowBounds();
    }

    public void SetUpdateIndicatorVisible(bool visible)
    {
        if (UpdateIndicator != null)
            UpdateIndicator.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public UpdateCheckService? UpdateCheckService => _updateCheckService;
    public UpdateIndicatorManager? UpdateIndicatorManager => _updateIndicatorManager;

    private void InitializeUpdateCheckService()
    {
        try
        {
            var app = System.Windows.Application.Current as App;
            var firebaseManager = app?.FirebaseManager;

            if (firebaseManager == null)
            {
                DebugLogger.Log("InitializeUpdateCheckService: FirebaseManager not available, skipping");
                return;
            }

            _updateCheckService = new UpdateCheckService(_settings, () => firebaseManager.CheckForUpdatesAsync());
            _updateCheckService.UpdateAvailable += (sender, updateInfo) =>
            {
                DebugLogger.Log($"Update notification received: v{updateInfo.LatestVersion} available");
                _updateIndicatorManager?.SetUpdateAvailable(true);
            };
            _updateCheckService.UpdateDismissed += (sender, _) =>
            {
                _updateIndicatorManager?.SetUpdateAvailable(false);
            };
            _updateCheckService.Start();
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"InitializeUpdateCheckService: Error: {ex.Message}");
        }
    }

    public void RefreshUpdateIndicator()
    {
        _updateIndicatorManager?.Refresh();
    }

    public void RestartUpdateCheckService()
    {
        _updateCheckService?.Restart();
    }

    public void UpdateTransparency()
    {
        try
        {
            Dispatcher.Invoke(() =>
            {
                // Update search overlay transparency
                var overlayTransparency = _settings.GetOverlayTransparency();
                var overlayAlpha = (byte)(overlayTransparency * 255);
                RootBorder.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(overlayAlpha, 0x12, 0x12, 0x12));
                
                DebugLogger.Log($"UpdateTransparency: SearchOverlay transparency updated to {overlayTransparency:F2}");
                
                // Update widget launcher transparency if it exists
                if (_widgetLauncher != null)
                {
                    _widgetLauncher.UpdateTransparency();
                }
                
                // Update timer overlay transparency if it exists
                if (_timerOverlay != null)
                {
                    _timerOverlay.UpdateTransparency();
                }
                
                // Update quick tasks overlay transparency if it exists
                if (_quickTasksOverlay != null)
                {
                    _quickTasksOverlay.UpdateTransparency();
                }
                
                // Update doc overlay transparency if it exists
                if (_docOverlay != null)
                {
                    _docOverlay.UpdateTransparency();
                }
                
                // Update frequent projects overlay transparency if it exists
                if (_frequentProjectsOverlay != null)
                {
                    _frequentProjectsOverlay.UpdateTransparency();
                }
                
                // Update quick launch overlay transparency if it exists
                if (_quickLaunchOverlay != null)
                {
                    _quickLaunchOverlay.UpdateTransparency();
                }

                if (_smartProjectSearchOverlay != null)
                {
                    _smartProjectSearchOverlay.UpdateTransparency();
                }
            });
        }
        catch (Exception ex)
        {
            DebugLogger.Log($"UpdateTransparency: Error updating transparency: {ex.Message}");
        }
    }
}
