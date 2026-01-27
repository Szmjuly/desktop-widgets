using System.Text.Json;
using ProjectSearcher.Core.Abstractions;

namespace ProjectSearcher.Infrastructure.Settings;

/// <summary>
/// Settings service using JSON file storage
/// </summary>
public class SettingsService : ISettingsService
{
    private const int DefaultHotkeyModifiers = 0x0003; // Ctrl+Alt
    private const int DefaultHotkeyKey = 0x20; // Space
    private const int LegacyHotkeyModifiers = 0x0006; // Ctrl+Shift
    private const int LegacyHotkeyKey = 0x50; // P
    private readonly string _settingsPath;
    private AppSettings _settings;

    public SettingsService()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProjectSearcher"
        );
        Directory.CreateDirectory(appDataPath);
        _settingsPath = Path.Combine(appDataPath, "settings.json");
        _settings = new AppSettings();
    }

    public string GetQDrivePath() => _settings.QDrivePath;
    public void SetQDrivePath(string path) => _settings.QDrivePath = path;

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

    public bool GetTransparencyLinked() => _settings.TransparencyLinked;
    public void SetTransparencyLinked(bool linked) => _settings.TransparencyLinked = linked;

    public int GetNotificationDurationMs() => _settings.NotificationDurationMs;
    public void SetNotificationDurationMs(int durationMs) => _settings.NotificationDurationMs = durationMs;

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
        public int ScanIntervalMinutes { get; set; } = 30;
        public int HotkeyModifiers { get; set; } = DefaultHotkeyModifiers; // Ctrl+Alt
        public int HotkeyKey { get; set; } = DefaultHotkeyKey; // Space
        public string Theme { get; set; } = "Dark";
        public bool AutoStart { get; set; } = false;
        public double SettingsTransparency { get; set; } = 0.78;
        public double OverlayTransparency { get; set; } = 0.78;
        public bool TransparencyLinked { get; set; } = true;
        public int NotificationDurationMs { get; set; } = 3000; // 3 seconds
    }
}
