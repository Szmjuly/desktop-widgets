namespace DesktopHub.Core.Models;

/// <summary>
/// Categories of telemetry events
/// </summary>
public static class TelemetryCategory
{
    public const string Session = "session";
    public const string Search = "search";
    public const string ProjectLaunch = "project_launch";
    public const string Widget = "widget";
    public const string QuickLaunch = "quick_launch";
    public const string QuickTask = "quick_task";
    public const string DocAccess = "doc_access";
    public const string Timer = "timer";
    public const string CheatSheet = "cheat_sheet";
    public const string Hotkey = "hotkey";
    public const string Settings = "settings";
    public const string Filter = "filter";
    public const string Clipboard = "clipboard";
    public const string Error = "error";
    public const string Performance = "performance";
    public const string Tag = "tag";
    public const string FieldManagement = "field_management";
}

/// <summary>
/// Specific event types within each category
/// </summary>
public static class TelemetryEventType
{
    // Session
    public const string SessionStart = "session_start";
    public const string SessionEnd = "session_end";

    // Search (Project Search overlay)
    public const string SearchExecuted = "search_executed";
    public const string SearchResultClicked = "search_result_clicked";
    public const string SearchProjectLaunched = "search_project_launched";
    public const string PathSearchExecuted = "path_search_executed";
    public const string PathResultClicked = "path_result_clicked";

    // Smart Project Search
    public const string SmartSearchExecuted = "smart_search_executed";
    public const string SmartSearchResultClicked = "smart_search_result_clicked";
    public const string SmartSearchFilterUsed = "smart_search_filter_used";

    // Doc Quick Open
    public const string DocSearchExecuted = "doc_search_executed";
    public const string DocOpened = "doc_opened";

    // Frequent Projects
    public const string FrequentProjectOpened = "frequent_project_opened";

    // Quick Launch
    public const string QuickLaunchItemLaunched = "quick_launch_item_launched";
    public const string QuickLaunchItemAdded = "quick_launch_item_added";
    public const string QuickLaunchItemRemoved = "quick_launch_item_removed";

    // Quick Tasks
    public const string TaskCreated = "task_created";
    public const string TaskCompleted = "task_completed";
    public const string TaskDeleted = "task_deleted";

    // Timer
    public const string TimerStarted = "timer_started";
    public const string TimerStopped = "timer_stopped";

    // CheatSheet
    public const string CheatSheetViewed = "cheat_sheet_viewed";
    public const string CheatSheetSearched = "cheat_sheet_searched";
    public const string CheatSheetLookup = "cheat_sheet_lookup";
    public const string CheatSheetMcaLookup = "cheat_sheet_mca_lookup";
    public const string CheatSheetCopied = "cheat_sheet_copied";
    public const string CheatSheetViewModeChanged = "cheat_sheet_view_mode_changed";
    public const string CheatSheetDisciplineChanged = "cheat_sheet_discipline_changed";
    public const string CheatSheetEdited = "cheat_sheet_edited";

    // Widget visibility
    public const string WidgetOpened = "widget_opened";
    public const string WidgetClosed = "widget_closed";

    // Hotkey
    public const string HotkeyPressed = "hotkey_pressed";

    // Settings
    public const string SettingChanged = "setting_changed";

    // Filter
    public const string FilterChanged = "filter_changed";
    public const string DisciplineChanged = "discipline_changed";

    // Clipboard
    public const string ClipboardCopy = "clipboard_copy";

    // Error
    public const string AppError = "app_error";
    public const string WidgetError = "widget_error";

    // Performance
    public const string StartupTiming = "startup_timing";
    public const string SearchTiming = "search_timing";

    // Tags
    public const string TagCreated = "tag_created";
    public const string TagUpdated = "tag_updated";
    public const string TagDeleted = "tag_deleted";
    public const string TagSearchExecuted = "tag_search_executed";
    public const string TagCarouselClicked = "tag_carousel_clicked";

    // Field Management (editor operations on master/project structure)
    public const string MasterFieldAdded = "master_field_added";
    public const string MasterCategoryAdded = "master_category_added";
    public const string MasterDropdownExtended = "master_dropdown_extended";
    public const string ProjectFieldAdded = "project_field_added";
    public const string ProjectCategoryAdded = "project_category_added";
    public const string ProjectDropdownExtended = "project_dropdown_extended";
    public const string MasterFieldRemoved = "master_field_removed";
    public const string MasterCategoryRemoved = "master_category_removed";
    public const string ProjectFieldRemoved = "project_field_removed";
    public const string ProjectCategoryRemoved = "project_category_removed";
}

/// <summary>
/// A single telemetry event stored in local SQLite
/// </summary>
public class TelemetryEvent
{
    public long Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? SessionId { get; set; }

    // Flexible key-value data serialized as JSON
    public string? DataJson { get; set; }

    // Denormalized fields for fast querying on common dimensions
    public string? WidgetName { get; set; }
    public string? QueryText { get; set; }
    public string? ProjectNumber { get; set; }
    public string? ProjectType { get; set; }
    public string? Discipline { get; set; }
    public string? FileExtension { get; set; }
    public int? ResultCount { get; set; }
    public int? ResultIndex { get; set; }
    public long? DurationMs { get; set; }
    public int? CharCount { get; set; }

    /// <summary>How the query was initiated (typed, pasted, frequent_project, quick_launch, history, etc.)</summary>
    public string? QuerySource { get; set; }

    // Synced to Firebase?
    public bool Synced { get; set; }
}

/// <summary>
/// Aggregated daily metrics for Firebase sync
/// </summary>
public class DailyMetricsSummary
{
    public string Date { get; set; } = string.Empty;
    public int SessionCount { get; set; }
    public long TotalSessionDurationMs { get; set; }
    public int TotalSearches { get; set; }
    public int TotalSmartSearches { get; set; }
    public int TotalDocSearches { get; set; }
    public int TotalPathSearches { get; set; }
    public int TotalProjectLaunches { get; set; }
    public int TotalQuickLaunchUses { get; set; }
    public int TotalQuickLaunchAdds { get; set; }
    public int TotalQuickLaunchRemoves { get; set; }
    public int TotalTasksCreated { get; set; }
    public int TotalTasksCompleted { get; set; }
    public int TotalDocOpens { get; set; }
    public int TotalTimerUses { get; set; }
    public int TotalCheatSheetViews { get; set; }
    public Dictionary<string, int> WidgetUsageCounts { get; set; } = new();
    public Dictionary<string, int> ProjectTypeFrequency { get; set; } = new();
    public Dictionary<string, int> DisciplineFrequency { get; set; } = new();
    public List<SearchQueryCount> TopSearchQueries { get; set; } = new();
    public int TotalHotkeyPresses { get; set; }
    public int TotalFilterChanges { get; set; }
    public int TotalClipboardCopies { get; set; }
    public int TotalErrors { get; set; }
    public int TotalTagsCreated { get; set; }
    public int TotalTagsUpdated { get; set; }
    public int TotalTagSearches { get; set; }
    public int TotalTagCarouselClicks { get; set; }
    public int TotalCheatSheetLookups { get; set; }
    public int TotalCheatSheetCopies { get; set; }
    public int TotalCheatSheetSearches { get; set; }
    public Dictionary<string, int> CheatSheetUsageFrequency { get; set; } = new();
    public Dictionary<string, int> CheatSheetLookupFrequency { get; set; } = new();
    public Dictionary<string, int> CheatSheetCopyFrequency { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> CheatSheetInteractions { get; set; } = new();

    // Task deletion count
    public int TotalTasksDeleted { get; set; }

    // Setting change count
    public int TotalSettingChanges { get; set; }

    // Smart search filter usage
    public int TotalSmartSearchFilterUses { get; set; }

    // Performance timing averages (ms)
    public double AvgStartupTimingMs { get; set; }
    public double AvgSearchTimingMs { get; set; }

    // Average search result click position (0-based index)
    public double AvgSearchResultClickPosition { get; set; }
    public int TotalSearchResultClicks { get; set; }

    // File extension frequency from doc opens
    public Dictionary<string, int> FileExtensionFrequency { get; set; } = new();

    // Device/user identity for admin telemetry
    public string DeviceName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
}

/// <summary>
/// A search query with its frequency count and source breakdown
/// </summary>
public class SearchQueryCount
{
    public string Query { get; set; } = string.Empty;
    public int Count { get; set; }
    /// <summary>Primary source for this query (most common initiation method)</summary>
    public string PrimarySource { get; set; } = string.Empty;
    /// <summary>Source breakdown: source -> count</summary>
    public Dictionary<string, int> SourceBreakdown { get; set; } = new();
}

/// <summary>
/// Constants for query source tracking — how a search was initiated
/// </summary>
public static class QuerySources
{
    public const string Typed = "typed";
    public const string Pasted = "pasted";
    public const string FrequentProject = "frequent_project";
    public const string QuickLaunch = "quick_launch";
    public const string History = "history";
    public const string SmartSearch = "smart_search";
    public const string PathSearch = "path_search";
    public const string DocSearch = "doc_search";
    public const string HotkeyDirect = "hotkey_direct";
    public const string WidgetLauncher = "widget_launcher";
    public const string TagCarousel = "tag_carousel";
    public const string TagSearch = "tag_search";

    public static string DisplayName(string source) => source switch
    {
        Typed => "Typed",
        Pasted => "Pasted",
        FrequentProject => "Freq. Project",
        QuickLaunch => "Quick Launch",
        History => "History",
        SmartSearch => "Smart Search",
        PathSearch => "Path Search",
        DocSearch => "Doc Search",
        HotkeyDirect => "Hotkey",
        WidgetLauncher => "Launcher",
        TagCarousel => "Tag Carousel",
        TagSearch => "Tag Search",
        _ => source
    };
}

/// <summary>
/// Information about a known user/device for admin metrics view
/// </summary>
public class MetricsUserInfo
{
    public string DeviceId { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime LastSeen { get; set; }
}

/// <summary>
/// Hourly event counts for a single day — used for activity heatmap
/// </summary>
public class HourlyBreakdown
{
    public int[] EventCounts { get; set; } = new int[24];
    public int PeakHour { get; set; }
    public int PeakCount { get; set; }
}

/// <summary>
/// A single session's summary (start, end, what was done)
/// </summary>
public class SessionDetail
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public long DurationMs { get; set; }
    public int EventCount { get; set; }
    public Dictionary<string, int> ActionBreakdown { get; set; } = new();
}

/// <summary>
/// A frequently accessed project with counts
/// </summary>
public class TopProjectInfo
{
    public string ProjectNumber { get; set; } = string.Empty;
    public string? ProjectType { get; set; }
    public int LaunchCount { get; set; }
    public int SearchCount { get; set; }
    public int DocOpenCount { get; set; }
    public int TotalInteractions => LaunchCount + SearchCount + DocOpenCount;
}

/// <summary>
/// Derived usage insights for a date range
/// </summary>
public class UsageInsights
{
    public double AvgSessionDurationMin { get; set; }
    public int PeakHour { get; set; }
    public int ActiveDays { get; set; }
    public int TotalDaysInRange { get; set; }
    public int CurrentStreak { get; set; }
    public double ProductivityScore { get; set; }
    public string MostUsedFeature { get; set; } = string.Empty;
    public int MostUsedFeatureCount { get; set; }
    public Dictionary<string, double> FeatureWeights { get; set; } = new();
}

/// <summary>
/// Multi-day trend data point for sparkline charts
/// </summary>
public class TrendDataPoint
{
    public string Date { get; set; } = string.Empty;
    public int Value { get; set; }
}

/// <summary>
/// Feature-to-feature transition for activity flow analysis
/// </summary>
public class FeatureTransition
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public int Count { get; set; }
}
