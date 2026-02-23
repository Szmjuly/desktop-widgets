using System.Text.Json;
using DesktopHub.Core.Abstractions;

namespace DesktopHub.Infrastructure.Settings;

/// <summary>
/// Settings service using JSON file storage
/// </summary>
public class SettingsService : ISettingsService
{
    private const int DefaultHotkeyModifiers = 0x0003; // Ctrl+Alt
    private const int DefaultHotkeyKey = 0x20; // Space
    private const int LegacyHotkeyModifiers = 0x0006; // Ctrl+Shift
    private const int LegacyHotkeyKey = 0x50; // P
    private const int DefaultCloseShortcutModifiers = 0x0000; // No modifiers
    private const int DefaultCloseShortcutKey = 0x1B; // ESC
    private readonly string _settingsPath;
    private AppSettings _settings;

    public SettingsService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "DesktopHub"
        );
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, "settings.json");
        _settings = new AppSettings();
    }

    public string GetQDrivePath() => _settings.QDrivePath;
    public void SetQDrivePath(string path) => _settings.QDrivePath = path;

    public bool GetQDriveEnabled() => _settings.QDriveEnabled;
    public void SetQDriveEnabled(bool enabled) => _settings.QDriveEnabled = enabled;

    public string GetPDrivePath() => _settings.PDrivePath;
    public void SetPDrivePath(string path) => _settings.PDrivePath = path;

    public bool GetPDriveEnabled() => _settings.PDriveEnabled;
    public void SetPDriveEnabled(bool enabled) => _settings.PDriveEnabled = enabled;

    public int GetScanIntervalMinutes() => _settings.ScanIntervalMinutes;
    public void SetScanIntervalMinutes(int minutes) => _settings.ScanIntervalMinutes = minutes;

    public (int modifiers, int key) GetHotkey() => (_settings.HotkeyModifiers, _settings.HotkeyKey);
    public void SetHotkey(int modifiers, int key)
    {
        _settings.HotkeyModifiers = modifiers;
        _settings.HotkeyKey = key;
    }

    public string GetTheme() => _settings.Theme;
    public void SetTheme(string theme) => _settings.Theme = theme;

    public bool GetAutoStart() => _settings.AutoStart;
    public void SetAutoStart(bool enabled) => _settings.AutoStart = enabled;

    public double GetSettingsTransparency() => _settings.SettingsTransparency;
    public void SetSettingsTransparency(double transparency) => _settings.SettingsTransparency = transparency;

    public double GetOverlayTransparency() => _settings.OverlayTransparency;
    public void SetOverlayTransparency(double transparency) => _settings.OverlayTransparency = transparency;

    public bool GetSettingsTransparencyLinked() => _settings.SettingsTransparencyLinked;
    public void SetSettingsTransparencyLinked(bool linked) => _settings.SettingsTransparencyLinked = linked;

    public bool GetOverlayTransparencyLinked() => _settings.OverlayTransparencyLinked;
    public void SetOverlayTransparencyLinked(bool linked) => _settings.OverlayTransparencyLinked = linked;

    public bool GetLauncherTransparencyLinked() => _settings.LauncherTransparencyLinked;
    public void SetLauncherTransparencyLinked(bool linked) => _settings.LauncherTransparencyLinked = linked;

    public bool GetTimerTransparencyLinked() => _settings.TimerTransparencyLinked;
    public void SetTimerTransparencyLinked(bool linked) => _settings.TimerTransparencyLinked = linked;

    public double GetWidgetLauncherTransparency() => _settings.WidgetLauncherTransparency;
    public void SetWidgetLauncherTransparency(double transparency) => _settings.WidgetLauncherTransparency = transparency;

    public double GetTimerWidgetTransparency() => _settings.TimerWidgetTransparency;
    public void SetTimerWidgetTransparency(double transparency) => _settings.TimerWidgetTransparency = transparency;

    public int GetNotificationDurationMs() => _settings.NotificationDurationMs;
    public void SetNotificationDurationMs(int durationMs) => _settings.NotificationDurationMs = durationMs;

    // --- Path Search ---
    public bool GetPathSearchEnabled() => _settings.PathSearchEnabled;
    public void SetPathSearchEnabled(bool enabled) => _settings.PathSearchEnabled = enabled;
    public bool GetPathSearchShowSubDirs() => _settings.PathSearchShowSubDirs;
    public void SetPathSearchShowSubDirs(bool enabled) => _settings.PathSearchShowSubDirs = enabled;
    public bool GetPathSearchShowSubFiles() => _settings.PathSearchShowSubFiles;
    public void SetPathSearchShowSubFiles(bool enabled) => _settings.PathSearchShowSubFiles = enabled;
    public bool GetPathSearchShowHidden() => _settings.PathSearchShowHidden;
    public void SetPathSearchShowHidden(bool enabled) => _settings.PathSearchShowHidden = enabled;

    public bool GetLivingWidgetsMode() => _settings.LivingWidgetsMode;
    public void SetLivingWidgetsMode(bool enabled) => _settings.LivingWidgetsMode = enabled;

    public int GetWidgetSnapGap() => Math.Clamp(_settings.WidgetSnapGap, 4, 64);
    public void SetWidgetSnapGap(int gapPixels) => _settings.WidgetSnapGap = Math.Clamp(gapPixels, 4, 64);
    
    public (double? left, double? top) GetSearchOverlayPosition() => (_settings.SearchOverlayLeft, _settings.SearchOverlayTop);
    public void SetSearchOverlayPosition(double left, double top)
    {
        _settings.SearchOverlayLeft = left;
        _settings.SearchOverlayTop = top;
    }
    
    public (double? left, double? top) GetWidgetLauncherPosition() => (_settings.WidgetLauncherLeft, _settings.WidgetLauncherTop);
    public void SetWidgetLauncherPosition(double left, double top)
    {
        _settings.WidgetLauncherLeft = left;
        _settings.WidgetLauncherTop = top;
    }

    public bool GetSearchOverlayVisible() => _settings.SearchOverlayVisible;
    public void SetSearchOverlayVisible(bool visible) => _settings.SearchOverlayVisible = visible;

    public bool GetWidgetLauncherVisible() => _settings.WidgetLauncherVisible;
    public void SetWidgetLauncherVisible(bool visible) => _settings.WidgetLauncherVisible = visible;

    public double GetQuickTasksWidgetTransparency() => _settings.QuickTasksWidgetTransparency;
    public void SetQuickTasksWidgetTransparency(double transparency) => _settings.QuickTasksWidgetTransparency = transparency;

    public bool GetQuickTasksTransparencyLinked() => _settings.QuickTasksTransparencyLinked;
    public void SetQuickTasksTransparencyLinked(bool linked) => _settings.QuickTasksTransparencyLinked = linked;

    public (double? left, double? top) GetTimerWidgetPosition() => (_settings.TimerWidgetLeft, _settings.TimerWidgetTop);
    public void SetTimerWidgetPosition(double left, double top)
    {
        _settings.TimerWidgetLeft = left;
        _settings.TimerWidgetTop = top;
    }

    public bool GetTimerWidgetVisible() => _settings.TimerWidgetVisible;
    public void SetTimerWidgetVisible(bool visible) => _settings.TimerWidgetVisible = visible;

    public (double? left, double? top) GetQuickTasksWidgetPosition() => (_settings.QuickTasksWidgetLeft, _settings.QuickTasksWidgetTop);
    public void SetQuickTasksWidgetPosition(double left, double top)
    {
        _settings.QuickTasksWidgetLeft = left;
        _settings.QuickTasksWidgetTop = top;
    }

    public bool GetQuickTasksWidgetVisible() => _settings.QuickTasksWidgetVisible;
    public void SetQuickTasksWidgetVisible(bool visible) => _settings.QuickTasksWidgetVisible = visible;

    public double GetDocWidgetTransparency() => _settings.DocWidgetTransparency;
    public void SetDocWidgetTransparency(double transparency) => _settings.DocWidgetTransparency = transparency;

    public bool GetDocTransparencyLinked() => _settings.DocTransparencyLinked;
    public void SetDocTransparencyLinked(bool linked) => _settings.DocTransparencyLinked = linked;

    public (double? left, double? top) GetDocWidgetPosition() => (_settings.DocWidgetLeft, _settings.DocWidgetTop);
    public void SetDocWidgetPosition(double left, double top)
    {
        _settings.DocWidgetLeft = left;
        _settings.DocWidgetTop = top;
    }

    public bool GetDocWidgetVisible() => _settings.DocWidgetVisible;
    public void SetDocWidgetVisible(bool visible) => _settings.DocWidgetVisible = visible;

    public bool GetTimerWidgetEnabled() => _settings.TimerWidgetEnabled;
    public void SetTimerWidgetEnabled(bool enabled) => _settings.TimerWidgetEnabled = enabled;

    public bool GetQuickTasksWidgetEnabled() => _settings.QuickTasksWidgetEnabled;
    public void SetQuickTasksWidgetEnabled(bool enabled) => _settings.QuickTasksWidgetEnabled = enabled;

    public bool GetDocWidgetEnabled() => _settings.DocWidgetEnabled;
    public void SetDocWidgetEnabled(bool enabled) => _settings.DocWidgetEnabled = enabled;

    public bool GetSearchWidgetEnabled() => _settings.SearchWidgetEnabled;
    public void SetSearchWidgetEnabled(bool enabled) => _settings.SearchWidgetEnabled = enabled;

    public bool GetAutoUpdateCheckEnabled() => _settings.AutoUpdateCheckEnabled;
    public void SetAutoUpdateCheckEnabled(bool enabled) => _settings.AutoUpdateCheckEnabled = enabled;

    public bool GetAutoUpdateInstallEnabled() => _settings.AutoUpdateInstallEnabled;
    public void SetAutoUpdateInstallEnabled(bool enabled) => _settings.AutoUpdateInstallEnabled = enabled;

    public int GetUpdateCheckFrequencyMinutes() => _settings.UpdateCheckFrequencyMinutes;
    public void SetUpdateCheckFrequencyMinutes(int minutes) => _settings.UpdateCheckFrequencyMinutes = minutes;

    // --- Frequent Projects Widget ---
    public double GetFrequentProjectsWidgetTransparency() => _settings.FrequentProjectsWidgetTransparency;
    public void SetFrequentProjectsWidgetTransparency(double transparency) => _settings.FrequentProjectsWidgetTransparency = transparency;
    public bool GetFrequentProjectsTransparencyLinked() => _settings.FrequentProjectsTransparencyLinked;
    public void SetFrequentProjectsTransparencyLinked(bool linked) => _settings.FrequentProjectsTransparencyLinked = linked;
    public (double? left, double? top) GetFrequentProjectsWidgetPosition() => (_settings.FrequentProjectsWidgetLeft, _settings.FrequentProjectsWidgetTop);
    public void SetFrequentProjectsWidgetPosition(double left, double top)
    {
        _settings.FrequentProjectsWidgetLeft = left;
        _settings.FrequentProjectsWidgetTop = top;
    }
    public bool GetFrequentProjectsWidgetVisible() => _settings.FrequentProjectsWidgetVisible;
    public void SetFrequentProjectsWidgetVisible(bool visible) => _settings.FrequentProjectsWidgetVisible = visible;
    public bool GetFrequentProjectsWidgetEnabled() => _settings.FrequentProjectsWidgetEnabled;
    public void SetFrequentProjectsWidgetEnabled(bool enabled) => _settings.FrequentProjectsWidgetEnabled = enabled;
    public int GetMaxFrequentProjectsShown() => _settings.MaxFrequentProjectsShown;
    public void SetMaxFrequentProjectsShown(int count) => _settings.MaxFrequentProjectsShown = count;
    public int GetMaxFrequentProjectsSaved() => _settings.MaxFrequentProjectsSaved;
    public void SetMaxFrequentProjectsSaved(int count) => _settings.MaxFrequentProjectsSaved = count;
    public bool GetFrequentProjectsGridMode() => _settings.FrequentProjectsGridMode;
    public void SetFrequentProjectsGridMode(bool gridMode) => _settings.FrequentProjectsGridMode = gridMode;

    // --- Quick Launch Widget ---
    public double GetQuickLaunchWidgetTransparency() => _settings.QuickLaunchWidgetTransparency;
    public void SetQuickLaunchWidgetTransparency(double transparency) => _settings.QuickLaunchWidgetTransparency = transparency;
    public bool GetQuickLaunchTransparencyLinked() => _settings.QuickLaunchTransparencyLinked;
    public void SetQuickLaunchTransparencyLinked(bool linked) => _settings.QuickLaunchTransparencyLinked = linked;
    public (double? left, double? top) GetQuickLaunchWidgetPosition() => (_settings.QuickLaunchWidgetLeft, _settings.QuickLaunchWidgetTop);
    public void SetQuickLaunchWidgetPosition(double left, double top)
    {
        _settings.QuickLaunchWidgetLeft = left;
        _settings.QuickLaunchWidgetTop = top;
    }
    public bool GetQuickLaunchWidgetVisible() => _settings.QuickLaunchWidgetVisible;
    public void SetQuickLaunchWidgetVisible(bool visible) => _settings.QuickLaunchWidgetVisible = visible;
    public bool GetQuickLaunchWidgetEnabled() => _settings.QuickLaunchWidgetEnabled;
    public void SetQuickLaunchWidgetEnabled(bool enabled) => _settings.QuickLaunchWidgetEnabled = enabled;
    public bool GetQuickLaunchHorizontalMode() => _settings.QuickLaunchHorizontalMode;
    public void SetQuickLaunchHorizontalMode(bool horizontal) => _settings.QuickLaunchHorizontalMode = horizontal;
    public int GetWidgetLauncherMaxVisibleWidgets() => Math.Clamp(_settings.WidgetLauncherMaxVisibleWidgets, 1, 12);
    public void SetWidgetLauncherMaxVisibleWidgets(int count) => _settings.WidgetLauncherMaxVisibleWidgets = Math.Clamp(count, 1, 12);

    // --- Smart Project Search Widget ---
    public (double? left, double? top) GetSmartProjectSearchWidgetPosition() => (_settings.SmartProjectSearchWidgetLeft, _settings.SmartProjectSearchWidgetTop);
    public void SetSmartProjectSearchWidgetPosition(double left, double top)
    {
        _settings.SmartProjectSearchWidgetLeft = left;
        _settings.SmartProjectSearchWidgetTop = top;
    }
    public bool GetSmartProjectSearchWidgetVisible() => _settings.SmartProjectSearchWidgetVisible;
    public void SetSmartProjectSearchWidgetVisible(bool visible) => _settings.SmartProjectSearchWidgetVisible = visible;
    public bool GetSmartProjectSearchWidgetEnabled() => _settings.SmartProjectSearchWidgetEnabled;
    public void SetSmartProjectSearchWidgetEnabled(bool enabled) => _settings.SmartProjectSearchWidgetEnabled = enabled;
    public bool GetSmartProjectSearchAttachToSearchOverlayMode() => _settings.SmartProjectSearchAttachToSearchOverlayMode;
    public void SetSmartProjectSearchAttachToSearchOverlayMode(bool enabled) => _settings.SmartProjectSearchAttachToSearchOverlayMode = enabled;
    public bool GetSmartProjectSearchWidgetEnabledBeforeAttachMode() => _settings.SmartProjectSearchWidgetEnabledBeforeAttachMode;
    public void SetSmartProjectSearchWidgetEnabledBeforeAttachMode(bool enabled) => _settings.SmartProjectSearchWidgetEnabledBeforeAttachMode = enabled;
    public string GetSmartProjectSearchLatestMode()
        => string.IsNullOrWhiteSpace(_settings.SmartProjectSearchLatestMode)
            ? "list"
            : _settings.SmartProjectSearchLatestMode;
    public void SetSmartProjectSearchLatestMode(string mode)
    {
        var normalized = (mode ?? string.Empty).Trim().ToLowerInvariant();
        _settings.SmartProjectSearchLatestMode = normalized == "single" ? "single" : "list";
    }
    public List<string> GetSmartProjectSearchFileTypes()
    {
        var values = _settings.SmartProjectSearchFileTypes ?? new List<string>();
        var normalized = values
            .Select(v => (v ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            normalized = GetDefaultSmartProjectSearchFileTypes();
            _settings.SmartProjectSearchFileTypes = normalized;
        }

        return normalized;
    }
    public void SetSmartProjectSearchFileTypes(IReadOnlyList<string> fileTypes)
    {
        var normalized = (fileTypes ?? Array.Empty<string>())
            .Select(v => (v ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _settings.SmartProjectSearchFileTypes = normalized.Count > 0
            ? normalized
            : GetDefaultSmartProjectSearchFileTypes();
    }

    // --- Hotkey Focus Behavior ---
    public bool GetHotkeyFocusWidgetLauncher() => _settings.HotkeyFocusWidgetLauncher;
    public void SetHotkeyFocusWidgetLauncher(bool enabled) => _settings.HotkeyFocusWidgetLauncher = enabled;
    public bool GetHotkeyFocusTimerWidget() => _settings.HotkeyFocusTimerWidget;
    public void SetHotkeyFocusTimerWidget(bool enabled) => _settings.HotkeyFocusTimerWidget = enabled;
    public bool GetHotkeyFocusQuickTasksWidget() => _settings.HotkeyFocusQuickTasksWidget;
    public void SetHotkeyFocusQuickTasksWidget(bool enabled) => _settings.HotkeyFocusQuickTasksWidget = enabled;
    public bool GetHotkeyFocusDocWidget() => _settings.HotkeyFocusDocWidget;
    public void SetHotkeyFocusDocWidget(bool enabled) => _settings.HotkeyFocusDocWidget = enabled;
    public bool GetHotkeyFocusFrequentProjectsWidget() => _settings.HotkeyFocusFrequentProjectsWidget;
    public void SetHotkeyFocusFrequentProjectsWidget(bool enabled) => _settings.HotkeyFocusFrequentProjectsWidget = enabled;
    public bool GetHotkeyFocusQuickLaunchWidget() => _settings.HotkeyFocusQuickLaunchWidget;
    public void SetHotkeyFocusQuickLaunchWidget(bool enabled) => _settings.HotkeyFocusQuickLaunchWidget = enabled;
    public bool GetHotkeyFocusSmartProjectSearchWidget() => _settings.HotkeyFocusSmartProjectSearchWidget;
    public void SetHotkeyFocusSmartProjectSearchWidget(bool enabled) => _settings.HotkeyFocusSmartProjectSearchWidget = enabled;

    public (int modifiers, int key) GetCloseShortcut() => (_settings.CloseShortcutModifiers, _settings.CloseShortcutKey);
    public void SetCloseShortcut(int modifiers, int key)
    {
        _settings.CloseShortcutModifiers = modifiers;
        _settings.CloseShortcutKey = key;
    }

    public async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(_settingsPath, json);
    }

    public async Task LoadAsync()
    {
        if (File.Exists(_settingsPath))
        {
            var json = await File.ReadAllTextAsync(_settingsPath);
            _settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();

            if (_settings.HotkeyModifiers == LegacyHotkeyModifiers && _settings.HotkeyKey == LegacyHotkeyKey)
            {
                _settings.HotkeyModifiers = DefaultHotkeyModifiers;
                _settings.HotkeyKey = DefaultHotkeyKey;
                await SaveAsync();
            }
        }
        else
        {
            _settings = new AppSettings();
            await SaveAsync();
        }
    }

    private class AppSettings
    {
        public string QDrivePath { get; set; } = @"Q:\";
        public bool QDriveEnabled { get; set; } = true;
        public string PDrivePath { get; set; } = @"P:\";
        public bool PDriveEnabled { get; set; } = false; // Disabled by default to minimize scan time
        public int ScanIntervalMinutes { get; set; } = 30;
        public int HotkeyModifiers { get; set; } = DefaultHotkeyModifiers; // Ctrl+Alt
        public int HotkeyKey { get; set; } = DefaultHotkeyKey; // Space
        public string Theme { get; set; } = "Dark";
        public bool AutoStart { get; set; } = false;
        public double SettingsTransparency { get; set; } = 0.78;
        public double OverlayTransparency { get; set; } = 0.78;
        public double WidgetLauncherTransparency { get; set; } = 0.78;
        public double TimerWidgetTransparency { get; set; } = 0.78;
        public bool SettingsTransparencyLinked { get; set; } = false;
        public bool OverlayTransparencyLinked { get; set; } = false;
        public bool LauncherTransparencyLinked { get; set; } = false;
        public bool TimerTransparencyLinked { get; set; } = false;
        public int NotificationDurationMs { get; set; } = 3000; // 3 seconds
        public bool PathSearchEnabled { get; set; } = false;
        public bool PathSearchShowSubDirs { get; set; } = true;
        public bool PathSearchShowSubFiles { get; set; } = true;
        public bool PathSearchShowHidden { get; set; } = false;
        public bool LivingWidgetsMode { get; set; } = false; // False = legacy overlay mode (auto-hide)
        public int WidgetSnapGap { get; set; } = 12;
        
        // Widget positions for Living Widgets Mode (null = use default positioning)
        public double? SearchOverlayLeft { get; set; } = null;
        public double? SearchOverlayTop { get; set; } = null;
        public double? WidgetLauncherLeft { get; set; } = null;
        public double? WidgetLauncherTop { get; set; } = null;
        
        // Widget visibility state (for Living Widgets Mode persistence)
        public bool SearchOverlayVisible { get; set; } = false; // False = overlay hidden by default
        public bool WidgetLauncherVisible { get; set; } = true; // True = show launcher by default
        
        // Quick Tasks widget
        public double QuickTasksWidgetTransparency { get; set; } = 0.78;
        public bool QuickTasksTransparencyLinked { get; set; } = false;
        
        // Timer widget position & visibility
        public double? TimerWidgetLeft { get; set; } = null;
        public double? TimerWidgetTop { get; set; } = null;
        public bool TimerWidgetVisible { get; set; } = false;
        
        // Quick Tasks widget position & visibility
        public double? QuickTasksWidgetLeft { get; set; } = null;
        public double? QuickTasksWidgetTop { get; set; } = null;
        public bool QuickTasksWidgetVisible { get; set; } = false;
        
        // Doc Quick Open widget
        public double DocWidgetTransparency { get; set; } = 0.78;
        public bool DocTransparencyLinked { get; set; } = false;
        public double? DocWidgetLeft { get; set; } = null;
        public double? DocWidgetTop { get; set; } = null;
        public bool DocWidgetVisible { get; set; } = false;

        // Widget enabled states (for widget launcher)
        public bool TimerWidgetEnabled { get; set; } = true;
        public bool QuickTasksWidgetEnabled { get; set; } = true;
        public bool DocWidgetEnabled { get; set; } = true;
        public bool SearchWidgetEnabled { get; set; } = true;

        // Close shortcut for closing widgets (ESC by default)
        public int CloseShortcutModifiers { get; set; } = DefaultCloseShortcutModifiers;
        public int CloseShortcutKey { get; set; } = DefaultCloseShortcutKey;

        // Update check settings
        public bool AutoUpdateCheckEnabled { get; set; } = true;
        public bool AutoUpdateInstallEnabled { get; set; } = false;
        public int UpdateCheckFrequencyMinutes { get; set; } = 360; // 6 hours

        // Frequent Projects widget
        public double FrequentProjectsWidgetTransparency { get; set; } = 0.78;
        public bool FrequentProjectsTransparencyLinked { get; set; } = false;
        public double? FrequentProjectsWidgetLeft { get; set; } = null;
        public double? FrequentProjectsWidgetTop { get; set; } = null;
        public bool FrequentProjectsWidgetVisible { get; set; } = false;
        public bool FrequentProjectsWidgetEnabled { get; set; } = true;
        public int MaxFrequentProjectsShown { get; set; } = 5;
        public int MaxFrequentProjectsSaved { get; set; } = 20;
        public bool FrequentProjectsGridMode { get; set; } = false;

        // Quick Launch widget
        public double QuickLaunchWidgetTransparency { get; set; } = 0.78;
        public bool QuickLaunchTransparencyLinked { get; set; } = false;
        public double? QuickLaunchWidgetLeft { get; set; } = null;
        public double? QuickLaunchWidgetTop { get; set; } = null;
        public bool QuickLaunchWidgetVisible { get; set; } = false;
        public bool QuickLaunchWidgetEnabled { get; set; } = true;
        public bool QuickLaunchHorizontalMode { get; set; } = false;
        public int WidgetLauncherMaxVisibleWidgets { get; set; } = 4;

        // Smart Project Search widget
        public double? SmartProjectSearchWidgetLeft { get; set; } = null;
        public double? SmartProjectSearchWidgetTop { get; set; } = null;
        public bool SmartProjectSearchWidgetVisible { get; set; } = false;
        public bool SmartProjectSearchWidgetEnabled { get; set; } = true;
        public bool SmartProjectSearchAttachToSearchOverlayMode { get; set; } = false;
        public bool SmartProjectSearchWidgetEnabledBeforeAttachMode { get; set; } = true;
        public string SmartProjectSearchLatestMode { get; set; } = "list";
        public List<string> SmartProjectSearchFileTypes { get; set; } = GetDefaultSmartProjectSearchFileTypes();

        // Hotkey focus behavior — which widgets to bring to focus on hotkey press
        public bool HotkeyFocusWidgetLauncher { get; set; } = true;
        public bool HotkeyFocusTimerWidget { get; set; } = false;
        public bool HotkeyFocusQuickTasksWidget { get; set; } = false;
        public bool HotkeyFocusDocWidget { get; set; } = false;
        public bool HotkeyFocusFrequentProjectsWidget { get; set; } = false;
        public bool HotkeyFocusQuickLaunchWidget { get; set; } = false;
        public bool HotkeyFocusSmartProjectSearchWidget { get; set; } = false;
    }

    private static List<string> GetDefaultSmartProjectSearchFileTypes()
        => new()
        {
            "doc",
            "docx",
            "pdf",
            "txt",
            "dwg",
            "rvt",
            "excel",
            "png",
            "jpeg",
            "msg"
        };
}
