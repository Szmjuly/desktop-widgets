using DesktopHub.Core.Abstractions;

namespace DesktopHub.UI.Services;

/// <summary>
/// Static accessor for the telemetry service so widgets can log events
/// without constructor injection. Initialized by App.xaml.cs.
/// All methods are null-safe — if telemetry is not initialized, calls are no-ops.
/// </summary>
public static class TelemetryAccessor
{
    private static ITelemetryService? _service;

    public static ITelemetryService? Service => _service;

    public static void Initialize(ITelemetryService service)
    {
        _service = service;
    }

    public static void TrackEvent(string category, string eventType, Dictionary<string, object?>? data = null)
    {
        _service?.TrackEvent(category, eventType, data);
    }

    public static void TrackSearch(string eventType, string? queryText, int? resultCount = null,
        int? resultIndex = null, long? timeToClickMs = null,
        string? widgetName = null, Dictionary<string, object?>? extraData = null)
    {
        _service?.TrackSearch(eventType, queryText, resultCount, resultIndex, timeToClickMs, widgetName, extraData);
    }

    public static void TrackProjectLaunch(string source, string? projectNumber, string? projectType = null)
    {
        _service?.TrackProjectLaunch(source, projectNumber, projectType);
    }

    public static void TrackWidgetVisibility(string widgetName, bool opened)
    {
        _service?.TrackWidgetVisibility(widgetName, opened);
    }

    public static void TrackDocAccess(string eventType, string? discipline = null,
        string? fileExtension = null, string? projectType = null,
        string? queryText = null, int? resultCount = null)
    {
        _service?.TrackDocAccess(eventType, discipline, fileExtension, projectType, queryText, resultCount);
    }

    public static void TrackQuickLaunch(string eventType, string? itemType = null, int? slotIndex = null)
    {
        _service?.TrackQuickLaunch(eventType, itemType, slotIndex);
    }

    public static void TrackQuickTask(string eventType, int? charCount = null,
        long? durationMs = null, int? taskCountAtTime = null)
    {
        _service?.TrackQuickTask(eventType, charCount, durationMs, taskCountAtTime);
    }

    public static void TrackTimer(string eventType, long? durationSeconds = null)
    {
        _service?.TrackTimer(eventType, durationSeconds);
    }

    public static void TrackCheatSheet(string sheetId, long? timeVisibleMs = null)
    {
        _service?.TrackCheatSheet(sheetId, timeVisibleMs);
    }
}
