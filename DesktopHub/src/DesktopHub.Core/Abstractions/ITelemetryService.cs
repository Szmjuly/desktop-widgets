using DesktopHub.Core.Models;

namespace DesktopHub.Core.Abstractions;

/// <summary>
/// Telemetry collection service for tracking app usage metrics.
/// Events are stored locally in SQLite and periodically synced to Firebase.
/// </summary>
public interface ITelemetryService
{
    /// <summary>
    /// Initialize the telemetry database and start a new session
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Current session ID
    /// </summary>
    string SessionId { get; }

    /// <summary>
    /// Whether telemetry collection is enabled (user opt-in setting)
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Track a generic telemetry event
    /// </summary>
    void TrackEvent(string category, string eventType, Dictionary<string, object?>? data = null);

    /// <summary>
    /// Track a search event with query and result details
    /// </summary>
    void TrackSearch(string eventType, string? queryText, int? resultCount = null,
        int? resultIndex = null, long? timeToClickMs = null,
        string? widgetName = null, Dictionary<string, object?>? extraData = null);

    /// <summary>
    /// Track a project launch event
    /// </summary>
    void TrackProjectLaunch(string source, string? projectNumber, string? projectType = null);

    /// <summary>
    /// Track a widget visibility event (opened/closed)
    /// </summary>
    void TrackWidgetVisibility(string widgetName, bool opened);

    /// <summary>
    /// Track a doc access event
    /// </summary>
    void TrackDocAccess(string eventType, string? discipline = null,
        string? fileExtension = null, string? projectType = null,
        string? queryText = null, int? resultCount = null);

    /// <summary>
    /// Track a Quick Launch event (add/remove/launch)
    /// </summary>
    void TrackQuickLaunch(string eventType, string? itemType = null,
        int? slotIndex = null);

    /// <summary>
    /// Track a Quick Task event (create/complete/delete)
    /// </summary>
    void TrackQuickTask(string eventType, int? charCount = null,
        long? durationMs = null, int? taskCountAtTime = null);

    /// <summary>
    /// Track timer start/stop
    /// </summary>
    void TrackTimer(string eventType, long? durationSeconds = null);

    /// <summary>
    /// Track cheat sheet view
    /// </summary>
    void TrackCheatSheet(string sheetId, long? timeVisibleMs = null);

    /// <summary>
    /// End the current session and record duration
    /// </summary>
    Task EndSessionAsync();

    /// <summary>
    /// Sync aggregated metrics to Firebase
    /// </summary>
    Task SyncToFirebaseAsync();

    /// <summary>
    /// Get daily summary for a given date (for metrics widget)
    /// </summary>
    Task<DailyMetricsSummary?> GetDailySummaryAsync(DateTime date);

    /// <summary>
    /// Get recent search queries (for analysis/ML training data)
    /// </summary>
    Task<List<TelemetryEvent>> GetRecentSearchEventsAsync(int count = 100);

    /// <summary>
    /// Get event count by category for a date range
    /// </summary>
    Task<Dictionary<string, int>> GetEventCountsAsync(DateTime from, DateTime to);

    /// <summary>
    /// Purge events older than the specified number of days
    /// </summary>
    Task PurgeOldEventsAsync(int retainDays = 90);
}
