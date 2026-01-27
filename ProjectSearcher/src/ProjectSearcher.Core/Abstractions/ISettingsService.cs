namespace ProjectSearcher.Core.Abstractions;

/// <summary>
/// Service for managing application settings
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Get Q: drive path
    /// </summary>
    string GetQDrivePath();

    /// <summary>
    /// Set Q: drive path
    /// </summary>
    void SetQDrivePath(string path);

    /// <summary>
    /// Get scan interval in minutes
    /// </summary>
    int GetScanIntervalMinutes();

    /// <summary>
    /// Set scan interval in minutes
    /// </summary>
    void SetScanIntervalMinutes(int minutes);

    /// <summary>
    /// Get global hotkey configuration
    /// </summary>
    (int modifiers, int key) GetHotkey();

    /// <summary>
    /// Set global hotkey configuration
    /// </summary>
    void SetHotkey(int modifiers, int key);

    /// <summary>
    /// Get theme (Light or Dark)
    /// </summary>
    string GetTheme();

    /// <summary>
    /// Set theme
    /// </summary>
    void SetTheme(string theme);

    /// <summary>
    /// Get auto-start with Windows setting
    /// </summary>
    bool GetAutoStart();

    /// <summary>
    /// Set auto-start with Windows
    /// </summary>
    void SetAutoStart(bool enabled);

    /// <summary>
    /// Get settings window transparency (0.0 to 1.0)
    /// </summary>
    double GetSettingsTransparency();

    /// <summary>
    /// Set settings window transparency
    /// </summary>
    void SetSettingsTransparency(double transparency);

    /// <summary>
    /// Get overlay window transparency (0.0 to 1.0)
    /// </summary>
    double GetOverlayTransparency();

    /// <summary>
    /// Set overlay window transparency
    /// </summary>
    void SetOverlayTransparency(double transparency);

    /// <summary>
    /// Get whether transparency is linked between windows
    /// </summary>
    bool GetTransparencyLinked();

    /// <summary>
    /// Set whether transparency is linked between windows
    /// </summary>
    void SetTransparencyLinked(bool linked);

    /// <summary>
    /// Get notification duration in milliseconds
    /// </summary>
    int GetNotificationDurationMs();

    /// <summary>
    /// Set notification duration in milliseconds
    /// </summary>
    void SetNotificationDurationMs(int durationMs);

    /// <summary>
    /// Save all settings to disk
    /// </summary>
    Task SaveAsync();

    /// <summary>
    /// Load settings from disk
    /// </summary>
    Task LoadAsync();
}
