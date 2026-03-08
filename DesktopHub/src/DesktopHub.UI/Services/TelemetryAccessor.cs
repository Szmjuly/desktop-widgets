using DesktopHub.Core.Abstractions;
using DesktopHub.Core.Models;

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
        string? widgetName = null, Dictionary<string, object?>? extraData = null,
        string? querySource = null)
    {
        _service?.TrackSearch(eventType, queryText, resultCount, resultIndex, timeToClickMs, widgetName, extraData, querySource);
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

    public static void TrackHotkey(string hotkeyGroup, int widgetCount)
    {
        _service?.TrackEvent(TelemetryCategory.Hotkey, TelemetryEventType.HotkeyPressed,
            new Dictionary<string, object?> { ["hotkeyGroup"] = hotkeyGroup, ["widgetCount"] = widgetCount });
    }

    public static void TrackSettingChanged(string settingName, string? newValue = null)
    {
        _service?.TrackEvent(TelemetryCategory.Settings, TelemetryEventType.SettingChanged,
            new Dictionary<string, object?> { ["settingName"] = settingName, ["newValue"] = newValue });
    }

    public static void TrackFilterChanged(string filterType, string? filterValue = null)
    {
        _service?.TrackEvent(TelemetryCategory.Filter, TelemetryEventType.FilterChanged,
            new Dictionary<string, object?> { ["filterType"] = filterType, ["filterValue"] = filterValue });
    }

    public static void TrackClipboardCopy(string copyType, string? widgetName = null)
    {
        _service?.TrackEvent(TelemetryCategory.Clipboard, TelemetryEventType.ClipboardCopy,
            new Dictionary<string, object?> { ["copyType"] = copyType, ["widgetName"] = widgetName });
    }

    public static void TrackError(string eventType, string errorType, string context, string? message = null)
    {
        _service?.TrackEvent(TelemetryCategory.Error, eventType,
            new Dictionary<string, object?> { ["errorType"] = errorType, ["context"] = context, ["message"] = message });
    }

    public static void TrackPerformance(string eventType, string phase, long durationMs, int? resultCount = null)
    {
        _service?.TrackEvent(TelemetryCategory.Performance, eventType,
            new Dictionary<string, object?> { ["phase"] = phase, ["durationMs"] = durationMs, ["resultCount"] = resultCount });
    }

    public static void TrackTag(string eventType, string? projectNumber = null, string? tagKey = null,
        string? tagValue = null, int? tagCount = null, string? source = null)
    {
        _service?.TrackEvent(TelemetryCategory.Tag, eventType,
            new Dictionary<string, object?>
            {
                ["projectNumber"] = projectNumber,
                ["tagKey"] = tagKey,
                ["tagValue"] = tagValue,
                ["tagCount"] = tagCount,
                ["source"] = source
            });
    }
}
