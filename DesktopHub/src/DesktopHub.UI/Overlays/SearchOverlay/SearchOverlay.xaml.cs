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
    private readonly List<GlobalHotkey> _hotkeys = new();
    private TrayIcon? _trayIcon;
    private TimerOverlay? _timerOverlay;
    private QuickTasksOverlay? _quickTasksOverlay;
    private DocQuickOpenOverlay? _docOverlay;
    private FrequentProjectsOverlay? _frequentProjectsOverlay;
    private QuickLaunchOverlay? _quickLaunchOverlay;
    private SmartProjectSearchOverlay? _smartProjectSearchOverlay;
    private CheatSheetOverlay? _cheatSheetOverlay;
    private WidgetLauncher? _widgetLauncher;
    private TaskService? _taskService;
    private DocOpenService? _docService;
    private SmartProjectSearchService? _smartProjectSearchService;
    private CheatSheetService? _cheatSheetService;
    private SmartProjectSearchWidget? _smartProjectSearchAttachedWidget;
    private IProjectLaunchDataStore? _launchDataStore;
    private List<Project> _allProjects = new();
    private List<Project> _filteredProjects = new();
    private CancellationTokenSource? _searchCts;
    private CancellationTokenSource? _selectionCts;
    private List<string> _searchHistory = new();
    private bool _isPathSearchResults = false;
    private string? _activePathSearchRootDisplay;
    private bool _isResultsCollapsed = false;
    private bool _isSmartProjectSearchAttachedPanelExpanded = false;
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
    private const double OverlayCollapsedBaseHeight = 140;
    private const double OverlayExpandedBaseHeight = 500;
    private const double SmartProjectSearchAttachedPanelExpandedHeight = 400;
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

}
