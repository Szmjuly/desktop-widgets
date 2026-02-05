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

    public bool GetLivingWidgetsMode() => _settings.LivingWidgetsMode;
    public void SetLivingWidgetsMode(bool enabled) => _settings.LivingWidgetsMode = enabled;
    
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
        public bool LivingWidgetsMode { get; set; } = false; // False = legacy overlay mode (auto-hide)
        
        // Widget positions for Living Widgets Mode (null = use default positioning)
        public double? SearchOverlayLeft { get; set; } = null;
        public double? SearchOverlayTop { get; set; } = null;
        public double? WidgetLauncherLeft { get; set; } = null;
        public double? WidgetLauncherTop { get; set; } = null;
        
        // Widget visibility state (for Living Widgets Mode persistence)
        public bool SearchOverlayVisible { get; set; } = false; // False = overlay hidden by default
        public bool WidgetLauncherVisible { get; set; } = true; // True = show launcher by default
        
        // Close shortcut for closing widgets (ESC by default)
        public int CloseShortcutModifiers { get; set; } = DefaultCloseShortcutModifiers;
        public int CloseShortcutKey { get; set; } = DefaultCloseShortcutKey;
    }
}
