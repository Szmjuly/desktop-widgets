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

    // Widget visibility
    public const string WidgetOpened = "widget_opened";
    public const string WidgetClosed = "widget_closed";
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
    public List<string> TopSearchQueries { get; set; } = new();
}
