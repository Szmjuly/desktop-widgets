using System.Collections.Concurrent;
using System.Text.Json;
using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;
using DesktopHub.Infrastructure.Data;
using DesktopHub.Infrastructure.Firebase;
using DesktopHub.Infrastructure.Logging;

namespace DesktopHub.Infrastructure.Telemetry;

/// <summary>
/// Telemetry service that collects events into a local SQLite database
/// and periodically syncs aggregated summaries to Firebase.
/// Events are fire-and-forget queued to avoid blocking UI threads.
/// </summary>
public class TelemetryService : ITelemetryService, IDisposable
{
    private readonly MetricsDatabase _db;
    private IFirebaseService? _firebaseService;
    private readonly ConcurrentQueue<TelemetryEvent> _eventQueue = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _flushLock = new(1, 1);
    private Timer? _flushTimer;
    private Timer? _syncTimer;
    private Timer? _purgeTimer;
    private DateTime _sessionStart;
    private bool _disposed;

    private const int FlushIntervalMs = 5_000;       // Flush queue to SQLite every 5s
    private const int SyncIntervalMs = 30 * 60_000;  // Sync to Firebase every 30 min
    private const int PurgeIntervalMs = 24 * 60 * 60_000; // Purge check daily
    private const int MaxQueueSize = 500;             // Force flush if queue exceeds this

    public string SessionId { get; private set; } = Guid.NewGuid().ToString("N")[..12];
    public bool IsEnabled { get; set; } = true;

    public TelemetryService(MetricsDatabase db, IFirebaseService? firebaseService = null)
    {
        _db = db;
        _firebaseService = firebaseService;
    }

    /// <summary>
    /// Late-bind the Firebase service after it finishes initializing.
    /// This allows telemetry to start collecting locally before Firebase is ready.
    /// </summary>
    public void SetFirebaseService(IFirebaseService firebaseService)
    {
        _firebaseService = firebaseService;
        InfraLogger.Log("TelemetryService: Firebase service connected for sync");
    }

    public async Task InitializeAsync()
    {
        try
        {
            await _db.InitializeAsync();
            _sessionStart = DateTime.UtcNow;

            // Start background flush timer
            _flushTimer = new Timer(
                async _ => await FlushQueueAsync(),
                null,
                TimeSpan.FromMilliseconds(FlushIntervalMs),
                TimeSpan.FromMilliseconds(FlushIntervalMs));

            // Start Firebase sync timer
            _syncTimer = new Timer(
                async _ => await SyncToFirebaseAsync(),
                null,
                TimeSpan.FromMinutes(5),  // First sync after 5 min
                TimeSpan.FromMilliseconds(SyncIntervalMs));

            // Start daily purge timer
            _purgeTimer = new Timer(
                async _ => await PurgeOldEventsAsync(),
                null,
                TimeSpan.FromHours(1),
                TimeSpan.FromMilliseconds(PurgeIntervalMs));

            // Record session start
            TrackEvent(TelemetryCategory.Session, TelemetryEventType.SessionStart);

            InfraLogger.Log($"TelemetryService: Initialized, session={SessionId}");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"TelemetryService: Initialization failed: {ex.Message}");
        }
    }

    public void TrackEvent(string category, string eventType, Dictionary<string, object?>? data = null)
    {
        if (!IsEnabled) return;

        var evt = new TelemetryEvent
        {
            Category = category,
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            SessionId = SessionId,
            DataJson = data != null ? JsonSerializer.Serialize(data) : null
        };

        _eventQueue.Enqueue(evt);

        // Force flush if queue is getting large
        if (_eventQueue.Count > MaxQueueSize)
        {
            _ = Task.Run(() => FlushQueueAsync());
        }
    }

    public void TrackSearch(string eventType, string? queryText, int? resultCount = null,
        int? resultIndex = null, long? timeToClickMs = null,
        string? widgetName = null, Dictionary<string, object?>? extraData = null)
    {
        if (!IsEnabled) return;

        var evt = new TelemetryEvent
        {
            Category = TelemetryCategory.Search,
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            SessionId = SessionId,
            QueryText = queryText,
            ResultCount = resultCount,
            ResultIndex = resultIndex,
            DurationMs = timeToClickMs,
            WidgetName = widgetName,
            DataJson = extraData != null ? JsonSerializer.Serialize(extraData) : null
        };

        _eventQueue.Enqueue(evt);
    }

    public void TrackProjectLaunch(string source, string? projectNumber, string? projectType = null)
    {
        if (!IsEnabled) return;

        var evt = new TelemetryEvent
        {
            Category = TelemetryCategory.ProjectLaunch,
            EventType = TelemetryEventType.SearchProjectLaunched,
            Timestamp = DateTime.UtcNow,
            SessionId = SessionId,
            ProjectNumber = projectNumber,
            ProjectType = projectType,
            WidgetName = source,
            DataJson = JsonSerializer.Serialize(new { source })
        };

        _eventQueue.Enqueue(evt);
    }

    public void TrackWidgetVisibility(string widgetName, bool opened)
    {
        if (!IsEnabled) return;

        var evt = new TelemetryEvent
        {
            Category = TelemetryCategory.Widget,
            EventType = opened ? TelemetryEventType.WidgetOpened : TelemetryEventType.WidgetClosed,
            Timestamp = DateTime.UtcNow,
            SessionId = SessionId,
            WidgetName = widgetName
        };

        _eventQueue.Enqueue(evt);
    }

    public void TrackDocAccess(string eventType, string? discipline = null,
        string? fileExtension = null, string? projectType = null,
        string? queryText = null, int? resultCount = null)
    {
        if (!IsEnabled) return;

        var evt = new TelemetryEvent
        {
            Category = TelemetryCategory.DocAccess,
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            SessionId = SessionId,
            Discipline = discipline,
            FileExtension = fileExtension,
            ProjectType = projectType,
            QueryText = queryText,
            ResultCount = resultCount,
            WidgetName = "DocQuickOpen"
        };

        _eventQueue.Enqueue(evt);
    }

    public void TrackQuickLaunch(string eventType, string? itemType = null, int? slotIndex = null)
    {
        if (!IsEnabled) return;

        var evt = new TelemetryEvent
        {
            Category = TelemetryCategory.QuickLaunch,
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            SessionId = SessionId,
            WidgetName = "QuickLaunch",
            ResultIndex = slotIndex,
            DataJson = itemType != null ? JsonSerializer.Serialize(new { item_type = itemType }) : null
        };

        _eventQueue.Enqueue(evt);
    }

    public void TrackQuickTask(string eventType, int? charCount = null,
        long? durationMs = null, int? taskCountAtTime = null)
    {
        if (!IsEnabled) return;

        var evt = new TelemetryEvent
        {
            Category = TelemetryCategory.QuickTask,
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            SessionId = SessionId,
            WidgetName = "QuickTasks",
            CharCount = charCount,
            DurationMs = durationMs,
            DataJson = taskCountAtTime != null
                ? JsonSerializer.Serialize(new { task_count = taskCountAtTime })
                : null
        };

        _eventQueue.Enqueue(evt);
    }

    public void TrackTimer(string eventType, long? durationSeconds = null)
    {
        if (!IsEnabled) return;

        var evt = new TelemetryEvent
        {
            Category = TelemetryCategory.Timer,
            EventType = eventType,
            Timestamp = DateTime.UtcNow,
            SessionId = SessionId,
            WidgetName = "Timer",
            DurationMs = durationSeconds.HasValue ? durationSeconds.Value * 1000 : null
        };

        _eventQueue.Enqueue(evt);
    }

    public void TrackCheatSheet(string sheetId, long? timeVisibleMs = null)
    {
        if (!IsEnabled) return;

        var evt = new TelemetryEvent
        {
            Category = TelemetryCategory.CheatSheet,
            EventType = TelemetryEventType.CheatSheetViewed,
            Timestamp = DateTime.UtcNow,
            SessionId = SessionId,
            WidgetName = "CheatSheet",
            DurationMs = timeVisibleMs,
            DataJson = JsonSerializer.Serialize(new { sheet_id = sheetId })
        };

        _eventQueue.Enqueue(evt);
    }

    public async Task EndSessionAsync()
    {
        var sessionDuration = (long)(DateTime.UtcNow - _sessionStart).TotalMilliseconds;

        var evt = new TelemetryEvent
        {
            Category = TelemetryCategory.Session,
            EventType = TelemetryEventType.SessionEnd,
            Timestamp = DateTime.UtcNow,
            SessionId = SessionId,
            DurationMs = sessionDuration
        };

        _eventQueue.Enqueue(evt);

        // Final flush
        await FlushQueueAsync();

        // Final sync
        await SyncToFirebaseAsync();

        InfraLogger.Log($"TelemetryService: Session ended, duration={sessionDuration}ms");
    }

    public async Task SyncToFirebaseAsync()
    {
        if (_firebaseService == null || !_firebaseService.IsInitialized) return;

        try
        {
            var unsyncedDates = await _db.GetUnsyncedDatesAsync();
            if (unsyncedDates.Count == 0) return;

            foreach (var dateStr in unsyncedDates)
            {
                if (!DateTime.TryParse(dateStr, out var date)) continue;

                var summary = await _db.GetDailySummaryAsync(date);
                if (summary == null) continue;

                // Push to Firebase under metrics/{deviceId}/{date}
                var deviceId = _firebaseService.GetDeviceId();
                var data = new Dictionary<string, object>
                {
                    ["date"] = summary.Date,
                    ["session_count"] = summary.SessionCount,
                    ["total_session_duration_ms"] = summary.TotalSessionDurationMs,
                    ["total_searches"] = summary.TotalSearches,
                    ["total_smart_searches"] = summary.TotalSmartSearches,
                    ["total_doc_searches"] = summary.TotalDocSearches,
                    ["total_path_searches"] = summary.TotalPathSearches,
                    ["total_project_launches"] = summary.TotalProjectLaunches,
                    ["total_quick_launch_uses"] = summary.TotalQuickLaunchUses,
                    ["total_quick_launch_adds"] = summary.TotalQuickLaunchAdds,
                    ["total_quick_launch_removes"] = summary.TotalQuickLaunchRemoves,
                    ["total_tasks_created"] = summary.TotalTasksCreated,
                    ["total_tasks_completed"] = summary.TotalTasksCompleted,
                    ["total_doc_opens"] = summary.TotalDocOpens,
                    ["total_timer_uses"] = summary.TotalTimerUses,
                    ["total_cheat_sheet_views"] = summary.TotalCheatSheetViews,
                    ["widget_usage"] = summary.WidgetUsageCounts,
                    ["project_type_frequency"] = summary.ProjectTypeFrequency,
                    ["discipline_frequency"] = summary.DisciplineFrequency,
                    ["top_search_queries"] = summary.TopSearchQueries,
                    ["synced_at"] = DateTime.UtcNow.ToString("O")
                };

                await _firebaseService.LogUsageEventAsync("daily_metrics_summary", data);

                // Mark events as synced
                await _db.MarkEventsSyncedAsync(date, date.AddDays(1));
            }

            InfraLogger.Log($"TelemetryService: Synced {unsyncedDates.Count} day(s) to Firebase");
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"TelemetryService: Firebase sync failed: {ex.Message}");
        }
    }

    public async Task<DailyMetricsSummary?> GetDailySummaryAsync(DateTime date)
    {
        return await _db.GetDailySummaryAsync(date);
    }

    public async Task<List<TelemetryEvent>> GetRecentSearchEventsAsync(int count = 100)
    {
        return await _db.GetRecentSearchEventsAsync(count);
    }

    public async Task<Dictionary<string, int>> GetEventCountsAsync(DateTime from, DateTime to)
    {
        return await _db.GetEventCountsByTypeAsync(from, to);
    }

    public async Task PurgeOldEventsAsync(int retainDays = 90)
    {
        await _db.PurgeOldEventsAsync(retainDays);
    }

    private async Task FlushQueueAsync()
    {
        if (_eventQueue.IsEmpty) return;
        if (!await _flushLock.WaitAsync(0)) return; // Skip if already flushing

        try
        {
            var batch = new List<TelemetryEvent>();
            while (_eventQueue.TryDequeue(out var evt))
            {
                batch.Add(evt);
                if (batch.Count >= 200) break; // Flush in chunks
            }

            if (batch.Count > 0)
            {
                await _db.InsertEventsBatchAsync(batch);
            }
        }
        catch (Exception ex)
        {
            InfraLogger.Log($"TelemetryService: Flush failed: {ex.Message}");
        }
        finally
        {
            _flushLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _flushTimer?.Dispose();
        _syncTimer?.Dispose();
        _purgeTimer?.Dispose();
        _flushLock.Dispose();
        _cts.Dispose();
    }
}
